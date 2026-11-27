# Perluasan RL The Hunter (Tahap C)

Rencana untuk membawa RL di Hollow House melampaui satu keputusan "ke mana setelah kehilangan jejak". Dokumen ini melanjutkan [`../plans/rl-enemy-design.md`](../plans/rl-enemy-design.md) (kontrak v1) dan [`../plans/rl-training-lab.md`](../plans/rl-training-lab.md) (pipeline lab). Semua perubahan kode C# di sini adalah **usulan** untuk dikerjakan di laptop (butuh Unity). Bagian lab sudah disiapkan di `ml/lab/hh.sh`.

Prinsip yang dipakai untuk semua fitur di bawah:

1. **Setiap fase selesai 100% sebelum fase berikutnya.** "Selesai" punya syarat terukur (bagian 9), bukan sekadar "sudah jalan".
2. **Tetap bagus kalau map atau desain berubah.** Policy tidak boleh hafal satu rumah. Ada uji layout yang tidak pernah dilihat saat training (bagian 2).
3. **RL tetap tidak menggantikan FSM**, dan monster tetap tidak curang: tidak ada posisi asli pemain di observasi selama pemain tidak terlihat.
4. **Kontrak append-only.** Aksi dan observasi baru selalu ditambahkan di belakang, jadi model v1 tetap bisa dibaca dan dibandingkan.

## 1. Ringkasan fitur

| # | Fitur | Inti | Agent | Fase |
|---|---|---|---|---|
| 6 | Checkpoint sebagai tingkat kesulitan | Hunter lemah/sedang/kuat dari titik training berbeda, dipilih DDA | Hunter (PPO dan Q) | C1 |
| 1 | Hunter belajar selama misi | Tabel Q terus di-update pelan dari permainan pemain sendiri | Hunter (Q) | C1 |
| 2 | Kebal trik kaleng | Aksi `IgnoreNoise`: tidak mengejar suara yang dicurigai pengalih | Hunter | C2 |
| 7 | Aksi teror | `Stalk`, `FakeRetreat`, `KnockOnHiding` | Hunter | C2 |
| 4 | Peran per monster | Pengejar / pencegat / penyisir dibagi dinamis | Tim monster | C3 |
| 3 | Pack hunting | Beberapa Hunter dilatih bersama (MA-POCA) | Tim monster | C3 |
| 5 | RL Director | Agent terpisah yang mengatur ritme ketegangan, bukan menangkap | Director | C4 |

## 2. Robustness terhadap perubahan map dan desain

### 2.1 Masalah di kontrak v1 (dicek ke `HunterContract.cs`)

| Hal | Kondisi v1 | Dampak kalau map berubah |
|---|---|---|
| Arah di observasi | Offset dunia XZ (`ToLastKnownX/Z`, `LastKnownToExitX/Z`, ...) | Rumah yang diputar atau dicerminkan menghasilkan input berbeda untuk situasi yang sama |
| Skala jarak | Dibagi konstanta `FarDistance = 20`, `HidingScale = 10` | Rumah lebih besar dari ~20 m membuat banyak jarak mentok di 1 |
| Layout | `LayoutSeed => 2401 + Number * 137` (`CampaignLevels.cs`), tetap per level | Training dan evaluasi memakai rumah yang sama persis; overfit tidak terdeteksi |
| Posisi start | Ditentukan level | Policy bisa hafal rute dari start tertentu |
| Bot lawan | Bobot ThiefBot diacak per episode (sudah baik) | — |

### 2.2 Usulan (kontrak v2, append-only)

