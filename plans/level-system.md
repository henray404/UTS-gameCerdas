# Sistem level Sneak House

## Lingkup dan penerimaan

- Enam level memakai empat prefab rumah yang sudah ada, tanpa aset atau paket baru.
- Level 1 terbuka pada profil baru. Menang level N hanya membuka N+1. Gagal, briefing, pause, dan replay tidak membuka level.
- Pemilihan level ditolak juga oleh logika sesi jika terkunci. Progres dan rekor per level tersimpan lokal.
- Target aktif, penjaga, persepsi, pencarian, kaleng, dan waktu bonus mengikuti konfigurasi level.
- Retry mempertahankan level/map/interior. Patroli tetap dapat diacak seperti mekanik sebelumnya.
- Navigasi gagal tidak boleh mengganti level sulit menjadi map mudah dan kemudian membuka progres.
- Laporan bahasa Indonesia menjelaskan rumus, contoh hitung, alasan map, dan batas bukti balancing.

## Fase

1. Audit kode/map dan sumber API. Selesai.
2. RED: uji aturan progres, katalog, dan perhitungan sebelum implementasi. Selesai, compiler menolak tipe kampanye yang belum tersedia.
3. GREEN: katalog immutable, progres, integrasi sesi/map/target, serta UI pemilihan level. Selesai.
4. Validasi Unity pada salinan proyek agar editor lokal yang lebih baru tidak mengubah versi proyek sumber. Selesai, seluruh level lulus.
5. Review diff C#, perbaikan, laporan dan README. Selesai.

## Risiko

- Target array berurutan drive/watch/console, sedangkan ruangan living/office/collection berurutan console/drive/watch.
- HouseShell/InteriorLayout/Navigation/Encounter berjalan sebelum GameSession.Awake. Konfigurasi harus diterapkan sebelum validasi navigasi dan spawn penjaga.
- Luas geometris bukan luas NavMesh. Rumus kesulitan merupakan hipotesis desain sampai diuji pemain.
- PlayerPrefs menyimpan progres lokal dan bukan mekanisme anti-cheat.

## Hasil validasi

- C# rules: 91 assertion lulus.
- Unity Play Mode: 434 assertion lulus di Unity 6000.6.0f1, salinan terisolasi.
- Review C# dan umum: tidak ada CRITICAL/HIGH tersisa. Fallback interior dan guard editor batch diperbaiki.
- Versi proyek sumber tetap 6000.2.10f1. Editor versi tersebut, input manual, balance manusia, dan persentase coverage belum diuji.
- Detail lingkungan, reproduksi, dan batas pengujian: `Tests/README.md`.
