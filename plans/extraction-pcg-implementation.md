# Rencana Implementasi Hollow House: Extraction dan PCG Modular

> Status: rancangan untuk disepakati tim; belum diimplementasikan atau divalidasi melalui playtest. Angka balancing yang disebut sebagai contoh bukan hasil pengujian.
>
> Dasar: [rencana EAS adaptif](eas-adaptive.md) dan keputusan desain tim dalam diskusi. Jika keduanya berbeda, bagian **Keputusan dan konflik** di dokumen ini mencatat perubahan yang perlu disepakati bersama.

## 1. Tujuan dan ruang lingkup

Pemain menjelajahi bangunan horor yang susunan ruangannya berubah tiap run, membawa maksimal empat barang bernilai, lalu memilih keluar. Keluar dengan nilai loot yang dibawa memenuhi quota menghasilkan kemenangan; mati atau keluar dengan nilai kurang menghasilkan kegagalan. Ancaman musuh meningkat seiring waktu.

Dua mode memakai generator, inventory, loot, dan sistem musuh bersama:

| Aturan | Campaign | Endless |
|---|---|---|
| Mulai | Pilih dari enam level yang terbuka | Mulai Day 1 |
| Berhasil | Keluar dengan nilai loot >= quota level; buka level berikutnya jika ada | Keluar dengan nilai loot >= quota day; lanjut ke day berikutnya |
| Gagal | Mati atau keluar di bawah quota; level tetap bisa dipilih dan diulang | Mati atau keluar di bawah quota; kembali ke Day 1 |
| Retry | Run baru dengan map, loot, dan spawn baru | Run baru dari Day 1 dengan map, loot, dan spawn baru |
| Penyimpanan progres | Unlock campaign disimpan | Day berlaku untuk run endless yang sedang berlangsung; gagal meresetnya |

Barang loot memakai satu slot. Kapasitas empat slot. Pemain memilih barang yang dijatuhkan; tidak ada penggantian otomatis. Hanya barang yang masih dibawa saat keluar yang dihitung. Usulan: tombol keluar menampilkan konfirmasi nilai dan quota sebelum mengakhiri run.

**Bukan target:** menyalin aset atau desain Lethal Company; multiplayer; sistem berat barang; perubahan menjadi game open world. Arsitektur ini tidak mengubah kebutuhan DDA, player modeling, AI, dan bukti materi P9–P14 yang sudah dibagi ke tim.

## 2. Baseline branch `fp_eas`

Kondisi berikut berasal dari pembacaan kode, bukan klaim hasil tes saat ini:

- `Assets/SneakHouse/Scripts/InteriorLayout.cs`: memilih variasi tiga ruangan berdasarkan seed; `ValidatePaths()` memeriksa akses target, rute patroli, persembunyian, dan exit.
- `HouseShell.cs`: memilih bentuk rumah dari katalog tetap. `CampaignLevels.cs` menentukan seed interior dan parameter enam level.
- `RuntimeNavigation.cs`: membangun NavMesh saat runtime; jika validasi gagal, mencoba sampai 32 seed berikutnya. Bounds NavMesh saat ini tetap dan harus ditinjau untuk map modular.
- `GuardEncounter.cs`: memilih roster dan menyusun encounter pada awal misi. Rute `PatrolPlans` ditentukan per level; rute tersebut belum tentu cocok dengan bangunan modular.
- `LootTarget.cs` merepresentasikan target misi; `ExtractionZone.cs` saat ini otomatis menyelesaikan misi ketika jumlah target terpenuhi. Ini berbeda dari keluar secara sengaja dan pemeriksaan quota.
- `GameSession.cs` sudah menampilkan seed interior dan encounter pada debug F1. Panel F2 perlu koordinasi dengan B.
- `Packages/manifest.json` mencantumkan `com.unity.ai.navigation` 2.0.16 dan Unity Test Framework 1.8.0. `ProjectSettings/ProjectVersion.txt` mencantumkan Unity 6000.6.0f1.
- `plans/eas-adaptive.md` menjelaskan DDA, metrik layout, resource, serta integrasi AI yang direncanakan. Kelas `DifficultyProfile`, `LevelMetrics`, `ResourceSpawner`, dan `AdaptiveLayoutPicker` tidak ditemukan dalam pencarian kode saat penyusunan rencana ini.