| Usulan | Detail | File |
|---|---|---|
| Observasi di frame lokal monster | Tambah 8 float: vektor ke posisi terakhir, ke exit, ke target, dan ke tempat sembunyi, **diputar ke arah hadap monster** (`transform.InverseTransformDirection`). Vektor dunia lama tetap ada (append-only), tapi PPO v2 dilatih dengan augmentasi rotasi supaya tidak bergantung padanya | `HunterContract.cs`, `HunterAgent.cs` |
| Skala jarak relatif ke rumah | Tambah 1 float `houseScale` = diagonal bounds NavMesh ÷ 30. Jarak baru dinormalisasi dengan diagonal rumah, bukan 20 m | `HunterContract.cs`, `RuntimeNavigation.cs` (hanya baca bounds) |
| Seed layout untuk training | Flag `-hunter-seed-offset K` dan `-hunter-seed random`. Training: offset acak 0–999. Evaluasi held-out: offset ≥ 1000 yang tidak pernah dipakai training | `GameSession` (hook mode training), `InteriorLayout`, `HouseShell`, `RuntimeNavigation` (membaca seed lewat satu properti) |
| Augmentasi rumah | Mode training boleh mencerminkan rumah (sumbu X) dengan peluang 50%. Kalau `PatrolPlans` menolak beat lama, fallback yang sudah ada (`GuardEncounter`) dipakai | `HouseShell`, `InteriorLayout` |
| Start acak | Posisi start pemain dan Hunter diacak di NavMesh dengan jarak minimal 8 m | `TrainingBootstrap` / `HunterDecision` |
| Log versi | Baris `HUNTER` ditambah `contract=2 seed=... mirrored=...`. Field baru ditambahkan di **akhir** baris, karena regex `hunter_log_summary.py` mencocokkan urutan field v1; dengan begitu log v1 dan v2 sama-sama terbaca | `HunterDecision.cs` |
| Metrik camping | Baris `HUNTER` ditambah `exitTime=<detik Hunter < 4 m dari exit>`, supaya batas 25% dari desain bisa diuji | `HunterDecision.cs` |

### 2.3 Gerbang robustness (dipakai di setiap fase)

Model hanya boleh di-commit ke `Resources/Adaptive/` kalau semua syarat ini terpenuhi:

1. **Layout held-out:** catch rate di seed ≥ 1000 tidak lebih rendah dari FSM di seed yang sama (p ≥ 0.05 diperbolehkan, artinya "tidak kalah"). Selisih dengan catch rate di layout training ≤ 10 poin.
2. **Rumah dicerminkan:** catch rate tidak turun lebih dari 5 poin dibanding versi tidak dicerminkan.
3. **Camping:** median `exitTime` ÷ durasi misi ≤ 25%.
4. **Distribusi aksi:** tidak ada aksi yang dipilih > 80% (sudah dicek otomatis oleh `ml/make_summary.py`).

**Kalau map atau desain berubah nanti:** jalankan ulang `hh.sh eval` dengan build baru, tanpa training. Kalau gerbang lolos, model lama tetap dipakai. Kalau gagal, lanjutkan training dari model lama (`--initialize-from`, atau tabel Q lama), bukan dari nol. Biasanya cukup 20–30% anggaran training awal.

## 3. Fitur #6: checkpoint sebagai tingkat kesulitan (C1)

- **Apa:** tiga Hunter dengan kekuatan berbeda dari satu kali training. DDA (`D` di `eas-adaptive.md`) memilih: `D < 0.6` tanpa Hunter (sesuai desain), `0.6–0.75` Hunter lemah, `0.75–0.9` sedang, `> 0.9` kuat.
- **Lab:** `hunter_ppo.yaml` menyimpan checkpoint tiap 10k step (`keep_checkpoints: 12`). Setiap checkpoint dievaluasi 150 misi di level 3 dan seed held-out. Dipilih tiga checkpoint yang catch rate-nya paling mendekati target **FSM + 0, +8, +15 poin**, dengan syarat CI-nya tidak tumpang tindih antara lemah dan kuat. Untuk Q-learning: salinan tabel di episode 300, 800, dan akhir.
- **C# (usulan):** `HunterDecision` memuat `HunterGuard_{weak,mid,strong}.onnx` / `hunter_qtable_{weak,mid,strong}.json` dari Resources sesuai field baru `DifficultyProfile.HunterTier`. Fallback ke file tanpa akhiran.
- **Selesai kalau:** tiga model ter-commit, urutan catch rate lemah < sedang < kuat signifikan (p < 0.05 antara lemah dan kuat), dan gerbang 2.3 lolos untuk ketiganya.

