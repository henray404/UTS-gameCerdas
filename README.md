# Hollow House (Sneak House)

Hollow House adalah game **horor stealth first-person** dengan visual low-poly. Pada malam hari pemain menyusup ke rumah terbengkalai yang dihuni makhluk bayangan, mengambil relik terkutuk sesuai level, lalu kembali ke pintu masuk tanpa tertangkap. Seluruh misi dimainkan dari mata pemain dengan senter sebagai sumber cahaya utama. AI penjaga (FSM Patrol, Investigate, Chase, Search, Return) tidak berubah; hanya wujud, suara, dan suasana yang diganti.

## Tujuan permainan

Kumpulkan barang yang ditandai dalam briefing. Level awal membutuhkan konsol saja, kemudian konsol dan drive, lalu ketiga barang termasuk jam tangan. Setelah semua target wajib terkumpul, kembali ke lingkaran hijau di pintu masuk untuk menyelesaikan misi. Pemain kalah jika tertangkap penjaga.

Tersedia enam level dengan empat bentuk rumah: rumah kolektor, vila berbentuk L, estate berbentuk U, serta Loft Villa dua lantai dengan tangga courtyard. Setiap misi memiliki satu titik keluar.

## Level dan progres

Pilih **Select Level** pada menu utama. Enam kartu level berjajar dari kiri ke kanan, masing-masing menampilkan gambar map asli dan jumlah penjaga. Ikon segitiga menandai level terbuka, gembok menandai level terkunci, dan centang menandai level selesai. Klik kartu atau gunakan panah kiri/kanan untuk melihat detail. Profil baru hanya membuka level 1. Menang level sebelumnya membuka level berikutnya. Progres dan rekor tiap level disimpan lokal, sedangkan level selesai tetap bisa dimainkan ulang. [Lihat tampilan menu](Tests/screenshots/level-cards.png).

| Level | Map | Penjaga | Target wajib | Kaleng |
|---:|---|---:|---|---:|
| 1 — First Steps | Collector's House | 1 | Konsol | 4 |
| 2 — Office Job | Collector's House | 2 | Konsol, drive | 4 |
| 3 — Full Collection | Collector's House | 3 | Konsol, drive, jam | 4 |
| 4 — Library Detour | Library Villa | 3 | Konsol, drive, jam | 3 |
| 5 — Across the Courtyard | Courtyard Estate | 4 | Konsol, drive, jam | 3 |
| 6 — The Upstairs Job | Loft Villa | 4 | Konsol, drive, jam | 2 |

Level awal memakai penjaga dengan jarak pandang pendek, sudut sempit, penyadaran lambat, dan pendengaran lemah. Di level 1, pemain yang terlihat dari jarak 3 m masih punya sekitar 4 detik (sekitar 6,5 detik saat mengendap) sebelum ketahuan. Semakin tinggi level, jarak pandang, sudut pandang, kecepatan penyadaran, ketepatan pendengaran, jangkauan laporan antarpenjaga, kecepatan pengejaran, durasi pencarian, dan jumlah penjaga meningkat bertahap, sedangkan waktu istirahat penjaga makin singkat. Pemain juga lebih sulit kehabisan stamina saat berlari.

## Alur permainan

Menu utama (Continue / Select Level / Leaderboard) → pilih level terbuka → briefing misi → menyusup dan mengambil target → keluar dari rumah → hasil dan skor → level berikutnya atau replay.

Pemain dapat menjeda permainan, mengulang misi, atau kembali ke menu utama. Mengulang misi mempertahankan rumah dan posisi target, mengembalikan perlengkapan, lalu mengacak susunan penjaga serta patroli.

Setiap level menentukan bentuk rumah dan seed interior tetap. Mengulang misi mempertahankan keduanya. Ruang tamu, kantor, dan koleksi masing-masing memiliki tiga tata letak, sehingga katalog mendukung 27 kombinasi interior per bentuk rumah. Pada vila, kantor berpindah ke sayap perpustakaan; pada estate, ruang koleksi juga berpindah ke sayap galeri. Loft Villa menempatkan kantor di lantai atas. Tangga dapat dipakai pemain dan penjaga, sedangkan tampilan atas membuka lantai yang sedang ditempati pemain secara otomatis. Courtyard memberi jalur alternatif melalui pintu samping. Target menempati meja atau display yang sesuai; tempat bersembunyi di kolong meja mengikuti posisi meja kantor. Jalur menuju target, tempat persembunyian, dan pintu keluar diperiksa sebelum misi dimulai. Interior bermasalah dicoba ulang pada map yang sama. Jika validasi tetap gagal, misi tidak dapat dimulai.