Perubahan ini mengganti asumsi rumah dan target tetap. Jangan mempertahankan dua alur objective berbeda tanpa keputusan tim; migrasikan semua caller, UI, tes, dan dokumentasi yang bergantung pada objective lama saat cutover.

## 3. Keputusan dan konflik yang perlu dibawa ke tim

### Disepakati dalam diskusi

1. Layout bangunan modular berubah setiap run, bukan hanya susunan furnitur.
2. Campaign tetap enam level dengan level selection.
3. Endless mulai Day 1; gagal mereset progres endless ke Day 1, bukan progres campaign.
4. Gagal berarti mati atau sengaja keluar saat nilai loot di bawah quota.
5. Empat slot; satu barang satu slot; pemain memilih barang yang dijatuhkan.
6. Retry membuat run baru dengan map, loot, dan spawn baru.
7. Ancaman meningkat seiring waktu; day atau level lebih tinggi dapat lebih menantang.

### Usulan teknis yang perlu dikonfirmasi tim

- Grid modular satu lantai sebagai langkah pertama, dengan pintu masuk sekaligus exit.
- Peringatan dan konfirmasi sebelum keluar, termasuk ketika nilai masih kurang.
- Tempat sembunyi dekat jalur dan paparan patroli sebagai soft constraint, bukan syarat mutlak.
- A menyediakan kandidat titik spawn; C menentukan waktu dan jenis musuh; B menentukan konfigurasi run dan DDA.
- Inventory dan aturan quota dipegang B; A menangani penempatan loot. Ini pembagian baru di luar daftar awal.
- Quota, jumlah ruang, nilai loot, waktu spawn, batas musuh, radius aman, normalisasi metrik, dan pertumbuhan endless perlu ditentukan lewat playtest, bukan dianggap sudah disepakati.

### Konflik dengan rencana lama

- Rencana awal meminta tabel **27 layout × 4 rumah**; map modular baru tidak otomatis memenuhi bukti tersebut. Sepakati apakah angka itu tetap menjadi baseline dan tambahkan tabel seed modular. Jangan mengklaim target lama selesai hanya karena generator baru berjalan.
- Dokumen tugas menyebut tempat sembunyi dekat target sebagai soft constraint, sedangkan `eas-adaptive.md` menyebutnya syarat buang kandidat pada D rendah. Tentukan kriteria penilaian bersama sebelum implementasi.
- Retry lama mempertahankan rumah dan target; desain baru meminta map, loot, dan spawn berubah. Perbarui README, UI, dan tes kontrak terkait pada saat implementasi.
- Baterai dan pil sanity bergantung pada mekanik yang harus disepakati dengan B/C. Generator tidak boleh mengklaim fitur resource selesai hanya karena titik spawn tersedia.

## 4. Kepemilikan dan kontrak integrasi

| Pemilik | Tanggung jawab | Output untuk anggota lain |
|---|---|---|
| A | Prefab modular, generasi dan validasi layout, titik loot, titik spawn, titik patroli, `LevelMetrics`, pemilihan layout adaptif | Map valid, seed, signature, daftar titik, metrik dan alasan penolakan |
| B | Konfigurasi run, quota, inventory, layar hasil, progres campaign/endless, DDA, telemetry, F2 | `RunConfig`, `DifficultyProfile`, nilai quota, hasil misi dan parameter adaptasi |
| C | Pemilihan dan jadwal musuh, AI, perilaku saat spawn dan patroli, RL/ML sesuai tugas | Penggunaan titik spawn, log ancaman, batas musuh dan kebutuhan waypoint |

Kontrak di bawah adalah **nama dan isi usulan**, bukan API yang sudah ada. Sepakati nama, tipe, kepemilikan file, waktu inisialisasi dan aturan perubahan sebelum coding paralel.

### `RunConfig` — B

- Mode `Campaign` atau `Endless`; nomor level/day.
- Quota bilangan bulat nonnegatif; kapasitas empat slot.
- Rentang ukuran map, aturan loot, dan parameter dasar ancaman.
- `DifficultyProfile` dari B; konfigurasi yang dibekukan untuk satu generasi run.
- Campaign mempertahankan enam level dan penyimpanan unlock; endless menyimpan day selama run berlangsung.

### `DifficultyProfile` — B

