# ML dan RL: setup trainer

Desain agent ada di [`../plans/rl-enemy-design.md`](../plans/rl-enemy-design.md).

## Versi

| Komponen | Versi | Catatan |
|---|---|---|
| Unity package `com.unity.ml-agents` | 4.2.0 | Minimal Unity 6000.0, memakai `com.unity.ai.inference` 2.6.1 |
| Python trainer `mlagents` | 1.1.0 | Butuh Python 3.10.1–3.10.12 |
| Python | 3.10.12 | Dipasang lewat `uv` |

## Pasang trainer (macOS dan Linux)

```bash
uv venv --python 3.10.12 ml/.venv
uv pip install --python ml/.venv/bin/python --build-constraints ml/build-constraints.txt -r ml/requirements.txt
ml/.venv/bin/mlagents-learn --help
```

Dua jebakan yang sudah ditangani oleh file di folder ini:

- `mlagents` 1.1.0 mematok `grpcio==1.48.2`, yang tidak punya wheel untuk Mac Apple Silicon. Wheel itu di-build dari source dan butuh `setuptools` lama. Batasan build-nya ada di `build-constraints.txt`. Build pertama di Mac makan waktu beberapa menit. Di Linux x86 (lab `dnp`) wheel-nya sudah tersedia.
- `mlagents-learn` masih meng-import `pkg_resources`, yang sudah dihapus dari setuptools 81, jadi `requirements.txt` mematok `setuptools<70`.

`ml/.venv/` dan `ml/results/` tidak ikut di-commit.

## Tahap A: Q-learning (tanpa Python)

Hunter dan bot hanya aktif lewat flag command line. Tanpa flag, game berjalan seperti biasa.

| Flag | Efek |
|---|---|
| `-thiefbot` | `ThiefBot` mengendalikan pemain (di game juga bisa dinyalakan atau dimatikan dengan F6) |
| `-hunter` | `guards[0]` menjadi Hunter dan memakai tabel Q yang sudah dilatih |
| `-hunter-train` | Hunter belajar melawan `ThiefBot` di level 1; misi diulang otomatis dengan kecepatan 4×, skor dan progres tidak disimpan |
| `-hunter-eval` | Seperti training, tapi Hunter memakai tabel dari Resources secara greedy tanpa belajar |
| `-hunter-baseline` | Loop misi yang sama dengan monster FSM biasa, sebagai pembanding |
| `-hunter-ppo-train` | Hunter dikendalikan policy ML-Agents (`HunterAgent`) yang dilatih `mlagents-learn ml/hunter_ppo.yaml` |
| `-hunter-ppo-eval` | Policy dari `Resources/Adaptive/HunterGuard.onnx` tanpa trainer (kalau file belum ada: heuristik GoLastKnown) |
| `-hunter-episodes N` | Berhenti setelah N episode (untuk training Q: setelah tabel berisi N episode). Player build keluar sendiri |
| `-hunter-level 1..6` atau `random` | Level tiap misi (default 1). Mode loop membuka semua level tanpa menyimpan progres |
| `-hunter-table <path>` | Lokasi tabel Q untuk dibaca dan ditulis, menggantikan `persistentDataPath` dan Resources |
| `-hunter-solo` | Mode loop saja: hanya `guards[0]` (Hunter) yang aktif, monster lain dinonaktifkan |
| `-thiefbot-skill 0..1` | Keahlian bot; bobot tiap episode diacak ±0,15 di sekitar nilai ini. Makin tinggi: makin sering mengendap, bersembunyi, melempar kaleng, menunggu monster yang menghalangi rute, dan memilih target yang menjauhi monster terakhir yang terlihat/terdengar. Tanpa flag: perilaku lama |
| `-hunter-seed-offset K` | Mode loop saja: seed layout = `LayoutSeed + K` (K ≥ 0) untuk interior, rumah, dan NavMesh. Beat patroli yang gagal validasi jatuh ke rute bawaan |
| `-hunter-seed random` | Seperti di atas, K diacak 0–999 tiap misi (mengalahkan `-hunter-seed-offset`) |
| `-hunter-eps-decay N` | Epsilon Q-learning turun ke 0,05 dalam N episode (default 150) |

