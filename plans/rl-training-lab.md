# Rencana Training RL di PC Lab (dnp-lab-planck)

Rencana kerja Anggota C untuk melatih dan mengevaluasi The Hunter: Q-learning (P13) dan PPO dengan Unity ML-Agents (P14). Desain agent ada di [`rl-enemy-design.md`](rl-enemy-design.md), setup trainer di [`../ml/README.md`](../ml/README.md).

## 1. Ringkasan

- **Laptop membuat build, lab melatih.** PC lab tidak punya Unity, dan player build Linux tidak butuh lisensi Unity. Semua perubahan kode C# dan build Linux dikerjakan di laptop (Fase 1). Lab hanya menjalankan build, trainer Python, dan analisis (Fase 2–5).
- **Yang dibandingkan:** FSM murni vs Q-learning vs PPO, dengan 6 aksi, reward, lawan (`ThiefBot`), dan metrik yang sama.
- **Metrik utama:** tingkat tangkap (catch rate) Hunter per level, dengan 95% CI Wilson dan uji dua proporsi.

## 2. Mesin

| | Laptop (Mac) | PC lab `dnp-lab-planck` |
|---|---|---|
| Akses | Lokal | `ssh dnp-lab-planck` (akun bersama `deenpeleb8h3b`, password; VPN ITS di luar kampus). Shortcut zsh: `dnp lab-planck` |
| Mesin | Apple Silicon | dnp2, 10.160.10.31, Manjaro x86_64, 12 core, 62 GB RAM, 2× RTX 2080 SUPER 8 GB, driver 615.71 |
| Unity | 6000.4.5f1, modul Mac saja | Tidak ada |
| Python | uv | `uv` di `/usr/bin/uv`, Python sistem 3.14 (3.10 dipasang lewat uv) |
| Tugas | Kode C#, build Linux, tes compile | Training, evaluasi, analisis |

Akun lab dipakai bersama orang lain. Semua pekerjaan ditaruh di satu folder (`~/hollowhouse-rl`), pakai paling banyak 1 GPU, jalankan proses panjang di `tmux`, dan jangan ubah file di luar folder itu.

## 3. Kondisi sekarang (27 Nov 2026)

Sudah ada di working tree laptop, **belum di-commit**:

| Bagian | File |
|---|---|
| Kontrak 6 aksi, 28 observasi, 48 state, mask, reward | `Assets/SneakHouse/Scripts/Adaptive/RL/HunterContract.cs` |
| Bot lawan | `ThiefBot.cs`, `ThiefPolicy.cs` |
| Q-learning dan integrasi ke game | `QLearningHunter.cs`, `HunterDecision.cs`, hook di `GuardBrain.cs` dan `PlayerMotor.cs` |
| Runner batch editor | `Assets/SneakHouse/Editor/HunterTrainingRunner.cs` |
| Tabel Q hasil 200 episode (level 1) | `Assets/SneakHouse/Resources/Adaptive/hunter_qtable.json` |
| Tes (472 asersi) | `Tests/HunterRulesTests.cs`, dijalankan `Tests/run-campaign-rules.sh` |
| Analisis log | `ml/hunter_log_summary.py` |
| Trainer Python | `ml/requirements.txt`, `ml/build-constraints.txt` |

Hasil terakhir (level 1, 100 misi per sisi): Hunter Q-learning menangkap 46% (95% CI 37–56%), FSM 36% (27–46%), p = 0,15. Belum signifikan.

Flag yang sudah ada: `-thiefbot`, `-hunter`, `-hunter-train`, `-hunter-eval`, `-hunter-baseline`, `-hunter-ppo-train`, `-hunter-ppo-eval`, `-hunter-episodes N`, `-hunter-level 1..6|random`, `-hunter-table <path>` (daftar lengkap di `ml/README.md`). Setiap misi menulis satu baris `HUNTER episode=... won=... hunterCaught=... time=... decisions=... actions=[...]` ke log.

## 4. Fase 0: prasyarat (laptop)