- D pada rentang 0..1; pengali parameter AI; jumlah atau batas monster; jumlah resource; flag perilaku dan alasan adaptasi sesuai kontrak tugas awal.
- A membaca D dan parameter PCG; C membaca parameter dan flag perilaku. Pastikan B/C/A menyetujui apakah D tetap selama run atau dapat berubah saat misi; perubahan tengah misi tidak boleh membangun ulang map diam-diam.

### `GeneratedLevel` — A

- Seed run, versi generator, signature struktur, konfigurasi yang digunakan.
- Daftar modul dan transform-nya; titik player start/exit, kandidat loot, hiding, patrol, dan enemy spawn.
- Hasil validasi, jumlah percobaan, alasan penolakan, serta `LevelMetrics` jika telah dihitung.
- Konsumen tidak menganggap kandidat spawn aman selamanya: C mengecek ulang kondisi saat spawn.

### Loot dan hasil — A/B

- A menyerahkan daftar loot yang ditempatkan, masing-masing dengan identitas stabil dalam run, nilai bilangan bulat dan posisi.
- B menjadi pemilik sumber kebenaran empat slot. Pickup memindahkan barang dari dunia ke inventory; drop mengembalikan barang yang sama ke posisi valid. Barang tidak boleh terhitung dua kali.
- Nilai extraction adalah jumlah nilai barang di inventory saat konfirmasi; kegagalan karena mati tidak menyimpan loot sebagai keberhasilan.

### Spawn — A/C

- Kandidat menyertakan posisi dan ruangan; validasi awal memeriksa NavMesh, keterjangkauan, tabrakan, dan zona aman.
- C memeriksa ulang jarak, visibility, tabrakan, batas monster aktif, dan kondisi NavMesh pada waktu spawn. Bila semua titik tidak aman, tunda spawn dan catat alasan.
- Titik patroli dan jalur dihasilkan atau dipilih berdasarkan map baru. Jangan menganggap `PatrolPlans` per level cocok dengan struktur modular.

## 5. Pipeline PCG

1. B memilih konfigurasi mode dan level/day, termasuk quota dan profil difficulty.
2. Buat seed run baru. Catat seed, versi generator, konfigurasi dan signature untuk debugging.
3. Tempatkan modul masuk pada grid. Tambahkan ruangan/koridor yang memiliki pintu kompatibel, tanpa overlap; tutup bukaan yang tidak terhubung.
4. Letakkan loot dan kandidat hiding, patrol, resource, dan spawn sesuai modul dan seed. Nilai/jenis harus berasal dari katalog yang disepakati.
5. Bangun NavMesh untuk bounds yang mencakup seluruh geometri; pastikan data dan objek kandidat sebelumnya dibersihkan sebelum percobaan ulang.
6. Validasi hard constraint, termasuk rute player, item untuk quota, exit, dan titik spawn. Catat alasan tiap kegagalan.
7. Hitung soft constraint dan metrik pada kandidat valid. Jika ada pemilihan adaptif, bandingkan kandidat valid dengan target D.
8. Pilih kandidat, pastikan resource dan encounter menggunakan map yang sama, lalu mulai misi. Jika seluruh percobaan habis, blokir misi dan tunjukkan alasan; jangan diam-diam menjalankan layout invalid.

### Seed dan retry

Seed yang sama, versi generator sama, konfigurasi sama, dan input katalog sama harus mereproduksi struktur serta penempatan yang sama pada lingkungan yang didukung. Jangan mengandalkan random global atau urutan objek yang tidak stabil. Tentukan aliran random terpisah untuk layout, loot, dan encounter dari seed run agar perubahan pada satu subsistem tidak tanpa sengaja mengubah subsistem lain.

Seed berbeda tidak menjamin struktur berbeda. Hitung signature kanonis yang mencakup jenis modul, posisi, rotasi, dan sambungan. Saat retry, bandingkan dengan run sebelumnya dan coba seed lain bila signature sama. Batasi jumlah percobaan; jika semua menghasilkan signature sama atau invalid, laporkan kegagalan. Penempatan loot dan spawn juga dibuat ulang, tetapi jangan mengklaim semua posisi pasti berbeda hanya karena seed berbeda.

### Modul dan data

Awal: satu lantai, grid, ukuran dan posisi pintu standar. Katalog minimum: entrance/exit, koridor lurus, belokan, percabangan, ruang biasa, ruang loot, dan jalan buntu. Tiap prefab memuat konektor, footprint, kategori/bobot, serta titik loot/hiding/patrol/spawn yang sesuai. Pintu tanpa sambungan ditutup dengan geometri yang benar-benar menghalangi gerak dan pandangan.

