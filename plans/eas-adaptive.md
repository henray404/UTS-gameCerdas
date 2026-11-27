# Plan Upgrade EAS: Hollow House Adaptive

Acuan tugas: `../context.md` (Project EAS — Adaptive Game AI: PCG + DDA + Game AI, ML/RL opsional).

## 0. Posisi sekarang dibanding syarat EAS

| Syarat | Yang sudah ada | Yang kurang |
|---|---|---|
| PCG + validasi | 4 bentuk rumah, 3 variasi tiap ruangan (27 kombinasi), roster monster dan rute patroli acak. Jalur ke target, tempat sembunyi, dan pintu keluar sudah divalidasi (`InteriorLayout.ValidatePaths`). | Seed dipilih per level dan **tidak dipengaruhi performa pemain**. |
| DDA | Kurva kesulitan tetap per level di `CampaignLevels` (jarak pandang, FOV, suspicion, kecepatan, kaleng). | **Tidak ada DDA sama sekali.** Kesulitan statis dan tidak membaca performa pemain. |
| Game AI | FSM 5 state (Patrol, Investigate, Chase, Search, Return), perception (penglihatan dan pendengaran), memory, NavMesh, laporan antar-penjaga. | Belum ada **Attack** dan **Flee**. Tertangkap langsung game over (`GuardBrain.cs:109`). Pemilihan aksi masih aturan if-else, dan DDA belum mengubah perilaku AI. |
| ML/RL | Belum ada. | Opsional (pengembangan). |

Kesimpulan: bagian yang paling lemah adalah DDA, padahal DDA adalah inti tugas. Prioritas pertama adalah DDA yang menggerakkan PCG dan AI sekaligus.

## 1. Alur target

```
Main misi → PlayerPerformance (telemetri) → SkillModel (aturan + ML opsional)
→ DifficultyDirector (D 0..1, dengan hysteresis)
→ DifficultyProfile → [PCG: layout, monster, resource] + [AI: angka + perilaku]
→ misi berikutnya / retry / penyesuaian di tengah misi
```

## 2. Arsitektur file baru

Lokasi: `Assets/SneakHouse/Scripts/Adaptive/`

| File | Isi | Bisa dites dengan mono? |
|---|---|---|
| `PlayerPerformance.cs` | Mencatat per misi: menang/kalah, waktu ÷ waktu acuan, jumlah alert, berapa kali ketahuan, detik dikejar, hit yang diterima (sanity), kaleng dipakai, baterai terpakai, jumlah sembunyi, jumlah retry, progres target. | Ya (inti datanya) |
| `SkillModel.cs` | Skor performa P (0..1), lalu skill S dihaluskan dengan EMA. Disimpan di PlayerPrefs `SneakHouse.Skill.v1`. | Ya |
| `DifficultyDirector.cs` | S → D, dengan batas perubahan dan hysteresis. Juga penyesuaian kecil di tengah misi. | Ya |
| `DifficultyProfile.cs` | Semua parameter hasil keputusan DDA, ditambah daftar alasannya untuk ditampilkan. | Ya |
| `AdaptiveLayoutPicker.cs` | Menilai semua kandidat layout dan memilih yang paling dekat dengan D. | Bagian penilaiannya ya |
| `ResourceSpawner.cs` | Menaruh baterai, pil sanity, dan kaleng secara procedural dengan aturan. | Bagian aturannya ya |
| `MonsterTactics.cs` | Pemilihan aksi monster dengan Utility AI dan pembagian peran (pengejar / pencegat). | Bagian penilaiannya ya |
| `TelemetryLog.cs` | Menulis CSV per misi ke `persistentDataPath`, untuk dataset ML dan grafik laporan. | — |
| `Tests/AdaptiveRulesTests.cs` | Tes logika murni, ikut dijalankan oleh `run-campaign-rules.sh`. | — |

Aturan penting: `CampaignLevels` tetap jadi **baseline**. DDA hanya menambah lapisan pengali di atasnya, jadi 426 tes yang ada tidak perlu diubah.

## 3. Rancangan DDA

**Skor performa per misi:**

```
P = .35·won + .20·(1 − clamp(timeRatio − .5, 0, 1))
  + .20·(1 − min(alerts, 5)/5) + .15·(1 − hits/maxHits) + .10·(1 − min(chaseSec, 60)/60)
Kalah: tambah .30 × (target terkumpul / target wajib), supaya progres tetap dihargai.
```

**Skill dan difficulty:**

- `S ← S + 0.35·(P − S)`, nilai awal 0.5.
- `D = clamp(S, .1, .95)`. Perubahan maksimal ±0.15 per misi. D hanya berubah kalau |ΔD| > 0.08, supaya tidak naik-turun terus.

**Penyesuaian di tengah misi:**

- Pemain kena 2 hit dalam 60 detik → suspicion rate ×0.85 selama 45 detik, dan monster lebih cepat kabur dari senter.
- Pemain tidak terdeteksi lebih dari 90 detik dan target sudah lebih dari 50% → satu monster berubah peran jadi "Hunter" yang berjaga dekat pintu keluar.

