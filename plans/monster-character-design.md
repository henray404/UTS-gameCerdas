# Desain Karakter Monster

Dokumen desain karakter untuk monster Hollow House: The Warden, The Hare, The Glutton, The Crawler, dan The Hunter. Perilaku RL The Hunter ada di [`rl-enemy-design.md`](rl-enemy-design.md). Rencana adaptif induknya ada di [`eas-adaptive.md`](eas-adaptive.md).

## 1. Kondisi sekarang (dicek ke kode, 24 Nov 2026)

| Aspek | Kondisi | Sumber |
|---|---|---|
| Roster | 4 karakter dengan id `bogo`, `judy`, `clawhauser`, `spike`. Tinggi 1.8, 1.65, 1.8, 1.55 m | `Editor/GuardEncounterBuilder.cs` |
| Nama tampil | `MonsterSkin.NameFor` mengubahnya jadi The Warden, The Hare, The Glutton, The Crawler. Ada juga "The Hollow Man" (`Stan`) yang tidak ada di roster | `MonsterSkin.cs` |
| Wujud | Semua memakai satu material hitam basah. Slot `FaceDark` menyala merah, plus satu point light merah di kepala yang lebih terang saat Chase | `MonsterSkin.cs` |
| Animasi | Semua memakai 5 clip yang sama: Idle, Walk, Run, LookAround, FaceActing. Kecepatan referensi 1.4 dan 3.2 m/s | `GuardEncounterBuilder.cs`, `ActorAnimation.cs` |
| Suara | Satu loop `monster-loop` dengan pitch acak 0.82–1.08, terdengar sampai 13 m. Langkah dari `GuardPursuitAudio` hanya berbunyi saat Chase | `MonsterSkin.cs`, `GuardPursuitAudio.cs` |
| Perilaku | Semua angka AI sama per level. Nama karakter tidak mengubah apa pun | `GuardBrain.cs`, `CampaignLevels` |
| The Hunter | Belum ada wujud, suara, atau nama di kode | `MonsterSkin.NameFor` tidak punya entri untuknya |

Masalah utamanya: pemain tidak bisa membedakan monster yang sedang dihadapi, jadi pilihan roster acak tidak terasa.

Id internal di builder memakai nama tokoh film terkenal. Dokumen ini dan semua teks untuk pemain hanya memakai nama monster.

## 2. Prinsip

- **Mata dan suara lebih penting daripada siluet.** Rumahnya gelap berkabut dan ambient sangat redup (`HorrorDirector.indoorAmbient`). Pemain paling sering melihat monster sebagai sepasang cahaya mata, atau hanya mendengarnya. Setiap monster wajib bisa dikenali dari mata dan suaranya saja, sebelum siluetnya terlihat.
- **Ciri tampilan mengumumkan sifatnya.** Sifat gameplay tiap monster harus bisa ditebak dari wujud atau suaranya. Telinga panjang The Hare memberi tahu pemain bahwa ia mendengar lebih jauh.
- **Satu sifat per monster.** Setiap monster punya tepat satu sifat yang berbeda dari baseline level. Sifat lain tetap mengikuti `CampaignLevels` dan DDA, jadi kurva kesulitan per level tidak rusak.
- **Tidak curang.** Sifat hanya mengubah angka yang sudah ada di `GuardBrain` atau aturan yang bisa dijelaskan ke pemain. Monster tetap tidak menembus dinding dan tidak tahu posisi pemain tanpa bukti.

## 3. Identitas per monster

### The Warden