Validasi katalog harus menolak modul tanpa konektor yang dibutuhkan atau prefab yang hilang; jangan menganggap semua kombinasi rotasi valid sebelum diuji.

## 6. Constraint dan perbaikan

### Hard constraint: wajib lulus

- Footprint modul tidak saling tumpang tindih; semua konektor sesuai atau tertutup.
- Player start dan exit berada pada NavMesh dan saling terjangkau.
- Loot yang digunakan untuk mencapai quota terjangkau dan dapat diambil; empat loot bernilai tertinggi yang terjangkau memiliki jumlah >= quota. Ini cukup hanya karena setiap item memakai tepat satu slot dan tidak ada pembatas jenis/berat lain.
- Area dan titik yang akan digunakan untuk spawn/patroli valid pada NavMesh, tidak tertanam geometri, dan tidak berada di zona aman awal.
- Tempat persembunyian dan resource yang benar-benar ditempatkan dapat dijangkau dan tidak memblokir jalur wajib.
- Loot yang dijatuhkan tidak boleh menghilang atau menciptakan duplikasi inventory/dunia.

### Soft constraint: dinilai, bukan otomatis ditolak

- Akses tempat persembunyian dekat jalur atau loot.
- Loot bernilai tinggi cenderung lebih jauh dari exit atau lebih dekat area patroli.
- Sebagian rute punya perlindungan dari pandangan musuh, dengan pemeriksaan dinding dan perbedaan lantai.
- Variasi percabangan dan distribusi nilai loot.

### Repair

Perbaiki penyebab yang bisa diperbaiki tanpa merusak invariant: pindahkan loot/resource atau pilih titik spawn lain; jika masalah terkait jumlah musuh, koordinasikan pengurangan jumlah dengan C dan catat perubahan pada profil/log. Bila masih gagal, generate kandidat lain. Jangan memperlonggar hard constraint diam-diam. Penilaian biaya repair dan jumlah percobaan ditentukan setelah pengukuran.

## 7. Metrik level dan DDA

A membuat `LevelMetrics`; B menampilkan metrik dan D; C menyediakan informasi patroli yang benar-benar dipakai. Definisi operasional harus dicatat dalam kode/laporan:

- **L:** panjang jalur NavMesh dari start ke urutan titik loot yang ditentukan dan kembali ke exit; jelaskan algoritme pemilihan titik dan urutan.
- **C:** proporsi sampel pada jalur L yang terlihat dari waypoint patroli; jelaskan jarak antar-sampel, sudut/jarak pandang dan raycast penghalang.
- **H:** jumlah atau cakupan hiding spot yang dapat dijangkau dekat jalur; tentukan radius dan hindari menghitung titik yang sama berkali-kali.
- **T:** jarak patroli terdekat ke loot yang dinilai; tentukan apakah memakai jarak NavMesh atau Euclidean dan satuan.
- Normalisasi L, H, dan T memakai rentang yang dinyatakan dan dijepit; C pada 0..1. Skor awal mengikuti `eas-adaptive.md`: `0.35*norm(L) + 0.35*C - 0.15*norm(H) + 0.15*(1-norm(T))`. Tentukan transformasi/rentang skor sebelum membandingkan dengan D 0..1.

`AdaptiveLayoutPicker` memilih kandidat valid dengan jarak skor terhadap D paling kecil; tentukan aturan tie-break deterministik. Catat metrik, skor, D, kandidat yang ditolak, dan alasan. Jangan menjanjikan D rendah dan tinggi selalu memilih layout berbeda bila katalog menghasilkan skor sama. Ukur waktu pembuatan NavMesh dan penilaian kandidat; batasi kandidat atau bagi pekerjaan lintas frame berdasarkan hasil pengukuran, tanpa mengorbankan validasi.

DDA B tetap lapisan atas baseline `CampaignLevels`; sesuaikan minimal tiga kelompok parameter sebagaimana tugas awal. DDA antar-misi dan rubber band tengah misi tidak boleh menimpa aturan quota, progress, atau validasi map tanpa kontrak yang jelas.

## 8. Spawn sepanjang waktu

C mengelola jam misi yang berhenti saat pause dan selesai saat run berakhir. Konfigurasi menentukan musuh awal, kapan percobaan spawn tambahan terjadi, jenis/bobot musuh, dan batas aktif. Level/day mengubah baseline; DDA memberi penyesuaian terbatas. Nilai waktu dan batas belum ditetapkan.

