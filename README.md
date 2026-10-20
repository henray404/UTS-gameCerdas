# Sneak House

Sneak House adalah game **stealth infiltration** dengan sudut pandang top-down miring dan visual low-poly. Pemain menyusup ke sebuah rumah, mengambil barang wajib sesuai level, lalu kembali ke titik keluar tanpa tertangkap penjaga.

## Tujuan permainan

Kumpulkan barang yang ditandai dalam briefing. Level awal membutuhkan konsol saja, kemudian konsol dan drive, lalu ketiga barang termasuk jam tangan. Setelah semua target wajib terkumpul, kembali ke lingkaran hijau di pintu masuk untuk menyelesaikan misi. Pemain kalah jika tertangkap penjaga.

Tersedia enam level dengan empat bentuk rumah: rumah kolektor, vila berbentuk L, estate berbentuk U, serta Loft Villa dua lantai dengan tangga courtyard. Setiap misi memiliki satu titik keluar.

## Level dan progres

Pilih **Select Level** pada menu utama. Papan misi menampilkan status **Available**, **Locked**, dan **Complete**. Profil baru hanya membuka level 1. Menang level sebelumnya membuka level berikutnya. Progres dan rekor tiap level disimpan lokal, sedangkan level selesai tetap bisa dimainkan ulang.

| Level | Map | Penjaga | Target wajib | Kaleng |
|---:|---|---:|---|---:|
| 1 — First Steps | Collector's House | 1 | Konsol | 4 |
| 2 — Office Job | Collector's House | 2 | Konsol, drive | 3 |
| 3 — Full Collection | Collector's House | 3 | Konsol, drive, jam | 3 |
| 4 — Library Detour | Library Villa | 3 | Konsol, drive, jam | 3 |
| 5 — Across the Courtyard | Courtyard Estate | 4 | Konsol, drive, jam | 2 |
| 6 — The Upstairs Job | Loft Villa | 4 | Konsol, drive, jam | 1 |

Jarak dan sudut pandang serta durasi pencarian penjaga meningkat bertahap. Kecepatan gerak penjaga tetap. 

## Alur permainan

Menu utama → pilih level terbuka → briefing misi → menyusup dan mengambil target → keluar dari rumah → hasil dan skor → level berikutnya atau replay.

Pemain dapat menjeda permainan, mengulang misi, atau kembali ke menu utama. Mengulang misi mempertahankan rumah dan posisi target, mengembalikan perlengkapan, lalu mengacak susunan penjaga serta patroli.

Setiap level menentukan bentuk rumah dan seed interior tetap. Mengulang misi mempertahankan keduanya. Ruang tamu, kantor, dan koleksi masing-masing memiliki tiga tata letak, sehingga katalog mendukung 27 kombinasi interior per bentuk rumah. Pada vila, kantor berpindah ke sayap perpustakaan; pada estate, ruang koleksi juga berpindah ke sayap galeri. Loft Villa menempatkan kantor di lantai atas. Tangga dapat dipakai pemain dan penjaga, sedangkan tampilan atas membuka lantai yang sedang ditempati pemain secara otomatis. Courtyard memberi jalur alternatif melalui pintu samping. Target menempati meja atau display yang sesuai; tempat bersembunyi di kolong meja mengikuti posisi meja kantor. Jalur menuju target, tempat persembunyian, dan pintu keluar diperiksa sebelum misi dimulai. Interior bermasalah dicoba ulang pada map yang sama. Jika validasi tetap gagal, misi tidak dapat dimulai.

## Mekanik utama

- **Mengendap:** bergerak lebih pelan tanpa mengeluarkan suara langkah. Berlari menghasilkan suara lebih besar dan dapat menarik perhatian penjaga.
- **Distraksi:** gunakan persediaan kaleng sesuai level untuk memancing penjaga memeriksa lokasi bunyi benturan. Kaleng terpisah dari barang target misi.
- **Bersembunyi:** manfaatkan semak, tenda, atau kolong meja. Saat bersembunyi, pemain tidak dapat bergerak, mengambil target, atau menjatuhkan kaleng.
- **Risiko ketahuan:** penjaga yang melihat pemain masuk ke tempat persembunyian tetap mengingat lokasinya dan dapat memeriksanya. Suara di dekat tempat persembunyian juga dapat memicu pemeriksaan.
- **Kamera pengejaran:** saat dikejar, kamera turun secara halus dari top-down ke first person. WASD mengikuti arah pandang dan mouse digunakan untuk menoleh. Setelah pengejaran berhenti selama beberapa detik atau pemain bersembunyi, kamera kembali ke tampilan atas. Pause melepas kursor.
- **Tekanan pengejaran:** detak jantung, napas, dengung rendah, dan langkah penjaga menambah ketegangan. Semakin dekat pengejar, semakin kuat tekanan suara dan visual. Sudut layar menggelap, kamera bergetar ringan, dan suara taman meredup. Efek mereda saat lolos dan berhenti ketika pause atau misi selesai.

## Perilaku penjaga

Penjaga berpatroli, mengamati area pandang, dan mendengarkan suara. Dinding menghalangi pandangan serta meredam suara. Ketika menemukan petunjuk, penjaga memeriksa sumbernya; jika pemain terlihat, penjaga mengejar.

Setiap misi memilih satu hingga empat karakter berbeda dari Bogo, Judy, Clawhauser, dan Spike. Jumlah penjaga mengikuti level. Luas interior bebas berjalan tetap diukur untuk informasi debug. Titik awal, arah rute, dan jeda patroli bervariasi. Zona patroli mengikuti bentuk rumah: ruang tamu, kantor/perpustakaan, ruang koleksi/galeri, serta penghubung pada estate. Ketika satu penjaga mendeteksi pemain, penjaga lain menerima laporan dan mencari di titik berbeda sekitar posisi terakhir yang diketahui. Laporan tidak memberi posisi pemain yang sudah berpindah tanpa terlihat.

Saat kehilangan jejak, penjaga menuju lokasi bukti lalu mencari sebelum kembali ke zona patrolinya. Waktu perjalanan mengikuti panjang jalur, termasuk naik-turun tangga; waktu pencarian dimulai setelah penjaga tiba. Dinding dan lantai menghalangi pandangan antar lantai. Bersembunyi tidak langsung menghapus ingatan penjaga.

## Kontrol

| Tombol | Aksi |
| --- | --- |
| WASD / tombol arah | Bergerak |
| Mouse | Menoleh saat first person |
| Shift | Berlari |
| Ctrl / C | Mengendap |
| E | Mengambil target terdekat |
| Q | Menjatuhkan kaleng distraksi |
| F | Masuk atau keluar tempat persembunyian |
| Esc | Jeda / lanjut |
| R | Mengulang misi |
| F1 | Menampilkan informasi perilaku penjaga |

## Skor

Kemenangan memberikan 6000 poin objektif, bonus stealth hingga 2500, dan bonus waktu hingga 1500. Semakin sedikit laporan penjaga dan semakin cepat misi selesai, semakin besar bonus yang diperoleh. Melewati waktu acuan menghilangkan bonus waktu tetapi tidak menggagalkan misi. Waktu acuan mengikuti jumlah target, penjaga, topologi map, dan lantai. Rekor terbaik disimpan lokal dan dipisahkan per level.

## Suasana

Interior rumah dan taman memakai bentuk low-poly dengan warna hangat. Pepohonan dan kain bergerak, daun berguguran, kincir berputar, sprinkler menyiram, serta kupu-kupu beterbangan. Antarmuka menggunakan bahasa Inggris dan tipografi pixel.