- **Konsep:** penjaga rumah lama yang masih menjalankan tugasnya setelah penghuninya mati. Disiplin, tidak terburu-buru.
- **Siluet:** tinggi 1.8 m, badan sangat kurus dengan rusuk terlihat, kepala kecil yang condong ke depan, lengan panjang sampai lutut dengan jari bercakar.
- **Mata:** dua mata kecil seperti titik, cahaya stabil tanpa kedip.
- **Suara:** dengung rendah yang berat. Langkah berat dengan irama rata.
- **Gerak:** berjalan teratur. Saat LookAround, kepalanya menoleh patah-patah per sudut.
- **Sifat: jarang istirahat.** Peluang berhenti di pos jaga (`idleChance`) ×0.5. Pemain mendapat lebih sedikit jendela aman, tetapi rutenya paling mudah ditebak karena iramanya rata.

### The Hare

- **Konsep:** makhluk yang hidup dari mendengar. Diam lama, lalu bergerak mendadak.
- **Siluet:** tinggi 1.65 m saat tegak, tetapi lebih sering membungkuk seperti hewan liar. Kaki belakang panjang, tangan mirip manusia dengan cakar, gigi terlihat, dan dua telinga panjang yang terlihat jelas saat disorot senter.
- **Mata:** dua mata besar dan bulat, posisinya tinggi di kepala.
- **Suara:** napas pendek yang cepat. Langkah ringan dan cepat.
- **Gerak:** tersentak lalu diam mendadak. Telinga berdiri saat ia mendengar suara.
- **Sifat: pendengaran tajam.** `hearingScale` ×1.25, jadi langkah pemain terdengar dari jarak lebih jauh. Kaleng tidak terpengaruh karena radius kaleng memang tidak memakai `hearingScale`.

### The Glutton

- **Konsep:** makhluk rakus yang selalu lapar dan selalu mengunyah sesuatu.
- **Siluet:** tinggi 1.8 m, massa daging yang berat, kepala kecil tenggelam di antara bahu, langkah lebar. Mulut aslinya vertikal di perut, bergigi, dan dalamnya menyala merah redup.
- **Mata:** dua mata kecil yang tertutup lipatan kulit, cahayanya paling redup.
- **Suara:** bunyi mengunyah dari perut dan perut bergemuruh yang terdengar dari jauh, ditambah napas basah. Pemain hampir selalu tahu di mana ia berada.
- **Gerak:** badan berayun ke kiri dan kanan saat berjalan.
- **Sifat: lambat mengejar.** `chaseSpeed` ×0.9. Ia monster paling ramah untuk pemain baru: mudah didengar dan bisa dilewati dengan lari.

### The Crawler

- **Konsep:** penghuni kolong, sudut gelap, dan tempat sembunyi.
- **Siluet:** tubuh mirip manusia yang merangkak seperti laba-laba: empat tumpuan, siku dan lutut terangkat lebih tinggi dari punggung, punggung berduri, kepala tergantung di bawah bahu.
- **Mata:** banyak mata kecil (empat sampai enam) yang tersusun tidak rata. Tidak ada monster lain yang punya mata lebih dari dua.
- **Suara:** kerikan kuku di lantai dan desis duri yang bergesek.
- **Gerak:** merayap rendah. Kepalanya menyapu ke kiri dan kanan dekat lantai.
- **Sifat: memeriksa tempat sembunyi lebih jauh.** Saat menyelidiki bukti, ia memeriksa tempat sembunyi dalam 2.0 m dari bukti. Monster lain hanya 1.1 m (`GuardBrain.CheckHiding`). Bersembunyi di dekat The Crawler lebih berisiko, dan wujudnya sudah memperingatkan hal itu.

### Ringkasan cara membedakan

| Monster | Mata dalam gelap | Suara khas | Sifat |
|---|---|---|---|
| The Warden | 2 titik kecil, stabil | Dengung berat, langkah berirama | `idleChance` ×0.5 |
| The Hare | 2 besar, tinggi | Napas cepat, langkah ringan | `hearingScale` ×1.25 |
| The Glutton | 2 kecil, redup, plus mulut perut merah redup | Mengunyah, perut bergemuruh | `chaseSpeed` ×0.9 |
| The Crawler | 4–6 kecil, tidak rata | Kerikan kuku, desis duri | Radius cek tempat sembunyi 2.0 m |

