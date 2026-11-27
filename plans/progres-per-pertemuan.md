# Progres FP per Pertemuan dan Pembagian Tugas

Materi Pertemuan 9–14 dijadikan daftar progres untuk Hollow House. Setiap progres menyebut konsep dari slide, kondisi di game sekarang, apa yang ditambahkan, dan bukti yang bisa didemokan. Detail teknis tetap di [`eas-adaptive.md`](eas-adaptive.md) (DDA, PCG, AI) dan [`rl-enemy-design.md`](rl-enemy-design.md) (RL).

Slide ada di folder `TM-9` sampai `TM-14` di luar repo (tidak di-commit karena ukurannya sekitar 17 MB per file).

## Pembagian peran

| Anggota | Peran | Pertemuan utama | Folder kode yang dipegang |
|---|---|---|---|
| **A** | PCG dan level | P9, P10 | `Scripts/Adaptive/Pcg/`, `InteriorLayout.cs`, `Editor/*Builder.cs` |
| **B** | DDA dan player modeling | P11, P12 | `Scripts/Adaptive/Dda/`, `Scripts/Adaptive/Telemetry/`, UI F2 dan layar hasil |
| **C** (Henry) | Enemy AI, ML, dan RL | P13, P14 | `GuardBrain.cs`, `Scripts/Adaptive/RL/`, `ml/` |

Setiap anggota memegang folder sendiri supaya tidak saling menimpa. Bagian yang dipakai bersama ditulis sebagai **kontrak** (lihat bagian akhir) dan disepakati di minggu pertama.

## Pertemuan 9: Procedural Content Generation (Anggota A)

| # | Konsep di slide | Di game sekarang | Progres yang ditambahkan | Bukti demo |
|---|---|---|---|---|
| 9.1 | Seed dan reproducibility (hal. 16–20) | Seed interior tetap per level | Seed tampil di panel debug, ditambah tes "seed sama → layout sama, seed beda → layout beda" | Screenshot 2 seed, tes lolos |
| 9.2 | Procedural spawning dan spawn rule (hal. 44–49) | Kaleng diberikan sebagai jumlah, tidak diletakkan | `ResourceSpawner`: baterai senter dan pil sanity ditaruh dengan `NavMesh.SamplePosition`, `Physics.CheckSphere`, jarak minimal dari rute patroli, dan jarak antar-item | Gizmo titik spawn dan alasan penolakan |
| 9.3 | Weighted random dan controlled randomness (hal. 12–14) | Roster monster diacak rata | Bobot pemilihan monster dan ruangan, bisa diatur dari inspector | Tabel frekuensi dari 100 seed |
| 9.4 | Generate-and-test (hal. 27–31) | `ValidatePaths` sudah ada | Laporan berapa kandidat ditolak dan alasannya | Log validasi di panel F2 |

## Pertemuan 10: PCG untuk Level dan Dungeon (Anggota A)

| # | Konsep di slide | Di game sekarang | Progres yang ditambahkan | Bukti demo |
|---|---|---|---|---|
| 10.1 | Hard dan soft constraint (hal. 43–45) | Hanya hard constraint (jalur ada) | Soft constraint: target punya tempat sembunyi dalam 5 m, jalur ke exit tidak 100% terlihat patroli | Skor soft constraint per kandidat |
| 10.2 | Repair strategy (hal. 46) | Gagal validasi → coba ulang | Repair dulu: pindahkan resource, kurangi 1 monster, baru generate ulang | Log "repaired: …" |
| 10.3 | Enemy dan item placement (hal. 48–50) | Rute patroli menjauhi target | Safe zone di pintu masuk (tidak ada monster di awal), baterai sebelum area gelap, reward di jalan buntu | Overlay zona aman |
| 10.4 | Level metrics dan difficulty metrics (hal. 51–52) | `DifficultyScore` statis per level | `LevelMetrics`: panjang jalur L, coverage patroli C, tempat sembunyi H, jarak patroli–target T, lalu skor kompleksitas | Tabel metrik 27 layout × 4 rumah |
| 10.5 | Constraint-based pipeline (hal. 44) | — | `AdaptiveLayoutPicker` memilih layout yang skornya paling dekat dengan D dari DDA | D rendah vs tinggi menghasilkan layout berbeda |

