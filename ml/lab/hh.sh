#!/usr/bin/env bash
# Hollow House RL lab runner (plans/rl-training-lab.md sections 6-9).
# Usage: repo/ml/lab/hh.sh <setup|preflight|smoke|q|q-opt|ppo|ppo-watch|eval|report|status|auto>
# Everything stays in $HH_ROOT (default ~/hollowhouse-rl). Long runs go to tmux sessions named hh-*.
set -euo pipefail

ROOT=${HH_ROOT:-$HOME/hollowhouse-rl}
REPO=$ROOT/repo
VENV=$ROOT/venv
BUILD_DIR=$ROOT/HollowHouseRL
GAME=$BUILD_DIR/HollowHouse.x86_64
TGZ=$ROOT/hollowhouse-rl-build.tgz
LOGS=$ROOT/logs
RES=$ROOT/results
EPISODES_Q=${HH_Q_EPISODES:-1500}
EPISODES_EVAL=${HH_EVAL_EPISODES:-350}
LEVELS=${HH_LEVELS:-"1 3 6"}
RUN_ID=${HH_RUN_ID:-hunter_ppo_v1}
EXTRA_EVAL_ARGS=${HH_EVAL_ARGS:-}   # e.g. "-hunter-seed-offset 1000" once held-out seeds exist (see ml/rl-expansion.md)
mkdir -p "$LOGS" "$RES/q" "$RES/eval"

say() { printf '[hh %(%H:%M:%S)T] %s\n' -1 "$*"; }
die() { say "ERROR: $*"; exit 1; }
stamp() { printf '%s %s %s\n' "$(date +%s)" "$1" "$2" >> "$RES/timings.txt"; }  # epoch event phase

# Cores we may use: all cores minus current load minus 2 reserved for other users.
free_cores() {
  local n load
  n=$(nproc); load=$(awk '{print int($1+0.999)}' /proc/loadavg)
  echo $(( n - load - 2 > 1 ? n - load - 2 : 1 ))
}
# Least busy GPU: mean utilisation (then memory) over 5 one-second samples, because shared GPUs spike.
pick_gpu() {
  { timeout 6 nvidia-smi --query-gpu=index,utilization.gpu,memory.used --format=csv,noheader,nounits -l 1 2>/dev/null || true; } |
    awk -F', *' '{u[$1]+=$2; m[$1]+=$3; c[$1]++} END {for (g in c) printf "%s %.1f %.0f\n", g, u[g]/c[g], m[g]/c[g]}' |
    sort -k2n -k3n | head -1 | cut -d' ' -f1
}
need_session_free() { ! tmux has-session -t "$1" 2>/dev/null || die "tmux session $1 already running (hh.sh status)"; }

# Flags the build must understand. Strings in a Mono assembly are UTF-16LE.
REQUIRED_Q=(-hunter-train -hunter-eval -hunter-baseline -hunter-episodes -hunter-level -hunter-table)
REQUIRED_PPO=(-hunter-ppo-train -hunter-ppo-eval)
build_has() {
  local dll=$BUILD_DIR/HollowHouse_Data/Managed/Assembly-CSharp.dll
  [ -f "$dll" ] || return 1
  "$VENV/bin/python" -I - "$dll" "$@" <<'PY'
import sys
data = open(sys.argv[1], "rb").read()
missing = [f for f in sys.argv[2:] if f.encode("utf-16-le") not in data]
print(" ".join(missing)); sys.exit(1 if missing else 0)
PY
}

cmd_setup() {
  command -v uv >/dev/null || die "uv not found"
  [ -x "$VENV/bin/python" ] || uv venv --python 3.10.12 "$VENV"
  uv pip install --python "$VENV/bin/python" --build-constraints "$REPO/ml/build-constraints.txt" \
    -r "$REPO/ml/requirements.txt" tensorboard
  "$VENV/bin/mlagents-learn" --help >/dev/null && say "trainer OK"
  if [ ! -x "$GAME" ]; then
    [ -f "$TGZ" ] || die "build missing: copy hollowhouse-rl-build.tgz to $ROOT (plans/rl-training-lab.md 1c)"
    tar xzf "$TGZ" -C "$ROOT" 2>&1 | grep -v 'unknown extended header' || true
    chmod +x "$GAME"
    find "$BUILD_DIR" -name '._*' -size -4k -delete   # AppleDouble metadata from a tarball made on macOS
  fi
  say "build at $GAME"
}