## 4. The Hunter

### Wujud: tanda di atas monster biasa

Desain RL menetapkan Hunter sebagai peran: satu `GuardBrain` di roster diberi flag `useHunterPolicy`, dan Hunter hanya muncul kalau D tinggi (usulan di sana: D ≥ 0.6). Karena itu Hunter tidak punya model sendiri. Monster yang terpilih tetap memakai siluet, gerak, dan suara dasarnya, lalu mendapat tanda Hunter di atasnya.

- **Mata putih dingin.** Hunter satu-satunya monster yang matanya tidak merah. Cahaya matanya berdenyut pelan selama hunt loop dan kembali stabil saat Patrol.
- **Lapisan suara klik.** Di atas suara dasar monster, Hunter mengeluarkan klik pelan yang berulang selama hunt loop, seperti sedang mengukur ruangan. Klik berhenti saat Patrol.
- **Tidak bisa tertukar.** `GuardEncounter.TryBuildPlan` tidak pernah memilih karakter yang sama dua kali dalam satu misi, jadi tanda Hunter selalu menempel pada monster yang unik.
- **Tanpa sifat karakter.** Mengikuti usulan di bagian 8, Hunter tidak memakai sifat dari bagian 3. Siluetnya tetap milik monster dasar, tetapi angka AI-nya mengikuti baseline supaya hasil training berlaku untuk karakter mana pun.
- **Perkenalan.** Saat Hunter pertama kali muncul di misi, briefing menampilkan satu baris peringatan dalam bahasa Inggris, misalnya "Something is hunting tonight." Pertama kali Hunter kehilangan jejak, ia mengeluarkan lolongan khasnya (lihat `InterceptExit`), supaya pemain mengenal suara itu.

### Tanda per aksi: jelas dan bisa dipelajari

Setiap aksi punya tanda suara dan gerak yang khas. Tanda berbunyi saat aksi dipilih, lalu bertahan selama aksi berjalan. Pemain yang belajar tanda ini bisa membaca dan melawan taktik Hunter, dan penonton demo bisa melihat keputusan RL tanpa membuka panel F1.

| Aksi | Suara | Gerak dan visual | Yang dipahami pemain | Jangkauan |
|---|---|---|---|---|
| `GoLastKnown` | Tarikan napas mengendus | Kepala rendah, berjalan lurus ke tujuan | "Dia menuju tempat terakhir aku terlihat" | Spasial, sama dengan geraman (13 m) |
| `SweepSector` | Ketukan kuku berirama | Kepala menyapu lebar di tiap titik, memakai clip LookAround | "Dia menyisir area sekitar" | Spasial, 13 m |
| `InterceptExit` | Satu lolongan naik saat aksi dipilih | Mata meredup selama perjalanan ke titik cegat | "Jalan keluar sedang dijaga" | Terdengar dari seluruh rumah, tetap berarah |
| `AmbushTarget` | Napas lambat dan berat | Mata padam, diam di dekat relik. Senter tetap memperlihatkan tubuhnya | "Ada yang menunggu di dekat relik" | Napas hanya terdengar dalam 4 m |
| `CheckHidingSpot` | Cakaran pada perabot | Menghadap tempat sembunyi selama 0.8 dtk pemeriksaan, memakai notifikasi "Guard is checking the hiding spot." yang sudah ada | "Dia memeriksa tempat sembunyi" | Spasial, 13 m |
| `GiveUp` | Desis kesal yang memudar | Mata kembali stabil, klik berhenti | "Dia berhenti memburu" | Spasial, 13 m |

Aturan keadilan:

- Tanda hanya memberi tahu apa yang dilakukan Hunter. Tanda tidak pernah memberi tahu pemain posisi Hunter yang tidak bisa ia dengar atau lihat, kecuali lolongan `InterceptExit` yang sengaja terdengar dari seluruh rumah karena aksinya memang mengancam seluruh rute keluar.
- `AmbushTarget` adalah satu-satunya aksi dengan mata padam. Ini paling menakutkan, jadi napasnya wajib terdengar dalam 4 m dan tubuhnya tetap terlihat oleh senter.
- Tanda tidak mengubah `GuardBrain.Sees` atau `Hear`. Mata padam hanya efek visual, dan penglihatan Hunter tetap sama.

## 5. Animasi per monster

### Batasan (dicek ke aset, 25 Nov 2026)

- Keempat model memakai rig Generic (`animationType: 2` di `.fbx.meta`). Clip humanoid dari Mixamo atau paket lain tidak bisa langsung dipakai.
- Clip Walk dan Run di-bake ulang oleh `LocomotionBuilder` supaya langkah kaki pas dengan kecepatan 1.4 dan 3.2 m/s. Clip baru harus lewat proses yang sama.
- Semua model punya tulang dengan nama yang sama: `root`, `pelvis`, `spine`, `chest`, `neck`, `head`, `face_mouth_open`, `face_eye.L/R`, plus lengan dan kaki. Model The Hare juga punya `ear.L/R` dan `ear_tip.L/R`.

### Pendekatan: prosedural di atas 5 clip yang ada

Konsep v2 (bagian 6) mengubah keputusan awal: bentuk tubuh baru dibuat di Blender (bagian 7), dan The Crawler butuh clip baru untuk merangkak. Lapisan prosedural tetap dipakai untuk kepala, telinga, dan mulut di atas clip tersebut.

Gaya gerak tiap monster dibuat oleh satu komponen presentasi yang memutar tulang di `LateUpdate`, setelah Animator selesai. Tidak ada clip baru dan tidak ada aset Blender.

- **Hanya tulang dari `spine` ke atas, telinga, dan mulut.** `pelvis` dan kaki tidak disentuh, jadi langkah kaki hasil `LocomotionBuilder` tetap pas.
- **Hanya membaca, tidak mengubah AI.** Komponen membaca `GuardBrain.State`, `Suspicion`, `MemorySource`, `LastEvidenceTime`, `Agent.velocity`, dan `ActorAnimation.CurrentState`. `GuardBrain` tidak berubah.
- **Tulang dicari dengan nama,** sama seperti `MonsterSkin.FindByName`. Kalau tulangnya tidak ada (misalnya `ear` di model selain The Hare), bagian itu dilewati.
- **State animasi yang dipakai** mengikuti `GuardBrain.Step`: Run saat Chase, LookAround saat diam di Search atau sedang istirahat, FaceActing saat diam dengan `Suspicion` > 0.1, Idle untuk sisanya, dan Walk saat bergerak.

### Gaya gerak tiap monster

| Monster | Postur dasar | Walk dan Run | LookAround | FaceActing (curiga) | Ciri tambahan |
|---|---|---|---|---|---|
| The Warden | Tegak, `spine` 0° | Kepala lurus tanpa ayunan. Saat Run, `chest` condong 10° | Kepala menoleh patah-patah ke tiga sudut (−50°, 0°, 50°). Tiap perpindahan 0.1 dtk, lalu diam 0.6 dtk | Kepala miring 12° ke arah bukti | Gerakan selalu berhenti tepat dan tidak goyah |
| The Hare | Sedikit condong ke depan, `spine` 8° | Kepala naik-turun kecil mengikuti langkah | Diam 0.8–1.5 dtk (acak), lalu kepala berputar cepat dalam 0.08 dtk | Telinga tegak penuh dan diam | Telinga berdiri 2 dtk setiap ada bukti suara baru, terbaca dari `MemorySource` dan `LastEvidenceTime` yang berubah |
| The Glutton | Agak bungkuk karena berat, `spine` 5° | Badan berayun kiri-kanan ±6° mengikuti langkah. Saat Run, mulut terbuka lebar | Ayunan berhenti, kepala menoleh pelan | Kunyahan berhenti | Mulut (`face_mouth_open`) mengunyah 2–3 kali per detik saat Idle dan Walk. Kunyahan yang berhenti memberi tahu pemain bahwa ia sedang curiga |
| The Crawler | Merangkak di empat tumpuan. Postur ini datang dari clip baru (bagian 7), tidak dari putaran tulang | Kepala yang tergantung berayun pelan ±20° | Kepala berputar miring sampai hampir terbalik sambil menyapu | Kepala diam dan menunduk ke arah bukti | Tidak ada gerak prosedural pada badan. Hanya kepala yang diputar |