| # | Langkah | Selesai kalau |
|---|---|---|
| 0.1 | Commit dan push kode RL ke `fp_eas` | `git log origin/fp_eas` memuat commit RL |
| 0.2 | Pasang Unity **6000.6.0f1** (changeset `f7f8ed4d1e24`) + modul **Linux Build Support (Mono)** lewat Unity Hub. Lisensi di Mac sudah aktif | `Unity -version` = 6000.6.0f1 dan folder `PlaybackEngines/LinuxStandaloneSupport` ada |
| 0.3 | Jalankan `Tests/run-campaign-rules.sh` dan buka proyek sekali di 6000.6 | 426 + 55 + 472 asersi lolos, tidak ada error compile |

## 5. Fase 1: kode sebelum ke lab (laptop, butuh Unity)

### 1a. Q-learning di player Linux

| Perubahan | Alasan |
|---|---|
| `HunterDecision`: kalau bukan editor dan `CompletedEpisodes >= TargetEpisodes`, panggil `Application.Quit()` | Di player belum ada runner editor yang menghentikan proses |
| Flag `-hunter-level N` dan `-hunter-level random` (acak 1–6 tiap misi) | Training sekarang hanya level 1, 26/48 state terkunjungi |
| Mode training/eval melewati kunci level (`GameSession.StartLevel` menolak level terkunci). Hook kecil, misalnya `GameSession.TrainingUnlock` yang hanya diisi `HunterDecision` | Level 2–6 terkunci di profil baru |
| Flag `-hunter-table <path>` untuk lokasi tabel Q | Supaya beberapa run (misalnya per level) tidak saling menimpa |

### 1b. PPO dengan ML-Agents

| Perubahan | Catatan |
|---|---|
| Tambah `com.unity.ml-agents` **4.2.0** ke `Packages/manifest.json` | Butuh Unity ≥ 6000.0; dependensi `com.unity.ai.inference` 2.6.1 |
| `Adaptive/RL/HunterAgent.cs : Agent` | `CollectObservations` = `HunterContract.Write` (28 float); `WriteDiscreteActionMask` = `HunterContract.IsAllowed` + cooldown GiveUp; `OnActionReceived` memakai eksekusi aksi yang sama dengan `HunterDecision`; `Heuristic` = GoLastKnown (perilaku FSM) |
| `RayPerceptionSensor3D` 9 ray (4 per sisi + depan, 360°), layer 8 (dinding) | Hanya di PPO |
| Keputusan diminta manual dengan `RequestDecision()` di titik yang sama dengan `HunterDecision.Decide` | Jangan pakai `DecisionRequester` per frame |
| Reward per keputusan dan terminal sama dengan Q-learning; `EndEpisode()` saat misi selesai, sebelum scene dimuat ulang | Episode = satu misi |
| Behavior Name `HunterGuard`, 1 branch diskrit ukuran 6 | Harus sama di Behavior Parameters, YAML, dan perintah training |
| Flag `-hunter-ppo-train` (training lewat `mlagents-learn`) dan `-hunter-ppo-eval` (inference dari `Resources/Adaptive/HunterGuard.onnx`) | Loop misi yang sama dengan Q-learning |
| Curriculum lewat `Academy.Instance.EnvironmentParameters.GetWithDefault("level", 1)` | Lesson diatur dari YAML |
| `ml/hunter_ppo.yaml` (lihat bagian 8) | |

### 1c. Build dan cek

| Langkah | Selesai kalau |
|---|---|
| Tes logika baru (level acak, parser flag) masuk `Tests/HunterRulesTests.cs` | `run-campaign-rules.sh` lolos |
| Uji editor: `mlagents-learn ml/hunter_ppo.yaml --run-id smoke` lalu Play, 2–3 menit | Log trainer menampilkan `Connected to Unity environment` dan step bertambah |
| Build Linux: `Unity -batchmode -quit -projectPath . -buildLinux64Player Builds/HollowHouseRL/HollowHouse.x86_64` (pola yang sama dengan `check-and-run.sh`) | File `HollowHouse.x86_64` dan folder `HollowHouse_Data` ada |
| Kemas: `tar czf hollowhouse-rl-build.tgz -C Builds HollowHouseRL` | |
| Commit dan push kode Fase 1 | |
| Salin build ke lab (password diketik sendiri): `scp hollowhouse-rl-build.tgz dnp-lab-planck:~/hollowhouse-rl/` | File ada di lab |

## 6. Fase 2: setup lab

