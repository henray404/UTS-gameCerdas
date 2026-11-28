# Hasil training dan evaluasi The Hunter: build v2 (layout held-out)

Dibuat otomatis oleh `ml/make_summary.py` pada 2026-11-28 dari log lab. Evaluasi tanpa belajar (epsilon 0). Uji: dua proporsi dua sisi terhadap FSM di level yang sama, CI 95% Wilson, plus p terkoreksi Holm untuk semua perbandingan sekaligus. Selisih dengan p ≥ 0.05 ditulis **belum signifikan**.

## Kesimpulan (build v2 `42fbf5f`, lab 28 Nov 2026)

Setelan training: `-hunter-solo` (hanya Hunter), `-thiefbot-skill 0.2` (hasil kalibrasi, lihat `../calibration_v2.md`), level acak 1–6, **layout acak** (`-hunter-seed random`, offset 0–999). Q-learning 3000 episode dengan `-hunter-eps-decay 600`; PPO 200k step dengan `ml/hunter_ppo_solo.yaml`.
Evaluasi: **layout held-out** (offset 1000–1004, tidak pernah dipakai saat training), 70 misi per layout = 350 misi per level, setelan bot dan mode solo sama.

1. **Robustness: tabel Q yang dilatih di layout acak jauh lebih tahan perubahan map.** Di layout held-out L1, tabel Q v1 (dilatih di satu layout tetap per level) hanya menangkap 117/350 (33,4%), lebih rendah dari FSM (178/350, 50,9%; −17 poin, p < 0,001) dan dari Hunter selalu GoLastKnown (188/350, 53,7%). Tabel Q v2 menangkap 188/350 (53,7%): **+20 poin vs Q v1, p < 0,001**. Q v1 menyimpang dari GoLastKnown (hanya 45% GoLastKnown, 30% SweepSector, 15% CheckHidingSpot), dan penyimpangan yang dipelajari di layout lama merugikan di layout baru. Catatan: kondisi uji juga memakai bot skill 0,2 dan mode solo, yang berbeda dari training v1, jadi penurunan Q v1 tidak bisa dibebankan seluruhnya ke layout.
2. **Kebijakan yang dipelajari tetap setara GoLastKnown.** Q v2 vs Hunter selalu GoLastKnown (keduanya mode build): L1 +0 poin (p = 1,00), L3 −3 (p = 0,21), L6 −3 (p = 0,27): **belum signifikan**. PPO v2 vs GoLastKnown di mode ML-Agents (mode yang sama): L1 +4 (p = 0,31), L3 +1 (p = 0,83), L6 +3 (p = 0,37): **belum signifikan**. Q v2 memilih GoLastKnown 78–88%, PPO v2 deterministik 99%.
3. **Hunt loop vs FSM bergantung pada gaya bot, bukan layout.** Uji atribusi di L1 held-out, mode solo, **bot lama** (tanpa `-thiefbot-skill`): Hunter selalu GoLastKnown 214/350 (61,1%) vs FSM 179/350 (51,1%), **+10 poin, p = 0,008**, mirip hasil v1 di layout tetap (+12, p = 0,002). Dengan bot skill 0,2, selisihnya +3 poin (p = 0,45), **belum signifikan**. Keunggulan hunt loop tahan perubahan layout, tetapi hilang melawan bot yang lebih sering mengendap dan bersembunyi.
4. **Layout memengaruhi angka absolut.** Dengan bot lama, layout held-out lebih mudah bagi Hunter: FSM +15 poin, GoLastKnown +13 poin dibanding layout tetap v1. Perbandingan antar-metode hanya sah di layout yang sama.
5. **Camping lolos.** Median waktu Hunter < 4 m dari pintu keluar 0–13% durasi misi untuk semua metode (batas 25%). Misi yang melewati 25%: 0–13% (tertinggi PPO v2 di L1, 13%, vs 11% untuk GoLastKnown di mode yang sama).
6. **Cakupan state naik.** Q v2: 40/48 state (v1: 34/48), dengan 2–4 dari 40 aksi greedy berubah per 250 episode di akhir training. State yang belum pernah muncul: s26, s30, s32, s34, s36, s38, s39, s46.
7. **Curriculum masih cepat.** Dengan threshold 0,3 / 500 episode, PPO v2 tetap mencapai L6 di step ±20k (v1: ±6k), karena di mode solo Hunter menangkap 75–90% di L3–L6.
8. **Angka PPO tetap tidak setara dengan baris mode build** (lihat `../analysis_v1.md` butir 4): perbandingan PPO harus memakai baris "mode ML-Agents".

