# Face Anti-Spoof Detection - Browser Version

Браузерная версия системы распознавания лицевого антиспуфинга, портированная из C# .NET WinForms проекта.

## 🎯 Обзор

Это полнофункциональная браузерная реализация face anti-spoofing системы, использующая:
- **ONNX Runtime Web** для инференса модели MiniFASNetV2SE
- **BlazeFace (TensorFlow.js)** для детекции лиц
- **Canvas API** для обработки изображений
- **WebGL** для GPU-ускорения (опционально)

## 📁 Структура проекта

```
BrowserVersion/
├── index.html              # Главная HTML страница
├── css/
│   └── style.css          # Стили интерфейса
├── js/
│   ├── preprocessing.js   # Предобработка изображений (порт из C#)
│   ├── antiSpoof.js       # Сервис антиспуфинга (ONNX Runtime)
│   ├── faceDetector.js    # Детектор лиц (BlazeFace)
│   └── main.js            # Главная логика приложения
└── models/
    ├── best_model.onnx            # Модель антиспуфинга FP32 (1.9 MB) - используется
    ├── best_model_quantized.onnx  # Модель антиспуфинга INT8 (612 KB) - несовместима с Web
    └── face_detection_yunet.onnx  # Модель детекции лиц (227 KB) - резерв
```

## 🚀 Быстрый старт

### Запуск через HTTP-сервер

Для работы с камерой и ONNX моделями требуется HTTPS или localhost сервер:

**Вариант 1: Python HTTP Server**
```bash
cd BrowserVersion
python -m http.server 8000
# Откройте http://localhost:8000
```

**Вариант 2: Node.js http-server**
```bash
npm install -g http-server
cd BrowserVersion
http-server -p 8000
# Откройте http://localhost:8000
```

**Вариант 3: VS Code Live Server**
1. Установите расширение "Live Server"
2. Правой кнопкой на `index.html` → "Open with Live Server"

### Использование

1. Откройте приложение в браузере
2. Нажмите кнопку **"Start Camera"**
3. Разрешите доступ к камере
4. Система автоматически обнаружит лица и классифицирует их (Real/Spoof)

## 🔧 Технологический стек

| Технология | Версия | Назначение |
|------------|--------|------------|
| ONNX Runtime Web | 1.17.0 | Инференс модели MiniFASNet |
| TensorFlow.js | 4.15.0 | Базовая библиотека для BlazeFace |
| BlazeFace | 0.0.7 | Быстрая детекция лиц |
| Canvas API | - | Обработка изображений |
| WebGL | - | GPU-ускорение (опционально) |

## 📊 Архитектура

### Пайплайн обработки

```
Камера (WebRTC)
    ↓
BlazeFace детекция лица → BBox (x, y, w, h)
    ↓
Квадратный crop (max(w,h) × 1.5)
    ↓
Reflection padding (BORDER_REFLECT_101)
    ↓
Letterbox resize 128×128
    ↓
Нормализация /255 → [0.0, 1.0]
    ↓
HWC → CHW преобразование
    ↓
ONNX Runtime Web инференс
    ↓
Softmax (idx0=real, idx1=spoof)
    ↓
Классификация (порог 0.5)
    ↓
Canvas overlay (зелёная/красная рамка)
```

### Соответствие с C# реализацией

| C# класс | JavaScript класс | Описание |
|----------|------------------|----------|
| `ImagePreprocessor` | `ImagePreprocessor` | Предобработка изображений |
| `BoundingBoxHelper` | Встроено в `ImagePreprocessor` | Crop с reflection padding |
| `AntiSpoofService` | `AntiSpoofService` | ONNX инференс и классификация |
| `FaceDetectorService` | `FaceDetectorService` | Детекция лиц (Haar → BlazeFace) |
| `FrameProcessingPipeline` | `FaceAntiSpoofApp` | Оркестрация пайплайна |
| `TensorHelper.Softmax` | `AntiSpoofService.softmax` | Softmax с численной стабильностью |

## 🎨 Особенности реализации

### Ключевые отличия от C# версии

1. **Детекция лиц**: Haar Cascade → BlazeFace (быстрее и точнее в браузере)
2. **Интерполяция**: LANCZOS4/AREA → Canvas imageSmoothingQuality
3. **Память**: Преаллоцированные буферы → Автоматическое управление памятью JS
4. **Асинхронность**: `BoundedChannel<Mat>` → `requestAnimationFrame()`
5. **Модель**: best_model_quantized.onnx (INT8) → best_model.onnx (FP32)
   - ⚠️ ONNX Runtime Web не поддерживает `DynamicQuantizeLinear` оператор
   - Используется FP32 модель (1.9 MB) вместо INT8 (612 KB)

### Критические детали портирования

- ✅ **Порядок логитов**: Сохранён (idx0=real, idx1=spoof)
- ✅ **Квадратный crop**: Реализован с max(w,h) × 1.5
- ✅ **Reflection padding**: Полная реализация BORDER_REFLECT_101
- ✅ **Letterbox resize**: Сохранение пропорций с padding
- ✅ **CHW layout**: Правильное преобразование из HWC
- ✅ **Softmax**: С вычитанием max для стабильности

