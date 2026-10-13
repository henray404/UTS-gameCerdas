# UTS-gameCerdas

Sneak House: prototype Stealth Infiltration untuk tugas Game Cerdas.

Unity **6000.2.10f1**, C#, built-in render pipeline. Folder project lengkap: Assets, Packages, ProjectSettings. Scene: Assets/SneakHouse/Scenes/PrototypeHouse.unity.

## Buka

Unity Hub → Projects → Add → Add project from disk → pilih folder ini. Pilih editor 6000.2.10f1. Buka scene PrototypeHouse, lalu tekan Play. Open-Unity.cmd juga membuka project dan scene ini.

Diverifikasi 14 Oktober 2026 di Unity 6000.2.10f1: 25 pemeriksaan Play Mode lolos (navigation, vision, suspicion, memory, hearing, pickup, win, lose). Lisensi Personal terlihat di Unity Hub dan editor berhasil menjalankan scene. Laporan: Documentation/unity-prototype-validation.json. MCP editor tersambung saat verifikasi. Uji otomatis tidak membuktikan balancing atau durasi permainan.

## Scope yang dipilih

Top-down miring, WASD, satu rumah kecil, satu penjaga aktif, tiga target, satu extraction point. Target: drive rahasia, jam tangan, konsol game. Bogo menjadi penjaga; model Stan V5 menjadi stand-in player dengan warna pakaian berbeda. Stand-in ini bisa diganti tanpa mengganti controller.

WASD/arrow: bergerak. Shift: lari dan suara lebih besar. Ctrl/C: mengendap. E: mengambil target di dekat pemain, dengan dinding sebagai penghalang interaksi. F1: debug. R: ulang. Esc: jeda.

Menang: tiga target diambil, lalu kembali ke lingkaran hijau di pintu masuk. Kalah: bersentuhan dekat dengan penjaga tanpa dinding di antaranya.

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

Aset FBX berasal dari hasil kerja Sneak House sebelumnya. Furniture adaptasi Kenney CC0; lisensi di Assets/SneakHouse/Licenses. Model kartun merupakan adaptasi proyek. Font Barlow dan Changa One mengikuti lisensi font yang disertakan.
