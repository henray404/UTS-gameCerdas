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
