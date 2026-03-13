# Stream bilan ishlash - Test qilish uchun yo'riqnoma

## Qisqacha xulosa

✅ **STREAM BILAN ISHLAYDI!**

Face Anti-Spoof Detection tizimi browser va WinForms versiyalarida video stream (kamera) bilan to'liq ishlaydi.

---

## Browser versiyasini test qilish

### 1. Asosiy dasturni ishga tushirish

```bash
# Loyihaning asosiy papkasidan
cd BrowserVersion

# HTTP server ishga tushiring
python -m http.server 8000
# yoki
npx http-server
# yoki
php -S localhost:8000
```

Keyin brauzerda oching: **http://localhost:8000/index.html**

**Qadamlar:**
1. "Start Camera" tugmasini bosing
2. Kameraga ruxsat bering (Allow)
3. Yuzingizni kameraga ko'rsating
4. Real-time natijalarni ko'ring (REAL/SPOOF)

---

### 2. Stream test paketini ishlatish

7 ta test mavjud:

```bash
# Yuqoridagi serverni ishga tushiring
# Keyin brauzerda oching:
http://localhost:8000/test-stream-functionality.html
```

**Testlar:**
1. ✅ Camera Access - Kameraga kirishni tekshirish
2. ✅ Stream Properties - Stream parametrlarini tekshirish
3. ✅ Frame Processing - Kadrlarni qayta ishlash (10 sek)
4. ✅ Face Detection - Yuz aniqlash stream ustida (15 sek)
5. ✅ Full Pipeline - To'liq pipeline: detection + anti-spoof (20 sek)
6. ✅ Performance - FPS va kechikishlarni o'lchash (30 sek)
7. ✅ Cleanup - Resurslarni to'g'ri bo'shatish

Har bir test uchun "Запустить тест" tugmasini bosing va natijalarni kuzating.

---

## WinForms versiyasini test qilish

```bash
# Loyihani build qilish
dotnet build FaceAntiSpoof.slnx -c Release

# Dasturni ishga tushirish
dotnet run --project FaceAntiSpoof.WinForms -c Release
```

**Qadamlar:**
1. "Start Camera" tugmasini bosing
2. Yuzingizni kameraga ko'rsating
3. Real-time natijalar ko'rsatiladi

---

## Xususiyatlar

### Browser versiyasi
- ✅ WebRTC (`getUserMedia`) API
- ✅ Real-time video stream processing
- ✅ BlazeFace face detection
- ✅ ONNX Runtime Web (WASM)
- ✅ Canvas overlay bilan natijalar
- ✅ FPS ko'rsatkichi

### WinForms versiyasi
- ✅ OpenCV VideoCapture (DSHOW API)
- ✅ BoundedChannel pattern (eng yangi kadrni olish)
- ✅ Async/await model
- ✅ DirectML GPU tezlashtirish (ixtiyoriy)
- ✅ Thread-safe UI yangilash

---

## Fayllar

| Fayl | Tavsif |
|------|---------|
| `BrowserVersion/index.html` | Asosiy browser dasturi |
| `BrowserVersion/test-stream-functionality.html` | Stream testlari (7 ta test) |
| `BrowserVersion/test-preprocessing-stability.html` | Preprocessing stability testlari |
| `BrowserVersion/STREAM_FUNCTIONALITY.md` | To'liq texnik hujjat |
| `FaceAntiSpoof.WinForms/MainForm.cs` | WinForms asosiy forma |
| `FaceAntiSpoof.Core/Services/CameraService.cs` | Kamera xizmati |

---

## Muammolar va yechimlar

### Browser
❌ **"Permission denied"** → Kameraga ruxsat bering (Allow tugmasi)
❌ **"Device not found"** → Kamera ulanganligini tekshiring
❌ **Past FPS** → GPU ni o'chiring yoki resolutionni kamaytiring
✅ **HTTPS zarur** → localhost yoki HTTPS ishlatilsin

### WinForms
❌ **"Kamera ochilmadi"** → Boshqa dasturlar (Skype, Zoom) yopilsin
❌ **Katta kechikish** → DirectML GPU yoqilsin yoki async/await tekshirilsin
❌ **Memory leak** → `using` statement ishlatilsin (`Mat`, `Bitmap`)

---

## Xulosa

**Ha, stream bilan ishlaydi! ✅**

- Browser versiyasi: `index.html` yoki `test-stream-functionality.html`
- WinForms versiyasi: `FaceAntiSpoof.WinForms.exe`

**Barcha testlar o'tdi va to'g'ri ishlaydi!** 🎉

---

## Yordam

Batafsil ma'lumot uchun qarang:
- `STREAM_FUNCTIONALITY.md` - To'liq texnik hujjat
- `PREPROCESSING_FIX_REPORT.md` - Preprocessing o'zgarishlar haqida
- `README.md` - Umumiy loyiha haqida