**Pemetaan D ke parameter (dikalikan ke baseline level):**

| Kelompok | Parameter | D rendah | D tinggi |
|---|---|---|---|
| PCG musuh | Jumlah monster | baseline −1 | baseline +1 (maks 4, divalidasi) |
| PCG musuh | Frekuensi patroli lewat dekat target | jarang | sering |
| PCG layout | Skor layout yang dipilih | sederhana | kompleks |
| PCG resource | Kaleng / baterai / pil sanity | +2 / +2 / +2 | −1 / 0 / 0 |
| PCG resource | Jarak resource dari rute patroli | ≥ 5 m (aman) | ≥ 2 m (berisiko) |
| Lingkungan | Lampu rumah yang masih menyala, kepadatan kabut | lebih terang | lebih gelap |
| AI angka | Jarak pandang, FOV, suspicion rate, hearing | ×0.8 | ×1.2–1.35 |
| AI angka | Lama mencari, radius laporan | ×0.7 | ×1.4–1.5 |
| AI angka | Jeda antar serangan, damage sanity | 4 s, 25 | 1.5 s, 40 |
| **AI perilaku** | Reaksi terhadap senter | kabur setelah 1 detik disorot | kebal cahaya, malah menoleh ke arah cahaya (cahaya membongkar posisi pemain) |
| **AI perilaku** | Memeriksa tempat sembunyi | hanya kalau melihat pemain masuk | juga memeriksa tempat sembunyi terdekat saat mencari |
| **AI perilaku** | Taktik bersama | semua menuju posisi terakhir pemain | peran pengejar + pencegat (satu monster menghadang jalur keluar) |
| **AI perilaku** | Penyergapan | tidak ada | menunggu di dekat target yang belum diambil |

Baris perilaku inilah yang menjawab poin "adaptasi tidak hanya mengubah angka statistik".

## 4. PCG adaptif dengan constraint

**AdaptiveLayoutPicker (generate-and-test):**

1. Bangkitkan kandidat: semua kombinasi layout × N seed encounter (misalnya 27 × 6).
2. Buang kandidat yang gagal validasi (validasi yang ada, ditambah constraint baru):
   - Di D rendah, setiap target punya minimal 1 tempat sembunyi dalam radius 5 m.
   - Jalur dari target ke pintu keluar tidak boleh 100% terlihat dari rute patroli.
3. Hitung fitur memakai NavMesh:
   - L = panjang jalur start → semua target → keluar.
   - C = persentase titik jalur yang terlihat dari waypoint patroli.
   - H = jumlah tempat sembunyi di sekitar jalur.
   - T = jarak terdekat dari rute patroli ke target.
4. `score = .35·norm(L) + .35·C − .15·norm(H) + .15·(1 − norm(T))`, lalu pilih kandidat dengan |score − D| terkecil.
5. Konsekuensi: **retry setelah kalah bisa menghasilkan layout berbeda yang lebih mudah**. README bagian retry perlu diperbarui.

**ResourceSpawner:**

- Mekanik baru: baterai senter (senter menguras baterai, dan ini sekaligus resource yang dibaca DDA) dan pil sanity.
- Aturan penempatan: ada di NavMesh dan terjangkau dari start, jarak minimal dari waypoint patroli tergantung D, antar-item minimal 3 m, tidak di dalam tempat sembunyi.
- Kalau aturan tidak terpenuhi, longgarkan bertahap, lalu fallback ke titik aman yang sudah ditentukan.

## 5. Upgrade Game AI

- **HFSM**, dikelompokkan jadi 4 lapis:
  - Calm: Patrol, Idle, Peek.
  - Alert: Investigate, Search, CheckHidingSpot.
  - Hostile: Chase, **Attack**, Flank.
  - Recover: **Flee/Retreat**, Return.
- **Attack:** jarak ≤ 1.4 m dan ada line of sight → ancang-ancang 0.45 detik (jeritan + mata membesar sebagai tanda) → kalau pemain masih dalam jangkauan, sanity berkurang dan pemain terhuyung → monster masuk **Retreat** selama jeda serangan, lalu Search. Game over terjadi saat sanity 0, menggantikan tangkap instan di `GuardBrain.cs:109`. Data hit ini juga jadi input DDA.
- **Flee dari cahaya:** paparan senter dihitung dari sudut, jarak, dan line of sight. Kalau melewati ambang (ditentukan D), monster kabur ke waypoint terjauh di luar sorotan. Senter jadi pilihan taktis yang punya risiko.
- **Utility AI** untuk memilih aksi di state Alert dan Hostile. Kandidat aksi: Investigate, Search, CheckHidingSpot, Intercept, Ambush, Return. Pertimbangannya: jarak, umur memori, suspicion, peran rekan monster (supaya tidak dobel), dan bobot agresi dari D. Skor tiap aksi ditampilkan di panel F1.

