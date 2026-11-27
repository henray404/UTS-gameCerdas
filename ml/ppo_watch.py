"""Watch a running ML-Agents PPO run for The Hunter.

Usage: python ml/ppo_watch.py <results/RUN_ID> [--check-step 30000]
Prints cumulative reward and episode length per summary step (from TensorBoard events), the action
distribution from the HUNTER lines the players write to run_logs/Player-*.log, and a verdict for the
plan's stop rule: "reward has not risen after 30k steps -> stop and report".
"""
import argparse
import glob
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from hunter_log_summary import ACTIONS, parse  # noqa: E402

TAGS = {"reward": "Environment/Cumulative Reward", "length": "Environment/Episode Length",
        "lesson": "Environment/Lesson Number/level"}


def scalars(run_dir):
    from tensorboard.backend.event_processing.event_accumulator import EventAccumulator
    series = {k: {} for k in TAGS}
    for events in glob.glob(os.path.join(run_dir, "HunterGuard", "events.out.tfevents.*")):
        acc = EventAccumulator(events, size_guidance={"scalars": 0})
        acc.Reload()
        have = acc.Tags().get("scalars", [])
        for key, tag in TAGS.items():
            if tag in have:
                series[key].update({e.step: e.value for e in acc.Scalars(tag)})
    return series


def rising(reward, check_step):
    """Compare the mean reward of the first and last 3 summaries up to check_step."""
    steps = sorted(s for s in reward if s <= check_step)
    if len(steps) < 6 or max(reward) < check_step:
        return None, steps
    first = sum(reward[s] for s in steps[:3]) / 3
    last = sum(reward[s] for s in steps[-3:]) / 3
    return last - first, steps


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("run_dir")
    ap.add_argument("--check-step", type=int, default=30000)
    ap.add_argument("--min-gain", type=float, default=0.05)
    args = ap.parse_args()
    series = scalars(args.run_dir)
    reward, length, lesson = series["reward"], series["length"], series["lesson"]
    if not reward:
        raise SystemExit("No reward summaries yet in " + args.run_dir)
    print(f"{'step':>8} {'reward':>8} {'ep_len':>7} {'lesson':>6}")
    for step in sorted(reward):
        print(f"{step:8d} {reward[step]:+8.3f} {length.get(step, float('nan')):7.2f} {lesson.get(step, float('nan')):6.0f}")

    episodes = []
    for log in sorted(glob.glob(os.path.join(args.run_dir, "run_logs", "Player-*.log"))):
        episodes += parse(log)
    if episodes:
        total = sum(sum(e[a] for a in ACTIONS) for e in episodes) or 1
        caught = sum(e["caught"] for e in episodes)
        print(f"\n{len(episodes)} missions in player logs, Hunter caught {caught} ({caught / len(episodes):.0%}), "
              f"mean mission time {sum(e['time'] for e in episodes) / len(episodes):.1f}s")
        print("action share: " + ", ".join(f"{a} {sum(e[a] for e in episodes) / total:.0%}" for a in ACTIONS))

    gain, steps = rising(reward, args.check_step)
    if gain is None:
        print(f"\nverdict: not enough data before step {args.check_step} yet")
    elif gain < args.min_gain:
        print(f"\nverdict: STOP - reward gain {gain:+.3f} over {steps[0]}-{steps[-1]} is below {args.min_gain}")
        sys.exit(3)
    else:
        print(f"\nverdict: OK - reward gain {gain:+.3f} over {steps[0]}-{steps[-1]}")


if __name__ == "__main__":
    main()