### The Hunter

Selama hunt loop, gerak dari tanda aksi (bagian 4) menimpa gaya dasar monster. Gaya dasar kembali saat Patrol.

| Aksi | Gerak |
|---|---|
| `GoLastKnown` | `neck` menunduk 15°, kepala lurus ke tujuan |
| `SweepSector` | Sapuan kepala lebar ±70° di tiap titik, lebih lebar daripada LookAround monster mana pun |
| `InterceptExit` | Badan tegak, kepala lurus, tanpa ayunan |
| `AmbushTarget` | Semua gerak prosedural berhenti, termasuk kunyahan The Glutton dan telinga The Hare. Hanya napas lambat (dada naik-turun ±2°) |
| `CheckHidingSpot` | Kepala mendekat ke tempat sembunyi, `chest` condong 20° selama 0.8 dtk pemeriksaan |
| `GiveUp` | Kepala turun lalu naik pelan dalam 1 dtk |

### Risiko

- **Titik pandang AI The Crawler berubah.** `GuardEncounterBuilder` mengisi `eyeHeight` dari posisi tulang `face_eye` pada model. Model merangkak dengan kepala tergantung membuat `eyeHeight` jauh lebih rendah, jadi `GuardBrain.Sees` melihat dari dekat lantai. Ini perubahan perilaku, jadi dicatat di bagian 8.
- **Animasi bertabrakan dengan `Face` dan `transform.Rotate`.** `GuardBrain` memutar seluruh badan saat Search dan saat memeriksa tempat sembunyi. Komponen prosedural hanya memutar tulang leher dan kepala relatif terhadap badan, jadi keduanya tidak saling menimpa.
- **Belum dicek di game.** Angka sudut di tabel adalah titik awal. Cek dengan `HorrorPreviewCapture` di mesin yang punya Unity 6000.6.

## 6. Referensi visual (konsep v2)

Dikumpulkan dari Pinterest pada 25 Nov 2026. Referensi ini dipakai untuk bentuk dan suasana saja. Model akhir tidak boleh menyalin karya mana pun.

### Desain