## 4. Fitur #1: Hunter belajar selama misi (C1)

- **Apa:** saat bermain sungguhan, tabel Q yang dikirim (shipped) disalin ke `persistentDataPath/hunter_qtable.player.json` lalu di-update setiap keputusan dengan α kecil. Pemain yang selalu bersembunyi di lemari akan menghadapi Hunter yang makin sering memeriksa lemari.
- **Hanya Q-learning.** Model PPO di game adalah ONNX beku (inference only), jadi tidak bisa belajar di tangan pemain.
- **Batas supaya tetap adil dan tidak rusak:**
  - `α_online = 0.05`, `ε = 0` (tidak ada aksi acak di depan pemain).
  - `|Q_player − Q_shipped| ≤ 0.5` per sel. Tabel tidak bisa melantur jauh dari hasil training.
  - Reset ke tabel shipped kalau pemain memulai profil baru, atau kalau tabel shipped diganti (dicek lewat hash).
  - Update hanya dari reward yang sama dengan training. Tidak ada reward baru.
- **Robust terhadap map:** tabel diindeks oleh state abstrak (jarak, umur memori, sumber, target, tempat sembunyi), bukan posisi. Kebiasaan yang dipelajari di satu rumah ikut terbawa ke rumah lain.
- **C# (usulan):** `QLearningHunter` ditambah `Clamp(shipped, maxDelta)`. `HunterDecision` ditambah mode `Online` (aktif kalau `-hunter` atau mode game normal + flag di `DifficultyProfile`).
- **Lab:** mode `-hunter-online-eval` dengan ThiefBot bergaya tetap (`-thiefbot-style hider` / `canner` / `runner`), 40 misi berurutan × 30 ulangan. Bandingkan catch rate misi 31–40 dengan tabel beku.
- **Selesai kalau:** untuk minimal 2 dari 3 gaya bot, catch rate misi 31–40 > tabel beku (p < 0.05), dan untuk gaya campuran tidak lebih buruk.

## 5. Kontrak v2: aksi baru #2 dan #7 (C2, satu build)

Semua aksi baru ditambahkan **di belakang** enum (`HunterAction` 6–9). Reward Hunter **tidak berubah**: aksi teror hanya berguna kalau membantu menangkap. Ini sengaja, supaya Hunter tidak belajar menakuti tanpa pernah menangkap (reward hacking).

| # | Aksi | Perilaku | Mask (kapan boleh) | Kenapa bisa menguntungkan Hunter |
|---|---|---|---|---|
| 6 | `IgnoreNoise` | Mengabaikan bukti suara terakhir, lanjut dari bukti sebelumnya (atau patroli) | Hanya kalau sumber memori = suara kaleng | Kaleng adalah pengalih; mengejarnya menjauhkan Hunter dari pemain |
| 7 | `Stalk` | Mengikuti posisi terakhir dari jarak 6–10 m, berjalan pelan, tidak Chase sampai `Suspicion` 1 | Memori < 8 dtk dan sumber = lihat | Pemain yang merasa aman bergerak lagi dan terlihat |
| 8 | `FakeRetreat` | Berjalan menjauh 8–12 m, lalu diam 4–8 dtk di balik sudut, lalu kembali | Memori < 15 dtk | Pemain keluar dari tempat sembunyi terlalu cepat |
| 9 | `KnockOnHiding` | Mengetuk tempat sembunyi terdekat tanpa membuka (suara + kamera bergetar) | Ada tempat sembunyi dalam 10 m | Pemain panik dan keluar; tidak ada penalti "kosong" karena tempat tidak dibuka |

**Observasi tambahan (append, setelah 28 float v1):**