Kurva PPO v2 (6 env, 2,85 jam):

| Step | 2k | 10k | 20k | 50k | 100k | 150k | 200k |
|---|---:|---:|---:|---:|---:|---:|---:|
| Reward | −0,05 | +0,24 | +0,79 | +0,85 | +0,88 | +0,92 | +0,92 |
| Lesson | L1 | L1 | L6 | L6 | L6 | L6 | L6 |

Waktu: Q-learning v2 4,8 jam, PPO v2 2,85 jam (berjalan bersamaan), evaluasi ±2 jam.

### Yang dikirim ke game (menggantikan model v1)

- `Resources/Adaptive/hunter_qtable.json`: Q v2 (3000 episode, layout acak, 40/48 state). Alasan: di layout yang belum pernah dilihat, Q v1 jatuh di bawah FSM, sedangkan Q v2 setara Hunter terbaik yang diuji.
- `Resources/Adaptive/HunterGuard.onnx`: PPO v2 200k step (layout acak). Setara PPO v1 di uji held-out (belum signifikan berbeda), tetapi dilatih di 1000 layout.

### Masih perlu di laptop

1. ThiefBot: `-thiefbot-skill` bekerja terbalik (lihat `../calibration_v2.md`); di L3/L6 bot lolos paling banyak 14–25%.
2. Build yang memuat `HunterGuard.onnx` di Resources, supaya PPO bisa dievaluasi di mode build yang sama dengan FSM dan Q-learning.
3. Kalau ingin RL benar-benar mengalahkan GoLastKnown, lawan harus membuat aksi lain berguna: bot yang lebih sering mengambil semua target (InterceptExit), bersembunyi dekat posisi terakhir (CheckHidingSpot), atau memakai kaleng sebagai pengalih (usulan `IgnoreNoise` di `../../rl-expansion.md`).

## Catch rate per level × metode

| Level | Metode | Tangkap / misi | Catch rate | CI 95% | Lolos | Timeout | Selisih vs FSM | p | p (Holm) | Kesimpulan |
|---:|---|---:|---:|---|---:|---:|---:|---:|---:|---|
| 1 | FSM (baseline) | 178/350 | 50.9% | 45.6%–56.1% | 49.1% | 0.0% | – | – | – | pembanding |
| 1 | Q-learning | 188/350 | 53.7% | 48.5%–58.9% | 46.3% | 0.0% | +2.9 poin | 0.449 | 1.000 | belum signifikan |
| 1 | Q-learning v1 (dilatih di layout tetap) | 117/350 | 33.4% | 28.7%–38.5% | 66.6% | 0.0% | -17.4 poin | 0.000 | 0.000 | signifikan lebih rendah |
| 1 | Hunter selalu GoLastKnown (heuristik) | 188/350 | 53.7% | 48.5%–58.9% | 46.3% | 0.0% | +2.9 poin | 0.449 | 1.000 | belum signifikan |
| 1 | Hunter selalu GoLastKnown (mode ML-Agents) | 210/350 | 60.0% | 54.8%–65.0% | 40.0% | 0.0% | +9.1 poin | 0.015 | 0.191 | lebih tinggi (p < 0.05, tapi tidak lolos koreksi Holm) |
| 1 | PPO deterministik (mode ML-Agents) | 223/350 | 63.7% | 58.6%–68.6% | 36.3% | 0.0% | +12.9 poin | 0.001 | 0.008 | signifikan lebih tinggi |
| 3 | FSM (baseline) | 304/350 | 86.9% | 82.9%–90.0% | 13.1% | 0.0% | – | – | – | pembanding |
| 3 | Q-learning | 290/350 | 82.9% | 78.6%–86.4% | 17.1% | 0.0% | -4.0 poin | 0.140 | 1.000 | belum signifikan |
| 3 | Q-learning v1 (dilatih di layout tetap) | 280/350 | 80.0% | 75.5%–83.9% | 20.0% | 0.0% | -6.9 poin | 0.015 | 0.191 | lebih rendah (p < 0.05, tapi tidak lolos koreksi Holm) |
| 3 | Hunter selalu GoLastKnown (heuristik) | 302/350 | 86.3% | 82.3%–89.5% | 13.7% | 0.0% | -0.6 poin | 0.825 | 1.000 | belum signifikan |
| 3 | Hunter selalu GoLastKnown (mode ML-Agents) | 298/350 | 85.1% | 81.0%–88.5% | 14.9% | 0.0% | -1.7 poin | 0.513 | 1.000 | belum signifikan |
| 3 | PPO deterministik (mode ML-Agents) | 300/350 | 85.7% | 81.7%–89.0% | 14.3% | 0.0% | -1.1 poin | 0.660 | 1.000 | belum signifikan |
| 6 | FSM (baseline) | 269/350 | 76.9% | 72.2%–81.0% | 23.1% | 0.0% | – | – | – | pembanding |
| 6 | Q-learning | 267/350 | 76.3% | 71.6%–80.4% | 23.7% | 0.0% | -0.6 poin | 0.858 | 1.000 | belum signifikan |
| 6 | Q-learning v1 (dilatih di layout tetap) | 257/350 | 73.4% | 68.6%–77.8% | 26.6% | 0.0% | -3.4 poin | 0.294 | 1.000 | belum signifikan |
| 6 | Hunter selalu GoLastKnown (heuristik) | 279/350 | 79.7% | 75.2%–83.6% | 20.3% | 0.0% | +2.9 poin | 0.359 | 1.000 | belum signifikan |
| 6 | Hunter selalu GoLastKnown (mode ML-Agents) | 285/350 | 81.4% | 77.0%–85.2% | 18.6% | 0.0% | +4.6 poin | 0.137 | 1.000 | belum signifikan |
| 6 | PPO deterministik (mode ML-Agents) | 294/350 | 84.0% | 79.8%–87.5% | 16.0% | 0.0% | +7.1 poin | 0.017 | 0.191 | lebih tinggi (p < 0.05, tapi tidak lolos koreksi Holm) |