cmd_preflight() {
  [ -x "$GAME" ] || die "no build at $GAME (run: hh.sh setup)"
  local miss
  miss=$(build_has "${REQUIRED_Q[@]}") || die "build lacks Q flags: $miss (Phase 1a not in this build)"
  if miss=$(build_has "${REQUIRED_PPO[@]}"); then say "PPO flags present"; else say "WARN: build lacks PPO flags: $miss"; fi
  ldd "$GAME" 2>/dev/null | grep -q "not found" && die "missing libraries: $(ldd "$GAME" 2>/dev/null | grep "not found")"
  say "free cores: $(free_cores), GPU pick: $(pick_gpu)"
}

# Runs the game for a fixed episode count and verifies it finished. Training counts the episodes stored in
# the Q-table (it resumes from the file); other modes count HUNTER lines. An existing log is rotated, not overwritten.
run_game() {  # log, episodes, max_seconds, args...
  local log=$1 n=$2 limit=$3; shift 3
  [ -f "$log" ] && mv "$log" "${log%.log}.$(date +%Y%m%d-%H%M%S).log"
  timeout --kill-after=30 "$limit" "$GAME" -batchmode -nographics "$@" -hunter-episodes "$n" -logFile "$log" || true
  local got=0
  if [[ " $* " == *" -hunter-train "* ]]; then
    got=$("$VENV/bin/python" -I -c 'import json,sys; print(json.load(open(sys.argv[1]))["episodes"])' "$RES/q/hunter_qtable.json" 2>/dev/null || echo 0)
  elif [ -f "$log" ]; then
    got=$(grep -c '^HUNTER episode=' "$log" || true)
  fi
  [ "$got" -ge "$n" ] || { say "FAIL $log: $got/$n episodes"; return 1; }
  say "OK $log: $got episodes"
}

cmd_smoke() {
  cmd_preflight
  run_game "$LOGS/smoke.log" 3 900 -hunter-baseline -hunter-level 1 || {
    grep -iE 'error|exception' "$LOGS/smoke.log" | head -20; die "smoke test failed, see $LOGS/smoke.log"; }
  grep '^HUNTER' "$LOGS/smoke.log"
}

SELF=$(readlink -f "$0")
in_tmux() {  # session, job (HH_TAG suffixes the session so parallel variants do not collide)
  set -- "$1${HH_TAG:+-$HH_TAG}" "$2"
  need_session_free "$1"
  # tmux runs jobs in its server's environment, so pass every HH_* setting explicitly.
  local envs; envs=$( (env | grep '^HH_' || true) | sed "s/'/'\\\\''/g; s/=\(.*\)/='\1'/" | tr '\n' ' ')
  tmux new-session -d -s "$1" "env HH_ROOT='$ROOT' $envs bash -c '\"$SELF\" $2 2>&1 | tee -a \"$LOGS/$1.out\"'"
  say "started tmux $1 (attach: tmux attach -t $1, log: $LOGS/$1.out)"
}

q_summary() {  # all training logs (including rotated ones from restarts), oldest first
  ls -tr "$LOGS"/q_train*.log | grep -v q_train_all | xargs cat > "$LOGS/q_train_all.log"
  "$VENV/bin/python" -I "$REPO/ml/hunter_log_summary.py" "$LOGS/q_train_all.log" "$RES/q/hunter_qtable.json" \
    --csv "$RES/q/train.csv" --window 100 > "$RES/q/train_summary.txt"
  grep -oP '\(\K\d+(?=/48 visited)' "$RES/q/train_summary.txt"
}
# Copies the Q-table every 250 episodes (results/q/snapshots/ep<N>.json) so convergence (greedy policy stability)
# can be checked and early tables can serve as weaker Hunters. Runs until the parent job ends.
q_snapshots() {
  local dir=$RES/q/snapshots next=250 ep; mkdir -p "$dir"
  while sleep 30; do
    ep=$("$VENV/bin/python" -I -c 'import json,sys; print(json.load(open(sys.argv[1]))["episodes"])' "$RES/q/hunter_qtable.json" 2>/dev/null) || continue
    # Named by the episode count actually in the table (the copy can lag the 250 mark by a mission or two).
    if [ "$ep" -ge "$next" ]; then cp "$RES/q/hunter_qtable.json" "$dir/ep$(printf %04d "$ep").json"; next=$(( (ep / 250 + 1) * 250 )); fi
  done
}
job_q() {
  stamp start q
  q_snapshots & QSNAP_PID=$!
  trap 'kill "${QSNAP_PID:-}" 2>/dev/null || true' EXIT
  run_game "$LOGS/q_train.log" "$EPISODES_Q" 36000 -hunter-train -hunter-level random -hunter-table "$RES/q/hunter_qtable.json"
  local visited; visited=$(q_summary)
  say "visited $visited/48 states after $EPISODES_Q episodes"
  if [ "$visited" -lt 40 ]; then  # plan section 7: continue to 3000 from the saved table
    run_game "$LOGS/q_train_ext.log" 3000 36000 -hunter-train -hunter-level random -hunter-table "$RES/q/hunter_qtable.json"
    visited=$(q_summary); say "visited $visited/48 states after 3000 episodes"
  fi
  kill "$QSNAP_PID" 2>/dev/null || true; trap - EXIT
  cp "$RES/q/hunter_qtable.json" "$RES/q/snapshots/final.json"
  stamp end q; say "Q-learning done"
}
cmd_q() { cmd_preflight; in_tmux hh-q _q; }