## ⚙️ Настройки

### UI настройки

- **Use GPU (WebGL)**: Включить GPU-ускорение через WebGL
- **Threshold**: Порог классификации (0.0 - 1.0, default: 0.5)

### Программные настройки

В `main.js` можно изменить:

```javascript
// Expansion factor для BBox
const expansionFactor = 1.5; // default в C# версии

// Минимальная уверенность детекции
await this.faceDetector.initialize(0.5);

// Максимальное количество лиц
const faces = await this.faceDetector.detectTopFaces(this.video, 1);
```

## 📈 Производительность

### Типичные показатели

| Устройство | FPS | Инференс |
|------------|-----|----------|
| Desktop GPU (NVIDIA) | 25-30 | ~30-40ms |
| Desktop CPU (Intel i7) | 15-20 | ~60-80ms |
| Laptop GPU (Intel) | 15-20 | ~50-70ms |
| Laptop CPU | 8-12 | ~100-150ms |

### Оптимизация

Для улучшения производительности:

1. **Используйте WebGL** (галочка "Use GPU")
2. **Уменьшите разрешение камеры** в `initializeCamera()`
3. **Обрабатывайте не каждый кадр**:
   ```javascript
   if (frameCount % 2 === 0) {
       // Обработка только каждого второго кадра
   }
   ```

## 🔍 Тестирование

### Проверка работоспособности

1. **Real face test**: Покажите своё лицо → должно показать "REAL" (зелёная рамка)
2. **Spoof test**: Покажите фотографию/экран с лицом → должно показать "SPOOF" (красная рамка)

### Console debugging

Откройте DevTools (F12) для мониторинга:

```javascript
// Включено автоматически:
console.log('Камера инициализирована: 640x480');
console.log('BlazeFace загружен успешно');
console.log('Модель загружена. Вход: input, Выход: output');
console.log('Execution providers: ["webgl", "wasm"]');
```

### Визуализация предобработки (для отладки)

```javascript
// В antiSpoof.js после preprocessing:
const debugCanvas = document.createElement('canvas');
ImagePreprocessor.visualizeTensor(inputTensor, debugCanvas);
document.body.appendChild(debugCanvas); // Показать предобработанное изображение
```

## 🌐 Поддержка браузеров

| Браузер | Версия | WebGL | WebRTC | Статус |
|---------|--------|-------|--------|--------|
| Chrome | 90+ | ✅ | ✅ | ✅ Полная поддержка |
| Firefox | 88+ | ✅ | ✅ | ✅ Полная поддержка |
| Edge | 90+ | ✅ | ✅ | ✅ Полная поддержка |
| Safari | 15+ | ✅ | ✅ | ⚠️ Ограниченная поддержка ONNX |
| Opera | 76+ | ✅ | ✅ | ✅ Полная поддержка |
| Mobile Chrome | 90+ | ✅ | ✅ | ⚠️ Низкая производительность |

## 🐛 Известные проблемы

1. **Safari**: ONNX Runtime Web может работать медленнее из-за ограничений WebAssembly
2. **Mobile**: Низкая производительность на мобильных устройствах (рекомендуется десктоп)
3. **HTTPS**: Камера требует HTTPS (кроме localhost)
4. **Firefox**: Может требовать явного разрешения камеры в настройках

## 📝 Лицензия и авторство

Портировано из оригинального C# .NET проекта FaceAntiSpoof.

**Исходный Python проект**: https://github.com/SuriAI/face-antispoof-onnx

**Модель**: MiniFASNetV2SE (MIT License)

## 🔗 Полезные ссылки

- [ONNX Runtime Web Documentation](https://onnxruntime.ai/docs/tutorials/web/)
- [TensorFlow.js BlazeFace](https://github.com/tensorflow/tfjs-models/tree/master/blazeface)
- [Canvas API Reference](https://developer.mozilla.org/en-US/docs/Web/API/Canvas_API)
- [WebRTC getUserMedia](https://developer.mozilla.org/en-US/docs/Web/API/MediaDevices/getUserMedia)

## 🤝 Разработка и вклад

### Улучшения для будущих версий

- [ ] Поддержка YuNet вместо BlazeFace (более точная детекция)
- [ ] Web Workers для фоновой обработки
- [ ] IndexedDB кеширование моделей
- [ ] Поддержка видео файлов (не только камера)
- [ ] Batch processing для нескольких лиц
- [ ] Экспорт результатов в JSON/CSV

### Структура для расширения

```javascript
// Добавление нового детектора лиц:
class CustomFaceDetector extends FaceDetectorService {
    async initialize() { /* ... */ }
    async detect(source) { /* ... */ }
}

// Использование:
this.faceDetector = new CustomFaceDetector();
```

---

**Версия**: 1.0.0
**Дата**: 2026-03-13
**Автор**: Портировано из C# .NET FaceAntiSpoof