| Observasi | Ukuran | Catatan |
|---|---:|---|
| Vektor lokal ke posisi terakhir, exit, target, tempat sembunyi | 8 | Bagian 2.2 |
| `houseScale` | 1 | Bagian 2.2 |
| Jumlah kaleng yang dilempar pemain di misi ini ÷ 5 | 1 | Informasi yang wajar: Hunter mendengar setiap kaleng |
| Rasio bukti suara kaleng yang ternyata palsu (EMA) | 1 | Dasar `IgnoreNoise` |
| Berapa kali pemain keluar dari tempat sembunyi dalam 20 dtk terakhir yang terlihat/terdengar | 1 | Dasar `KnockOnHiding` / `FakeRetreat` |
| **Total v2** | **40** | |

**Q-learning v2:** state ditambah 1 bit "sumber = kaleng" dan 1 bit "pemain baru keluar dari persembunyian", jadi 48 × 4 = 192 state × 10 aksi. Tabel v1 dipetakan ke v2 (bit baru = 0, aksi baru = 0) supaya training tidak mulai dari nol.

**ThiefBot harus bisa bereaksi**, kalau tidak aksi baru tidak ada gunanya saat training:

- Mendengar ketukan: peluang `panic` (diacak 0.2–0.6 per episode) keluar dan lari.
- Melihat monster menjauh: peluang keluar dari tempat sembunyi setelah 2–6 dtk.
- Gaya `canner`: melempar kaleng jauh lebih sering (untuk melatih `IgnoreNoise`).

**Lab:** training ulang Q dan PPO dengan kontrak v2 (`behaviors: HunterGuardV2` supaya log v1 dan v2 tidak tercampur), evaluasi 3 level × {FSM, Q v1, Q v2, PPO v1, PPO v2} × 350 misi + gerbang 2.3.

**Selesai kalau:** v2 tidak lebih buruk dari v1 di semua level (p ≥ 0.05 atau lebih tinggi), setiap aksi baru dipakai ≥ 3% keputusan di situasi yang membolehkannya, dan melawan bot `canner` catch rate v2 > v1 (p < 0.05).

## 6. Fitur #4 dan #3: tim monster (C3)

**Urutan:** #4 dulu (lebih sederhana), #3 dibangun di atasnya.

- **#4 Peran:** setiap monster yang sedang Search memilih peran {`Chaser`, `Interceptor`, `Sweeper`} dengan policy yang sama. Observasi tambahan: peran monster lain (one-hot × 3) dan jarak ke mereka (lokal). Aksi tingkat rendah tetap dari kontrak v2 tapi di-mask per peran (contoh: `Interceptor` hanya `InterceptExit`/`AmbushTarget`).
- **#3 Pack hunting:** `MA-POCA` di ML-Agents (`trainer_type: poca`), satu `SimpleMultiAgentGroup` per misi. Reward grup +1 kalau siapa pun menangkap, −1 kalau pemain lolos. Reward individual shaping tetap kecil. Ini mengganti aturan v1 "tangkapan monster lain = 0".
- **Paralel tetap lewat proses** (satu rumah per proses), karena `GameSession` adalah singleton. Tidak butuh refactor scene.
- **Kurikulum:** 2 monster di level 3 → 3 monster di level 5–6 → jumlah sesuai DDA.
- **Risiko:** budget step jauh lebih besar (keputusan × jumlah monster), dan ada risiko semua monster berkumpul di exit. Gerbang camping (2.3) dihitung **per tim**: maksimal satu monster < 4 m dari exit pada satu waktu, selama > 25% misi.
- **Selesai kalau:** tim RL > tim FSM dengan jumlah monster sama (p < 0.05) di level 5 dan 6, dan gerbang 2.3 lolos.

## 7. Fitur #5: RL Director (C4)

- **Apa:** agent terpisah yang **tidak mengendalikan gerak monster**. Setiap 10 dtk dia memilih satu dari {`Calm`, `Build`, `Pressure`, `Release`}:

| Aksi Director | Efek (semua lewat sistem yang sudah ada) |
|---|---|
| `Calm` | Hunter dibatasi ke aksi v1 tanpa teror; `HorrorScares` jarang |
| `Build` | Suara jauh (`HorrorScares`), lampu berkedip, Hunter boleh `Stalk` |
| `Pressure` | Semua aksi Hunter boleh, patroli lebih dekat ke target belum diambil |
| `Release` | Hunter `GiveUp` lebih mudah (mask aksi agresif) selama 20 dtk |

- **Reward:** mengikuti kurva ketegangan target, bukan menangkap. `tension` dihitung dari telemetri yang sudah direncanakan di `PlayerPerformance` (jarak monster terdekat saat pemain tidak terlihat, detik dikejar, nyaris tertangkap < 2 m, waktu sejak ancaman terakhir). Reward per keputusan `−|tension − target(t)|`, dengan `target(t)` naik ke puncak di 70–85% durasi misi lalu turun. Reward terminal: +0.5 kalau hasil misi sesuai target win-rate untuk `D` saat itu.
- **Hubungan dengan Hunter:** Director mengatur mask dan parameter; Hunter tetap memaksimalkan tangkapan dalam batas itu. Dua tujuan dipisah supaya tidak saling merusak.
- **ThiefBot tidak bisa takut**, jadi `tension` hanya proxy. Wajib ada playtest manusia: kuesioner 1–5 ("tegang", "adil", "bosan"), minimal 8 orang × 2 misi (Director vs aturan DDA biasa), urutan diacak.
- **Selesai kalau:** kurva `tension` rata-rata lebih dekat ke target dibanding DDA aturan (uji pada 200 misi bot), dan playtest menunjukkan skor "tegang" tidak lebih rendah serta skor "curang" ≤ 2.

## 8. Daftar usulan perubahan C# (dikerjakan di laptop)

| Fase | File | Perubahan |
|---|---|---|
| Wajib sebelum lab (1a/1b) | `HunterDecision.cs`, `GameSession.cs`, `HunterAgent.cs` (baru), `Packages/manifest.json` | Flag `-hunter-level`, `-hunter-table`, `-hunter-ppo-train`, `-hunter-ppo-eval`, `Application.Quit()` di player (rencana lab bagian 5) |
| C1 | `HunterDecision.cs`, `QLearningHunter.cs`, `DifficultyProfile.cs` | Pemilihan tier model, mode online + clamp, `-hunter-online-eval`, `-thiefbot-style` |
| C1 | `HunterDecision.cs` | Field log `contract=`, `seed=`, `mirrored=`, `exitTime=` |
| C1 | `GameSession.cs`, `InteriorLayout.cs`, `HouseShell.cs`, `RuntimeNavigation.cs` | `-hunter-seed-offset`, `-hunter-seed random`, mirror training, satu properti seed bersama |
| C2 | `HunterContract.cs` | Aksi 6–9 (append), observasi 29–40, mask baru, state Q v2 + pemetaan dari v1 |
| C2 | `HunterDecision.cs`, `GuardBrain.cs` | Eksekusi `IgnoreNoise`, `Stalk`, `FakeRetreat`, `KnockOnHiding` lewat NavMesh; event `HunterDecision.Chosen` untuk presentasi |
| C2 | `ThiefBot.cs`, `ThiefPolicy.cs` | Reaksi panik terhadap ketukan dan monster menjauh, gaya bot tetap |
| C3 | `HunterAgent.cs`, `GuardEncounter.cs` | Beberapa Hunter, `SimpleMultiAgentGroup`, peran |
| C4 | `DirectorAgent.cs` (baru), `PlayerPerformance.cs`, `HorrorScares.cs` | Agent Director, sinyal `tension`, hook aksi Director |

Setiap perubahan pada `GuardBrain`, `PlayerMotor`, dan `GameSession` adalah file bersama: kabari Anggota A dan B sebelum push.