Pada setiap percobaan: pilih titik valid dari A; periksa kembali zona aman, jarak dan line of sight ke pemain, fisika, NavMesh, dan jumlah musuh aktif; buat musuh hanya jika aman. Bila tidak ada titik, jadwalkan percobaan berikutnya dan tampilkan alasan di debug. Pertimbangkan petunjuk suara agar pemain mendapat informasi ancaman tanpa membocorkan posisi pasti. Tentukan apakah musuh yang terhapus boleh digantikan agar batas dan tingkat ancaman jelas.

## 9. Rencana kerja bertahap

Setiap tahap menghasilkan perilaku yang bisa diuji. Jalankan tes terfokus dan catat hasilnya; contoh perintah/hasil di bawah adalah target, bukan bukti sudah lolos. Koordinasikan perubahan file bersama sebelum memulai.

### Tahap 0 — Kontrak, baseline, dan konfigurasi

**Pemilik:** semua; B mengoordinasikan `RunConfig`/`DifficultyProfile`.

- [ ] Sepakati struktur kontrak pada bagian 4, alur inisialisasi, serta siapa mengubah `GameSession.cs`, `ExtractionZone.cs`, dan scene.
- [ ] Catat baseline dari `InteriorLayout`, `RuntimeNavigation`, `GuardEncounter`, `CampaignLevels`, `LootTarget`, dan UI misi; tetapkan tes yang harus dimigrasikan karena objective lama berubah.
- [ ] Tetapkan nilai uji awal quota, ukuran map, katalog loot, spawn dan batas percobaan sebagai **parameter uji**, bukan balancing final.
- [ ] Sepakati cara mempertahankan/mengganti bukti 27 × 4 dan mekanik resource dari tugas awal.
- [ ] Buktikan lingkungan Unity 6000.6.0f1 dapat membuka salinan proyek terisolasi; jangan hapus data atau overwrite perubahan lokal pengguna.

**Selesai jika:** semua anggota dapat menjelaskan satu run dari konfigurasi sampai hasil dan kontrak bersama memiliki satu pemilik.

### Tahap 1 — Struktur modular dan seed

**Pemilik:** A. Lokasi yang diusulkan: `Assets/SneakHouse/Scripts/Adaptive/Pcg/`, prefab di `Assets/SneakHouse/Prefabs/`, dan alat builder seperlunya di `Assets/SneakHouse/Editor/`.

- [ ] Buat katalog modul grid, konektor, footprint, entrance dan penutupan pintu; definisikan signature kanonis.
- [ ] Buat generator struktur menggunakan seed dan konfigurasi; hindari ketergantungan pada urutan objek Unity yang tidak stabil.
- [ ] Buat retry dengan seed baru, bandingkan signature dengan run sebelumnya dan batasi percobaan.
- [ ] Tes: seed/config sama menghasilkan signature sama; beberapa seed terpilih menghasilkan struktur berbeda; retry tidak menerima signature sebelumnya; limit percobaan memberi kegagalan jelas.
- [ ] Jalankan scene uji: lihat sambungan, overlap, ruang tertutup dan ukuran seluruh map di Unity.

**Selesai jika:** struktur konsisten, dapat direproduksi, dan terlihat berubah ketika retry; belum perlu AI atau inventory selesai.

### Tahap 2 — NavMesh, validasi, dan laporan

**Pemilik:** A; koordinasi C untuk kebutuhan patroli.

- [ ] Integrasikan geometri modular dengan `RuntimeNavigation`; hitung bounds yang mencakup seluruh map dan bersihkan data kandidat terdahulu.
- [ ] Validasi jalur start/exit, titik pickup/hiding/patrol/spawn dan collider yang relevan; ukur waktu rebuild.
- [ ] Implementasikan batas percobaan, repair yang aman, jumlah kandidat dan alasan penolakan; tampilkan seed/status di debug yang disepakati B.
- [ ] Tes seed valid dan sengaja invalid: jalur putus, pintu buntu terbuka, spawn di luar NavMesh, dan semua percobaan gagal.
- [ ] Mainkan beberapa map hingga kembali ke exit; pastikan tidak hanya mengandalkan pathfinding otomatis.

