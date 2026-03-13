# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Обзор проекта

Портирование Python-проекта [face-antispoof-onnx](https://github.com/SuriAI/face-antispoof-onnx) на C# .NET 10 WinForms. Система распознавания лицевого антиспуфинга на базе модели **MiniFASNetV2SE** (~600 КБ, INT8) с использованием ONNX Runtime.

Исходное ТЗ: `docs/compass_artifact_wf-838a0c0a-0354-4e4b-a1df-9b31384482a5_text_markdown.md`

## Архитектура

Три проекта в Solution (`FaceAntiSpoof.slnx`):

- **FaceAntiSpoof.Core** — библиотека классов с логикой инференса, предобработки, детекции
- **FaceAntiSpoof.WinForms** — UI-приложение (PictureBox + видеопоток с оверлеями)
- **FaceAntiSpoof.Tests** — юнит-тесты (xUnit)

### Ключевые слои Core

| Слой | Классы | Назначение |
|------|--------|------------|
| Models | `FaceDetectionResult`, `AntiSpoofResult`, `FrameProcessingResult` | DTO |
| Services | `FaceDetectorService`, `AntiSpoofService`, `CameraService`, `FrameProcessingPipeline` | Бизнес-логика |
| Preprocessing | `ImagePreprocessor`, `BoundingBoxHelper` | Предобработка изображений |
| Infrastructure | `OnnxSessionManager`, `TensorHelper` | Управление ONNX-сессиями и буферами |

### Пайплайн инференса

```
Камера/Файл → YuNet детекция (320×320) → Квадратный crop (max(w,h)×1.5, BORDER_REFLECT_101)
→ BGR→RGB → Letterbox resize 128×128 (LANCZOS4/AREA + reflect padding) → /255 → HWC→CHW
→ ONNX MiniFASNet → Softmax (idx0=real, idx1=spoof) → Real/Spoof (порог 0.5)
```

### Критические отличия от ТЗ (обнаружены при анализе Python-кода)

- **Порядок логитов**: индекс 0 = real, индекс 1 = spoof (ТЗ указывало наоборот)
- **Crop**: квадратный по max(w,h), не прямоугольный; padding через `BORDER_REFLECT_101`
- **Resize**: letterboxing (сохранение пропорций + reflect padding), не простой resize
- **Интерполяция**: `LANCZOS4` при увеличении, `AREA` при уменьшении

### Асинхронная модель

Камера и инференс работают через `BoundedChannel<Mat>(capacity: 1, DropOldest)` — обработка всегда свежего кадра без очереди.

## Стек технологий

| Пакет | Назначение |
|-------|------------|
| `Microsoft.ML.OnnxRuntime` 1.24.3 | ONNX-инференс |
| `Microsoft.ML.OnnxRuntime.DirectML` 1.24.2 | GPU (опционально, любой DX12 GPU) |
| `OpenCvSharp4` 4.13.0 | Обработка изображений, детекция лиц (YuNet), захват камеры |
| `OpenCvSharp4.runtime.win` | Нативные Win-x64 библиотеки |
| `OpenCvSharp4.Extensions` | Конвертация Mat ↔ Bitmap для WinForms |

### Особенности API OpenCvSharp4

- `FaceDetectorYN` не имеет `SetInputSize()` — размер задаётся только при `Create()`, кадр масштабируется до inputSize перед детекцией
- Для чтения пикселей Mat использовать `Marshal.Copy(mat.Data, ...)` вместо `mat.GetArray()`

## ONNX-модели

Расположение: `Resources/models/` (копируются в output через `CopyToOutputDirectory`)

| Модель | Размер | Вход | Выход |
|--------|--------|------|-------|
| `best_model_quantized.onnx` (продуктив) | 612 КБ | `[1,3,128,128]` float32 RGB [0,1] | `[1,2]` логиты (real, spoof) |
| `best_model.onnx` (отладка) | 1.82 МБ | То же | То же |
| `face_detection_yunet.onnx` | 227 КБ | `[1,3,320,320]` | BBox + 5 landmarks + confidence |

Источник: `https://github.com/SuriAI/face-antispoof-onnx/tree/main/models`

## Критические детали реализации

- **InferenceSession** создаётся однократно, переиспользуется для всех кадров
- Входные/выходные буферы `float[]` преаллоцируются, OrtValue создаётся через `CreateTensorValueFromMemory()`
- Каждый `Mat` и `Bitmap` обязательно `Dispose()` — иначе утечка GDI+ хендлов
- Softmax с вычитанием max для численной стабильности
- Платформа: только **x64** (все три проекта)
- `GraphOptimizationLevel.ORT_ENABLE_ALL` в `SessionOptions`

## Команды сборки и запуска

```bash
# Сборка
dotnet build FaceAntiSpoof.slnx -c Release

# Запуск приложения
dotnet run --project FaceAntiSpoof.WinForms -c Release

# Запуск всех тестов
dotnet test FaceAntiSpoof.Tests

# Запуск одного теста
dotnet test FaceAntiSpoof.Tests --filter "FullyQualifiedName~TestName"
```

## Соглашения

- Все комментарии и документация на русском языке
- Не использовать `_` в начале названий полей, свойств и переменных класса
- Интерфейсы сервисов в `Services/Interfaces/`
- Сервисы реализуют `IDisposable` для освобождения нативных ресурсов
