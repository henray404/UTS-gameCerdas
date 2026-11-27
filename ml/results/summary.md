# Hasil training dan evaluasi The Hunter

Dibuat otomatis oleh `ml/make_summary.py` pada 2026-11-28 dari log lab. Evaluasi tanpa belajar (epsilon 0). Uji: dua proporsi dua sisi terhadap FSM di level yang sama, CI 95% Wilson, plus p terkoreksi Holm untuk semua perbandingan sekaligus. Selisih dengan p ≥ 0.05 ditulis **belum signifikan**.

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

## Catch rate per level × metode

| Level | Metode | Tangkap / misi | Catch rate | CI 95% | Lolos | Timeout | Selisih vs FSM | p | p (Holm) | Kesimpulan |
|---:|---|---:|---:|---|---:|---:|---:|---:|---:|---|
| 1 | FSM (baseline) | 126/350 | 36.0% | 31.1%–41.2% | 64.0% | 0.0% | – | – | – | pembanding |
| 1 | Q-learning | 167/350 | 47.7% | 42.5%–52.9% | 52.3% | 0.0% | +11.7 poin | 0.002 | 0.020 | signifikan lebih tinggi |
| 1 | Q-learning (tabel awal optimis) | 189/350 | 54.0% | 48.8%–59.1% | 45.7% | 0.3% | +18.0 poin | 0.000 | 0.000 | signifikan lebih tinggi |
| 1 | Hunter selalu GoLastKnown (heuristik) | 167/350 | 47.7% | 42.5%–52.9% | 52.3% | 0.0% | +11.7 poin | 0.002 | 0.020 | signifikan lebih tinggi |
| 1 | PPO deterministik (mode ML-Agents) | 220/350 | 62.9% | 57.7%–67.8% | 37.1% | 0.0% | +26.9 poin | 0.000 | 0.000 | signifikan lebih tinggi |
| 1 | PPO stokastik (mode ML-Agents) | 211/350 | 60.3% | 55.1%–65.3% | 39.7% | 0.0% | +24.3 poin | 0.000 | 0.000 | signifikan lebih tinggi |
| 3 | FSM (baseline) | 31/350 | 8.9% | 6.3%–12.3% | 0.0% | 0.0% | – | – | – | pembanding |
| 3 | Q-learning | 31/350 | 8.9% | 6.3%–12.3% | 0.0% | 0.0% | +0.0 poin | 1.000 | 1.000 | belum signifikan |
| 3 | Q-learning (tabel awal optimis) | 25/350 | 7.1% | 4.9%–10.3% | 0.0% | 0.0% | -1.7 poin | 0.403 | 1.000 | belum signifikan |
| 3 | Hunter selalu GoLastKnown (heuristik) | 40/350 | 11.4% | 8.5%–15.2% | 0.0% | 0.0% | +2.6 poin | 0.260 | 1.000 | belum signifikan |
| 3 | PPO deterministik (mode ML-Agents) | 35/350 | 10.0% | 7.3%–13.6% | 0.0% | 0.0% | +1.1 poin | 0.605 | 1.000 | belum signifikan |
| 3 | PPO stokastik (mode ML-Agents) | 47/350 | 13.4% | 10.3%–17.4% | 0.0% | 0.0% | +4.6 poin | 0.055 | 0.546 | belum signifikan |
| 6 | FSM (baseline) | 295/350 | 84.3% | 80.1%–87.7% | 0.0% | 0.0% | – | – | – | pembanding |
| 6 | Q-learning | 288/350 | 82.3% | 77.9%–85.9% | 0.0% | 0.0% | -2.0 poin | 0.478 | 1.000 | belum signifikan |
| 6 | Q-learning (tabel awal optimis) | 284/350 | 81.1% | 76.7%–84.9% | 0.0% | 0.0% | -3.1 poin | 0.272 | 1.000 | belum signifikan |
| 6 | Hunter selalu GoLastKnown (heuristik) | 292/350 | 83.4% | 79.2%–87.0% | 0.0% | 0.0% | -0.9 poin | 0.758 | 1.000 | belum signifikan |
| 6 | PPO deterministik (mode ML-Agents) | 295/350 | 84.3% | 80.1%–87.7% | 0.0% | 0.0% | +0.0 poin | 1.000 | 1.000 | belum signifikan |
| 6 | PPO stokastik (mode ML-Agents) | 294/350 | 84.0% | 79.8%–87.5% | 0.0% | 0.0% | -0.3 poin | 0.918 | 1.000 | belum signifikan |

Ringkasan:

- Level 1, Q-learning: +11.7 poin vs FSM, p = 0.002 → signifikan lebih tinggi.
- Level 1, Q-learning (tabel awal optimis): +18.0 poin vs FSM, p = 0.000 → signifikan lebih tinggi.
- Level 1, Hunter selalu GoLastKnown (heuristik): +11.7 poin vs FSM, p = 0.002 → signifikan lebih tinggi.
- Level 1, PPO deterministik (mode ML-Agents): +26.9 poin vs FSM, p = 0.000 → signifikan lebih tinggi.
- Level 1, PPO stokastik (mode ML-Agents): +24.3 poin vs FSM, p = 0.000 → signifikan lebih tinggi.
- Level 3, Q-learning: +0.0 poin vs FSM, p = 1.000 → belum signifikan.
- Level 3, Q-learning (tabel awal optimis): -1.7 poin vs FSM, p = 0.403 → belum signifikan.
- Level 3, Hunter selalu GoLastKnown (heuristik): +2.6 poin vs FSM, p = 0.260 → belum signifikan.
- Level 3, PPO deterministik (mode ML-Agents): +1.1 poin vs FSM, p = 0.605 → belum signifikan.
- Level 3, PPO stokastik (mode ML-Agents): +4.6 poin vs FSM, p = 0.055 → belum signifikan.
- Level 6, Q-learning: -2.0 poin vs FSM, p = 0.478 → belum signifikan.
- Level 6, Q-learning (tabel awal optimis): -3.1 poin vs FSM, p = 0.272 → belum signifikan.
- Level 6, Hunter selalu GoLastKnown (heuristik): -0.9 poin vs FSM, p = 0.758 → belum signifikan.
- Level 6, PPO deterministik (mode ML-Agents): +0.0 poin vs FSM, p = 1.000 → belum signifikan.
- Level 6, PPO stokastik (mode ML-Agents): -0.3 poin vs FSM, p = 0.918 → belum signifikan.

## Distribusi aksi saat evaluasi

| Level | Metode | Keputusan / misi | GoLastKnown | SweepSector | InterceptExit | AmbushTarget | CheckHidingSpot | GiveUp |
|---:|---|---:|---:|---:|---:|---:|---:|---:|
| 1 | Q-learning | 2.92 | 73.3% | 2.0% | 10.4% | 0.1% | 14.3% | 0.0% |
| 1 | Q-learning (tabel awal optimis) | 4.71 | 5.0% | 43.2% | 49.4% | 0.0% | 1.1% | 1.3% |
| 1 | Hunter selalu GoLastKnown (heuristik) | 2.95 | 100.0% | 0.0% | 0.0% | 0.0% | 0.0% | 0.0% |
| 1 | PPO deterministik (mode ML-Agents) | 4.06 | 100.0% | 0.0% | 0.0% | 0.0% | 0.0% | 0.0% |
| 1 | PPO stokastik (mode ML-Agents) | 4.12 | 80.7% | 6.7% | 3.1% | 0.6% | 8.1% | 0.8% |
| 3 | Q-learning | 1.03 | 56.7% | 27.5% | 0.0% | 0.0% | 15.8% | 0.0% |
| 3 | Q-learning (tabel awal optimis) | 0.95 | 23.1% | 58.3% | 17.7% | 0.0% | 0.3% | 0.6% |
| 3 | Hunter selalu GoLastKnown (heuristik) | 1.21 | 100.0% | 0.0% | 0.0% | 0.0% | 0.0% | 0.0% |
| 3 | PPO deterministik (mode ML-Agents) | 1.03 | 100.0% | 0.0% | 0.0% | 0.0% | 0.0% | 0.0% |
| 3 | PPO stokastik (mode ML-Agents) | 1.25 | 81.3% | 4.8% | 5.0% | 2.5% | 5.9% | 0.5% |
| 6 | Q-learning | 4.81 | 25.7% | 38.0% | 2.9% | 0.5% | 32.8% | 0.0% |
| 6 | Q-learning (tabel awal optimis) | 4.41 | 29.4% | 21.4% | 46.1% | 0.6% | 0.0% | 2.4% |
| 6 | Hunter selalu GoLastKnown (heuristik) | 4.50 | 100.0% | 0.0% | 0.0% | 0.0% | 0.0% | 0.0% |
| 6 | PPO deterministik (mode ML-Agents) | 4.15 | 100.0% | 0.0% | 0.0% | 0.0% | 0.0% | 0.0% |
| 6 | PPO stokastik (mode ML-Agents) | 4.19 | 84.0% | 4.4% | 2.5% | 1.3% | 6.7% | 1.1% |

## Training

- Q-learning: 3000 episode, level 1, 2, 3, 4, 5, 6, waktu 3.3 jam.
- Tabel Q: 34/48 state terkunjungi, 3000 episode tersimpan di tabel.
- State belum terkunjungi: s02, s10, s14, s22, s26, s30, s32, s34, s35, s42, s43, s44, s46, s47.

<details><summary>Aksi greedy per state</summary>

