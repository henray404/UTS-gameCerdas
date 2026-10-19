# UTS-gameCerdas

Sneak House: prototype Stealth Infiltration untuk tugas Game Cerdas.

Unity **6000.2.10f1**, C#, built-in render pipeline. Folder project lengkap: Assets, Packages, ProjectSettings. Scene: Assets/SneakHouse/Scenes/PrototypeHouse.unity.

## Buka

Unity Hub → Projects → Add → Add project from disk → pilih folder ini. Pilih editor 6000.2.10f1. Buka scene PrototypeHouse, lalu tekan Play. Menu utama muncul; pilih Start Mission → Enter House. Open-Unity.cmd juga membuka project dan scene ini.

Diverifikasi 18 Oktober 2026 di Unity 6000.2.10f1: 43 pemeriksaan Play Mode lolos, termasuk alur menu, briefing, pause, hasil, skor, retry, serta pemeriksaan gameplay dan AI sebelumnya. Laporan terbaru: Documentation/Verification/unity-prototype-validation.json. Laporan 14 Oktober tetap disimpan sebagai baseline. Rekor lokal juga diperiksa melalui MCP sampai scene reload; nilai sebelum uji dipulihkan. Uji otomatis tidak membuktikan balancing atau durasi permainan.

## Tahap 1: alur misi

Menu utama → briefing → gameplay → hasil → siapkan ulang misi atau menu utama. Menu dan briefing menghentikan simulasi. Retry memuat ulang target, timer, dan status misi, kemudian kembali ke briefing.

Skor kemenangan: objektif 6.000, stealth maksimal 2.500 (berkurang 500 per laporan penjaga), bonus waktu maksimal 1.500 yang menurun hingga nol pada Target Time. Default Target Time 240 detik, bisa diubah pada GameSession di objek MISSION. Melewati waktu tersebut tidak menggagalkan misi. Rekor tersimpan melalui PlayerPrefs per scene; kegagalan tidak mendapat skor. Bobot awal ini belum dibalancing.

UI dibuat oleh MissionFlowUI saat runtime. Versi terbaru memakai panel tengah, teks English, material kayu low-poly, dan font pixel. Menu tanpa potret karakter; ornamen berupa kaca pembesar, berkas misi, jejak kaki, dan kunci, digambar sebagai bentuk bersegi yang disimpan dalam cache. Preview menu: Documentation/Verification/stealth-elements-menu.png; briefing: Documentation/Verification/pixel-final-briefing.png. Tahap berikutnya: tiga NPC aktif dengan peran berbeda, Shared Alert terkoordinasi, dan random ulang yang valid. Tahap ini masih memakai satu penjaga aktif dan susunan yang sama saat retry.

## Scope yang dipilih

Top-down miring, WASD, satu rumah kecil, satu penjaga aktif, tiga target, satu extraction point. Target: drive rahasia, jam tangan, konsol game. Bogo menjadi penjaga; model Stan V5 menjadi stand-in player dengan warna pakaian berbeda. Stand-in ini bisa diganti tanpa mengganti controller.

WASD/arrow: bergerak. Shift: lari dan suara lebih besar. Ctrl/C: mengendap. E: mengambil target di dekat pemain, dengan dinding sebagai penghalang interaksi. Q: jatuhkan kaleng distraksi. F: masuk/keluar hidden spot. F1: debug. R: ulang. Esc: jeda.

## Hiding spots — 19 Oktober 2026

Tiga spot: semak di halaman barat, tenda dekat teras selatan (akses sisi timur), dan bawah meja kantor. Dekati titik akses sampai prompt [F] muncul. F menyembunyikan player; F lagi mengembalikan ke posisi masuk yang aman. Gerak, pickup dan drop kaleng diblokir ketika hidden. Pause juga memblokir masuk/keluar. UI menunjukkan Hidden atau Compromised.

Hiding tidak menghapus memori AI. Guard yang melihat saat player masuk mencatat posisi akses dengan source Saw hiding entry, menuju titik itu, lalu memeriksa sekitar 0.8 detik sebelum menangkap jika player masih bersembunyi. Investigate/Search dengan bukti suara dekat titik akses juga bisa memeriksa spot yang terisi; patroli biasa tidak otomatis menemukan player hidden. Keluar sebelum diperiksa memungkinkan melarikan diri, tetapi memori penjaga tetap ada.

Hierarchy: HOUSE / Navigation & occlusion geometry → HIDING / Spots. Pada tiap HidingSpot tersedia Hidden anchor, Approach and inspection, serta Interaction Radius. PlayerHiding berada pada PLAYER. Posisi akses terpisah dari anchor di dalam prop supaya NavMeshAgent tidak diminta berjalan ke dalam collider meja/semak/tenda. Source model di Tools/Art/hiding-spots.blend; menu Sneak House / Set up hiding spots memperbarui grup hiding yang dikelola builder. Interior dan kamera tetap dipertahankan.

