"""Summarize Hunter Q-learning training from a Unity log (and optionally the saved Q-table).

Usage: python3 ml/hunter_log_summary.py <unity.log> [hunter_qtable.json] [--csv out.csv] [--window 20]
Reads lines written by HunterDecision.EndMission:
  HUNTER episode=N eps=E level=L won=B hunterCaught=B time=T decisions=D actions=[a0,...,a5]
"""
import argparse
import csv
import json
import math
import os
import re

ACTIONS = ["GoLastKnown", "SweepSector", "InterceptExit", "AmbushTarget", "CheckHidingSpot", "GiveUp"]
LINE = re.compile(r"HUNTER episode=(\d+) eps=([\d.]+) level=(\d+) won=(\w+) hunterCaught=(\w+) "
                  r"time=([\d.]+) decisions=(\d+) actions=\[([\d,]*)\]")


def parse(path):
    episodes = []
    with open(path, errors="replace") as log:
        for line in log:
            m = LINE.search(line)
            if not m:
                continue
            counts = [int(x) for x in m.group(8).split(",")] if m.group(8) else [0] * 6
            if len(counts) != len(ACTIONS):
                raise ValueError(f"episode {m.group(1)}: expected 6 action counts, got {len(counts)}")
            episodes.append({
                "episode": int(m.group(1)), "epsilon": float(m.group(2)), "level": int(m.group(3)),
                "won": m.group(4) == "True", "caught": m.group(5) == "True", "time": float(m.group(6)),
                "decisions": int(m.group(7)), "timeout": "timeout=True" in line,
                **{a: c for a, c in zip(ACTIONS, counts)},
            })
    return episodes


def windows(episodes, size):
    for start in range(0, len(episodes), size):
        block = episodes[start:start + size]
        n = len(block)
        total = sum(sum(e[a] for a in ACTIONS) for e in block) or 1
        yield {
            "range": f"{block[0]['episode']}-{block[-1]['episode']}", "eps": block[-1]["epsilon"],
            "caught": sum(e["caught"] for e in block) / n, "escaped": sum(e["won"] for e in block) / n,
            "decisions": sum(e["decisions"] for e in block) / n, "time": sum(e["time"] for e in block) / n,
            "share": [sum(e[a] for e in block) / total for a in ACTIONS],
        }


def greedy_policy(qtable_path):
    with open(qtable_path) as f:
        table = json.load(f)
    q = table["q"]
    # A table that started optimistic (lab run q-opt) records its starting value next to it; "visited" means changed from it.
    here = os.path.dirname(os.path.abspath(qtable_path))
    found = [p for p in (os.path.join(here, "init.json"), os.path.join(os.path.dirname(here), "init.json")) if os.path.exists(p)]
    init = json.load(open(found[0]))["init"] if found else 0.0
    if len(q) != 48 * 6:
        raise ValueError(f"Q-table has {len(q)} values, contract needs 288")
    rows = []
    for state in range(48):
        values = q[state * 6:(state + 1) * 6]
        if all(abs(v - init) < 1e-9 for v in values):
            continue  # never visited
        near, rest = divmod(state, 16)
        stale, rest = divmod(rest, 8)
        heard, rest = divmod(rest, 4)
        no_target, far_hiding = divmod(rest, 2)
        label = (f"{['<3m', '3-8m', '>8m'][near]:>4} {'old' if stale else 'new'} "
                 f"{'heard' if heard else 'seen '} {'noTarget' if no_target else 'target  '} {'' if far_hiding else 'hideNear'}")
        # Same mask as the game where the state encodes it: no target left means AmbushTarget is off.
        allowed = [a for a in range(6) if not (no_target and ACTIONS[a] == "AmbushTarget")]
        best = max(allowed, key=lambda a: values[a])
        rows.append((state, label, ACTIONS[best], values[best]))
    return table["episodes"], rows


def wilson(hits, n, z=1.96):
    """95% Wilson interval for a proportion."""
    p = hits / n
    centre = (p + z * z / (2 * n)) / (1 + z * z / n)
    half = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / (1 + z * z / n)
    return centre - half, centre + half


def compare(a_name, a, b_name, b):
    """Two-proportion z-test on Hunter catch rate."""
    ha, hb = sum(e["caught"] for e in a), sum(e["caught"] for e in b)
    na, nb = len(a), len(b)
    pooled = (ha + hb) / (na + nb)
    se = math.sqrt(pooled * (1 - pooled) * (1 / na + 1 / nb)) or 1
    z = (ha / na - hb / nb) / se
    p_value = math.erfc(abs(z) / math.sqrt(2))
    for name, hits, n in ((a_name, ha, na), (b_name, hb, nb)):
        low, high = wilson(hits, n)
        print(f"{name:>10}: caught {hits}/{n} = {hits / n:.0%}  (95% CI {low:.0%}-{high:.0%})")
    print(f"difference {ha / na - hb / nb:+.0%}, z = {z:.2f}, two-sided p = {p_value:.3f}")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("log")
    parser.add_argument("qtable", nargs="?")
    parser.add_argument("--csv")
    parser.add_argument("--window", type=int, default=20)
    parser.add_argument("--against", help="second log (e.g. -hunter-baseline run) to compare catch rates with")
    args = parser.parse_args()
    episodes = parse(args.log)
    if not episodes:
        raise SystemExit("No HUNTER episode lines found.")
    if args.csv:
        with open(args.csv, "w", newline="") as f:
            writer = csv.DictWriter(f, fieldnames=list(episodes[0]))
            writer.writeheader()
            writer.writerows(episodes)
    print(f"{len(episodes)} episodes, Hunter caught {sum(e['caught'] for e in episodes)}, "
          f"bot escaped {sum(e['won'] for e in episodes)}, timeouts {sum(e['timeout'] for e in episodes)}")
    print(f"{'episodes':>9} {'eps':>5} {'caught':>6} {'escape':>6} {'dec':>5} {'time':>6}  " +
          " ".join(f"{a[:8]:>8}" for a in ACTIONS))
    for w in windows(episodes, args.window):
        print(f"{w['range']:>9} {w['eps']:5.2f} {w['caught']:6.0%} {w['escaped']:6.0%} {w['decisions']:5.1f} "
              f"{w['time']:6.1f}  " + " ".join(f"{s:8.0%}" for s in w["share"]))
    if args.against:
        other = parse(args.against)
        if not other:
            raise SystemExit("No HUNTER episode lines in " + args.against)
        print()
        compare("this log", episodes, "against", other)
    if args.qtable:
        trained, rows = greedy_policy(args.qtable)
        print(f"\nGreedy action per visited state ({len(rows)}/48 visited, table has {trained} episodes):")
        for state, label, action, value in rows:
            print(f"  s{state:02d} {label:<36} -> {action:<16} Q={value:+.3f}")


if __name__ == "__main__":
    main()