| State | Arti | Aksi | Q |
|---|---|---|---:|
| s00 | `<3m new seen  target   hideNear` | InterceptExit | +0.448 |
| s01 | `<3m new seen  target` | GoLastKnown | +0.839 |
| s03 | `<3m new seen  noTarget` | GoLastKnown | +0.137 |
| s04 | `<3m new heard target   hideNear` | AmbushTarget | +0.242 |
| s05 | `<3m new heard target` | SweepSector | +0.594 |
| s06 | `<3m new heard noTarget hideNear` | GoLastKnown | +0.076 |
| s07 | `<3m new heard noTarget` | GoLastKnown | +0.224 |
| s08 | `<3m old seen  target   hideNear` | GoLastKnown | +0.086 |
| s09 | `<3m old seen  target` | AmbushTarget | +0.082 |
| s11 | `<3m old seen  noTarget` | InterceptExit | +0.052 |
| s12 | `<3m old heard target   hideNear` | AmbushTarget | +0.062 |
| s13 | `<3m old heard target` | CheckHidingSpot | +0.071 |
| s15 | `<3m old heard noTarget` | CheckHidingSpot | +0.000 |
| s16 | `3-8m new seen  target   hideNear` | CheckHidingSpot | +0.803 |
| s17 | `3-8m new seen  target` | CheckHidingSpot | +0.753 |
| s18 | `3-8m new seen  noTarget hideNear` | GoLastKnown | +0.100 |
| s19 | `3-8m new seen  noTarget` | InterceptExit | -0.370 |
| s20 | `3-8m new heard target   hideNear` | SweepSector | +0.034 |
| s21 | `3-8m new heard target` | GoLastKnown | +0.613 |
| s23 | `3-8m new heard noTarget` | GoLastKnown | +0.228 |
| s24 | `3-8m old seen  target   hideNear` | SweepSector | +0.000 |
| s25 | `3-8m old seen  target` | GoLastKnown | +0.106 |
| s27 | `3-8m old seen  noTarget` | SweepSector | +0.074 |
| s28 | `3-8m old heard target   hideNear` | GoLastKnown | +0.070 |
| s29 | `3-8m old heard target` | AmbushTarget | +0.274 |
| s31 | `3-8m old heard noTarget` | SweepSector | +0.000 |
| s33 | `>8m new seen  target` | GoLastKnown | +0.388 |
| s36 | `>8m new heard target   hideNear` | CheckHidingSpot | +0.033 |
| s37 | `>8m new heard target` | SweepSector | +0.366 |
| s38 | `>8m new heard noTarget hideNear` | InterceptExit | +0.000 |
| s39 | `>8m new heard noTarget` | SweepSector | +0.000 |
| s40 | `>8m old seen  target   hideNear` | GoLastKnown | +0.091 |
| s41 | `>8m old seen  target` | GoLastKnown | +0.324 |
| s45 | `>8m old heard target` | GoLastKnown | +0.110 |

</details>

Konvergensi tabel Q (aksi greedy per state dibanding snapshot sebelumnya; hanya state yang terkunjungi di keduanya):

| Snapshot | State terkunjungi | Aksi greedy berubah |
|---|---:|---:|
| ep0255.json | 16/48 | – |
| ep0500.json | 25/48 | 5/16 (31.2%) |
| ep0751.json | 27/48 | 2/25 (8.0%) |
| ep1003.json | 29/48 | 3/27 (11.1%) |
| ep1253.json | 30/48 | 3/29 (10.3%) |
| ep1505.json | 32/48 | 1/30 (3.3%) |
| ep1750.json | 32/48 | 3/32 (9.4%) |
| ep2007.json | 34/48 | 4/32 (12.5%) |
| ep2253.json | 34/48 | 2/34 (5.9%) |
| ep2504.json | 34/48 | 3/34 (8.8%) |
| ep2753.json | 34/48 | 2/34 (5.9%) |
| akhir | 34/48 | 3/34 (8.8%) |

- Q-learning tabel awal optimis: 36/48 state terkunjungi, 3000 episode tersimpan di tabel.
- PPO: waktu 1.3 jam (lihat kurva di TensorBoard, `results/<run-id>`).

## Catatan masalah

- Level 1, Hunter selalu GoLastKnown (heuristik): GoLastKnown dipilih 100.0% dari semua keputusan.
- Level 1, PPO deterministik (mode ML-Agents): GoLastKnown dipilih 100.0% dari semua keputusan.
- Level 1, PPO stokastik (mode ML-Agents): GoLastKnown dipilih 80.7% dari semua keputusan.
- Level 3, Hunter selalu GoLastKnown (heuristik): GoLastKnown dipilih 100.0% dari semua keputusan.
- Level 3, PPO deterministik (mode ML-Agents): GoLastKnown dipilih 100.0% dari semua keputusan.
- Level 3, PPO stokastik (mode ML-Agents): GoLastKnown dipilih 81.3% dari semua keputusan.
- Level 6, Hunter selalu GoLastKnown (heuristik): GoLastKnown dipilih 100.0% dari semua keputusan.
- Level 6, PPO deterministik (mode ML-Agents): GoLastKnown dipilih 100.0% dari semua keputusan.
- Level 6, PPO stokastik (mode ML-Agents): GoLastKnown dipilih 84.0% dari semua keputusan.
- Waktu Hunter < 4 m dari pintu keluar belum tercatat di log `HUNTER`, jadi batas camping 25% belum bisa diuji (usulan kode di `ml/rl-expansion.md`).
- Layout per level memakai seed tetap (`CampaignLevels.cs`: `LayoutSeed => 2401 + Number * 137`), jadi evaluasi memakai layout yang sama dengan training. Uji layout baru butuh flag seed (usulan di `ml/rl-expansion.md`).