## 6. Bahan demo dan cara mengamatinya

| Yang harus ditunjukkan | Cara menunjukkannya |
|---|---|
| PCG menghasilkan variasi | Panel F2 menampilkan kandidat layout dan skornya, plus layout yang terpilih. Tool capture menyimpan 2 layout dengan D berbeda. |
| DDA membaca performa | Panel F2 menampilkan metrik live (alert, hit, waktu chase, baterai) beserta P dan S. |
| DDA mengubah lebih dari 1 parameter | Layar hasil menambah bagian **"Adaptation"** berisi diff sebelum → sesudah, contohnya: `D .58 → .44: monster 3→2, vision 6.5→5.6 m, kaleng 3→4, baterai 1→2, monster kabur dari senter`. |
| NPC punya decision making | F1: state HFSM, skor utility, peran (pengejar/pencegat), isi memori. |
| Dampak terlihat | Tombol F3 (khusus mode debug) memaksa profil rendah atau tinggi, untuk perbandingan langsung di video. |
| ML (kalau dipakai) | F2 menampilkan P(gagal) dari model dan bobotnya terhadap S. |

## 7. ML (pengembangan)

**Opsi A, direkomendasikan: prediktor skill dengan logistic regression.**

- Input: fitur telemetri CSV.
- Label: "gagal di attempt berikutnya".
- Training offline dengan Python (`ml/train_skill_model.py`, scikit-learn), bobot diekspor ke `Resources/Adaptive/skill_model.json`, lalu inference di C# cukup dot product + sigmoid.
- Rumus akhir: `S' = .6·EMA + .4·(1 − P(gagal))`.
- Untuk laporan: AUC dari cross-validation dibanding baseline berbasis aturan saja.
- Catatan jujur: datanya kecil (target minimal 150 misi dari playtest tim). Jangan klaim akurasinya tinggi.

**Opsi B, hanya kalau waktu sisa: RL dengan ML-Agents** untuk satu monster "Hunter".

- Observation: ray sensor + posisi terakhir pemain. Action: arah gerak. Reward: menangkap pemain, dikurangi waktu.
- Risikonya tinggi (instal package, training berjam-jam, perilaku sulit dikontrol). Jangan dijadikan andalan.
- Desain lengkapnya, termasuk fallback Q-learning tanpa package, ada di [`rl-enemy-design.md`](rl-enemy-design.md).

## 8. Fase kerja (perkiraan total sekitar 10 hari kerja)

Pemetaan fase ini ke materi Pertemuan 9–14 dan pembagian tugas 3 orang ada di [`progres-per-pertemuan.md`](progres-per-pertemuan.md).

| Fase | Isi | Estimasi | Selesai kalau |
|---|---|---|---|
| 0 | Commit konversi horor ke branch `eas-adaptive`, perbarui naskah demo | 0.5 hari | Build dan `./check-and-run.sh check` lolos |
| 1 | Telemetri, SkillModel aturan, DifficultyDirector, Profile, penyimpanan; diterapkan ke angka AI, jumlah monster, kaleng; tes | 2 hari | Kalah 2× di level 3 membuat D turun dan minimal 3 parameter berubah, tes logika lolos |
| 2 | AdaptiveLayoutPicker, ResourceSpawner, baterai senter, pil sanity | 2 hari | D rendah dan tinggi menghasilkan layout dan resource yang terukur berbeda, semuanya lolos validasi |
| 3 | Sanity + Attack + Retreat, flee dari cahaya, Utility AI, pengejar/pencegat, perilaku yang dikunci oleh D | 2.5 hari | F1 menampilkan state, skor, dan peran, perilaku berbeda di D rendah vs tinggi |
| 4 | Panel F2, laporan Adaptation di layar hasil, F3, perluasan tool capture | 1 hari | Semua poin demo bisa direkam dalam 10 menit |
| 5 | ML opsi A: pengumpulan data, training, integrasi | 1.5 hari | Model termuat, AUC tercatat |
| 6 | Balancing, video demo, README/laporan | 1 hari | Video lengkap sesuai poin demo |

## 9. Risiko

- **D naik-turun terus:** dicegah dengan hysteresis dan batas langkah per misi.
- **Validasi gagal saat monster ditambah:** fallback ke jumlah monster lebih sedikit, dan alasannya dicatat di profile.
- **Pemain sengaja kalah supaya lebih mudah:** bisa diterima untuk tugas ini, cukup disebutkan di laporan.
- **Penilaian 27 × N kandidat lambat:** dibagi ke beberapa frame di layar briefing.
- **Monster terasa curang di D tinggi:** setiap serangan wajib ada tanda (telegraph), dan monster tidak boleh menembus dinding.
- **Dokumen lain belum sinkron:** `LAPORAN-SISTEM-LEVEL.md`, `PANDUAN-DEMO-TIGA-ORANG.md`, dan `script.md` masih menjelaskan versi top-down tanpa DDA. Perlu ditulis ulang di Fase 6.