## 9. Definisi selesai per fase (100%)

Sebuah fase dianggap selesai hanya kalau **semua** poin ini terpenuhi:

1. Kode di laptop: compile bersih, `Tests/run-campaign-rules.sh` lolos, tes logika baru untuk mask/observasi/state ditambahkan.
2. Build Linux baru lolos `hh.sh preflight` (semua flag fase itu ada) dan `hh.sh smoke`.
3. Training selesai sesuai anggaran tanpa crash; untuk PPO, `ppo_watch.py` memberi verdict OK di 30k step.
4. Evaluasi 350 misi per sel di level 1, 3, 6, plus seed held-out dan rumah dicerminkan.
5. Gerbang robustness 2.3 lolos dan syarat "Selesai kalau" fitur itu terpenuhi.
6. `ml/results/summary.md` diperbarui oleh `make_summary.py`; klaim sesuai angka, p ≥ 0.05 ditulis "belum signifikan".
7. Model/tabel ter-commit ke `Resources/Adaptive/`, status di `plans/rl-training-lab.md` diperbarui.

Kalau satu poin gagal, fase itu belum selesai: perbaiki, atau catat alasan dan keputusan tim di `summary.md` sebelum lanjut.

## 10. Urutan dan perkiraan waktu

| Fase | Isi | Laptop (kode + build) | Lab (training + evaluasi) |
|---|---|---|---|
| B (sekarang) | Tugas 1–6 dengan kontrak v1 | Fase 1a/1b/1c rencana lab | 2–3 hari |
| C1 | #6, #1, seed/mirror/log v2 | 1–1,5 hari | 1 hari |
| C2 | #2, #7, kontrak v2, ThiefBot reaktif | 2–3 hari | 2 hari |
| C3 | #4, lalu #3 | 4–6 hari | 3–4 hari |
| C4 | #5 + playtest | 3–4 hari | 2 hari + playtest |

Kalau waktu tidak cukup, C3 dan C4 boleh berhenti sebagai desain (dokumen ini) di laporan; C1 dan C2 memberi peningkatan terbesar per hari kerja.

## 11. Perintah lab

```bash
cd ~/hollowhouse-rl
repo/ml/lab/hh.sh setup       # venv + trainer + ekstrak build (berhenti kalau tgz belum ada)
repo/ml/lab/hh.sh preflight   # cek build, flag di Assembly-CSharp.dll, library, core, GPU
repo/ml/lab/hh.sh smoke       # 3 misi baseline
repo/ml/lab/hh.sh q           # tmux hh-q: Q-learning random level 1500 ep (lanjut 3000 kalau state < 40/48)
repo/ml/lab/hh.sh ppo         # tmux hh-ppo + hh-tb: PPO, berhenti otomatis kalau reward tidak naik di 30k step
repo/ml/lab/hh.sh ppo-watch   # reward, panjang episode, lesson, distribusi aksi, verdict
repo/ml/lab/hh.sh eval        # tmux hh-eval: level × {baseline, eval, ppo-eval} × 350, lanjut dari yang belum selesai
repo/ml/lab/hh.sh report      # tulis ml/results/summary.md + CSV
repo/ml/lab/hh.sh status
```

Pengaturan lewat env: `HH_NUM_ENVS` (default 10, dibatasi core bebas − 2), `HH_EVAL_PAR` (default 6), `HH_LEVELS`, `HH_EVAL_EPISODES`, `HH_RUN_ID`, `HH_EVAL_ARGS` (contoh nanti: `-hunter-seed-offset 1000` untuk held-out), `HH_PPO_EVAL` (`auto` | `python` | `build` | `none`).

`ppo-eval` memakai model hasil training di lab lewat `mlagents-learn --inference --deterministic --initialize-from <run>` dengan level dikunci. Ini dipakai karena `-hunter-ppo-eval` di build hanya menjalankan ONNX yang ikut terpanggang saat build.
