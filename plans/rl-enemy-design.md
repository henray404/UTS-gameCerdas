# Desain RL Musuh: The Hunter

Dokumen desain sebelum implementasi. Acuan materi: Pertemuan 13 (Machine Learning for Games, Q-learning) dan Pertemuan 14 (Reinforcement Learning with Unity ML-Agents). Rencana adaptif induknya ada di [`eas-adaptive.md`](eas-adaptive.md).

## 1. Prinsip

- **RL tidak menggantikan FSM.** Sesuai materi P14 hal. 67 ("ML tidak harus menggantikan seluruh AI"), FSM `GuardBrain` tetap mengatur mode besar (Patrol, Investigate, Chase, Search, Return). NavMesh tetap mengatur jalan. Policy RL hanya mengambil **satu keputusan taktis**: ke mana monster pergi setelah kehilangan jejak pemain.
- **Hanya satu monster yang memakai RL.** Hunter adalah peran, bukan karakter baru: satu `GuardBrain` di roster diberi flag `useHunterPolicy`. Monster lain tetap FSM biasa, jadi perbandingan rule-based dan learning-based bisa dilihat langsung di satu misi.
- **Tidak curang.** Observasi hanya berisi hal yang memang bisa diketahui monster: penglihatan, suara, memori, dan laporan rekan. Posisi asli pemain tidak pernah diberikan saat pemain tidak terlihat.
- **Aksi diskrit tingkat tinggi**, bukan gerak kontinu. P14 hal. 65 menyebut NavMeshAgent lebih praktis untuk navigasi, dan aksi diskrit lebih mudah dijelaskan (P13 hal. 77) dan dilatih.

## 2. Kapan policy dipanggil

```
Patrol ──lihat/dengar/laporan──▶ (Hunter) HUNT LOOP ◀── kehilangan jejak ── Chase (FSM murni)
                                      │   pilih 1 dari 6 aksi        ▲
                                      │   NavMesh ke tujuan aksi     │ Suspicion ≥ 1
                                      │   tiba / timeout / bukti baru ─▶ keputusan berikutnya
                                      ▼
                         GiveUp atau memori > 15 dtk ──▶ Return ──▶ Patrol
```

### Istilah dalam kode

- **Kehilangan jejak:** di `GuardBrain.Step` case `Chase`, pemain tidak terlihat lebih lama dari `loseSightSeconds` (`now - lastVisible > loseSightSeconds`), lalu `SetMode(Mode.Search)`. Untuk Hunter, ini masuk ke hunt loop.
- **Bukti baru:** `Hear` (suara langkah atau kaleng) dan `ReceiveAlert` (laporan rekan lewat `GameSession.Broadcast`). Keduanya memanggil `Remember` lalu `SetMode(Investigate/Search)`, jadi otomatis memotong aksi yang sedang berjalan dan meminta keputusan baru.
- **Hunt loop** menggantikan Investigate dan Search khusus untuk Hunter. Patrol, Chase, dan Return tidak berubah.

### Satu keputusan selesai kalau

1. **Tiba** di tujuan (`Reached()`), lalu diam 0.9 dtk sambil menoleh (sama seperti Search sekarang). Kalau aksinya `CheckHidingSpot`, diam 0.8 dtk di depan tempat sembunyi.
2. **Timeout:** waktu tempuh melewati `travelBudget` (rumus sekarang: panjang jalur ÷ kecepatan + 5 dtk, dibatasi 10–60 dtk).
3. **Bukti baru** dari `Hear` atau `ReceiveAlert`.
4. **Pemain terlihat dan `Suspicion` mencapai 1:** FSM pindah ke Chase seperti biasa dan keputusan dibatalkan.

Keputusan **tidak** diminta tiap 2 dtk. Aksi yang butuh perjalanan jauh (misalnya `InterceptExit`) akan selalu terpotong kalau diminta berkala.

### Titik sambung di `GuardBrain`