# Comparison run: same training, but the Q-table starts optimistic (every value HH_Q_INIT, default 0.5) so even the
# greedy choice tries each action in each state until its value falls to what it really earns. Rewards, contract and
# C# are unchanged; only the starting table differs. Lives in $ROOT/qopt with its own logs/results; tmux hh-q-opt.
cmd_q_opt() {
  cmd_preflight
  local r2=$ROOT/qopt init=${HH_Q_INIT:-0.5}
  mkdir -p "$r2/results/q"
  for d in repo venv HollowHouseRL; do [ -e "$r2/$d" ] || ln -s "$ROOT/$d" "$r2/$d"; done
  if [ ! -f "$r2/results/q/hunter_qtable.json" ]; then
    "$VENV/bin/python" -I -c 'import json,sys; v=float(sys.argv[2]); json.dump({"episodes": 0, "q": [v]*288}, open(sys.argv[1], "w"), separators=(",", ":"))' \
      "$r2/results/q/hunter_qtable.json" "$init"
    printf '{"init": %s}\n' "$init" > "$r2/results/q/init.json"
  fi
  HH_ROOT=$r2 HH_TAG=opt "$SELF" q
}

job_ppo() {
  local envs=${HH_NUM_ENVS:-10} cap gpu resume=""
  cap=$(free_cores); [ "$envs" -le "$cap" ] || envs=$cap
  gpu=$(pick_gpu); [ -d "$RES/$RUN_ID" ] && resume="--resume"
  say "PPO run $RUN_ID: $envs envs, GPU $gpu $resume"
  stamp start ppo
  cd "$RES"
  # Background jobs of a non-interactive shell ignore SIGINT; restore it so the stop below lets mlagents save the model.
  CUDA_VISIBLE_DEVICES=$gpu "$VENV/bin/python" -I -c \
    'import os, signal, sys; signal.signal(signal.SIGINT, signal.SIG_DFL); os.execv(sys.argv[1], sys.argv[1:])' \
    "$VENV/bin/mlagents-learn" "$REPO/ml/hunter_ppo.yaml" --run-id "$RUN_ID" $resume \
    --env "$GAME" --num-envs "$envs" --results-dir "$RES" --no-graphics --time-scale 4 --timeout-wait 600 --env-args -hunter-ppo-train &
  local trainer=$!
  # Plan rule: if reward has not risen by 30k steps, stop (SIGINT makes mlagents save the model) and report.
  while kill -0 "$trainer" 2>/dev/null; do
    sleep "${HH_WATCH_SECS:-300}"
    local rc=0
    "$VENV/bin/python" -I "$REPO/ml/ppo_watch.py" "$RES/$RUN_ID" > "$RES/$RUN_ID.watch.txt" 2>&1 || rc=$?
    if [ "$rc" -eq 3 ]; then
      say "reward did not rise by 30k steps, stopping PPO (see $RES/$RUN_ID.watch.txt)"
      kill -INT "$trainer"; wait "$trainer" || true; stamp end ppo; return 3
    fi
  done
  wait "$trainer" || say "mlagents-learn exited with an error"
  "$VENV/bin/python" -I "$REPO/ml/ppo_watch.py" "$RES/$RUN_ID" > "$RES/$RUN_ID.watch.txt" 2>&1 || true
  stamp end ppo; say "PPO done"; tail -4 "$RES/$RUN_ID.watch.txt"
}
cmd_ppo() {
  cmd_preflight
  build_has "${REQUIRED_PPO[@]}" >/dev/null || die "build lacks PPO flags"
  in_tmux hh-ppo _ppo
  tmux has-session -t hh-tb 2>/dev/null || tmux new-session -d -s hh-tb \
    "\"$VENV/bin/tensorboard\" --logdir \"$RES\" --port ${HH_TB_PORT:-6006}"
  say "TensorBoard on port ${HH_TB_PORT:-6006} (from laptop: ssh -L 6006:localhost:6006 <lab>); watch: hh.sh ppo-watch"
}
cmd_ppo_watch() { "$VENV/bin/python" -I "$REPO/ml/ppo_watch.py" "$RES/$RUN_ID"; }

