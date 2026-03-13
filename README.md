# FaceAntiSpoof

Windows-приложение для обнаружения спуфинг-атак на систему распознавания лиц. Определяет, предъявлено ли камере реальное лицо или его подделка (фото на бумаге, изображение на экране).

Порт Python-проекта [SuriAI/face-antispoof-onnx](https://github.com/SuriAI/face-antispoof-onnx) на C# .NET 10 WinForms.

## Возможности

- Классификация **Real / Spoof** в реальном времени с веб-камеры
- Анализ фотографий и видеофайлов
- Модель **MiniFASNetV2SE** — 612 КБ, точность 98.20%, инференс < 40 мс на CPU
- Детекция лиц через **YuNet** (227 КБ, встроен в OpenCV)
- Опциональное GPU-ускорение через **DirectML** (любой DX12 GPU, без установки CUDA)

## Требования

- Windows 10/11 x64
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Быстрый старт

```bash
# Клонирование
git clone <repo-url>
cd FaceAntiSpoof

# Сборка
dotnet build FaceAntiSpoof.slnx -c Release

# Запуск
dotnet run --project FaceAntiSpoof.WinForms -c Release
```

ONNX-модели уже размещены в `Resources/models/` и автоматически копируются при сборке.

## Режимы работы

| Режим | Описание |
|-------|----------|
| **Фото** | Загрузка изображения, однократный анализ |
| **Видео** | Покадровая обработка видеофайла |
| **Камера** | Непрерывный анализ с веб-камеры в реальном времени |

Результат отображается цветной рамкой: зелёная — реальное лицо, красная — спуфинг.

## Структура проекта

```
FaceAntiSpoof.slnx
│
├── FaceAntiSpoof.Core/           # Библиотека (логика инференса)
│   ├── Models/                   # DTO: FaceDetectionResult, AntiSpoofResult
│   ├── Services/                 # FaceDetectorService, AntiSpoofService, CameraService
│   │   └── Interfaces/           # Контракты сервисов
│   ├── Preprocessing/            # ImagePreprocessor, BoundingBoxHelper
│   └── Infrastructure/           # OnnxSessionManager, TensorHelper
│
├── FaceAntiSpoof.WinForms/       # UI-приложение
│   ├── MainForm.cs               # Главная форма (фото/видео/камера)
│   └── Program.cs
│
├── FaceAntiSpoof.Tests/          # Юнит-тесты (xUnit)
│
└── Resources/models/             # ONNX-модели
    ├── best_model_quantized.onnx # Антиспуфинг INT8 (612 КБ)
    ├── best_model.onnx           # Антиспуфинг FP32 (1.82 МБ)
    └── face_detection_yunet.onnx # Детектор лиц YuNet (227 КБ)
```

## Пайплайн инференса

```
Кадр → YuNet детекция (320×320)
     → Квадратный crop (max(w,h) × 1.5, reflect padding)
     → BGR → RGB
     → Letterbox resize 128×128
     → Нормализация [0, 1]
     → HWC → CHW
     → ONNX MiniFASNetV2SE
     → Softmax (idx0=real, idx1=spoof)
     → Пороговое решение
```

## Настройки UI

- **Модель**: INT8 (быстрая, 612 КБ) или FP32 (точная, 1.82 МБ)
- **Порог решения**: 0.0 – 1.0 (по умолчанию 0.5)
- **DirectML**: включение GPU-ускорения
- **Камера**: выбор устройства (индекс 0–4)

## Тестирование

```bash
# Все тесты
dotnet test FaceAntiSpoof.Tests

# Конкретный тест
dotnet test FaceAntiSpoof.Tests --filter "FullyQualifiedName~TestSoftmax"
```

## Стек технологий

| Компонент | Пакет | Версия |
|-----------|-------|--------|
| ONNX-инференс | Microsoft.ML.OnnxRuntime | 1.24.3 |
| GPU (опционально) | Microsoft.ML.OnnxRuntime.DirectML | 1.24.2 |
| Компьютерное зрение | OpenCvSharp4 | 4.13.0 |
| Тестирование | xUnit | 2.9.3 |

## Производительность

| Этап | CPU | DirectML |
|------|-----|----------|
| Детекция лица (YuNet) | < 15 мс | < 5 мс |
| Предобработка | < 3 мс | — |
| Антиспуфинг (INT8) | < 20 мс | < 5 мс |
| **Полный пайплайн** | **< 40 мс** | **< 15 мс** |

## Лицензии

- Модель MiniFASNetV2SE — [Apache-2.0](https://github.com/SuriAI/face-antispoof-onnx/blob/main/LICENSE)
- YuNet — MIT
- ONNX Runtime — MIT
- OpenCvSharp4 — Apache-2.0