| Monster | Pin | Yang diambil |
|---|---|---|
| The Warden | [makhluk kurus abu-abu](https://id.pinterest.com/pin/960744533030918290/), [turnaround depan-belakang](https://id.pinterest.com/pin/881790802082791765/) | Badan sangat kurus, lengan sampai lutut, jari bercakar. Turnaround-nya cocok sebagai acuan modeling |
| The Hare | [hare liar membungkuk](https://id.pinterest.com/pin/586101339045591680/), [makhluk bertopeng tengkorak](https://id.pinterest.com/pin/659214464183046599/) | Postur membungkuk, tangan bercakar, gigi terlihat, kaki belakang panjang |
| The Glutton | [daging menggumpal](https://id.pinterest.com/pin/782852347735080606/), [makhluk bermulut di badan](https://id.pinterest.com/pin/605100899934701727/) | Massa daging berat, kepala tenggelam, mulut vertikal di perut |
| The Crawler | [Cave Crawler](https://id.pinterest.com/pin/564849978279050698/), [sketsa merangkak](https://id.pinterest.com/pin/933230354042165571/) | Tubuh manusia yang merangkak seperti laba-laba, siku tinggi, kepala tergantung |
| Mata dalam gelap | [mata titik dan seringai samar](https://id.pinterest.com/pin/1095782153116995119/), [siluet tinggi bermata kecil](https://id.pinterest.com/pin/955537245971921168/) | Mata kecil seperti titik lebih seram daripada mata besar yang menyala |

### Gerakan

| Untuk | Pin | Kegunaan |
|---|---|---|
| The Crawler | [transisi tegak ke merangkak](https://id.pinterest.com/pin/362680576236291476/), [video makhluk merangkak](https://id.pinterest.com/pin/375346950210863340/) | Pose kunci walk cycle merangkak |
| The Warden, The Glutton | [zombie walk cycle](https://id.pinterest.com/pin/642325965607090742/) | Langkah kaku dan berat, pose per frame |
| Umum | [creature walk cycle](https://id.pinterest.com/pin/562175965981893965/), [siluet makhluk bayangan](https://id.pinterest.com/pin/746542075774674348/) | Gaya pose dan siluet |

## 7. Brief Blender

### Kondisi alat

- Blender 5.3.0 terpasang di Mac Henry lewat Homebrew (25 Nov 2026), dengan perintah `blender` di `/opt/homebrew/bin/blender`.
- Unity di Mac Henry hanya 6000.4.5f1, sedangkan proyek memakai 6000.6. Model bisa dibuat di Mac, tetapi pengecekan di Unity harus dilakukan di mesin yang punya 6000.6.

### Aturan wajib dari pipeline yang ada

Aturan ini diambil dari `GuardEncounterBuilder`, `LocomotionBuilder`, dan `MonsterSkin`. Model yang melanggarnya akan gagal saat menu "Sneak House/Set up randomized guard encounters" dijalankan.

| Aturan | Alasan di kode |
|---|---|
| Rig tetap Generic. Nama tulang lama dipertahankan: `root`, `pelvis`, `spine`, `chest`, `neck`, `head`, `upper_arm.L/R`, `hand.L/R`, `thigh.L/R`, `shin.L/R`, `foot.L/R`, `face_eye.L/R`, `face_mouth_open`. Tulang baru boleh ditambah | Builder mencari `head` dan tulang yang diawali `face_eye`. `LocomotionBuilder` mencari `thigh`, `shin`, dan `foot` kiri-kanan. Komponen prosedural (bagian 5) mencari tulang dengan nama |
| Lima clip di dalam FBX dengan nama persis `Idle`, `Walk`, `Run`, `LookAround`, `FaceActing`. Semua in-place tanpa root motion | Builder menolak model tanpa salah satu clip. `applyRootMotion` dimatikan |
| Walk dan Run harus lolos bake kaki dengan error maksimal 0.03 m. Acuannya Walk 0.65 dtk per siklus pada 1.4 m/s dan Run 0.44 dtk pada 3.2 m/s | `LocomotionBuilder.Bake` melempar error kalau lewat 0.03 |
| Mata, dan mulut perut The Glutton, memakai material yang namanya mengandung `FaceDark` | `MonsterSkin` membuat slot `FaceDark` menyala. Material lain diganti kulit bayangan |
| Mata ada di depan kepala | Builder memutar model berdasarkan arah `head` ke `face_eye` |
| Tapak tubuh maksimal 1.6 m di sumbu X dan Z | Builder menskala model supaya tinggi sesuai target dan lebar maksimal 1.6 m. Model yang lebih lebar akan mengecil |

Pengaturan ekspor FBX yang disarankan: Forward −Z, Up Y, Apply Scalings "FBX All", Add Leaf Bones mati, Bake Animation hidup, dan setiap action diekspor sebagai take dengan nama clip di atas.

### Lokasi file

- File kerja `.blend` disimpan di luar `Assets/`, misalnya `Art/Blender/`, supaya Unity tidak ikut mengimpornya.
- Hasil FBX disimpan sebagai file baru, misalnya `Assets/SneakHouse/Models/Monsters/warden.fbx`. Model lama tidak ditimpa. Builder perlu diarahkan ke path baru, dan itu perubahan kode untuk sesi utama.

### Tugas per monster

| Monster | Model dasar | Ubah di mesh | Ubah di clip | Tingkat sulit |
|---|---|---|---|---|
| The Warden | `Actors/01_bogo_guard` | Torso dikurus, rusuk terlihat, lengan diperpanjang sampai lutut, jari bercakar, kepala kecil condong ke depan | Walk lebih kaku dan berat (acuan zombie walk). Lengan menggantung lurus | Rendah, cocok sebagai pilot |
| The Hare | `PortraitCast/03_judy_guard` | Mulut bergigi, tangan bercakar, kaki belakang lebih panjang | Semua clip dengan `spine` membungkuk. Telinga tetap memakai tulang `ear` yang ada | Sedang |
| The Glutton | `Actors/02_clawhauser_guard` | Massa perut diperbesar, kepala diturunkan ke bahu, mulut vertikal di perut dengan material `FaceDark` di dalamnya. Tambah tulang `belly_mouth` untuk buka-tutup | Walk berayun berat. Mulut perut dianimasikan prosedural lewat `belly_mouth` | Sedang |
| The Crawler | `Actors/04_spike_guard` | Proporsi untuk merangkak, duri di punggung, lima mata kecil dengan material `FaceDark` | Kelima clip dibuat ulang dalam pose merangkak | Tinggi, kerjakan terakhir |
| The Hunter | Tidak ada model | Tidak ada | Tidak ada. Mata putih diatur `MonsterSkin` | Tidak perlu Blender |

### Urutan kerja

1. The Warden sebagai pilot: ubah mesh, ekspor, jalankan builder di Unity 6000.6, lalu cek dengan `HorrorPreviewCapture`.
2. The Hare dan The Glutton setelah pilot lolos.
3. The Crawler terakhir, setelah sesi utama memutuskan cara bake kakinya (bagian 8).

### Hasil di Blender (25 Nov 2026)

| File | Isi |
|---|---|
| `Art/Blender/scripts/warden_pilot.py`, `warden_sculpt.py` | Membentuk ulang model Bogo jadi The Warden v2: badan kurus, lengan sampai lutut, seragam dan wajah kartun dibuang, tanduk panjang, mata titik `FaceDark`, cakar. Hasil: `warden_v2.blend`, `export/warden.fbx` |
| `Art/Blender/scripts/build_sentry_bull.py` | Karakter Sentry Bull dari nol (metaball, voxel remesh, Skin, Curve), material Cycles procedural, armature 38 tulang dengan pose Day dan Night. Flag `--unity` mengekspor versi game: satu skinned mesh, 2 material, 5 clip procedural. Hasil: `sentry_bull.blend`, `export/sentry_bull.fbx` |
| `Art/Blender/renders/` | Render preview Warden dan Sentry Bull |

Tes di game dilakukan di salinan proyek dengan Unity 6000.4.5, di luar repo. Kedua FBX lolos `GuardEncounterBuilder` (clip ditemukan, bake kaki lolos). Probe Play mode mencatat Sentry Bull berjalan 4,25 m dalam 6 detik dan The Warden v2 4,91 m. Kedua model belum dipasang ke `Assets/` repo karena butuh perubahan builder (bagian 8).

## 8. Usulan untuk sesi utama (menyentuh perilaku)

Sifat di bagian 3 mengubah angka AI, jadi perlu disepakati sesi utama sebelum diimplementasikan:

- **Tempat menyimpan sifat.** Usulan: tambah field pengali di `GuardEncounter.Character` (misalnya `idleScale`, `hearingScale`, `chaseScale`, `hidingCheckRadius`), lalu `GuardEncounter.Apply` mengalikannya ke `GuardBrain` setelah baseline level dan DDA diterapkan.
- **Radius cek tempat sembunyi.** Angka 1.1 m sekarang tertulis langsung di `GuardBrain.CheckHiding`. Sifat The Crawler butuh angka itu jadi field.
- **Urutan pengali.** Baseline level, lalu DDA, lalu sifat karakter. Perlu dicek bahwa kombinasi DDA tinggi dengan sifat The Hare masih adil.
- **Dampak ke RL.** Kalau The Hunter memakai salah satu dari empat karakter ini, sifatnya ikut memengaruhi training. Usulan: The Hunter tidak memakai sifat karakter apa pun selama training dan evaluasi.

Usulan dari desain The Hunter (bagian 4):

- **Event aksi untuk presentasi.** Tanda per aksi butuh satu event saat `HunterDecision` memilih aksi, misalnya `HunterDecision.Chosen(HunterAction)`. `MonsterSkin` atau komponen presentasi baru cukup mendengarkan event itu. Event ini tidak mengubah aksi, observasi, atau reward.
- **Lama menunggu untuk `AmbushTarget` dan `InterceptExit`.** Di `rl-enemy-design.md` bagian 2, Hunter hanya diam 0.9 dtk setelah tiba, lalu memilih aksi berikutnya. Dengan aturan itu, penyergapan dan pencegatan selesai sebelum pemain sempat membaca tandanya. Usulan: kedua aksi ini menunggu lebih lama (misalnya sampai 6 dtk), dan tetap terpotong oleh bukti baru atau saat pemain terlihat. Ini mengubah perilaku dan jumlah keputusan per episode, jadi perlu diputuskan sesi utama.
- **Perbedaan bot dan manusia.** `ThiefBot` tidak bereaksi pada tanda Hunter, sedangkan pemain manusia akan bereaksi, misalnya menunda keluar setelah mendengar lolongan `InterceptExit`. Policy yang dilatih melawan bot bisa terlalu percaya diri pada aksi yang tandanya jelas. Usulan: evaluasi fairness dengan playtest manusia (`rl-enemy-design.md` bagian 8) mencatat juga catch rate per aksi, atau `ThiefBot` diberi reaksi sederhana pada lolongan di curriculum tahap 4.

Usulan dari animasi (bagian 5):

- **`eyeHeight` The Crawler.** Model merangkak otomatis menurunkan `eyeHeight` lewat builder, jadi The Crawler melihat dari dekat lantai dan bisa terhalang perabot rendah. Pilihannya: terima sebagai sifat tambahan, atau builder memakai `eyeHeight` tetap untuk The Crawler. Ini mengubah perilaku, jadi perlu diputuskan sesi utama.

Usulan dari brief Blender (bagian 7):

- **Path model baru di `GuardEncounterBuilder`.** Model hasil Blender disimpan sebagai file baru, jadi daftar `Character(...)` di builder perlu menunjuk path baru.
- **Bake kaki untuk The Crawler.** `LocomotionBuilder.Bake` hanya mengunci dua kaki (`thigh`, `shin`, `foot`). Pada model merangkak, tangan yang jadi kaki depan tidak ikut dikunci dan akan terlihat meluncur di lantai. Pilihannya: tambah kunci untuk `hand.L/R`, atau lewati bake untuk The Crawler dan pakai clip Blender apa adanya.

## 9. Belum dibahas

- Temuan screenshot `Tests/screenshots/horror-fps-monster.png`: kulit monster terlihat abu-abu mengilap saat disorot senter, dan matanya tidak terlihat menyala merah. Desain di bagian 2 dan 3 bergantung pada mata yang terlihat dalam gelap. Penyebabnya belum dicek: screenshot lama atau glow yang kalah oleh senter.
- Tanda visual dan audio sebelum menyerang (bergantung pada mekanik Attack di `eas-adaptive.md` bagian 5).
- Nasib nama "The Hollow Man" yang tidak dipakai roster.