eval_one() {  # mode level
  local log=$LOGS/eval_$1_L$2.log
  run_game "$log" "$EPISODES_EVAL" 43200 "-hunter-$1" -hunter-level "$2" -hunter-table "$RES/q/hunter_qtable.json" \
    $EXTRA_EVAL_ARGS && touch "$log.done"
}
# PPO evaluation through the Python trainer in inference mode. The build's -hunter-ppo-eval only runs the ONNX
# baked into Resources at build time, so a model trained here needs this path (or a rebuild on the laptop).
eval_ppo_py() {  # level
  local L=$1 run=${RUN_ID}_evalL$1 log=$LOGS/eval_ppo-eval_L$1.log
  local envs=${HH_EVAL_PPO_ENVS:-4} cap; cap=$(free_cores); [ "$envs" -le "$cap" ] || envs=$cap
  [ -d "$RES/$RUN_ID" ] || { say "FAIL no trained run $RES/$RUN_ID"; return 1; }
  rm -rf "${RES:?}/$run"
  "$VENV/bin/python" -I - "$REPO/ml/hunter_ppo.yaml" "$RES/$run.yaml" "$L" <<'PY'
import sys, yaml
cfg = yaml.safe_load(open(sys.argv[1]))
cfg["environment_parameters"] = {"level": int(sys.argv[3])}   # fixed level, no curriculum
cfg["behaviors"]["HunterGuard"]["max_steps"] = 10_000_000       # stopped by episode count below
yaml.safe_dump(cfg, open(sys.argv[2], "w"))
PY
  (cd "$RES" && exec "$VENV/bin/python" -I -c \
    'import os, signal, sys; signal.signal(signal.SIGINT, signal.SIG_DFL); os.execv(sys.argv[1], sys.argv[1:])' \
    "$VENV/bin/mlagents-learn" "$RES/$run.yaml" --run-id "$run" --initialize-from "$RUN_ID" --inference --deterministic \
    --env "$GAME" --num-envs "$envs" --results-dir "$RES" --no-graphics --time-scale 4 --timeout-wait 600 --env-args -hunter-ppo-train -hunter-level "$L" \
    > "$LOGS/$run.trainer.log" 2>&1) &
  local trainer=$! got=0 start=$SECONDS
  while kill -0 "$trainer" 2>/dev/null && [ $((SECONDS - start)) -lt 43200 ]; do
    sleep 20
    got=$(cat "$RES/$run"/run_logs/Player-*.log 2>/dev/null | grep -c '^HUNTER episode=' || true)
    [ "$got" -ge "$EPISODES_EVAL" ] && break
  done
  kill -INT "$trainer" 2>/dev/null || true; wait "$trainer" || true
  cat "$RES/$run"/run_logs/Player-*.log 2>/dev/null | grep '^HUNTER episode=' | head -n "$EPISODES_EVAL" > "$log" || true
  got=$(grep -c '^HUNTER episode=' "$log" || true)
  [ "$got" -ge "$EPISODES_EVAL" ] || { say "FAIL $log: $got/$EPISODES_EVAL (see $LOGS/$run.trainer.log)"; return 1; }
  touch "$log.done"; say "OK $log: $got episodes (python inference, deterministic)"
}