## Mekanik utama

- **Penanda target:** setiap barang wajib yang belum diambil memiliki garis luar emas, cincin berdenyut, lampu sorot kecil, dan penanda berlian yang melayang. Label nama serta jarak juga tampil di layar, termasuk panah tepi layar saat barang berada di luar pandangan.
- **Mengendap:** bergerak lebih pelan tanpa mengeluarkan suara langkah. Berlari menghasilkan suara lebih besar dan dapat menarik perhatian penjaga.
- **Distraksi:** gunakan persediaan kaleng sesuai level untuk memancing penjaga memeriksa lokasi bunyi benturan. Kaleng terpisah dari barang target misi.
- **Bersembunyi:** manfaatkan semak, tenda, atau kolong meja. Saat bersembunyi, pemain tidak dapat bergerak, mengambil target, atau menjatuhkan kaleng.
- **Risiko ketahuan:** penjaga yang melihat pemain masuk ke tempat persembunyian tetap mengingat lokasinya dan dapat memeriksanya. Suara di dekat tempat persembunyian juga dapat memicu pemeriksaan.
- **First person penuh:** saat misi dimulai, kamera turun dari tampilan atas ke mata pemain dan tetap first person sampai misi selesai. WASD mengikuti arah pandang dan mouse dipakai untuk menoleh. Saat bersembunyi, kamera mengintip dari dalam tempat persembunyian dan masih bisa menoleh. Pause melepas kursor. Kamera atas hanya dipakai di menu.
- **Senter:** tombol T menyalakan atau mematikan senter. Senter berkedip sesekali, dan bergetar parah saat monster mengejar dari dekat.
- **Tekanan horor:** detak jantung, napas, dengung rendah, dan langkah monster menambah ketegangan. Tekanan naik pelan saat monster mulai curiga, memuncak saat dikejar, dan juga naik saat monster lewat di dekat tempat persembunyian. Sudut layar menggelap, kamera bergetar ringan, dan suara malam meredup. Efek mereda saat lolos dan berhenti ketika pause atau misi selesai.
- **Gangguan acak:** setiap 16 sampai 38 detik terdengar suara tanpa sumber jelas (pintu berderit, ketukan, bisikan, jeritan jauh) dari arah acak di sekitar pemain, kadang disertai lampu berkedip. Gangguan tidak muncul saat sedang dikejar.
- **Jumpscare:** tertangkap memicu jeritan, kilatan merah, dan sepasang mata menyala sebelum layar hasil.

## Perilaku penjaga

Penjaga berpatroli, mengamati area pandang, dan mendengarkan suara. Dinding menghalangi pandangan serta meredam suara. Ketika menemukan petunjuk, penjaga memeriksa sumbernya; jika pemain terlihat, penjaga mengejar.

Setiap misi memilih satu hingga empat makhluk berbeda: The Warden, The Hare, The Glutton, dan The Crawler. Semua tampil sebagai siluet hitam basah dengan mata merah menyala dan geraman yang terdengar dari dekat. Kerucut pandang di lantai disembunyikan agar monster tetap menakutkan; F1 menampilkannya lagi untuk penjelasan AI. Jumlah penjaga mengikuti level. Luas interior bebas berjalan tetap diukur untuk informasi debug. Titik awal, arah rute, dan jeda patroli bervariasi. Zona patroli mengikuti bentuk rumah: ruang tamu, kantor/perpustakaan, ruang koleksi/galeri, serta penghubung pada estate. Ketika satu penjaga mendeteksi pemain, penjaga lain menerima laporan dan mencari di titik berbeda sekitar posisi terakhir yang diketahui. Laporan tidak memberi posisi pemain yang sudah berpindah tanpa terlihat.

Setiap penjaga punya rute keliling yang menjaga jarak dari titik ambil target (3,5 m di level 1, 3 m di level 2–3, 2,5 m di level 4–6). Setiap dua atau tiga putaran, penjaga sekali melewati titik intip di dekat target lalu kembali ke rutenya, sehingga ada jendela aman untuk mengambil barang. Di pos jaga pada rute keliling, penjaga kadang berhenti dan melihat sekeliling. Selama istirahat, kerucut pandangnya lebih pendek dan sempit, dan penyadarannya lebih lambat. Penjaga tidak beristirahat di titik intip. Lama dan seringnya istirahat berkurang di level yang lebih tinggi.