```bash
mkdir -p ~/hollowhouse-rl && cd ~/hollowhouse-rl
git clone -b fp_eas https://github.com/henray404/UTS-gameCerdas.git repo
uv python install 3.10.12
uv venv --python 3.10.12 venv
uv pip install --python venv/bin/python --build-constraints repo/ml/build-constraints.txt -r repo/ml/requirements.txt
uv pip install --python venv/bin/python tensorboard
venv/bin/mlagents-learn --help | head -3
tar xzf hollowhouse-rl-build.tgz -C . && chmod +x HollowHouseRL/HollowHouse.x86_64
```

Cek build jalan tanpa layar, 3 misi:

```bash
HollowHouseRL/HollowHouse.x86_64 -batchmode -nographics -hunter-baseline -hunter-episodes 3 -logFile logs/smoke.log
grep HUNTER logs/smoke.log
```

Tabel Q player Linux disimpan di `~/.config/unity3d/Game Cerdas/Sneak House Prototype/hunter_qtable.json`, kecuali kalau `-hunter-table` dipakai.

## 7. Fase 3: Q-learning multi-level

| Run | Perintah (di `tmux`) | Perkiraan |
|---|---|---|
| Training | `HollowHouseRL/HollowHouse.x86_64 -batchmode -nographics -hunter-train -hunter-level random -hunter-episodes 1500 -hunter-table results/q/hunter_qtable.json -logFile logs/q_train.log` | Sekitar 6 dtk per misi di level 1, lebih lama di rumah besar: 3–5 jam |
| Ringkasan | `python3 repo/ml/hunter_log_summary.py logs/q_train.log results/q/hunter_qtable.json --csv results/q/train.csv --window 100` | |

Satu proses saja untuk training Q-learning, karena satu tabel tidak bisa ditulis beberapa proses sekaligus. Epsilon turun dalam 150 episode pertama. Kalau state yang terkunjungi masih kurang dari 40/48 setelah 1500 episode, lanjutkan sampai 3000 (tabel dilanjutkan dari file).

## 8. Fase 4: PPO

**Masalah anggaran step.** Hunter hanya mengambil sekitar 3 keputusan per misi. Trainer ML-Agents menghitung step per keputusan, jadi `max_steps: 2e6` dari slide akan butuh berhari-hari. Anggarannya dihitung dari keputusan:

- 10 env paralel × sekitar 0,5 keputusan per detik ≈ 5 keputusan per detik ≈ 18 ribu per jam.
- `max_steps: 100000` ≈ 5–6 jam.

`ml/hunter_ppo.yaml` awal:

```yaml
behaviors:
  HunterGuard:
    trainer_type: ppo
    hyperparameters: {batch_size: 256, buffer_size: 4096, learning_rate: 3.0e-4, beta: 5.0e-3, epsilon: 0.2, lambd: 0.95, num_epoch: 3}
    network_settings: {normalize: false, hidden_units: 128, num_layers: 2}
    reward_signals: {extrinsic: {gamma: 0.95, strength: 1.0}}
    time_horizon: 64
    max_steps: 100000
    summary_freq: 2000
    checkpoint_interval: 10000
environment_parameters:
  level:
    curriculum:
      - {name: L1, completion_criteria: {measure: reward, behavior: HunterGuard, threshold: 0.0, min_lesson_length: 200}, value: 1}
      - {name: L3, completion_criteria: {measure: reward, behavior: HunterGuard, threshold: 0.0, min_lesson_length: 200}, value: 3}
      - {name: L6, value: 6}
```

Gamma 0,95 disamakan dengan Q-learning supaya perbandingannya adil. Threshold curriculum perlu disesuaikan setelah melihat reward rata-rata di lesson pertama.

```bash
CUDA_VISIBLE_DEVICES=<gpu paling kosong> venv/bin/mlagents-learn repo/ml/hunter_ppo.yaml --run-id hunter_ppo_v1 \
  --env HollowHouseRL/HollowHouse.x86_64 --num-envs 10 --no-graphics --time-scale 4 --timeout-wait 600 --env-args -hunter-ppo-train
venv/bin/tensorboard --logdir results --port 6006   # dari laptop: ssh -L 6006:localhost:6006 dnp-lab-planck
```

