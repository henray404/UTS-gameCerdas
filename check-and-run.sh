#!/usr/bin/env bash
# Hollow House: check the project, build and play it, or open it in the editor.
#   ./check-and-run.sh check   metadata + rule tests + Unity compile + first-person screenshots
#   ./check-and-run.sh play    build the Linux player into Builds/ and start it
#   ./check-and-run.sh editor  open the project in the Unity editor (press Play there)
# Override the editor path with UNITY_EDITOR=/path/to/Unity.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")"
project="$PWD"
version="$(awk '/m_EditorVersion:/{print $2}' ProjectSettings/ProjectVersion.txt)"
unity="${UNITY_EDITOR:-$HOME/Unity/Hub/Editor/$version/Editor/Unity}"
log_dir="$project/Logs"
player="$project/Builds/HollowHouse/HollowHouse.x86_64"

fail() { printf 'FAIL: %s\n' "$1" >&2; exit 1; }

require_unity() {
    [[ -x "$unity" ]] || fail "Unity $version not found at $unity. Install it with Unity Hub or set UNITY_EDITOR."
}

# Batch mode cannot open a project that an editor already has open.
require_closed_project() {
    if pgrep -if -- "-projectPath[= ]$project" >/dev/null 2>&1; then
        fail "This project is open in a Unity editor. Close it first."
    fi
}

run_batch() {
    local name="$1"; shift
    mkdir -p "$log_dir"
    local log="$log_dir/$name.log"
    printf '... %s (log: %s)\n' "$name" "$log"
    if ! "$unity" -batchmode -projectPath "$project" -logFile "$log" "$@"; then
        grep -E "error CS|Exception|HORROR_CAPTURE" "$log" | head -20 >&2 || true
        fail "$name failed, see $log"
    fi
    if grep -q "error CS" "$log"; then
        grep "error CS" "$log" | head -20 >&2
        fail "$name has compile errors"
    fi
}

check() {
    require_unity
    bash Tests/check-unity-metadata.sh
    bash Tests/run-campaign-rules.sh "$unity"
    require_closed_project
    run_batch horror-check -executeMethod SneakHouse.EditorTools.HorrorPreviewCapture.RunBatch
    grep -q "HORROR_CAPTURE_DONE: captured" "$log_dir/horror-check.log" || fail "play-mode capture did not finish"
    printf 'PASS: compiled, played level 1, screenshots:\n'
    ls -1 Tests/screenshots/horror-fps-*.png
}

play() {
    require_unity
    require_closed_project
    run_batch horror-build -quit -buildLinux64Player "$player"
    [[ -x "$player" ]] || fail "build finished but $player is missing"
    printf 'PASS: built %s, starting...\n' "$player"
    "$player"
}

editor() {
    require_unity
    require_closed_project
    "$unity" -projectPath "$project" &
    printf 'Unity is opening. Open Assets/SneakHouse/Scenes/PrototypeHouse.unity and press Play.\n'
}

case "${1:-check}" in
    check) check ;;
    play) play ;;
    editor) editor ;;
    *) printf 'Usage: %s [check|play|editor]\n' "$0" >&2; exit 2 ;;
esac