**Selesai jika:** map invalid tidak bisa memulai misi dan map valid bisa dijelajahi serta dikembalikan ke exit.

### Tahap 3 — Loot, empat slot, quota dan keluar

**Pemilik:** A untuk sebaran; B untuk inventory, extraction dan hasil.

- [ ] A menempatkan item terjangkau dengan nilai dan ID stabil; validasi jumlah empat nilai tertinggi terhadap quota.
- [ ] B membuat pickup, drop barang pilihan, kapasitas empat slot, total nilai, pilihan keluar dan konfirmasi.
- [ ] Migrasikan target tetap, UI, dan `ExtractionZone.cs` ke objective quota; pastikan barang yang dijatuhkan tetap ada dan dapat diambil kembali.
- [ ] Tes: quota tepat menang; satu nilai di bawah gagal; slot kelima ditolak sampai barang dipilih untuk drop; item drop mengurangi nilai dan tidak terhitung dua kali.
- [ ] Tes map dengan nilai total cukup tetapi empat item terbaik kurang dari quota: generator harus menolak atau memperbaiki kandidat.

**Selesai jika:** satu run playable dengan loot, pilihan keluar, dan hasil yang benar tanpa mengharuskan musuh baru selesai.

### Tahap 4 — Encounter dan spawn bertahap

**Pemilik:** A menyediakan titik; C mengimplementasikan director dan AI.

- [ ] Buat kandidat spawn/patrol dan zona aman dari map modular; verifikasi rute dapat dipakai di NavMesh yang sebenarnya.
- [ ] C mengganti ketergantungan rute statis yang tidak cocok; implementasikan jadwal spawn berbasis waktu, batas aktif, dan pilihan tipe sesuai konfigurasi.
- [ ] Saat spawn cek ulang pemain, visibility, collision, NavMesh dan kapasitas; bila tidak ada kandidat aman, tunda dan log.
- [ ] Tes titik dekat atau terlihat pemain, semua titik tidak aman, pause, batas aktif, serta rute terputus.
- [ ] Tampilkan waktu, jumlah musuh, titik dipilih/ditolak dan alasan pada debug yang disepakati.

**Selesai jika:** musuh bertambah sesuai jadwal yang diuji, tidak muncul secara tidak adil, dan dapat bernavigasi.

### Tahap 5 — Campaign dan endless

**Pemilik:** B; A/C mendukung integrasi run config dan reset.

- [ ] Pertahankan pemilihan enam level dan unlock campaign; hubungkan quota masing-masing.
- [ ] Tambah endless dengan day meningkat setiap menang; gagal mengakhiri progres endless dan kembali ke Day 1.
- [ ] Reset inventory, loot dunia, timer, encounter, NavMesh dan seed saat run baru tanpa mereset unlock campaign.
- [ ] Tes menang/gagal kedua mode, retry level campaign yang sudah terbuka, gagal endless pada day > 1, dan pemisahan progres.

**Selesai jika:** aturan mode sesuai tabel bagian 1, termasuk setelah kembali ke menu dan memulai run lain.

### Tahap 6 — Metrik, bobot dan adaptasi

**Pemilik:** A untuk metrik/picker; B DDA/F2; C data dan perilaku AI.

- [ ] Implementasikan metrik L/C/H/T beserta normalisasi yang eksplisit dan tes kasus geometri sederhana.
- [ ] Tambahkan bobot modul dan roster pada pemilik masing-masing; uji frekuensi atas 100 seed sebagai pengamatan, bukan jaminan distribusi pasti.
- [ ] Implementasikan pemilihan kandidat valid terdekat dengan D, tie-break deterministik dan log pilihan.
- [ ] Verifikasi DDA mengubah kelompok parameter yang disepakati tanpa melanggar hard constraint; ukur waktu generasi dan penilaian.
- [ ] Hasilkan tabel baseline 27 × 4 jika diwajibkan dan tabel eksperimen seed modular yang terpisah.

**Selesai jika:** metrik dan keputusan dapat dijelaskan dari data aktual; kasus D rendah/tinggi yang dipilih dapat dibandingkan.

### Tahap 7 — Verifikasi, playtest dan bukti

**Pemilik:** semua.