Pilih GPU dengan `nvidia-smi`. Jaringan PPO-nya kecil, jadi `--num-envs` (CPU) lebih menentukan kecepatan daripada GPU. Simpan checkpoint 30k, 60k, dan 100k step; DDA nanti bisa memakai checkpoint awal sebagai Hunter yang lebih lemah.

| Masalah | Cek |
|---|---|
| Reward tidak naik | Distribusi aksi (TensorBoard), mask, apakah `EndEpisode` terpanggil |
| Hunter diam di pintu keluar | % waktu < 4 m dari exit (batas 25%) |
| Episode tidak berakhir | Batas misi 180 dtk aktif |

## 9. Fase 5: evaluasi dan laporan

Tanpa belajar, epsilon 0, per level 1, 3, dan 6, masing-masing **350 misi** (cukup untuk mendeteksi selisih sekitar 10 poin di α = 0,05):

```bash
for L in 1 3 6; do for M in baseline eval ppo-eval; do
  HollowHouseRL/HollowHouse.x86_64 -batchmode -nographics -hunter-$M -hunter-level $L -hunter-episodes 350 \
    -hunter-table results/q/hunter_qtable.json -logFile logs/eval_${M}_L$L.log
done; done
python3 repo/ml/hunter_log_summary.py logs/eval_eval_L1.log --against logs/eval_baseline_L1.log
```

Evaluasi tidak menulis tabel, jadi beberapa run boleh paralel (maksimal 6 proses, sisakan core untuk orang lain).

**Hasil yang di-commit ke repo** (bukan build, bukan log mentah besar):

| File | Isi |
|---|---|
| `Assets/SneakHouse/Resources/Adaptive/hunter_qtable.json` | Tabel Q multi-level |
| `Assets/SneakHouse/Resources/Adaptive/HunterGuard.onnx` | Model PPO terbaik |
| `ml/results/summary.md` | Tabel catch rate per level × metode (CI, p), distribusi aksi, durasi training, state terkunjungi |
| `ml/results/*.csv` | CSV per run dari `hunter_log_summary.py --csv` |

## 10. Jadwal

| Fase | Di mana | Perkiraan |
|---|---|---|
| 0 Prasyarat | Laptop | 1–2 jam (unduh Unity) |
| 1 Kode + build | Laptop | 1–1,5 hari |
| 2 Setup lab | Lab | 30 menit |
| 3 Q-learning | Lab | 3–5 jam (bisa ditinggal) |
| 4 PPO | Lab | 6–8 jam (bisa ditinggal) |
| 5 Evaluasi + laporan | Lab | 3–4 jam proses + 1 jam menulis |

## 11. Risiko

| Risiko | Penanganan |
|---|---|
| ML-Agents 4.2.0 bermasalah di Unity 6000.6 | Cek di Fase 1c sebelum build; kalau gagal, laporan memakai FSM vs Q-learning saja |
| Build tidak jalan di Manjaro (library hilang) | Cek di Fase 2 dengan smoke test; lihat `Player.log` |
| Mesin bersama sedang dipakai | Cek `nvidia-smi` dan `htop` dulu; kurangi `--num-envs` |
| VPN putus | Semua run panjang di `tmux` |
| Arah game berubah ke kumpul-loot dengan kuota | Kontrak bersifat append-only; training ulang setelah loop kuota final |
| Perubahan file bersama (`GuardBrain`, `PlayerMotor`, `GameSession`) | Kabari Anggota A dan B sebelum push |

## 12. Prompt untuk agent di lab

Salin blok berikut ke Claude Code di `dnp-lab-planck` setelah Fase 0 dan 1 selesai dan build sudah ada di `~/hollowhouse-rl/hollowhouse-rl-build.tgz`.