| Lokasi | Perilaku sekarang | Perilaku Hunter |
|---|---|---|
| `SetMode`, cabang `else` (mode Investigate/Search) | `Go(LastKnownPosition)` (+`SearchOffset` kalau `assistingSearch`), lalu hitung `travelBudget` | Minta keputusan ke `HunterDecision`, `Go(tujuan aksi)`, lalu hitung `travelBudget` dengan rumus yang sama |
| `Step`, case `Investigate`, saat `Reached()` | `CheckHiding()`, lalu pindah ke Search | `CheckHiding()` untuk `witnessedSpot` saja, lalu diam dan minta keputusan berikutnya |
| `Step`, case `Search`, saat diam selesai | `Go(LastKnownPosition + SearchOffset(...))` | Minta keputusan berikutnya |
| `Step`, case `Search`, batas waktu | Return setelah `searchDuration` dtk sejak tiba | Return kalau `now - LastEvidenceTime > 15` dtk atau aksi `GiveUp`. `searchDuration` tidak dipakai Hunter |
| `Hear`, `ReceiveAlert` | Ubah memori, `SetMode` | Tidak berubah; `SetMode` sudah meneruskan ke `HunterDecision` |
| `Remember` | Menyimpan posisi dan sumber | Juga menyimpan `PlayerMotor.Velocity` saat sumbernya `"Vision"`, untuk observasi kecepatan |

`CheckHiding()` otomatis tetap berlaku untuk `witnessedSpot` (monster melihat pemain masuk, jadi wajar memeriksa). Pemeriksaan tempat sembunyi lain hanya lewat aksi `CheckHidingSpot`.

**Kebocoran informasi yang harus ditutup:** `TryInspect` sekarang langsung `return false` kalau `spot.Occupant == null`, jadi monster "tahu" tempat itu kosong tanpa melihat. Aksi `CheckHidingSpot` wajib berjalan ke `Approach` dan diam 0.8 dtk dulu, baru menentukan hasilnya.

## 3. Kontrak agent (sama untuk Q-learning dan PPO)

### Aksi (1 branch diskrit, 6 pilihan)

| # | Aksi | Tujuan NavMesh |
|---|---|---|
| 0 | `GoLastKnown` | `LastKnownPosition` |
| 1 | `SweepSector` | `LastKnownPosition + SearchOffset(n)`; `n` naik tiap kali aksi ini dipilih |
| 2 | `InterceptExit` | Titik di jalur NavMesh dari posisi terakhir ke `ExtractionZone`, sekitar 60% panjangnya |
| 3 | `AmbushTarget` | Dekat `LootTarget` dengan `IsRequiredTarget` true dan `Collected` false yang terdekat |
| 4 | `CheckHidingSpot` | `HidingSpot.Approach` terdekat dari posisi terakhir |
| 5 | `GiveUp` | Kembali ke patroli (`SetMode(Mode.Return)`) |

**Action masking:** `AmbushTarget` mati kalau `GameSession.Collected == RequiredTargetCount`. `InterceptExit` mati kalau umur memori ≥ 15 dtk. `CheckHidingSpot` mati kalau tidak ada tempat sembunyi dalam 10 m dari posisi terakhir.

### Observasi (vektor, relatif terhadap monster, sesuai P14 hal. 16)

| Observasi | Ukuran | Normalisasi |
|---|---:|---|
| Arah ke posisi terakhir pemain (x, z) | 2 | unit vector |
| Jarak ke posisi terakhir | 1 | ÷ 20 m, clamp 0..1 |
| Umur memori (`now - LastEvidenceTime`) | 1 | ÷ 15 dtk |
| Sumber memori | 4 | one-hot, lihat pemetaan di bawah |
| `Suspicion` | 1 | sudah 0..1 |
| Kecepatan pemain saat terakhir terlihat (x, z) | 2 | ÷ `PlayerMotor.runSpeed` (4.2) |
| Arah dan jarak posisi terakhir → pintu keluar | 3 | unit vector + ÷ 20 |
| Arah dan jarak ke target belum diambil terdekat | 3 | unit vector + ÷ 20 (0 kalau tidak ada) |
| Rasio target sudah diambil | 1 | `Collected ÷ RequiredTargetCount` |
| Arah dan jarak ke tempat sembunyi terdekat dari posisi terakhir | 3 | unit vector + ÷ 10 |
| Jumlah monster lain yang sedang Search/Chase | 1 | ÷ 3 |
| Aksi sebelumnya | 6 | one-hot |
| **Total** | **28** | |