Bunyi kaleng selalu terdengar jelas dan tepat. Langkah kaki terdengar lebih dekat di level awal dan lokasinya hanya diperkirakan, sehingga penjaga memeriksa titik di sekitar pemain, bukan posisi persisnya. Di level awal, laporan penjaga hanya menjangkau rekan di dekatnya.

Saat kehilangan jejak, penjaga menuju lokasi bukti lalu mencari sebelum kembali ke zona patrolinya. Waktu perjalanan mengikuti panjang jalur, termasuk naik-turun tangga; waktu pencarian dimulai setelah penjaga tiba. Dinding dan lantai menghalangi pandangan antar lantai. Bersembunyi tidak langsung menghapus ingatan penjaga.

## Kontrol

| Tombol | Aksi |
| --- | --- |
| WASD / tombol arah | Bergerak |
| Mouse | Menoleh |
| Shift | Berlari |
| Ctrl / C | Mengendap |
| E | Mengambil target terdekat |
| Q | Menjatuhkan kaleng distraksi |
| F | Masuk atau keluar tempat persembunyian |
| T | Menyalakan / mematikan senter |
| Esc | Jeda / lanjut |
| R | Mengulang misi |
| F1 | Menampilkan informasi perilaku penjaga |

## Skor, leaderboard, dan speedrun

Kemenangan memberikan 6000 poin objektif, bonus stealth hingga 2500 (berkurang 500 per laporan penjaga), dan bonus waktu hingga 1500. Bonus waktu menurun linear sampai waktu acuan level; melewati waktu acuan menghilangkan bonus waktu tetapi tidak menggagalkan misi. Skor maksimum 10.000. Hanya kemenangan yang masuk peringkat.

Selain skor, setiap kemenangan mencatat **waktu tempuh** seperti speedrun: semakin cepat semakin baik. Layar hasil menampilkan rincian Objective / Stealth / Time, skor terbaik, waktu tercepat, serta penanda **NEW BEST TIME** saat rekor waktu pecah.

Menu utama kini menampilkan papan **Best Records** (skor terbaik dan waktu tercepat tiap level, total skor, dan total speedrun). Tombol **Leaderboard** membuka **Hall of Records** dengan tab **SCORE** dan **SPEEDRUN**, enam level, serta lima catatan teratas per level (skor, waktu, jumlah alert, tanggal). Seri skor dipecahkan oleh waktu lebih cepat; seri waktu dipecahkan oleh skor lebih tinggi. Total speedrun hanya menjumlahkan level yang sudah punya waktu, dengan keterangan jumlah level yang tercatat.

Semua rekor disimpan **lokal** di perangkat (`PlayerPrefs`, kunci `SneakHouse.Scores.v2`) untuk profil lokal. Tidak ada server online maupun perlindungan anti-cheat. Rekor lama per level (`SneakHouse.Score.Campaign.v1.Level.N`) dimigrasikan otomatis bila lebih tinggi; rekor lama tidak memiliki waktu sehingga hanya tampil di papan skor sebagai `imported`.

## Suasana

Permainan berlangsung pada malam hari: cahaya bulan biru redup, kabut tebal yang menipis saat kamera di menu, dan lampu rumah yang berdengung serta berkedip. Dinding dan lantai memakai tekstur plester kotor dan kayu tua dengan noda lembap dan jelaga dekat lantai (shader triplanar `SneakHouse/HorrorTriplanar`). Taman masih bergerak, tetapi kupu-kupu hilang dan suara burung diganti suara malam. Antarmuka berbahasa Inggris dengan palet kertas tua dan merah darah, judul memakai font Creepster, teks tetap tipografi pixel.

Semua aset unduhan bersifat gratis (CC0, CC BY, atau OFL). Sumber, pembuat, dan lisensi tercatat di `Assets/StreamingAssets/Audio-Credits.txt`.

Untuk mengambil cuplikan first person tanpa membuka editor: `Unity -batchmode -projectPath . -executeMethod SneakHouse.EditorTools.HorrorPreviewCapture.RunBatch` menyimpan gambar ke `Tests/screenshots/horror-*.png`.