```text
Kamu bekerja di PC lab dnp-lab-planck (Manjaro x86_64, 12 core, 62 GB RAM, 2x RTX 2080 SUPER 8 GB). Akun ini dipakai bersama orang lain: kerjakan semuanya di ~/hollowhouse-rl, jangan ubah file di luar folder itu, pakai paling banyak 1 GPU (pilih yang paling kosong di nvidia-smi), sisakan minimal 2 core, dan jalankan proses panjang di tmux dengan nama sesi berawalan hh-. Jangan pernah meminta, mengetik, atau menyimpan password, token, atau kredensial.

KONTEKS
Proyek: Hollow House, game horor stealth first-person di Unity 6000.6, final project mata kuliah Game Cerdas. Repo: https://github.com/henray404/UTS-gameCerdas.git, branch fp_eas. Aku Anggota C (Enemy AI, ML, RL). Monster "The Hunter" (guards[0]) memakai RL untuk memilih 1 dari 6 aksi saat kehilangan jejak pemain: GoLastKnown, SweepSector, InterceptExit, AmbushTarget, CheckHidingSpot, GiveUp. FSM tetap mengatur Patrol/Chase; NavMesh menjalankan aksi. Lawan training adalah ThiefBot (bot pemain berbasis aturan). Reward: +1 tangkap, -1 pemain lolos, +0.2 melihat lagi, +0.3/-0.02 cek tempat sembunyi, -0.001 per keputusan. Tahap A = Q-learning tabular 48 state x 6 aksi (sudah jalan: level 1, Hunter 46% vs FSM 36%, p=0.15). Tahap B = PPO lewat Unity ML-Agents 4.2.0 dengan 28 observasi + ray sensor, behavior name HunterGuard. Di mesin ini tidak ada Unity; kamu hanya menjalankan player build Linux yang sudah jadi.
Baca dulu: repo/plans/rl-training-lab.md (rencana ini, bagian 6-9), repo/plans/rl-enemy-design.md, repo/ml/README.md.

TUGAS
1. Setup (bagian 6): clone repo ke ~/hollowhouse-rl/repo, buat venv Python 3.10.12 dengan uv, pasang trainer dari repo/ml/requirements.txt dengan repo/ml/build-constraints.txt, ekstrak build ke ~/hollowhouse-rl/HollowHouseRL, lalu smoke test 3 misi baseline. Berhenti dan laporkan kalau build gagal jalan; jangan coba memperbaiki kode C#.
2. Q-learning (bagian 7): training -hunter-level random sampai 1500 episode, lalu ringkas dengan repo/ml/hunter_log_summary.py.
3. PPO (bagian 8): pakai repo/ml/hunter_ppo.yaml, --num-envs 10 (turunkan kalau mesin sedang dipakai orang lain), pantau reward, distribusi aksi, dan durasi episode. Kalau reward tidak naik setelah 30k step, berhenti dan laporkan dengan grafik/angkanya; jangan mengubah reward atau kontrak.
4. Evaluasi (bagian 9): level 1, 3, 6 x {baseline, eval, ppo-eval} x 350 misi, lalu bandingkan dengan --against.
5. Tulis ml/results/summary.md: tabel catch rate per level x metode dengan 95% CI dan p-value, distribusi aksi, waktu training, state Q yang terkunjungi, catatan masalah. Klaim harus sesuai angka; selisih dengan p >= 0.05 ditulis "belum signifikan".
6. Salin hunter_qtable.json dan HunterGuard.onnx terbaik ke repo/Assets/SneakHouse/Resources/Adaptive/, tambahkan CSV ke ml/results/, lalu commit dan push ke fp_eas. Jangan commit build, venv, log mentah, atau folder results ML-Agents.

ATURAN
- Yang boleh kamu ubah di repo: ml/, plans/rl-training-lab.md (bagian status), dan file hasil di Resources/Adaptive/. Jangan ubah kode C#, scene, atau prefab; kalau butuh perubahan kode, tulis usulannya di laporan.
- Pesan commit satu baris, maksimal 72 karakter, imperatif (contoh: "add multi-level q-learning results"), tanpa body, tanpa baris Co-Authored-By.
- Sebelum push, jalankan git pull --rebase; kalau ada konflik, berhenti dan laporkan.
- Setiap selesai satu tugas, laporkan singkat: apa yang jalan, angka utama, file yang dihasilkan, dan perkiraan sisa waktu.
```

## 13. Status

- [ ] Fase 0: prasyarat
- [ ] Fase 1: kode + build
- [ ] Fase 2: setup lab
- [ ] Fase 3: Q-learning multi-level
- [ ] Fase 4: PPO
- [ ] Fase 5: evaluasi + laporan