**Pemetaan `MemorySource`** (string yang dipakai `GuardBrain` sekarang):

| One-hot | String di kode |
|---|---|
| Lihat | `"Vision"` |
| Suara | `"Footsteps"`, `"Running footsteps"`, `"Dropped can"` |
| Laporan | `"Shared alert"` |
| Hiding | `"Saw hiding entry"` |

Ditambah `RayPerceptionSensor3D` (8 ray, layer 8 = dinding, layer yang sama dengan raycast di `GuardBrain.Sees`). Ray sensor hanya dipakai di versi PPO.

### Reward

| Kejadian | Reward | Catatan |
|---|---:|---|
| Menangkap pemain | +1.0 | Episode selesai |
| Pemain keluar lewat `ExtractionZone` | −1.0 | Episode selesai |
| Melihat pemain lagi setelah kehilangan jejak | +0.2 | Maksimal sekali per 10 dtk |
| Memeriksa tempat sembunyi dan pemain ada di dalam | +0.3 | Lihat "Usulan perubahan kontrak" |
| Memeriksa tempat sembunyi kosong | −0.02 | Mencegah spam cek |
| Setiap keputusan | −0.001 | Mendorong keputusan cepat |

Reward utama (+1/−1) sengaja jauh lebih besar dari shaping, mengikuti peringatan P14 hal. 55 dan 63. Untuk Q-learning, reward yang terkumpul di antara dua keputusan dijumlahkan menjadi satu `r`.

### Cara mendeteksi akhir episode

| Kejadian | Sumber di kode | Catatan |
|---|---|---|
| Tertangkap di tempat terbuka | `GuardBrain.Step`: jarak < 0.72 m tanpa dinding → `GameSession.End(false)` | Bisa dipicu monster mana pun |
| Tertangkap di tempat sembunyi | `HidingSpot.Inspect(guard)` → `End(false)` | Bisa dipicu monster mana pun |
| Pemain lolos | `ExtractionZone.Check` → `End(true)` kalau `Collected == RequiredTargetCount` | `GameSession.Won` jadi true |

`End` tidak mencatat siapa yang menangkap. **Diimplementasikan** tanpa mengubah `GameSession`: `GuardBrain.CaughtPlayer` diisi `true` tepat sebelum `End(false)` di `Step`, saat `HidingSpot.Inspect` berhasil di `TryInspect`, dan di `HunterDecision.ResolveCheck`. Hunter memberi +1 hanya kalau `CaughtPlayer` miliknya sendiri true, dan 0 kalau yang menangkap monster lain.

**Risiko reward hacking yang harus diuji:** Hunter belajar diam di pintu keluar sepanjang misi (reward −1 tidak pernah terjadi). Penangkalnya: `InterceptExit` hanya boleh dipilih kalau memori < 15 dtk, dan evaluasi wajib mencatat persentase waktu Hunter berada < 4 m dari pintu keluar.

### Episode

- **Mulai:** pilih level acak 1–6 (menentukan bentuk rumah dan target) dan seed interior acak (27 kombinasi per bentuk), lalu posisi start pemain dan Hunter acak. Randomisasi ini mencegah agent menghafal (P14 hal. 50 dan 64).
- **Selesai:** tertangkap, pemain lolos, atau max 3000 keputusan.
- **Reset:** muat ulang scene, seperti `GameSession.Restart()`. Interior dan NavMesh (`RuntimeNavigation.Rebuild`) dibangun ulang dari seed baru. Academy ML-Agents bertahan saat scene dimuat ulang.

## 4. Lawan saat training: `ThiefBot`

Training tidak bisa memakai pemain manusia, jadi perlu `ThiefBot` berbasis aturan yang mengendalikan `PlayerMotor` yang sama:

1. Hitung jalur dengan `NavMesh.CalculatePath` dari posisi pemain ke target wajib terdekat, lalu arahkan ke corner berikutnya. Pemain memakai `CharacterController`, bukan NavMeshAgent.
2. Gerak lewat `PlayerMotor.Move(arahDunia, running, sneak, Time.deltaTime)` setiap frame. Mengendap saat ada monster dalam 8 m.
3. Ambil target dengan `PlayerMotor.TryInteract()` saat jaraknya < 1.55 m dan tidak terhalang dinding (aturan `RefreshLoot`).
4. Setelah semua target terambil, menuju `ExtractionZone`.
5. Saat terlihat (ada monster dengan `CanSeePlayer`): lari menjauh. Peluang 50% masuk tempat sembunyi lewat `PlayerHiding.TryEnter()` (butuh tempat dalam `interactionRadius` 1.25 m), keluar dengan `TryExit()` setelah monster pergi.
6. Peluang 30% melempar kaleng dengan `PlayerDistraction.TryDrop()` saat monster mendekat. Kaleng jatuh ke arah `transform.forward`, jadi bot harus menghadap dulu dan tidak terlalu dekat dinding (0.48 m).

Bobot perilaku bot diacak per episode supaya Hunter melawan banyak gaya bermain. Bot ini sekaligus dipakai untuk playtest otomatis DDA.

### Hook minimal di `PlayerMotor`

`PlayerMotor.Update` membaca keyboard dan memanggil `Move` setiap frame. Kalau bot juga memanggil `Move`, gerakannya bertabrakan, stamina berkurang dua kali, dan kecepatan tertimpa. Tambahkan satu field:

```csharp
public bool externalControl; // diisi ThiefBot
// Update: setelah cek Finished/Paused
if (externalControl) return;
// Move: jangan paksa rotasi ke kamera saat dikendalikan bot
if (View != null && View.FirstPersonControls && !externalControl)
```

`PlayerHiding.Update` hanya bereaksi ke tombol F, jadi aman tanpa perubahan.

### Hook minimal di `GameSession` untuk mode training

- `PersistRecords = false`, supaya skor dan progres pemain tidak tercemar oleh ribuan episode.
- `BeginMission` sekarang mensyaratkan `Progress.IsUnlocked(level)`. Mode training perlu melewati cek ini.
- `InteriorLayout` membaca seed dari `GameSession.SelectedLevel.LayoutSeed` (tetap per level). Mode training perlu seed override.
- Mulai otomatis dari Briefing ke Playing tanpa menu.

## 5. Monster lain saat training

- **Curriculum tahap 1–3:** hanya Hunter yang aktif (`activeGuardCount = 1`). Semua tangkapan pasti milik Hunter.
- **Curriculum tahap 4:** jumlah monster mengikuti level, monster lain tetap FSM. Hunter menerima laporan mereka lewat `SharedAlert` dan ikut mengirim `Broadcast` saat masuk Chase. Tangkapan oleh monster lain bernilai 0 untuk Hunter (lewat `CaughtBy`).
- `SearchOffset` dan `SearchSector` tetap dipakai monster FSM. Hunter memakai `SweepSector` sendiri.

## 6. Tahapan implementasi

### Tahap A: Q-learning tabular (materi P13)

Tanpa package tambahan, bisa dilatih langsung di Unity dan mudah dijelaskan.

- **State diskrit** (discretization, P13 hal. 42): jarak ke posisi terakhir {dekat < 3 m, sedang < 8 m, jauh}, umur memori {baru < 4 dtk, lama}, sumber memori {lihat, suara/laporan}, target tersisa {ya, tidak}, posisi terakhir dekat tempat sembunyi {ya, tidak}. Totalnya 3 × 2 × 2 × 2 × 2 = 48 state × 6 aksi. Sumber `"Saw hiding entry"` masuk ke "lihat".
- **Update:** `Q(s,a) ← Q(s,a) + α[r + γ·max Q(s',a') − Q(s,a)]`, dengan α = 0.1, γ = 0.95, epsilon-greedy dari 1.0 turun ke 0.05. Aksi yang di-mask tidak ikut dipilih maupun dihitung di `max`.
- **Output:** `Resources/Adaptive/hunter_qtable.json`. Panel F1 menampilkan Q-value state saat ini (explainability, P13 hal. 77).

### Tahap B: PPO dengan Unity ML-Agents (materi P14)

**Fakta lingkungan (dicek 24 Nov 2026):**

