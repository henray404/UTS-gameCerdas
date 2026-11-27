"""Build ml/results/summary.md from the lab logs (plans/rl-training-lab.md section 9).

Usage: python ml/make_summary.py --logs ~/hollowhouse-rl/logs --results ~/hollowhouse-rl/results \
           --levels 1 3 6 --out ml/results/summary.md --csv-dir ml/results
Reads logs/eval_{baseline,eval,ppo-eval}_L{level}.log, logs/q_train*.log, results/q/hunter_qtable.json
and results/timings.txt. Every claim in the generated text is computed from those files.
"""
import argparse
import csv
import datetime
import glob
import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from hunter_log_summary import ACTIONS, greedy_policy, parse, wilson  # noqa: E402

METHODS = [("baseline", "FSM (baseline)"), ("eval", "Q-learning"), ("eval-qopt", "Q-learning (tabel awal optimis)"),
           ("golastknown", "Hunter selalu GoLastKnown (heuristik)"),
           ("ppo-eval", "PPO deterministik (mode ML-Agents)"), ("ppo-stoch", "PPO stokastik (mode ML-Agents)")]
# Comparison runs: left out of the table entirely when they were not evaluated.
OPTIONAL = {"eval-qopt", "golastknown", "ppo-stoch"}
ALPHA = 0.05


def two_prop(h1, n1, h2, n2):
    pooled = (h1 + h2) / (n1 + n2)
    se = math.sqrt(pooled * (1 - pooled) * (1 / n1 + 1 / n2))
    if se == 0:
        return 0.0, 1.0
    z = (h1 / n1 - h2 / n2) / se
    return z, math.erfc(abs(z) / math.sqrt(2))


def holm(pvalues):
    """Holm-Bonferroni adjusted p-values, same order as input."""
    order = sorted(range(len(pvalues)), key=lambda i: pvalues[i])
    adjusted, running = [0.0] * len(pvalues), 0.0
    for rank, i in enumerate(order):
        running = max(running, min(1.0, (len(pvalues) - rank) * pvalues[i]))
        adjusted[i] = running
    return adjusted


def pct(x):
    return f"{x * 100:.1f}%"


def write_csv(path, episodes):
    with open(path, "w", newline="") as f:
        w = csv.DictWriter(f, fieldnames=list(episodes[0]))
        w.writeheader()
        w.writerows(episodes)