Ringkasan:

- Level 1, Q-learning: +2.9 poin vs FSM, p = 0.449 → belum signifikan.
- Level 1, Q-learning v1 (dilatih di layout tetap): -17.4 poin vs FSM, p = 0.000 → signifikan lebih rendah.
- Level 1, Hunter selalu GoLastKnown (heuristik): +2.9 poin vs FSM, p = 0.449 → belum signifikan.
- Level 1, Hunter selalu GoLastKnown (mode ML-Agents): +9.1 poin vs FSM, p = 0.015 → lebih tinggi (p < 0.05, tapi tidak lolos koreksi Holm).
- Level 1, PPO deterministik (mode ML-Agents): +12.9 poin vs FSM, p = 0.001 → signifikan lebih tinggi.
- Level 3, Q-learning: -4.0 poin vs FSM, p = 0.140 → belum signifikan.
- Level 3, Q-learning v1 (dilatih di layout tetap): -6.9 poin vs FSM, p = 0.015 → lebih rendah (p < 0.05, tapi tidak lolos koreksi Holm).
- Level 3, Hunter selalu GoLastKnown (heuristik): -0.6 poin vs FSM, p = 0.825 → belum signifikan.
- Level 3, Hunter selalu GoLastKnown (mode ML-Agents): -1.7 poin vs FSM, p = 0.513 → belum signifikan.
- Level 3, PPO deterministik (mode ML-Agents): -1.1 poin vs FSM, p = 0.660 → belum signifikan.
- Level 6, Q-learning: -0.6 poin vs FSM, p = 0.858 → belum signifikan.
- Level 6, Q-learning v1 (dilatih di layout tetap): -3.4 poin vs FSM, p = 0.294 → belum signifikan.
- Level 6, Hunter selalu GoLastKnown (heuristik): +2.9 poin vs FSM, p = 0.359 → belum signifikan.
- Level 6, Hunter selalu GoLastKnown (mode ML-Agents): +4.6 poin vs FSM, p = 0.137 → belum signifikan.
- Level 6, PPO deterministik (mode ML-Agents): +7.1 poin vs FSM, p = 0.017 → lebih tinggi (p < 0.05, tapi tidak lolos koreksi Holm).

## Distribusi aksi saat evaluasi

