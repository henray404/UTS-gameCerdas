## Kesimpulan (Tahap B, build v1 `85992cf`, lab 27–28 Nov 2026)

1. **Di level 1, Hunter mengalahkan FSM, tetapi bukan karena aksi yang dipelajari.** Q-learning menangkap 167/350 (47,7%) vs FSM 126/350 (36,0%), p = 0,002. Pembanding "Hunter selalu GoLastKnown" (heuristik, mode build yang sama) juga menangkap **167/350 (47,7%)**. Selisih Q-learning vs heuristik +0 poin, p = 1,000: **belum signifikan**. Keunggulan atas FSM datang dari mekanik hunt loop (keputusan ulang setiap ada bukti baru, kembali patroli setelah memori 15 dtk, tanpa `searchDuration` FSM), bukan dari pemilihan aksi.
2. **Q-learning dengan tabel awal optimis** (semua Q = +0,5) menangkap 189/350 (54,0%) di L1: +6 poin vs heuristik, p = 0,096, **belum signifikan**. Tabelnya belum konvergen (25–35% aksi greedy masih berubah per 250 episode).
3. **PPO belajar memilih GoLastKnown.** Deterministik: 100% GoLastKnown di semua level. Stokastik: 81–84% GoLastKnown. Jadi PPO dan Q-learning sampai pada kebijakan yang sama: di kontrak dan lawan sekarang, GoLastKnown hampir selalu pilihan terbaik.
4. **Angka PPO di tabel tidak setara dengan baris lain.** PPO dievaluasi lewat `mlagents-learn --inference` karena `-hunter-ppo-eval` di build hanya menjalankan ONNX yang ikut terpanggang. Di mode itu trainer memakai capture frame rate 60 (Δt = 4/60 dtk per frame), sedangkan build berjalan mengikuti jam. Dengan kebijakan yang identik (GoLastKnown), mode ML-Agents memberi 62,9% di L1 vs 47,7% di mode build (+15 poin, p < 0,001). Selisih PPO vs FSM di L1 karena itu **efek mode simulasi**, bukan hasil belajar. Evaluasi adil butuh build yang memuat `HunterGuard.onnx` di Resources.
5. **Level 3 dan 6 tidak membedakan metode.** ThiefBot lolos 0% di semua metode karena selalu tertangkap monster FSM lain; catch rate di sana hanya mengukur siapa yang lebih dulu menangkap. Tidak ada selisih yang signifikan di L3/L6.
6. **Curriculum PPO terlalu longgar.** Lesson naik ke L6 di step ±6.000 (threshold reward 0,0, 200 episode), sehingga ±93% training terjadi di L6. Kenaikan reward −0,12 → +1,0 terutama mencerminkan perpindahan ke level tempat bot tidak bisa lolos.
7. **Cakupan state struktural.** Q-learning 34/48 dan Q optimis 36/48 state setelah 3000 episode. 10 dari 12 state yang tidak pernah muncul di kedua run adalah state "semua target sudah diambil": bot jarang sampai ke situasi itu saat Hunter sedang mencari.

Kurva training PPO (`results/hunter_ppo_v1`, 5 env, 1,3 jam): reward rata-rata per episode dengan keputusan Hunter.

| Step | 2k | 6k | 10k | 20k | 30k | 50k | 70k | 100k |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Reward | −0,12 | +0,10 | +0,84 | +0,89 | +0,94 | +0,98 | +1,01 | +1,00 |
| Lesson | L1 | L6 | L6 | L6 | L6 | L6 | L6 | L6 |

### Yang dikirim ke game

- `Resources/Adaptive/hunter_qtable.json`: tabel Q-learning biasa (3000 episode, 34/48 state, hampir konvergen). Tabel optimis tidak dipilih karena selisihnya belum signifikan dan masih menyimpan nilai awal +0,5 di aksi yang belum dicoba.
- `Resources/Adaptive/HunterGuard.onnx`: PPO 100k step (opset 9, obs 28 + 18 ray, mask 6). Dalam inference deterministik perilakunya setara GoLastKnown.

### Masalah teknis yang ditemukan dan diperbaiki di lab

| Masalah | Dampak | Perbaikan |
|---|---|---|
| torch 2.12 (versi terbaru yang dipilih `uv`) memakai eksportir ONNX dynamo | `HunterGuard.onnx` gagal ditulis | `torch==2.5.1` di `ml/requirements.txt`; ekspor dicek opset 9 |
| Thread pekerja Unity berputar di core kosong | 1 game ≈ 1,6 core, PPO hanya dapat 1–3 env | `-job-worker-count 0`: kecepatan sama, ≈ 0,9 core |
| 10 env di rencana | Tidak muat di mesin bersama (12 core) | 5 env; 100k step selesai 1,3 jam (±25 step/dtk) |
| Tar dari macOS | 201 file `._*` | Dihapus otomatis; laptop memakai `COPYFILE_DISABLE=1` |

### Usulan berikutnya (sudah dikerjakan di build v2 `42fbf5f`)

`-hunter-solo`, `-thiefbot-skill`, `-hunter-seed-offset`/`random`, `-hunter-eps-decay`, dan field log `exitTime` (batas camping 25%). Hasil v2 dilaporkan terpisah. Yang masih perlu di laptop: memasukkan `HunterGuard.onnx` ke build supaya PPO bisa dievaluasi di mode build yang sama dengan FSM dan Q-learning.