def timings(path):
    spans, open_at = {}, {}
    if not os.path.exists(path):
        return spans
    for line in open(path):
        parts = line.split()
        if len(parts) != 3:
            continue
        t, event, phase = int(parts[0]), parts[1], parts[2]
        if event == "start":
            open_at[phase] = t
        elif event == "end" and phase in open_at:
            spans[phase] = spans.get(phase, 0) + t - open_at.pop(phase)
    return spans


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--logs", required=True)
    ap.add_argument("--results", required=True)
    ap.add_argument("--levels", nargs="+", type=int, default=[1, 3, 6])
    ap.add_argument("--out", required=True)
    ap.add_argument("--csv-dir", required=True)
    ap.add_argument("--notes", help="hand-written analysis (markdown) inserted after the introduction")
    ap.add_argument("--extra-table", action="append", default=[],
                    help="label=path of another Q-table to report visited states for (e.g. the optimistic run)")
    args = ap.parse_args()
    os.makedirs(args.csv_dir, exist_ok=True)

    data = {}
    for level in args.levels:
        for key, _ in METHODS:
            log = os.path.join(args.logs, f"eval_{key}_L{level}.log")
            if os.path.exists(log):
                episodes = parse(log)
                if episodes:
                    data[key, level] = episodes
                    write_csv(os.path.join(args.csv_dir, f"eval_{key}_L{level}.csv"), episodes)

    md = ["# Hasil training dan evaluasi The Hunter", "",
          f"Dibuat otomatis oleh `ml/make_summary.py` pada {datetime.date.today().isoformat()} dari log lab. "
          f"Evaluasi tanpa belajar (epsilon 0). Uji: dua proporsi dua sisi terhadap FSM di level yang sama, "
          f"CI 95% Wilson, plus p terkoreksi Holm untuk semua perbandingan sekaligus. "
          f"Selisih dengan p ≥ {ALPHA} ditulis **belum signifikan**.", ""]

    if args.notes and os.path.exists(args.notes):
        md += [open(args.notes).read().rstrip(), ""]

    # Catch-rate table and comparisons.
    md += ["## Catch rate per level × metode", "",
           "| Level | Metode | Tangkap / misi | Catch rate | CI 95% | Lolos | Timeout | Selisih vs FSM | p | p (Holm) | Kesimpulan |",
           "|---:|---|---:|---:|---|---:|---:|---:|---:|---:|---|"]
    comps = []
    for level in args.levels:
        base = data.get(("baseline", level))
        for key, _ in METHODS[1:]:
            eps = data.get((key, level))
            if base and eps:
                hb, nb = sum(e["caught"] for e in base), len(base)
                hm, nm = sum(e["caught"] for e in eps), len(eps)
                comps.append(((key, level), hm / nm - hb / nb, two_prop(hm, nm, hb, nb)[1]))
    adj = dict(zip([c[0] for c in comps], holm([c[2] for c in comps]))) if comps else {}
    comp = {c[0]: c for c in comps}
    verdicts = []
    for level in args.levels:
        for key, name in METHODS:
            eps = data.get((key, level))
            if not eps:
                if key not in OPTIONAL:
                    md.append(f"| {level} | {name} | – | – | – | – | – | – | – | – | belum dijalankan |")
                continue
            n, h = len(eps), sum(e["caught"] for e in eps)
            lo, hi = wilson(h, n)
            row = (f"| {level} | {name} | {h}/{n} | {pct(h / n)} | {pct(lo)}–{pct(hi)} | "
                   f"{pct(sum(e['won'] for e in eps) / n)} | {pct(sum(e['timeout'] for e in eps) / n)} |")
            if (key, level) in comp:
                _, diff, p = comp[key, level]
                ph = adj[key, level]
                if p >= ALPHA:
                    verdict = "belum signifikan"
                elif ph >= ALPHA:
                    verdict = f"{'lebih tinggi' if diff > 0 else 'lebih rendah'} (p < 0.05, tapi tidak lolos koreksi Holm)"
                else:
                    verdict = f"signifikan {'lebih tinggi' if diff > 0 else 'lebih rendah'}"
                verdicts.append((level, name, diff, p, verdict))
                row += f" {diff * 100:+.1f} poin | {p:.3f} | {ph:.3f} | {verdict} |"
            else:
                row += " – | – | – | pembanding |" if key == "baseline" else " – | – | – | tanpa baseline |"
            md.append(row)
    md.append("")
    if verdicts:
        md += ["Ringkasan:", ""]
        md += [f"- Level {lv}, {nm}: {d * 100:+.1f} poin vs FSM, p = {p:.3f} → {v}." for lv, nm, d, p, v in verdicts]
        md.append("")

    # Action distribution.
    md += ["## Distribusi aksi saat evaluasi", "",
           "| Level | Metode | Keputusan / misi | " + " | ".join(ACTIONS) + " |",
           "|---:|---|---:|" + "---:|" * len(ACTIONS)]
    dominant = []
    for level in args.levels:
        for key, name in METHODS[1:]:
            eps = data.get((key, level))
            if not eps:
                continue
            total = sum(sum(e[a] for a in ACTIONS) for e in eps) or 1
            share = [sum(e[a] for e in eps) / total for a in ACTIONS]
            if max(share) > 0.8:
                dominant.append(f"Level {level}, {name}: {ACTIONS[share.index(max(share))]} dipilih {pct(max(share))} dari semua keputusan.")
            md.append(f"| {level} | {name} | {sum(e['decisions'] for e in eps) / len(eps):.2f} | " +
                      " | ".join(pct(s) for s in share) + " |")
    md.append("")

    # Training.
    md += ["## Training", ""]
    spans = timings(os.path.join(args.results, "timings.txt"))
    q_logs = [os.path.join(args.logs, f) for f in ("q_train_all.log", "q_train.log") if os.path.exists(os.path.join(args.logs, f))]
    if q_logs:
        q_eps = parse(q_logs[0])
        write_csv(os.path.join(args.csv_dir, "q_train.csv"), q_eps)
        levels = sorted({e["level"] for e in q_eps})
        md.append(f"- Q-learning: {len(q_eps)} episode, level {', '.join(map(str, levels))}, "
                  f"waktu {spans.get('q', 0) / 3600:.1f} jam.")
    table = os.path.join(args.results, "q", "hunter_qtable.json")
    if os.path.exists(table):
        trained, rows = greedy_policy(table)
        md.append(f"- Tabel Q: {len(rows)}/48 state terkunjungi, {trained} episode tersimpan di tabel.")
        unvisited = sorted(set(range(48)) - {r[0] for r in rows})
        if unvisited:
            md.append(f"- State belum terkunjungi: {', '.join(f's{s:02d}' for s in unvisited)}.")
        md += ["", "<details><summary>Aksi greedy per state</summary>", "", "| State | Arti | Aksi | Q |", "|---|---|---|---:|"]
        md += [f"| s{s:02d} | `{label.strip()}` | {action} | {value:+.3f} |" for s, label, action, value in rows]
        md += ["", "</details>"]
    snaps = sorted(glob.glob(os.path.join(args.results, "q", "snapshots", "ep*.json")))
    if len(snaps) >= 2:
        md += ["", "Konvergensi tabel Q (aksi greedy per state dibanding snapshot sebelumnya; hanya state yang terkunjungi di keduanya):", "",
               "| Snapshot | State terkunjungi | Aksi greedy berubah |", "|---|---:|---:|"]
        prev = None
        for snap in snaps + ([table] if os.path.exists(table) else []):
            _, rows = greedy_policy(snap)
            cur = {r[0]: r[2] for r in rows}
            name = os.path.basename(snap) if snap != table else "akhir"
            if prev is not None:
                common = set(prev) & set(cur)
                changed = sum(prev[s_] != cur[s_] for s_ in common)
                md.append(f"| {name} | {len(cur)}/48 | {changed}/{len(common)} ({pct(changed / len(common)) if common else '–'}) |")
            else:
                md.append(f"| {name} | {len(cur)}/48 | – |")
            prev = cur
    for spec in args.extra_table:
        label, path = spec.split("=", 1)
        if os.path.exists(path):
            trained, rows = greedy_policy(path)
            md.append(f"- {label}: {len(rows)}/48 state terkunjungi, {trained} episode tersimpan di tabel.")
    if "ppo" in spans:
        md.append(f"- PPO: waktu {spans['ppo'] / 3600:.1f} jam (lihat kurva di TensorBoard, `results/<run-id>`).")
    md.append("")

    # Issues found automatically.
    md += ["## Catatan masalah", ""]
    issues = list(dominant)
    for (key, level), eps in data.items():
        t = sum(e["timeout"] for e in eps) / len(eps)
        if t > 0.1:
            issues.append(f"Level {level}, {dict(METHODS)[key]}: {pct(t)} misi berakhir karena batas 180 dtk.")
        if len(eps) < 350:
            issues.append(f"Level {level}, {dict(METHODS)[key]}: hanya {len(eps)} misi (target 350).")
    for watch in sorted(glob.glob(os.path.join(args.results, "*.watch.txt"))):
        verdict = [line.strip() for line in open(watch) if line.startswith("verdict:")]
        if verdict and "STOP" in verdict[-1]:
            issues.append(f"PPO `{os.path.basename(watch)[:-10]}` dihentikan aturan 30k step: {verdict[-1][9:]}. "
                          "Hasil PPO di atas memakai model terakhir yang tersimpan.")
    missing = [f"L{lv} {name}" for lv in args.levels for key, name in METHODS if (key, lv) not in data and key not in OPTIONAL]
    if missing:
        issues.append("Belum ada log untuk: " + ", ".join(missing) + ".")
    issues.append("Waktu Hunter < 4 m dari pintu keluar belum tercatat di log `HUNTER`, jadi batas camping 25% belum bisa diuji "
                  "(usulan kode di `ml/rl-expansion.md`).")
    issues.append("Layout per level memakai seed tetap (`CampaignLevels.cs`: `LayoutSeed => 2401 + Number * 137`), jadi evaluasi memakai layout yang sama "
                  "dengan training. Uji layout baru butuh flag seed (usulan di `ml/rl-expansion.md`).")
    md += [f"- {i}" for i in issues] + [""]

    with open(args.out, "w") as f:
        f.write("\n".join(md))
    print(f"wrote {args.out} ({len(data)} eval logs)")


if __name__ == "__main__":
    main()