- [ ] Jalankan uji unit/logika dan uji Unity Play Mode terfokus pada interaksi lintas sistem. Gunakan proyek terisolasi jika tes menyentuh PlayerPrefs atau scene.
- [ ] Uji kumpulan seed terdokumentasi untuk akses, quota empat slot, retry, spawn dan performa; catat jumlah gagal serta penyebabnya.
- [ ] Playtest manusia: waktu generasi, keterbacaan quota/slot, rasa adil spawn, tingkat berhasil tiap level/day, dan alasan pemain gagal.
- [ ] Ambil screenshot/video aktual: dua layout, HUD quota/slot, keluar kurang quota, musuh spawn, F2 dan tabel metrik; jangan membuat hasil sintetis sebagai bukti gameplay.
- [ ] Perbarui README, panduan demo, rencana EAS dan status tugas P9–P14 beserta commit yang relevan. Centang hanya perilaku yang benar-benar diimplementasikan dan diuji.

**Selesai jika:** seluruh alur playable di kedua mode, hasil tes dan batasan dicatat, dan bukti demo sesuai perilaku nyata.

## 10. Pemetaan tugas A terhadap P9–P10

| Butir | Implementasi yang direncanakan | Bukti |
|---|---|---|
| 9.1 | Seed, signature, reproducibility dan retry | Tes seed sama; dua screenshot layout berbeda |
| 9.2 | ResourceSpawner dan aturan titik setelah mekanik B/C tersedia | Gizmo, alasan penolakan, resource dapat dipakai |
| 9.3 | Bobot modul/ruangan; bobot monster dikoordinasikan dengan C | Frekuensi dari 100 seed dan konfigurasi bobot |
| 9.4 | Generate-and-test dan log penolakan/repair | Hitungan kandidat, alasan dan status debug |
| 10.1 | Hard/soft constraint | Hasil validasi dan skor tiap kandidat |
| 10.2 | Repair aman sebelum kandidat baru | Log aksi repair dan hasil validasi ulang |
| 10.3 | Safe zone, placement item dan patroli | Overlay zona serta uji pickup/penempatan |
| 10.4 | `LevelMetrics` L/C/H/T dan kompleksitas | Tabel baseline yang disepakati serta tabel seed modular |
| 10.5 | Picker skor terdekat dengan D | Perbandingan kandidat dan pilihan D rendah/tinggi |

## 11. Risiko dan mitigasi

| Risiko | Mitigasi dan verifikasi |
|---|---|
| Map baru merusak rute `PatrolPlans` | Rute berdasarkan modul/NavMesh; uji per seed bersama C. |
| NavMesh lambat atau tidak mencakup map | Bounds dinamis, pengukuran waktu dan batas kandidat; uji area terjauh. |
| Total loot cukup tetapi quota mustahil dengan empat slot | Validasi empat loot terbaik yang bisa dijangkau. |
| Retry menghasilkan struktur sama | Bandingkan signature dan coba ulang dengan batas; gagal jelas jika tidak berhasil. |
| Spawn adil saat generasi tetapi dekat pemain saat dipakai | Validasi ulang kondisi pada waktu spawn; tunda jika tidak aman. |
| Endless menghapus unlock campaign | Pisahkan state, uji gagal day > 1 dan data campaign tetap ada. |
| Map valid secara graf tetapi tidak dapat dimainkan secara fisik | Validasi NavMesh, interaksi nyata, collision, dan playtest manual. |
| Tugas P9/P10 berubah tanpa persetujuan | Pisahkan baseline 27 × 4 dan eksperimen modular; minta kesepakatan tim/dosen. |
| Perubahan tiga anggota bertabrakan | Tetapkan pemilik file dan kontrak sebelum mulai; integrasikan bertahap. |

## 12. Keputusan lanjutan sebelum balancing

- Quota enam level dan rumus pertumbuhan endless; kapan dan bagaimana angka dibatasi.
- Jumlah modul, ukuran grid, jumlah kandidat dan batas waktu generasi.
- Nilai, bobot dan katalog loot; kebutuhan item khusus serta cara drop saat ruang sempit.
- Waktu spawn, batas aktif, bobot monster dan ketentuan aman/visibility.
- Definisi pasti metrik, normalisasi skor, dan target bukti 27 × 4.
- Mekanik resource baterai/pil dan pihak yang memilikinya.
- Apakah konfirmasi keluar dipakai dan bagaimana perilaku save/keluar aplikasi selama run endless.

Jangan menganggap usulan atau angka uji sebagai hasil playtest. Perubahan ini baru siap diimplementasikan setelah kontrak dan pembagian kerja bersama disepakati.