| Level | Metode | Keputusan / misi | GoLastKnown | SweepSector | InterceptExit | AmbushTarget | CheckHidingSpot | GiveUp |
|---:|---|---:|---:|---:|---:|---:|---:|---:|
| 1 | Q-learning | 2.86 | 87.6% | 10.0% | 0.6% | 0.1% | 1.7% | 0.0% |
| 1 | Q-learning v1 (dilatih di layout tetap) | 2.48 | 44.8% | 30.4% | 7.7% | 2.2% | 14.9% | 0.0% |
| 1 | Hunter selalu GoLastKnown (heuristik) | 3.09 | 100.0% | 0.0% | 0.0% | 0.0% | 0.0% | 0.0% |
| 1 | Hunter selalu GoLastKnown (mode ML-Agents) | 2.96 | 100.0% | 0.0% | 0.0% | 0.0% | 0.0% | 0.0% |
| 1 | PPO deterministik (mode ML-Agents) | 3.09 | 98.9% | 0.0% | 1.1% | 0.0% | 0.0% | 0.0% |
| 3 | Q-learning | 4.54 | 81.0% | 11.0% | 1.1% | 1.8% | 4.3% | 0.7% |
| 3 | Q-learning v1 (dilatih di layout tetap) | 5.74 | 28.9% | 42.9% | 4.3% | 3.3% | 20.6% | 0.0% |
| 3 | Hunter selalu GoLastKnown (heuristik) | 5.27 | 100.0% | 0.0% | 0.0% | 0.0% | 0.0% | 0.0% |
| 3 | Hunter selalu GoLastKnown (mode ML-Agents) | 4.49 | 100.0% | 0.0% | 0.0% | 0.0% | 0.0% | 0.0% |
| 3 | PPO deterministik (mode ML-Agents) | 4.96 | 99.6% | 0.0% | 0.4% | 0.0% | 0.0% | 0.0% |
| 6 | Q-learning | 3.49 | 78.0% | 9.6% | 6.0% | 0.0% | 6.5% | 0.0% |
| 6 | Q-learning v1 (dilatih di layout tetap) | 4.03 | 16.0% | 36.6% | 7.8% | 0.3% | 39.3% | 0.0% |
| 6 | Hunter selalu GoLastKnown (heuristik) | 3.84 | 100.0% | 0.0% | 0.0% | 0.0% | 0.0% | 0.0% |
| 6 | Hunter selalu GoLastKnown (mode ML-Agents) | 3.53 | 100.0% | 0.0% | 0.0% | 0.0% | 0.0% | 0.0% |
| 6 | PPO deterministik (mode ML-Agents) | 3.64 | 99.4% | 0.0% | 0.6% | 0.0% | 0.0% | 0.0% |

## Training

- Q-learning: 3000 episode, level 1, 2, 3, 4, 5, 6, waktu 4.8 jam.
- Tabel Q: 40/48 state terkunjungi, 3000 episode tersimpan di tabel.
- State belum terkunjungi: s26, s30, s32, s34, s36, s38, s39, s46.

<details><summary>Aksi greedy per state</summary>