## Pertemuan 11: Dynamic Difficulty Adjustment (Anggota B)

| # | Konsep di slide | Di game sekarang | Progres yang ditambahkan | Bukti demo |
|---|---|---|---|---|
| 11.1 | Static vs dynamic difficulty (hal. 9–11) | Kurva tetap di `CampaignLevels` | `CampaignLevels` jadi baseline, DDA menambah lapisan pengali | Grafik static vs dynamic |
| 11.2 | Performance metrics dan normalisasi (hal. 15–22) | Hanya skor akhir | `PlayerPerformanceTracker`: waktu ÷ waktu acuan, alert, waktu dikejar, hit, kaleng, baterai, retry; semua dinormalisasi 0..1 | Panel metrik live |
| 11.3 | Skill score (hal. 23, 61) | — | Skill score berbobot sesuai rumus P di `eas-adaptive.md` bagian 3 | Nilai P dan S tiap misi |
| 11.4 | Adjustment step, batas, smoothing, oscillation (hal. 38–44) | — | `DifficultyDirector`: EMA, langkah ±0.15, hysteresis 0.08, batas 0.1–0.95 | Grafik D stabil dalam 10 misi |
| 11.5 | Parameter adaptation (hal. 29–32) | — | D mengubah minimal 3 kelompok: angka AI, jumlah monster, resource | Diff parameter sebelum → sesudah |
| 11.6 | Rubber banding (hal. 33–35, pptx slide 15) | — | Penyesuaian di tengah misi: 2 hit dalam 60 dtk → suspicion ×0.85 selama 45 dtk | Log event rubber band |
| 11.7 | Visible vs invisible adaptation, transparansi (hal. 36, 47, pptx slide 16 dan 30) | — | Bagian "Adaptation" di layar hasil, plus opsi mematikan DDA di menu (agency pemain) | Screenshot layar hasil |
| 11.8 | Debug UI DDA (hal. 63–64) | F1 hanya untuk AI | Panel F2: skill, D, parameter aktif, arah adjustment | Rekaman F2 |

## Pertemuan 12: Player Modeling dan Adaptive Game AI (Anggota B, perilaku musuh oleh C)

| # | Konsep di slide | Di game sekarang | Progres yang ditambahkan | Bukti demo |
|---|---|---|---|---|
| 12.1 | Event-based telemetry (hal. 10–17) | — | `TelemetryLog`: event Seen, Chased, Hid, CanThrown, TargetTaken, Sprint, Caught, Escaped, disimpan sebagai CSV di `persistentDataPath` | Isi CSV 1 misi |
| 12.2 | Play style dan multi-label (hal. 29–38) | — | Skor gaya khusus stealth: **Ghost** (tidak pernah terlihat), **Hider** (sering sembunyi), **Distractor** (banyak kaleng), **Speedrunner** (waktu cepat), **Risk-taker** (sering lari dan dikejar) | Profil berbeda untuk 3 cara main |
| 12.3 | Profil sementara dan jangka panjang (hal. 40–41) | — | Profil misi ini plus profil gabungan EMA yang disimpan di PlayerPrefs | Profil setelah 1 vs 5 misi |
| 12.4 | Confidence dan cold start (hal. 42–43) | — | Confidence naik sesuai jumlah misi; di bawah 3 misi, adaptasi gaya tidak aktif | Panel menampilkan confidence |
| 12.5 | Adaptation policy table (hal. 44–53) | — | Tabel gaya → adaptasi. Hider: monster lebih sering cek tempat sembunyi. Distractor: monster mulai mengabaikan kaleng berulang di titik sama. Speedrunner: Hunter menjaga jalur keluar. Ghost: patroli lebih sering lewat titik intip | Video 2 gaya berbeda → monster bereaksi berbeda |
| 12.6 | Adaptive enemy AI (hal. 58–60, pptx slide 26) | FSM tanpa adaptasi | Anggota C mengimplementasikan perilaku dari 12.5 di `GuardBrain`, plus Attack/sanity dan kabur dari senter (`eas-adaptive.md` bagian 5) | F1 menampilkan alasan aksi |
| 12.7 | Etika dan privasi telemetri (hal. 79, pptx slide 31) | — | Telemetri hanya lokal, tanpa data pribadi, ada tombol hapus data | Paragraf di laporan |