job_eval() {
  local par=${HH_EVAL_PAR:-6} cap modes="baseline eval" ppo=${HH_PPO_EVAL:-auto}
  cap=$(free_cores); [ "$par" -le "$cap" ] || par=$cap
  # auto: use the trained run if there is one, else the ONNX inside the build, else skip PPO.
  if [ "$ppo" = auto ]; then
    if [ -d "$RES/$RUN_ID" ]; then ppo=python
    elif build_has "${REQUIRED_PPO[@]}" >/dev/null; then ppo=build
    else ppo=none; fi
  fi
  [ "$ppo" = build ] && modes="$modes ppo-eval"
  local jobs=$RES/eval/jobs.txt; : > "$jobs"
  for L in $LEVELS; do for M in $modes; do
    [ -f "$LOGS/eval_${M}_L$L.log.done" ] || echo "$M $L" >> "$jobs"
  done; done
  say "$(wc -l < "$jobs") build eval jobs ($modes), $par in parallel; PPO eval: $ppo"
  stamp start eval
  local failed=0
  xargs -r -P "$par" -L 1 "$SELF" _eval_one < "$jobs" || failed=1
  if [ "$ppo" = python ]; then
    for L in $LEVELS; do
      [ -f "$LOGS/eval_ppo-eval_L$L.log.done" ] || eval_ppo_py "$L" || failed=1
    done
  fi
  stamp end eval
  if [ "$failed" = 1 ]; then say "some eval jobs failed (rerun hh.sh eval to retry only those)"; else say "evaluation done"; fi
}
cmd_eval() { cmd_preflight; in_tmux hh-eval _eval; }

# Waits for the build tarball, then runs the whole lab plan in order. Stops at the first failing step
# (a failing smoke test means the build is broken: report it, do not patch C#).
job_auto() {
  local waited=0 limit=${HH_AUTO_WAIT_DAYS:-14}
  until [ -x "$GAME" ] || [ -f "$TGZ" ]; do
    [ $((waited % 3600)) -eq 0 ] && say "waiting for $TGZ (${waited}s)"
    sleep 300; waited=$((waited + 300))
    [ "$waited" -lt $((limit * 86400)) ] || die "no build after $limit days"
  done
  sleep 120   # let an scp in progress finish
  if [ -f "$TGZ" ]; then local size=0; until [ "$size" = "$(stat -c %s "$TGZ")" ]; do size=$(stat -c %s "$TGZ"); sleep 30; done; fi
  cmd_setup
  cmd_smoke
  # Q-learning is a single process, so it runs next to PPO (which takes the remaining cores).
  ( job_q ) & local qpid=$!
  sleep 60
  if build_has "${REQUIRED_PPO[@]}" >/dev/null; then
    local rc=0; ( job_ppo ) || rc=$?
    [ "$rc" -eq 3 ] && say "PPO stopped by the 30k-step rule; evaluating Q-learning and the saved PPO model anyway"
    [ "$rc" -ne 0 ] && [ "$rc" -ne 3 ] && say "PPO failed (exit $rc); continuing with Q-learning only"
  else
    say "build has no PPO flags: skipping PPO"
  fi
  wait "$qpid" || die "Q-learning failed, see $LOGS/q_train.log"
  job_eval
  cmd_report
  say "AUTO DONE: see ${HH_REPORT_DIR:-$REPO/ml/results}/summary.md"
}
cmd_auto() { in_tmux hh-auto _auto; }

cmd_report() {
  "$VENV/bin/python" -I "$REPO/ml/make_summary.py" --logs "$LOGS" --results "$RES" --levels $LEVELS \
    --out "${HH_REPORT_DIR:-$REPO/ml/results}/summary.md" --csv-dir "${HH_REPORT_DIR:-$REPO/ml/results}"
}

cmd_status() {
  tmux ls 2>/dev/null | grep '^hh-' || say "no hh-* sessions"
  for f in "$LOGS"/q_train*.log "$LOGS"/eval_*.log; do
    [ -f "$f" ] && printf '%-40s %5s episodes\n' "$(basename "$f")" "$(grep -c '^HUNTER episode=' "$f" || true)"
  done
  ls -d "$RES/$RUN_ID"/HunterGuard 2>/dev/null && ls "$RES/$RUN_ID"/HunterGuard | tail -5
  say "free cores $(free_cores)"; nvidia-smi --query-gpu=index,utilization.gpu,memory.used --format=csv,noheader
}

case "${1:-}" in
  setup|preflight|smoke|q|ppo|eval|report|status|auto) "cmd_$1" ;;
  q-opt) cmd_q_opt ;;
  ppo-watch) cmd_ppo_watch ;;
  _q|_ppo|_eval|_auto) "job${1}" ;;
  _eval_one) eval_one "$2" "$3" ;;
  *) sed -n 2,4p "$0"; exit 2 ;;
esac