37 pemeriksaan hiding lulus, termasuk akses NavMesh, enter/exit, renderer/controller, larangan aksi saat hidden, witness memory, inspeksi, dan reset saat retry. Regresi 25 pemeriksaan gerak/kaleng serta 43 gameplay lama juga lulus. Laporan: Documentation/Verification/hiding-validation.json. Source FBX tambahan hiding_bush dan hiding_tent. Preview enter/exit terskrip: Documentation/Verification/hiding-preview.gif; penjaga dibuat menunggu untuk montage, sedangkan witness/search diuji terpisah.

## Sinkronisasi langkah dan distraksi — 19 Oktober 2026

ActorAnimation.SetLocomotion memakai perpindahan aktual CharacterController dan kecepatan NavMeshAgent. Kecepatan playback dibagi reference speed clip, bukan multiplier konstan dari tombol. Walk/Run baru disimpan sebagai .anim di Assets/SneakHouse/Animations; FBX asli tetap tersedia. LocomotionBuilder membake ulang pose kaki dengan two-bone IK, panjang fase kontak yang sesuai kecepatan referensi, serta gaya tubuh atas dari clip asli. Menu Sneak House / Calibrate locomotion dapat membuat ulang clip dari sumber asli. Pengukuran foot contact dan batas reach tersimpan di Documentation/Verification/gait-measurement.json dan locomotion-calibration.txt. Sinkronisasi bersifat gait cartoon; perpindahan gerak dan animasi sekarang memakai skala jarak yang sama.

Player mendapat tiga kaleng terpisah dari target misi. Q menjatuhkan/melempar pendek ke arah hadap, cooldown 0.7 detik. Rigidbody dan collider menentukan titik benturan; bunyi logam, pulse visual, serta event hearing dipancarkan sekali pada benturan pertama yang cukup kuat. Guard dalam radius mendengar akan Investigate titik benturan lalu Search; vision tetap punya prioritas ketika player terlihat. Hearing tetap teredam dinding seperti sistem sebelumnya. Kaleng tidak mengurangi target misi. Menu, briefing, pause, hasil, inventory kosong, dan posisi terlalu dekat dinding memblokir aksi. Retry mengisi tiga kaleng dan membersihkan prop lama.

Parameter: PLAYER → PlayerDistraction (Starting Cans, Cooldown, Hearing Radius). Model/prefab: Resources/SneakHouseGameplay/DistractionCan.prefab; source Blender: Tools/Art/distraction-can.blend. Suara can-impact.wav disintesis orisinal. Kaleng dibersihkan setelah 25 detik waktu gameplay. Animasi saat tertahan dinding kembali Idle, dan input gerak tanpa perpindahan tidak membuat suara langkah.

25 pemeriksaan fisika, stok, state AI, dan gerak aktual lulus; laporan: Documentation/Verification/movement-distraction-validation.json. Preview input terskrip: movement-drop-preview.gif; pemindahan karakter memakai controller yang sama dengan keyboard, guard menunggu selama segmen perbandingan gerak lalu aktif untuk segmen suara.

Menang: tiga target diambil, lalu kembali ke lingkaran hijau di pintu masuk. Kalah: bersentuhan dekat dengan penjaga tanpa dinding di antaranya.

## Exterior hidup — 19 Oktober 2026

Taman diterapkan langsung pada scene PrototypeHouse: jalur batu, pagar rendah dan gerbang, kotak surat, pepohonan/bunga/semak, sudut servis, teras duduk, tempat air burung, serta trotoar di luar batas permainan. 19 aset modular dibuat lewat Blender MCP; source editable di Tools/Art/exterior-kit-v1.blend dan garden-activity-v2.blend, FBX di Assets/SneakHouse/Models/Exterior. Script pembuat disertakan di Tools/Art. Tidak menggunakan gambar konsep sebagai background game.

Hierarchy: HOUSE / Navigation & occlusion geometry → EXTERIOR / Living garden. GardenAmbience mengatur 61 kelompok gerak, empat daun jatuh, dua riak air, dan tiga WAV ambience sintetis orisinal. Ambient Motion dan Ambience Volume bisa diubah di Inspector pada grup tersebut. Menu/briefing tetap beranimasi; pause gameplay menghentikan gerak dan mematikan volume ambience. Suara ambience tidak dikirim ke sensor hearing AI. Semak dekoratif belum merupakan fitur hiding.