| Hal | Kondisi |
|---|---|
| Versi Unity proyek | 6000.6.0f1 (anggota di Windows menjalankannya dengan versi ini) |
| Unity di Mac Henry | Hanya 6000.4.5f1. Jangan membuka proyek dengan versi ini karena berarti downgrade |
| Package `com.unity.ml-agents` | Versi terbaru 4.2.0 (dist-tag `latest`, rilis 30 Okt 2026). Minimal Unity 6000.0. Dependensinya `com.unity.ai.inference` 2.6.1, bukan Barracuda. Seri 4.x mulai dari 4.0.1 (Jan 2026) |
| Trainer Python `mlagents` | Versi terbaru di PyPI 1.1.0. Butuh Python ≥ 3.10.1 dan ≤ 3.10.12, `numpy < 1.24`, `protobuf < 3.21`, `grpcio ≤ 1.48.2`, `torch ≥ 2.1.1` |
| Python di Mac | Homebrew 3.15; Python 3.10 harus dipasang lewat `uv` di venv terpisah |
| Mesin training | Lab `dnp`: Linux x86_64, 16 core, 2× RTX 2080 Ti, `uv` tersedia, dijangkau lewat `gpurun` (perlu VPN ITS dari luar kampus) |

Belum terverifikasi: package 4.2.0 benar-benar jalan di Unity 6000.6, kecocokan protokol trainer 1.1.0 dengan package 4.2.0, dan ketersediaan wheel `grpcio 1.48.2` untuk Python 3.10 di macOS arm64.

**Spike (maksimal 1 hari):**

1. Di mesin yang punya Unity 6000.6 (pasang lewat Unity Hub di Mac, atau pakai mesin Windows anggota), tambahkan `com.unity.ml-agents` 4.2.0 dan pastikan proyek tetap compile.
2. Buat venv Python 3.10 dengan `uv`, pasang `mlagents==1.1.0`, lalu jalankan `mlagents-learn --help`.
3. Jalankan satu training singkat dengan agent dummy di editor untuk memastikan trainer dan Unity tersambung.
4. Kalau salah satu langkah gagal, berhenti di Tahap A dan catat alasannya di laporan.

**Rancangan training:**

- Script `HunterAgent : Agent` berisi `OnEpisodeBegin`, `CollectObservations`, `OnActionReceived`, `Heuristic` (heuristic = aturan FSM sekarang, untuk dibandingkan), dan `WriteDiscreteActionMask`.
- Behavior Name `HunterGuard`, konsisten di Behavior Parameters, YAML, dan perintah training (P14 hal. 58).
- Keputusan diminta manual (`RequestDecision()`) pada empat kejadian di bagian 2, bukan lewat Decision Requester berkala.
- **Paralel lewat banyak proses, bukan banyak rumah dalam satu scene.** `GameSession` adalah singleton dengan event statis (`Noise`, `SharedAlert`), dan `RuntimeNavigation` membangun NavMesh di bounds tetap sekitar titik nol (24 × 7 × 18 m). Menduplikasi rumah di satu scene butuh refactor besar. Sebagai gantinya, build player Linux headless dan jalankan `mlagents-learn ... --env=<build> --num-envs=8 --no-graphics` di `dnp`. Ini tetap memenuhi konsep parallel training area (P14 hal. 49).
- Build Linux dibuat dari mesin dengan Unity 6000.6 plus modul Linux Build Support. PPO dengan MLP kecil cukup di CPU; bottleneck-nya jumlah step environment, jadi 16 core `dnp` lebih penting daripada GPU.
- **Curriculum** (P14 hal. 51):
  1. Rumah Collector's House, bot tidak bersembunyi dan tidak melempar kaleng.
  2. Bot boleh bersembunyi.
  3. Bot boleh melempar kaleng.
  4. Semua bentuk rumah, termasuk Loft Villa dua lantai, dengan monster FSM lain aktif.
- **Config awal:** `trainer_type: ppo`, `batch_size: 1024`, `buffer_size: 20480`, `learning_rate: 3e-4`, `beta: 5e-3`, `epsilon: 0.2`, `max_steps: 2e6`. Hasilnya `HunterGuard.onnx` di `Resources/Adaptive/`, Behavior Type = Inference Only.