## Pertemuan 13: Machine Learning for Games (Anggota C, data dari B)

| # | Konsep di slide | Di game sekarang | Progres yang ditambahkan | Bukti demo |
|---|---|---|---|---|
| 13.1 | Supervised learning pipeline (hal. 11–14) | — | `ml/train_skill_model.py`: logistic regression memprediksi "gagal di misi berikutnya" dari CSV telemetri | AUC cross-validation vs baseline aturan |
| 13.2 | ML dan DDA (hal. 74) | — | Bobot model diekspor ke JSON, inference di C# (dot product + sigmoid), lalu `S' = .6·EMA + .4·(1 − P(gagal))` | F2 menampilkan P(gagal) |
| 13.3 | Q-learning, state, reward, epsilon-greedy (hal. 18–34) | — | Tahap A di `rl-enemy-design.md`: Q-learning tabular 48 state × 6 aksi untuk Hunter | Kurva reward dan tabel Q |
| 13.4 | Discretization (hal. 41–42) | — | Diskretisasi jarak, umur memori, sumber memori | Tabel state di laporan |
| 13.5 | Explainability (hal. 77) | F1 menampilkan state FSM | F1 juga menampilkan Q-value aksi Hunter | Screenshot F1 |
| 13.6 | Data playtest | — | Bot pencuri (`ThiefBot`) menghasilkan data tambahan; target minimal 150 misi manusia + bot | Jumlah baris dataset |

## Pertemuan 14: RL dengan Unity ML-Agents (Anggota C)

| # | Konsep di slide | Di game sekarang | Progres yang ditambahkan | Bukti demo |
|---|---|---|---|---|
| 14.1 | Setup ML-Agents (hal. 38–40) | Package belum ada | Spike 1 hari: package di Unity 6000.6 dan `mlagents` Python 3.10. Kalau gagal, berhenti di 13.3. Setup trainer: [`ml/README.md`](../ml/README.md) | Catatan hasil spike |
| 14.2 | Observation, action, reward, episode (hal. 14–35) | — | `HunterAgent` dengan kontrak di `rl-enemy-design.md` bagian 3 | Kode dan tes observasi |
| 14.3 | Parallel training area dan randomization (hal. 49–50) | — | Scene `HunterTraining` dengan 4–8 rumah dan seed acak | Screenshot scene |
| 14.4 | Curriculum learning (hal. 51) | — | 4 tahap curriculum bot | Kurva reward per tahap |
| 14.5 | Measuring training progress (hal. 56) | — | TensorBoard: cumulative reward, episode length | Grafik di laporan |
| 14.6 | Heuristic vs trained policy (hal. 78) | — | Bandingkan Heuristic (aturan FSM), Q-learning, dan PPO di 200 episode seed baru | Tabel catch rate |
| 14.7 | Integrasi dengan AI klasik (hal. 67) | — | FSM tetap mode besar, policy hanya memilih tujuan; Hunter hanya muncul saat D ≥ 0.6 | Video Hunter vs monster FSM |

## Urutan kerja

| Minggu | Anggota A (PCG) | Anggota B (DDA dan modeling) | Anggota C (AI, ML, RL) |
|---|---|---|---|
| 1 | 9.1, 9.4, 10.4 metrik layout | Kontrak `DifficultyProfile` dan CSV, 11.2, 12.1 | Spike 14.1, `ThiefBot`, kontrak `HunterDecision` |
| 2 | 9.2, 10.1–10.3 | 11.3–11.6, 12.2–12.4 | 13.3–13.5 Q-learning, Attack/sanity |
| 3 | 9.3, 10.5 (butuh D dari B) | 11.7–11.8, 12.5 policy table | 12.6 perilaku adaptif, 14.2–14.4 training |
| 4 | Capture layout untuk laporan | Pengumpulan data playtest, 13.2 integrasi model | 13.1 training model, 14.5–14.7 evaluasi |
| Akhir | Semua: playtest bersama, video demo, laporan | | |

