# Техническое задание: портирование face-antispoof-onnx на C# .NET 10 WinForms

Система распознавания лицевого антиспуфинга на базе модели **MiniFASNetV2SE** (600 КБ, точность **98.20%**) портируется из Python в нативное Windows-приложение на C# .NET 10 WinForms с использованием ONNX Runtime. Проект закрывает пробел в экосистеме .NET — на данный момент **не существует ни одного C#-порта MiniFASNet**, а единственная альтернатива (FaceONNX) использует менее точный подход на основе глубины. Система должна поддерживать два режима: тестирование по фото/видеофайлам и продуктивная работа с веб-камерой в реальном времени. Целевая платформа — Windows x64.

---

## 1. Анализ исходного проекта face-antispoof-onnx

### Репозиторий и лицензирование

Исходный проект размещён на GitHub: **[SuriAI/face-antispoof-onnx](https://github.com/SuriAI/face-antispoof-onnx)**, лицензия **Apache-2.0** (свободное коммерческое использование). Проект основан на архитектуре [Silent-Face-Anti-Spoofing](https://github.com/minivision-ai/Silent-Face-Anti-Spoofing) от Minivision AI (小视科技, ~1 600 звёзд). Релиз v1.0.0 «MiniFASNet V2 SE (98.20% Acc)» опубликован 3 января 2026 года. Python 3.8+, зависимости: `opencv-python`, `onnxruntime`, `numpy`.

### Структура каталогов

```
face-antispoof-onnx/
├── demo.py                          # Точка входа (веб-камера и изображения)
├── requirements.txt                 # Зависимости runtime
├── requirements_dev.txt             # Зависимости для обучения
├── models/
│   ├── best_model.pth               # PyTorch-модель (1.95 МБ)
│   ├── best_model.onnx              # ONNX FP32 (1.82 МБ)
│   └── best_model_quantized.onnx    # ONNX INT8 — продуктивная модель (600 КБ)
├── src/
│   ├── detection/
│   │   └── face.py                  # Детекция лиц (load_detector, detect)
│   ├── inference/
│   │   ├── loader.py                # Загрузка ONNX-сессии
│   │   ├── inference.py             # Инференс и обработка логитов
│   │   ├── preprocess.py            # Кроп, ресайз, нормализация
│   │   └── system.py                # Информация о CPU/GPU
│   └── minifasv2/                   # Код обучения MiniFASV2SE
├── scripts/                         # Экспорт, квантизация, подготовка данных
├── docs/                            # Техническая документация
└── assets/                          # Демо-материалы
```

### Полный пайплайн инференса

Поток данных в оригинальном Python-проекте выглядит следующим образом:

**Камера/Файл → Детекция лица → Расширение BBox → Кроп → Resize 128×128 → BGR→RGB → Нормализация /255 → HWC→CHW → ONNX-инференс → Softmax → Пороговое решение → Отрисовка (зелёная рамка = реальное лицо, красная = спуфинг)**

Ключевые параметры пайплайна:

| Параметр | Значение |
|----------|----------|
| Размер входного тензора | `[1, 3, 128, 128]` (NCHW, float32) |
| Размер выходного тензора | `[1, 2]` (логиты: spoof, real) |
| Нормализация пикселей | Деление на 255.0 → диапазон [0.0, 1.0] |
| Постобработка | Softmax → бинарное решение |
| Коэффициент расширения BBox | 1.5× (при подготовке данных) |
| Порог решения | 0.5 (настраиваемый) |
| Число классов | 2 (Real / Spoof) |

---

## 2. Спецификации ONNX-моделей

### Модель антиспуфинга MiniFASNetV2SE

Архитектура MiniFASNetV2SE представляет собой облегчённую модификацию MobileFaceNet с блоками Squeeze-and-Excitation (SE). Модель содержит **~435 тысяч параметров** и требует **~0.044 GFLOPS** — это позволяет выполнять инференс в реальном времени даже на CPU.

Архитектурная цепочка: `Conv 3→32 (stride 2) → DepthWise → Depth_Wise 32→64 (stride 2) → ResidualSE ×4 → Depth_Wise 64→128 (stride 2) → ResidualSE ×6 → Depth_Wise 128→128 (stride 2) → ResidualSE ×2 → Conv 1×1 128→512 → DepthWise Spatial Collapse → Flatten → FC 512→128 → BN → Dropout(0.75) → FC 128→2`

Блоки ResidualSE добавляют канальное внимание через AdaptiveAvgPool → FC → ReLU → FC → Sigmoid → Scale. **Ветка Фурье-преобразования (FTGenerator) используется только при обучении** и отсутствует в ONNX-модели.

**Входной тензор:**

| Свойство | Значение |
|----------|----------|
| Имя | `input` (получить через `session.InputMetadata`) |
| Форма | `[1, 3, 128, 128]` (batch × channels × height × width) |
| Тип данных | `float32` |
| Цветовое пространство | RGB |
| Диапазон значений | [0.0, 1.0] |

**Выходной тензор:**

| Свойство | Значение |
|----------|----------|
| Форма | `[1, 2]` |
| Тип данных | `float32` |
| Значения | Сырые логиты (требуется softmax) |
| Индекс 0 | Вероятность спуфинга |
| Индекс 1 | Вероятность реального лица |

**Модели для скачивания:**

| Файл | Размер | Источник | Назначение |
|------|--------|----------|------------|
| `best_model_quantized.onnx` | 600 КБ | [SuriAI/face-antispoof-onnx/models](https://github.com/SuriAI/face-antispoof-onnx/tree/main/models) | **Продуктивное развёртывание** (INT8) |
| `best_model.onnx` | 1.82 МБ | Тот же репозиторий | Эталон для отладки (FP32) |
| `MiniFASNetV2.onnx` | ~1.7 МБ | [yakhyo/face-anti-spoofing/releases](https://github.com/yakhyo/face-anti-spoofing/releases/tag/weights) | Альтернативная версия (80×80) |
| `MiniFASNetV1SE.onnx` | ~1.7 МБ | Тот же репозиторий | Модель для мульти-масштабного слияния |

### Модель детекции лиц

Оригинальный Python-проект использует OpenCV DNN-детектор лиц. Для C#-реализации рекомендуется **YuNet** — ультралёгкий детектор, встроенный в OpenCV 4.x+.

**YuNet (рекомендуемый детектор):**

| Свойство | Значение |
|----------|----------|
| Размер модели | ~400 КБ |
| Входная форма | `[1, 3, H, W]` (настраиваемый, типично 320×320 или 640×640) |
| Выход | Координаты BBox + 5 ландмарков + confidence |
| Скорость | Очень быстрый на CPU |
| Доступ | OpenCV Zoo / HuggingFace |
| Лицензия | MIT |

Альтернативы: **UltraFace-RFB-320** (~1 МБ, MIT, ONNX доступен), **RetinaFace-MobileNet** (~1.7 МБ, MIT, высокая точность), **SCRFD** (~2.5 МБ, InsightFace, state-of-the-art).

---

## 3. Выбор библиотек C# .NET 10

### Основной стек NuGet-пакетов

| Назначение | Пакет | Версия | Лицензия | Обоснование |
|-----------|-------|--------|----------|-------------|
| ONNX-инференс | `Microsoft.ML.OnnxRuntime` | **1.24.3** | MIT | Референсная реализация, поддержка .NET Standard 2.0+, .NET 10 |
| GPU-ускорение | `Microsoft.ML.OnnxRuntime.DirectML` | **1.24.2** | MIT | Работает с любым DX12-GPU без дополнительных зависимостей |
| Обработка изображений | `OpenCvSharp4` | **4.13.0.20260308** | Apache-2.0 | Полный OpenCV 4.13: resize, cvtColor, нормализация |
| Нативные библиотеки | `OpenCvSharp4.runtime.win` | **4.13.0.20260214** | Apache-2.0 | Win-x64 бинарники OpenCV |
| Интеграция с WinForms | `OpenCvSharp4.Extensions` | **4.11.0.20250507** | Apache-2.0 | `BitmapConverter.ToBitmap()` для PictureBox |

### Обоснование выбора OpenCvSharp4

Анализ трёх кандидатов показал явное преимущество OpenCvSharp4 для данной задачи:

**OpenCvSharp4** покрывает все потребности проекта в одном пакете: захват камеры (`VideoCapture`), предобработка изображений (`Cv2.Resize`, `Cv2.CvtColor`), встроенная детекция лиц (`FaceDetectorYN` для YuNet), конвертация для WinForms (`BitmapConverter`). Лицензия Apache-2.0 не создаёт ограничений для коммерческого использования. Нативный C++-бэкенд обеспечивает максимальную производительность обработки кадров.

**SixLabors.ImageSharp** (v3.1.12) отклонён из-за **Split License** — при годовом доходе компании ≥$1M USD требуется коммерческая лицензия. Кроме того, отсутствует захват видео и функции компьютерного зрения.

**SkiaSharp** (v3.119.2, MIT) пригоден для отрисовки оверлеев, но не предоставляет конвертацию цветовых пространств и захват камеры. Может использоваться опционально для рендеринга.

### DirectML vs CUDA для GPU-ускорения

Для данного проекта рекомендуется **DirectML** как основной GPU-провайдер:

- Работает с **любым GPU** (NVIDIA, AMD, Intel) через DirectX 12 — без установки CUDA Toolkit
- Поставляется с Windows 10+ — **нулевые дополнительные зависимости** для развёртывания
- Пакет развёртывания CUDA составляет ~2.6 ГБ против 0 у DirectML
- Для модели размером 600 КБ разница в производительности между CUDA и DirectML минимальна
- **CPU-инференс достаточен** для одного лица — MiniFASNetV2SE выполняется за ~5-20 мс на современном CPU

Рекомендуемая стратегия: CPU по умолчанию, DirectML как опциональное ускорение.

---

## 4. Архитектура приложения

### Структура проекта

```
FaceAntiSpoof/
├── FaceAntiSpoof.sln
├── FaceAntiSpoof.Core/                    # Библиотека классов (.NET 10)
│   ├── Models/
│   │   ├── FaceDetectionResult.cs         # BBox, confidence, landmarks
│   │   ├── AntiSpoofResult.cs             # IsReal, confidence, label
│   │   └── FrameProcessingResult.cs       # Составной результат обработки кадра
│   ├── Services/
│   │   ├── Interfaces/
│   │   │   ├── IFaceDetectorService.cs    # Контракт детекции лиц
│   │   │   ├── IAntiSpoofService.cs       # Контракт антиспуфинга
│   │   │   └── ICameraService.cs          # Контракт захвата камеры
│   │   ├── FaceDetectorService.cs         # ONNX YuNet детекция
│   │   ├── AntiSpoofService.cs            # ONNX MiniFASNet инференс
│   │   ├── CameraService.cs               # OpenCvSharp VideoCapture
│   │   └── FrameProcessingPipeline.cs     # Оркестрация: detect → crop → classify
│   ├── Preprocessing/
│   │   ├── ImagePreprocessor.cs           # Resize, нормализация, CHW-конверсия
│   │   └── BoundingBoxHelper.cs           # Расширение и нормализация BBox
│   └── Infrastructure/
│       ├── OnnxSessionManager.cs          # Управление жизненным циклом сессий
│       └── TensorHelper.cs                # Создание и управление OrtValue-буферами
│
├── FaceAntiSpoof.WinForms/               # WinForms-приложение (.NET 10)
│   ├── MainForm.cs                        # Главная форма с PictureBox и управлением
│   ├── MainForm.Designer.cs
│   ├── Controls/
│   │   └── VideoDisplayControl.cs         # Отрисовка кадров с оверлеями
│   └── Program.cs
│
├── FaceAntiSpoof.Tests/                   # Юнит и интеграционные тесты
│   ├── PreprocessingTests.cs
│   ├── FaceDetectionTests.cs
│   ├── AntiSpoofInferenceTests.cs
│   └── PipelineIntegrationTests.cs
│
└── Resources/
    ├── models/
    │   ├── best_model_quantized.onnx      # Модель антиспуфинга (600 КБ)
    │   ├── best_model.onnx                # Полная модель для отладки (1.82 МБ)
    │   └── face_detection_yunet.onnx      # Детектор лиц YuNet (~400 КБ)
    └── test_data/
        ├── real/                           # Фото реальных лиц
        └── spoof/                          # Фото спуфинг-атак
```

### Ключевые архитектурные решения

**Разделение ответственности (Separation of Concerns).** Проект разбит на Core-библиотеку (вся логика инференса, предобработки, детекции) и WinForms-приложение (только UI). Это позволяет переиспользовать Core в других типах приложений (WPF, MAUI, консольное).

**Асинхронная обработка кадров.** Между захватом камеры и инференсом используется `System.Threading.Channels.BoundedChannel<Mat>` с ёмкостью **1 кадр** и политикой `DropOldest`. Это гарантирует, что инференс всегда обрабатывает свежий кадр, а не копит очередь устаревших:

```csharp
// Инициализация канала
private readonly Channel<Mat> _frameChannel = 
    Channel.CreateBounded<Mat>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
        SingleWriter = true
    });

// Продюсер (поток камеры)
_frameChannel.Writer.TryWrite(frame.Clone());

// Потребитель (поток инференса)
await foreach (var frame in _frameChannel.Reader.ReadAllAsync(ct))
{
    var result = _pipeline.Process(frame);
    this.Invoke(() => UpdateUI(result));
    frame.Dispose();
}
```

**Управление памятью.** `InferenceSession` создаётся **однократно** при старте и переиспользуется для всех кадров — создание сессии дорогостоящая операция. Входные и выходные буферы `OrtValue` **преаллоцируются** и переиспользуются через `OrtValue.CreateTensorValueFromMemory()` для минимизации GC-давления. Каждый объект `Mat` и `Bitmap` явно вызывает `Dispose()` после обработки — иначе происходит утечка GDI+-хендлов.

### Паттерн инференс-сервиса

```csharp
public sealed class AntiSpoofService : IAntiSpoofService, IDisposable
{
    private readonly InferenceSession _session;
    private readonly float[] _inputBuffer;   // преаллоцированный [1×3×128×128]
    private readonly float[] _outputBuffer;  // преаллоцированный [1×2]
    private readonly string _inputName;

    public AntiSpoofService(string modelPath, bool useGpu = false)
    {
        var options = new SessionOptions();
        options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
        if (useGpu) options.AppendExecutionProvider_DML(0);
        
        _session = new InferenceSession(modelPath, options);
        _inputName = _session.InputNames[0];
        _inputBuffer = new float[1 * 3 * 128 * 128];
        _outputBuffer = new float[2];
    }

    public AntiSpoofResult Classify(Mat faceCrop)
    {
        Preprocess(faceCrop, _inputBuffer);
        
        using var inputOrt = OrtValue.CreateTensorValueFromMemory(
            _inputBuffer, new long[] { 1, 3, 128, 128 });
        var inputs = new Dictionary<string, OrtValue> { { _inputName, inputOrt } };
        
        using var results = _session.Run(new RunOptions(), inputs, _session.OutputNames);
        var logits = results[0].GetTensorDataAsSpan<float>();
        
        var (realProb, spoofProb) = Softmax(logits[0], logits[1]);
        return new AntiSpoofResult(realProb > 0.5f, realProb);
    }

    public void Dispose() => _session?.Dispose();
}
```

---

## 5. Поэтапный план реализации

### Фаза 1: Инициализация проекта (1-2 дня)

Создание Solution с тремя проектами (Core, WinForms, Tests). Установка NuGet-пакетов: `Microsoft.ML.OnnxRuntime` (1.24.3), `OpenCvSharp4.Windows` (4.13.0), `OpenCvSharp4.Extensions` (4.11.0). Опционально `Microsoft.ML.OnnxRuntime.DirectML` (1.24.2) для GPU. Настройка `.csproj` для .NET 10, `x64` Platform Target. Скачивание и размещение ONNX-моделей в `Resources/models/` с `CopyToOutputDirectory = PreserveNewest`. Верификация загрузки моделей — создание `InferenceSession` и вывод `InputMetadata`/`OutputMetadata` в консоль.

**Критерий завершения:** модели загружаются без ошибок, метаданные тензоров совпадают со спецификацией.

### Фаза 2: Пайплайн детекции лиц (3-4 дня)

Реализация `FaceDetectorService` с использованием **OpenCvSharp4 FaceDetectorYN** (встроенный YuNet-детектор):

```csharp
var detector = FaceDetectorYN.Create(
    model: "face_detection_yunet.onnx",
    config: "",
    inputSize: new Size(320, 320),
    scoreThreshold: 0.7f,
    nmsThreshold: 0.3f,
    topK: 5000);
```

Реализация `ImagePreprocessor` с полной цепочкой предобработки для антиспуфинга:

1. Получение BBox от детектора → расширение на коэффициент **1.5×** через `BoundingBoxHelper`
2. `Cv2.Resize(crop, new Size(128, 128), interpolation: InterpolationFlags.Linear)`
3. `Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB)`
4. Попиксельное деление на 255.0f → float32
5. Транспозиция HWC [128, 128, 3] → CHW [3, 128, 128]
6. Упаковка в одномерный массив `float[1 × 3 × 128 × 128]`

**Критерий завершения:** детектор находит лица на тестовых изображениях, предобработанные тензоры соответствуют ожидаемой форме.

### Фаза 3: Пайплайн антиспуфинга (3-4 дня)

Реализация `AntiSpoofService` по паттерну, описанному в разделе архитектуры. Реализация Softmax-постобработки:

```csharp
private static (float real, float spoof) Softmax(float logitSpoof, float logitReal)
{
    float maxVal = Math.Max(logitSpoof, logitReal);
    float expSpoof = MathF.Exp(logitSpoof - maxVal);
    float expReal = MathF.Exp(logitReal - maxVal);
    float sum = expSpoof + expReal;
    return (expReal / sum, expSpoof / sum);
}
```

Реализация `FrameProcessingPipeline` — оркестратор: принимает `Mat`-кадр, вызывает детекцию, для каждого найденного лица выполняет кроп → предобработку → инференс → возвращает `FrameProcessingResult` с рамками и метками.

**Критерий завершения:** пайплайн корректно классифицирует тестовые изображения реальных лиц и спуфинг-атак с точностью ≥95%.

### Фаза 4: WinForms UI (4-5 дней)

Главная форма `MainForm` содержит:

- **PictureBox** для отображения видеопотока с наложенными рамками и метками
- **Панель управления**: кнопки «Открыть фото», «Открыть видео», «Запустить камеру», «Остановить»
- **Панель результатов**: индикатор Real/Spoof, значение confidence, время инференса (мс)
- **Настройки**: выбор модели (FP32/INT8), выбор провайдера (CPU/DirectML), порог решения (ползунок 0.0–1.0), выбор камеры (ComboBox с перечислением устройств)

Режимы работы:

- **Фото**: загрузка файла через `OpenFileDialog` → однократная обработка → отображение результата
- **Видео**: загрузка файла → покадровая обработка через `VideoCapture` с таймером → отображение потока
- **Камера**: `VideoCapture(cameraIndex)` → непрерывная обработка через `BoundedChannel` + фоновый поток

Для отрисовки оверлеев (рамки, надписи) используется `Cv2.Rectangle()` и `Cv2.PutText()` непосредственно на `Mat`-кадре перед конвертацией в `Bitmap`.

**Критерий завершения:** UI отзывчив при обработке 30 FPS камеры, все три режима работы функционируют.

### Фаза 5: Тестирование (3-4 дня)

Подробный план тестирования описан в разделе 6.

### Фаза 6: Оптимизация и продуктивная готовность (2-3 дня)

- Профилирование производительности: замер latency каждого этапа (детекция, предобработка, инференс, постобработка, отрисовка)
- Переключение на INT8-модель (`best_model_quantized.onnx`) для продуктива
- Включение `GraphOptimizationLevel.ORT_ENABLE_ALL` в `SessionOptions`
- Пул буферов через `ArrayPool<float>.Shared` для уменьшения аллокаций
- Graceful shutdown: `CancellationTokenSource` для корректной остановки потоков камеры и инференса
- Логирование через `Microsoft.Extensions.Logging` (уровни: Info — результаты классификации, Warning — потеря кадров, Error — сбои модели)
- Обработка исключений: `OnnxRuntimeException`, `OpenCVException`, `InvalidOperationException` при отсутствии камеры
- Вынос настроек (пути к моделям, порог, индекс камеры) в `appsettings.json`

---

## 6. Стратегия тестирования

### Юнит-тесты предобработки

Тесты проверяют математическую корректность каждого шага предобработки изолированно:

- **TestResize**: подача изображения 640×480 → проверка выходного размера 128×128
- **TestColorConversion**: подача BGR-пикселя (255, 0, 0) → проверка RGB (0, 0, 255)
- **TestNormalization**: подача пикселя 128 → проверка значения 128/255 ≈ 0.502
- **TestTranspose**: подача HWC-массива [H, W, 3] → проверка CHW-массива [3, H, W] с корректным порядком каналов
- **TestBBoxExpansion**: подача BBox (100, 100, 50, 50) с коэффициентом 1.5 → проверка расширенного BBox (87, 87, 75, 75) с клиппингом по границам изображения

### Юнит-тесты инференса

- **TestModelLoading**: загрузка `best_model_quantized.onnx`, проверка `InputMetadata` (имя, форма `[1,3,128,128]`, тип float32) и `OutputMetadata` (форма `[1,2]`)
- **TestSoftmax**: подача логитов [2.0, 5.0] → проверка softmax-выхода с точностью до 1e-5
- **TestDeterminism**: 10 прогонов одного изображения → проверка идентичности результатов
- **TestPerformance**: замер времени инференса 100 кадров → среднее < 50 мс на CPU

### Тестирование с реальными изображениями

Рекомендуемые публичные датасеты для тестирования:

| Датасет | Размер | Типы атак | Доступ |
|---------|--------|-----------|--------|
| **CelebA-Spoof** | 625 537 изображений, 10 177 субъектов | Печать, экран, 3D-маска | GitHub / Kaggle (некоммерческий) |
| **NUAA** | 12 614 изображений | Печать | Открытый |
| **Replay-Attack** | 1 200 видео, 50 субъектов | Печать, экран | Idiap, открытый |
| **CASIA-FASD** | 400 видео, 50 субъектов | Печать, экран | По запросу |

Минимальный собственный тестовый набор (для CI/CD): **20 реальных фотографий + 20 спуфинг-фотографий** (10 печать на бумаге, 10 экран телефона/монитора). Изображения разного разрешения (480p, 720p, 1080p), освещения и угла поворота.

### Тестирование спуфинг-атак

**Атака печатью:** распечатать фото на бумаге A4 (цветной лазерный, струйный принтер), предъявить камере на расстоянии 30-50 см. Тестировать плоскую и изогнутую бумагу.

**Атака экраном:** отобразить фото/видео лица на экране смартфона, планшета, монитора. Варьировать яркость экрана и расстояние до камеры.

**Ожидаемые метрики:** точность (accuracy) ≥ 95%, AUC-ROC ≥ 0.99, FAR (False Accept Rate) < 5%, FRR (False Reject Rate) < 5%.

### Интеграционное тестирование

- **TestFullPipeline_RealFace**: подача фото реального лица → результат `IsReal = true` с confidence > 0.7
- **TestFullPipeline_PrintAttack**: подача фото печатного спуфинга → результат `IsReal = false`
- **TestFullPipeline_NoFace**: подача изображения без лиц → пустой список результатов
- **TestFullPipeline_MultipleFaces**: подача изображения с 3+ лицами → корректная классификация каждого
- **TestWebcamCapture**: запуск камеры на 5 секунд → получение ≥ 100 кадров без утечек памяти

### Бенчмарк производительности

| Этап | Целевое время (CPU) | Целевое время (DirectML) |
|------|---------------------|--------------------------|
| Детекция лица (YuNet, 320×320) | < 15 мс | < 5 мс |
| Предобработка (crop + resize + normalize) | < 3 мс | — |
| Антиспуфинг-инференс (INT8, 128×128) | < 20 мс | < 5 мс |
| Отрисовка оверлея | < 2 мс | — |
| **Полный пайплайн** | **< 40 мс (25+ FPS)** | **< 15 мс (60+ FPS)** |

---

## 7. Реализация ключевых компонентов

### Предобработка изображения для антиспуфинга

```csharp
public static class ImagePreprocessor
{
    public static float[] PreprocessFace(Mat bgr, Rect bbox, float expansionFactor = 1.5f)
    {
        // 1. Расширение BBox
        var expanded = BoundingBoxHelper.Expand(bbox, expansionFactor, bgr.Size());
        
        // 2. Кроп лица
        using var crop = new Mat(bgr, expanded);
        
        // 3. Resize до 128×128
        using var resized = new Mat();
        Cv2.Resize(crop, resized, new Size(128, 128), interpolation: InterpolationFlags.Linear);
        
        // 4. BGR → RGB
        using var rgb = new Mat();
        Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);
        
        // 5. Нормализация и транспозиция в float[1×3×128×128]
        var tensor = new float[1 * 3 * 128 * 128];
        rgb.GetArray(out byte[] pixels); // HWC формат, 128*128*3 байт
        
        for (int c = 0; c < 3; c++)
            for (int h = 0; h < 128; h++)
                for (int w = 0; w < 128; w++)
                    tensor[c * 128 * 128 + h * 128 + w] = 
                        pixels[(h * 128 + w) * 3 + c] / 255.0f;
        
        return tensor;
    }
}
```

### Захват камеры с асинхронной обработкой

```csharp
public sealed class CameraService : ICameraService, IDisposable
{
    private VideoCapture? _capture;
    private CancellationTokenSource? _cts;
    private readonly Channel<Mat> _frames;

    public CameraService()
    {
        _frames = Channel.CreateBounded<Mat>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });
    }

    public void Start(int cameraIndex = 0)
    {
        _capture = new VideoCapture(cameraIndex);
        _capture.Set(VideoCaptureProperties.FrameWidth, 640);
        _capture.Set(VideoCaptureProperties.FrameHeight, 480);
        _cts = new CancellationTokenSource();
        
        Task.Run(() => CaptureLoop(_cts.Token));
    }

    private async Task CaptureLoop(CancellationToken ct)
    {
        using var frame = new Mat();
        while (!ct.IsCancellationRequested)
        {
            if (_capture!.Read(frame) && !frame.Empty())
                _frames.Writer.TryWrite(frame.Clone());
            await Task.Delay(1, ct); // ~30 FPS контроль
        }
    }

    public ChannelReader<Mat> Frames => _frames.Reader;

    public void Stop() => _cts?.Cancel();
    public void Dispose() { Stop(); _capture?.Dispose(); }
}
```

---

## 8. Аналогичные проекты и что из них взять

**FaceONNX** (GitHub, 268 звёзд, MIT) — единственная зрелая C#-библиотека для анализа лиц на ONNX Runtime. Использует паттерн «один класс = один анализатор» с `IDisposable`. Включает антиспуфинг, но на основе оценки глубины (менее точный подход, чем текстурный анализ MiniFASNet). **Что взять:** архитектура сервисного слоя, управление `InferenceSession`, структура NuGet-пакета.

**Silent-Face-Anti-Spoofing** (GitHub, ~1 600 звёзд, Apache-2.0) — оригинальный Python-проект, определяющий архитектуру MiniFASNet. Использует мульти-масштабное слияние (scales 1.0, 2.7, 4.0). **Что взять:** алгоритм расширения BBox, параметры предобработки, логика fusion при необходимости мульти-масштабного подхода.

**yakhyo/face-anti-spoofing** (GitHub, Apache-2.0) — упрощённая Python-реализация с готовыми ONNX-файлами MiniFASNetV1SE и V2. **Что взять:** скачать ONNX-модели как запасной вариант; проверить совместимость 80×80 vs 128×128 моделей.

---

## Заключение

Данное ТЗ описывает полный цикл портирования Python-проекта face-antispoof-onnx в нативное Windows-приложение. Три ключевых вывода отличают этот проект от существующих решений. Во-первых, стек **OpenCvSharp4 + ONNX Runtime + MiniFASNetV2SE** позволяет уместить весь инференс-пайплайн (детекция + антиспуфинг) в **~1 МБ моделей** с латентностью **< 40 мс на CPU**. Во-вторых, архитектура с `BoundedChannel` и преаллоцированными буферами решает типичную проблему WinForms-приложений — фризы UI при непрерывной обработке видео. В-третьих, **все компоненты используют MIT/Apache-2.0 лицензии**, что снимает риски для коммерческого развёртывания. Общая оценка трудозатрат на реализацию — **16-22 рабочих дня** для одного C#-разработчика среднего уровня.