Revisi gerak v2 menambah kincir taman berputar, sprinkler menyapu lima pancaran air dengan 15 tetes bergerak, serta dua kupu-kupu dengan jalur terbang dan kepakan sayap. Amplitudo kanopi dan kain ditingkatkan agar terbaca dari kamera jauh. GardenActivity berada di child ACTIVITY / Moving garden props, memakai clock GardenAmbience supaya pause dan reduced motion konsisten. Pinwheel Speed tersedia di Inspector. Menu Sneak House / Enhance garden motion memperbarui grup activity tanpa membangun ulang seluruh taman. Preview environment-only direkam saat misi dan karakter diam.

Menu Sneak House / Apply garden exterior memperbarui grup exterior yang dikelola builder; pertahankan perubahan manual pada grup terpisah jika memakai perintah ini lagi. Interior, target, posisi kamera manual, orthographic size, dan batas collider halaman dipertahankan. Penghalang exterior baru memakai collider diam; daun dan kanopi bergerak tanpa collider bergerak. NavMesh memakai geometri collider aktual.

Validasi gerak v2: 30 pemeriksaan exterior lulus; laporan di Documentation/Verification/garden-validation.json. Laporan gameplay terpisah di unity-prototype-validation.json. Apply garden exterior dijalankan dua kali untuk memastikan grup tetap satu. Preview v2: Documentation/Verification/garden-motion-v2-gameplay.png dan garden-motion-v2.gif; preview v1 tetap disimpan. Belum ada klaim FPS/build standalone.

## AI yang disiapkan

Patrol, Investigate, Chase, Search, Return. Vision memakai jarak, FOV, dan raycast. Hearing menerima stimulus suara beserta posisi dan radius. Memori menyimpan posisi bukti terakhir, waktu, serta sumber. Chase memperkirakan pergerakan hanya ketika player terlihat; Search memakai posisi memori saat player menghilang.

NavMesh dibangun dari collider architecture dan furnitur saat scene dimulai. Collider pemain/NPC serta marker visual dikecualikan. Dinding cutaway memakai collider penuh agar line of sight konsisten. Debug menampilkan state, sensor, memori, dan rute.

MISSION memiliki Active Guard Count, default 1. Array berisi tiga NPC dengan dua NPC nonaktif untuk prototype ini. Nilai 3 mengaktifkan dua NPC tambahan saat mulai; Shared Alert meneruskan laporan posisi terakhir dengan timestamp. Mode tiga NPC dan Shared Alert perlu diuji tersendiri.

## Sesuai tugas dan pekerjaan tersisa

Slide 8 mensyaratkan minimal 3 NPC aktif. Default prototype satu NPC mengikuti scope hari ini, sehingga **belum memenuhi jumlah NPC tugas akhir**. Durasi target tugas 5–10 menit belum dibuktikan. Build standalone, balancing, animasi player final, dan video demo belum menjadi hasil yang telah divalidasi.

## MCP

Package MCP for Unity disertakan di Packages/com.coplaydev.unity-mcp. Source revision aa5fc638d623f56178d50329d9ad8c541a57fe66. Endpoint yang dikonfigurasi: http://127.0.0.1:8080/mcp.

Server Python berjalan terpisah. Source disertakan di Tools/MCPServer; Start-MCP.cmd menjalankannya melalui uv. Jangan menjalankan server kedua jika port 8080 sudah dipakai server MCP yang sama. Setelah editor aktif, pilih Sneak House → Connect Unity MCP. Status “connected” harus diperiksa; package dan konfigurasi saja belum membuktikan koneksi editor. Koneksi editor telah diverifikasi melalui resource MCP instances dan perintah menu pengujian gameplay pada 14 Oktober 2026.

Dokumentasi resmi: https://github.com/CoplayDev/unity-mcp

## Mengubah prototype

- GameSession: jumlah penjaga, target, HUD, kondisi misi.
- PlayerMotor: kecepatan, suara, jarak pickup.
- GuardBrain: radius vision/FOV, suspicion, state, memori, durasi pencarian.
- RuntimeNavigation: radius dan voxel bake.
- PrototypeBuilder: layout dan generasi scene. Menu Build prototype scene menulis ulang scene prototype; simpan perubahan manual di scene lain sebelum regenerasi.
- Menu Sneak House / Verify prototype gameplay (PrototypeVerification.Run): pemeriksaan Play Mode untuk navigation, vision, memory, hearing, pickup, win, lose. Memerlukan editor dengan lisensi aktif.

Aset FBX berasal dari hasil kerja Sneak House sebelumnya. Furniture adaptasi Kenney CC0; lisensi di Assets/SneakHouse/Licenses. Model kartun merupakan adaptasi proyek. Font UI aktif: Press Start 2P untuk judul dan Pixelify Sans untuk teks/tombol, dengan impor HintedRaster dan filter Point. Lisensi OFL disertakan; font lama tetap tersedia.
