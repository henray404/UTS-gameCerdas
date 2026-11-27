# Kalibrasi ThiefBot di build v2 (`42fbf5f`)

Lab, 28 Nov 2026. FSM baseline (`-hunter-baseline`), `-hunter-solo` (hanya `guards[0]` aktif), `-hunter-seed random` (layout acak 0–999 per misi), `-job-worker-count 0`, 100 misi per sel. Angka = persen misi tempat bot lolos lewat pintu keluar.

| `-thiefbot-skill` | L1 | L3 | L6 |
|---|---:|---:|---:|
| tanpa flag (bot lama) | 54% | 6% | 17% |
| 0,2 | 52% | **14%** | **25%** |
| 0,5 | 50% | 9% | 15% |
| 0,7 | 38% | 4% | 10% |
| 0,9 | 25% | 3% | 9% |

Tidak ada timeout di semua sel.

## Temuan

1. **Skill bekerja terbalik dari maksudnya.** Makin tinggi skill, makin jarang bot lolos, di ketiga level. Perilaku yang ditambah skill (mengendap, menunggu monster yang menghalangi rute, bersembunyi, melempar kaleng) membuat bot lebih lambat dan lebih lama terpapar. Uji laptop (20 misi, player macOS) memberi kesan sebaliknya; dengan 100 misi per sel di lab, trennya konsisten.
2. **Level besar tetap terlalu mudah untuk Hunter**, bahkan tanpa monster lain: bot lolos paling banyak 14% (L3) dan 25% (L6). Rumah besar berarti rute dan daftar target lebih panjang, sehingga bot lebih lama di dalam.
3. **Skill 0,2 dipilih untuk training v2**: paling menantang di L3/L6, setara bot lama di L1. Bobot bot tetap diacak ±0,15 per episode, jadi gaya bermain bervariasi.

## Usulan untuk laptop (ThiefBot)

- Balik atau pisahkan arah skill: perilaku hati-hati (mengendap, menunggu) jangan otomatis naik bersama skill. Pisahkan misalnya `-thiefbot-caution` dan `-thiefbot-route` (pemilihan rute menjauhi monster), lalu kalibrasi masing-masing.
- Untuk rumah besar, bot butuh rute pulang yang lebih cepat (misalnya lari ke pintu keluar begitu semua target terambil dan tidak ada monster terlihat), supaya tingkat lolos di L3/L6 bisa mencapai 30–60%.