## 7. Hubungan dengan DDA dan player modeling

- **Hunter hanya muncul kalau D tinggi** (misalnya D ≥ 0.6). Di D rendah semua monster tetap FSM.
- **Checkpoint sebagai tingkat kesulitan:** simpan model di beberapa titik training (misalnya 300k, 1M, 2M step). DDA memilih model yang lebih awal (lebih lemah) untuk pemain yang sedang kesulitan.
- **Gaya bermain memengaruhi aksi:** kalau profil pemain dominan "Hider", nilai awal aksi `CheckHidingSpot` dinaikkan (bias logit / Q awal). Kalau "Speedrunner", `InterceptExit` lebih dipertimbangkan.

## 8. Evaluasi

| Metrik | Cara ukur | Pembanding |
|---|---|---|
| Catch rate | 200 episode bot dengan seed yang **tidak** dipakai saat training | FSM Search sekarang |
| Waktu rata-rata sampai menangkap lagi setelah kehilangan jejak | Log per episode | FSM |
| Distribusi aksi | Histogram 6 aksi | Harus bervariasi, tidak satu aksi saja |
| Waktu berkemah di pintu keluar | % waktu < 4 m dari exit | Batas maksimal 25% |
| Kurva training | Cumulative reward, episode length (TensorBoard) | Naik lalu stabil |
| Fairness (playtest manusia) | Kuesioner 1–5: "monster terasa curang?" | Target ≤ 2 |

## 9. File baru dan perubahan kecil

```
Assets/SneakHouse/Scripts/Adaptive/RL/
  HunterDecision.cs      enum aksi, eksekusi aksi ke NavMesh, masking (dipakai Q dan PPO)
  HunterObservation.cs   penyusun vektor observasi (murni, bisa dites)
  QLearningHunter.cs     tabel Q, epsilon-greedy, simpan/muat JSON
  HunterAgent.cs         turunan ML-Agents Agent (hanya kalau spike berhasil)
  ThiefBot.cs            lawan berbasis aturan untuk training dan playtest
  TrainingBootstrap.cs   mode training: level/seed acak, tanpa menu, tanpa simpan skor
Assets/SneakHouse/Scenes/HunterTraining.unity   salinan scene utama + TrainingBootstrap
ml/hunter_ppo.yaml
Tests/HunterRulesTests.cs  tes observasi, masking, dan update Q
```

Perubahan pada file lama:

- `GuardBrain.cs`: flag `useHunterPolicy`, sambungan di tabel bagian 2, simpan kecepatan pemain di `Remember`.
- `PlayerMotor.cs`: field `externalControl` (bagian 4).
- `GameSession.cs`: `CaughtBy`, serta jalur mode training (bypass unlock, seed override, mulai otomatis).
- `HidingSpot.cs`: isi `CaughtBy` sebelum `End(false)`.

## 10. Usulan perubahan kontrak

Kontrak di atas belum diubah. Usulan berikut perlu disepakati dulu:

- **Reward +0.3 untuk menemukan pemain di tempat sembunyi.** Di kode, `HidingSpot.Inspect` pada tempat berisi pemain langsung memanggil `End(false)`, jadi kejadian ini selalu juga bernilai +1 tangkapan. Total 1.3 membuat tangkapan di tempat sembunyi lebih berharga daripada tangkapan di tempat terbuka. Usulan: hapus +0.3, atau sengaja dipertahankan dan dicatat sebagai bias yang disengaja.
- **Batas episode 3000 keputusan.** Dengan penalti −0.001 per keputusan, total penalti bisa mencapai −3, lebih besar dari reward tangkapan +1. Usulan: batasi sekitar 300 keputusan (misi acuan 240 dtk, paling sering 1 keputusan per detik), sehingga penalti maksimal −0.3. `Tests/HunterRulesTests.cs` sudah memakai batas ini.

## 11. Pertanyaan terbuka

- Siapa yang memasang Unity 6000.6 untuk spike dan build Linux: Mac Henry atau mesin Windows anggota lain?
- Apakah perubahan kecil di `PlayerMotor`, `GameSession`, dan `HidingSpot` boleh dilakukan Anggota C, atau harus lewat pemilik file lain (Anggota A/B)?