| State | Arti | Aksi | Q |
|---|---|---|---:|
| s00 | `<3m new seen  target   hideNear` | GoLastKnown | +0.579 |
| s01 | `<3m new seen  target` | GoLastKnown | +0.874 |
| s02 | `<3m new seen  noTarget hideNear` | GoLastKnown | +0.180 |
| s03 | `<3m new seen  noTarget` | GoLastKnown | +0.553 |
| s04 | `<3m new heard target   hideNear` | SweepSector | +0.823 |
| s05 | `<3m new heard target` | GoLastKnown | +0.856 |
| s06 | `<3m new heard noTarget hideNear` | GoLastKnown | +0.106 |
| s07 | `<3m new heard noTarget` | GoLastKnown | +0.621 |
| s08 | `<3m old seen  target   hideNear` | AmbushTarget | +0.459 |
| s09 | `<3m old seen  target` | InterceptExit | +0.051 |
| s10 | `<3m old seen  noTarget hideNear` | InterceptExit | +0.000 |
| s11 | `<3m old seen  noTarget` | InterceptExit | -0.412 |
| s12 | `<3m old heard target   hideNear` | CheckHidingSpot | +0.609 |
| s13 | `<3m old heard target` | GiveUp | +0.659 |
| s14 | `<3m old heard noTarget hideNear` | GoLastKnown | +0.000 |
| s15 | `<3m old heard noTarget` | GoLastKnown | -0.160 |
| s16 | `3-8m new seen  target   hideNear` | CheckHidingSpot | +0.998 |
| s17 | `3-8m new seen  target` | GoLastKnown | +0.849 |
| s18 | `3-8m new seen  noTarget hideNear` | GoLastKnown | +0.474 |
| s19 | `3-8m new seen  noTarget` | GoLastKnown | -0.488 |
| s20 | `3-8m new heard target   hideNear` | GoLastKnown | +0.868 |
| s21 | `3-8m new heard target` | SweepSector | +0.821 |
| s22 | `3-8m new heard noTarget hideNear` | GoLastKnown | +0.090 |
| s23 | `3-8m new heard noTarget` | SweepSector | +0.333 |
| s24 | `3-8m old seen  target   hideNear` | SweepSector | +0.171 |
| s25 | `3-8m old seen  target` | GiveUp | +0.314 |
| s27 | `3-8m old seen  noTarget` | CheckHidingSpot | +0.080 |
| s28 | `3-8m old heard target   hideNear` | SweepSector | +0.000 |
| s29 | `3-8m old heard target` | AmbushTarget | +0.093 |
| s31 | `3-8m old heard noTarget` | InterceptExit | +0.028 |
| s33 | `>8m new seen  target` | SweepSector | +0.240 |
| s35 | `>8m new seen  noTarget` | GoLastKnown | +0.045 |
| s37 | `>8m new heard target` | SweepSector | +0.000 |
| s40 | `>8m old seen  target   hideNear` | GoLastKnown | +0.385 |
| s41 | `>8m old seen  target` | InterceptExit | +0.560 |
| s42 | `>8m old seen  noTarget hideNear` | SweepSector | +0.010 |
| s43 | `>8m old seen  noTarget` | GoLastKnown | -0.090 |
| s44 | `>8m old heard target   hideNear` | GoLastKnown | +0.083 |
| s45 | `>8m old heard target` | InterceptExit | +0.265 |
| s47 | `>8m old heard noTarget` | InterceptExit | +0.058 |

</details>

Konvergensi tabel Q (aksi greedy per state dibanding snapshot sebelumnya; hanya state yang terkunjungi di keduanya):

| Snapshot | State terkunjungi | Aksi greedy berubah |
|---|---:|---:|
| ep0252.json | 31/48 | – |
| ep0502.json | 35/48 | 11/31 (35.5%) |
| ep0753.json | 36/48 | 7/35 (20.0%) |
| ep1003.json | 40/48 | 7/36 (19.4%) |
| ep1252.json | 40/48 | 4/40 (10.0%) |
| ep1500.json | 40/48 | 2/40 (5.0%) |
| ep1753.json | 40/48 | 5/40 (12.5%) |
| ep2004.json | 40/48 | 3/40 (7.5%) |
| ep2253.json | 40/48 | 4/40 (10.0%) |
| ep2505.json | 40/48 | 4/40 (10.0%) |
| ep2750.json | 40/48 | 2/40 (5.0%) |
| akhir | 40/48 | 2/40 (5.0%) |

- Q-learning v1 (layout tetap): 34/48 state terkunjungi, 3000 episode tersimpan di tabel.
- PPO: waktu 2.8 jam (lihat kurva di TensorBoard, `results/<run-id>`).

## Catatan masalah

- Level 1, Q-learning: GoLastKnown dipilih 87.6% dari semua keputusan.
- Level 1, Hunter selalu GoLastKnown (heuristik): GoLastKnown dipilih 100.0% dari semua keputusan.
- Level 1, Hunter selalu GoLastKnown (mode ML-Agents): GoLastKnown dipilih 100.0% dari semua keputusan.
- Level 1, PPO deterministik (mode ML-Agents): GoLastKnown dipilih 98.9% dari semua keputusan.
- Level 3, Q-learning: GoLastKnown dipilih 81.0% dari semua keputusan.
- Level 3, Hunter selalu GoLastKnown (heuristik): GoLastKnown dipilih 100.0% dari semua keputusan.
- Level 3, Hunter selalu GoLastKnown (mode ML-Agents): GoLastKnown dipilih 100.0% dari semua keputusan.
- Level 3, PPO deterministik (mode ML-Agents): GoLastKnown dipilih 99.6% dari semua keputusan.
- Level 6, Hunter selalu GoLastKnown (heuristik): GoLastKnown dipilih 100.0% dari semua keputusan.
- Level 6, Hunter selalu GoLastKnown (mode ML-Agents): GoLastKnown dipilih 100.0% dari semua keputusan.
- Level 6, PPO deterministik (mode ML-Agents): GoLastKnown dipilih 99.4% dari semua keputusan.