## Kontrak bersama (disepakati minggu 1)

- **`DifficultyProfile`** (pemilik B): nilai D, pengali angka AI, jumlah monster, jumlah resource, flag perilaku (`checksHidingSpots`, `useHunter`, `hunterModel`), dan daftar alasan. Dibaca oleh A (layout dan resource) dan C (perilaku monster).
- **`PlayerProfile`** (pemilik B): skill, confidence, dan skor 5 gaya bermain. Dibaca oleh C.
- **Skema CSV telemetri** (pemilik B): satu baris per misi untuk ML, satu file event per misi untuk analisis. Dibaca oleh C.
- **`LevelMetrics`** (pemilik A): L, C, H, T, skor kompleksitas. Dibaca oleh B untuk panel dan laporan.

## Status

Centang setelah selesai dan sebutkan commit-nya.

- [ ] P9: 9.1 · 9.2 · 9.3 · 9.4
- [ ] P10: 10.1 · 10.2 · 10.3 · 10.4 · 10.5
- [ ] P11: 11.1 · 11.2 · 11.3 · 11.4 · 11.5 · 11.6 · 11.7 · 11.8
- [ ] P12: 12.1 · 12.2 · 12.3 · 12.4 · 12.5 · 12.6 · 12.7
- [ ] P13: 13.1 · 13.2 · 13.3 · 13.4 · 13.5 · 13.6
  - 13.3 infrastruktur selesai: `QLearningHunter`, `HunterDecision`, hook di `GuardBrain`, `HunterTrainingRunner`. Training 200 episode di level 1 (19,5 menit, Unity 6000.4 di salinan proyek): tingkat tangkap Hunter 25% di 40 episode pertama dan 34% di 56 episode terakhir (epsilon 0,05). GiveUp turun dari 23% ke 0% dan InterceptExit naik dari 10% ke 40%. 26 dari 48 state terkunjungi. Tabel terpasang di `Resources/Adaptive/hunter_qtable.json`.
  - Evaluasi 100 misi per sisi, level 1, `ThiefBot`, tanpa belajar: Hunter greedy menangkap **46%** (95% CI 37–56%), FSM murni **36%** (95% CI 27–46%). Selisih +10 poin, uji dua proporsi p = 0,15, jadi **belum signifikan** di α = 0,05. Aksi Hunter saat evaluasi: GoLastKnown 44%, InterceptExit 36%, GiveUp 11%, sisanya SweepSector dan CheckHidingSpot. Evaluasi ini menemukan loop GiveUp (ditambal dengan cooldown 5 dtk di action mask) dan misi macet (dibatasi 180 dtk untuk training dan evaluasi).
  - 13.4 selesai: diskretisasi 48 state di `HunterContract.StateIndex`, dites di `Tests/HunterRulesTests.cs`.
  - 13.6 selesai untuk bot: `ThiefBot` menyelesaikan misi level 1 dalam 16–30 dtk. Dataset telemetri belum (menunggu CSV dari Anggota B).
- [ ] P14: 14.1 · 14.2 · 14.3 · 14.4 · 14.5 · 14.6 · 14.7
  - Rencana training dan evaluasi di PC lab (Q-learning multi-level, PPO, perbandingan 3 metode): [`rl-training-lab.md`](rl-training-lab.md).
  - 14.1 sebagian: trainer `mlagents` 1.1.0 terpasang dan jalan ([`ml/README.md`](../ml/README.md)). Package Unity `com.unity.ml-agents` 4.2.0 belum diuji di proyek.
  - 14.2 kontrak selesai: `HunterContract` (28 observasi, 6 aksi, masking, reward) dites. `HunterAgent` belum.