Baris `HUNTER` juga diakhiri `seed=<seed> solo=<True|False> skill=<x.xx> exitTime=<detik>`. `skill=-1.00` artinya tanpa `-thiefbot-skill`, dan `exitTime` adalah lama `guards[0]` berada < 4 m dari pintu keluar.

Mode training dan evaluasi membatasi misi 180 detik (dicatat `timeout=True`).

Training dari editor, di **salinan proyek** (jangan di folder repo yang sedang terbuka di Unity):

```bash
Unity -batchmode -nographics -projectPath <salinan> \
  -executeMethod SneakHouseEditor.HunterTrainingRunner.Run -hunter-train -hunter-episodes 200 -logFile train.log
grep HUNTER train.log
```

Ringkasan per 20 episode (tingkat tangkap, tingkat lolos bot, distribusi aksi) dan aksi greedy per state:

```bash
python3 ml/hunter_log_summary.py train.log "<persistentDataPath>/hunter_qtable.json" --csv ml/results/hunter.csv
```

Membandingkan evaluasi dengan baseline (interval kepercayaan Wilson 95% dan uji dua proporsi):

```bash
python3 ml/hunter_log_summary.py eval.log --against baseline.log
```

Setiap misi menulis satu baris `HUNTER episode=... won=... hunterCaught=... actions=[...]`, dan tabel disimpan ke `persistentDataPath/hunter_qtable.json` setelah setiap misi, jadi training bisa dilanjutkan. Untuk dipakai di game, salin file itu ke `Assets/SneakHouse/Resources/Adaptive/hunter_qtable.json`.

## Lab: satu pintu untuk Tugas 1–6

`ml/lab/hh.sh` menjalankan seluruh pipeline lab (setup, smoke test, Q-learning, PPO, evaluasi, laporan) di tmux `hh-*`, dengan batas core dan pemilihan GPU paling kosong. Script ini sudah diuji end-to-end dengan build tiruan. File terkait:

| File | Isi |
|---|---|
| `ml/lab/hh.sh` | Runner lab; `hh.sh` tanpa argumen menampilkan cara pakai |
| `ml/hunter_ppo.yaml` | Config PPO (rencana lab bagian 8), sudah divalidasi dengan parser ML-Agents 1.1.0 |
| `ml/ppo_watch.py` | Reward, panjang episode, lesson, distribusi aksi, verdict "reward naik di 30k step" |
| `ml/make_summary.py` | Menulis `ml/results/summary.md` + CSV: catch rate, CI Wilson, p (dan Holm), distribusi aksi, state Q |
| `ml/rl-expansion.md` | Rencana perluasan RL (Tahap C) dan gerbang robustness terhadap perubahan map |
| `ml/hunter_ppo_solo.yaml` | Config PPO build v2 (curriculum L1→L3→L5→L6, threshold 0,3 / 500 episode, 200k step) |

Pengaturan runner lewat env (semua opsional): `HH_GAME_ARGS` (default `-job-worker-count 0`), `HH_Q_ARGS`, `HH_PPO_ARGS`, `HH_PPO_YAML`, `HH_RUN_ID`, `HH_EVAL_OFFSETS` (layout held-out, misi dibagi rata), `HH_PPO_EVAL` (`auto`/`python`/`build`/`none`), `HH_REPORT_DIR`. Setelan eksperimen v2 lengkap ada di `ml/results/v2/analysis_v2.md`.

## Tahap B: training PPO

Rencana lengkap training di PC lab, termasuk prompt untuk agent di sana: [`../plans/rl-training-lab.md`](../plans/rl-training-lab.md).

Training panjang dijalankan di lab `dnp` lewat `gpurun` dengan build Linux headless, bukan di laptop. Untuk percobaan singkat dari editor, jalankan `mlagents-learn <config>.yaml --run-id <nama>` lalu tekan Play di Unity.
