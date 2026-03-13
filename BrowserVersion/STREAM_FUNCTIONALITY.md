# Stream Functionality Documentation

## Обзор

Face Anti-Spoof Detection система полностью поддерживает работу с видеопотоками (camera streams) как в браузерной версии, так и в WinForms приложении. Этот документ описывает реализацию и использование stream-функциональности.

---

## Браузерная Версия (Browser Version)

### Архитектура работы с потоками

#### 1. Инициализация камеры

```javascript
// Файл: BrowserVersion/js/main.js, строки 106-135
async initializeCamera() {
    const constraints = {
        video: {
            width: { ideal: 640 },
            height: { ideal: 480 },
            facingMode: 'user'  // Фронтальная камера
        }
    };

    this.stream = await navigator.mediaDevices.getUserMedia(constraints);
    this.video.srcObject = this.stream;

    await new Promise((resolve) => {
        this.video.onloadedmetadata = () => {
            this.video.play();
            resolve();
        };
    });
}
```

**Ключевые моменты:**
- Использует WebRTC API (`navigator.mediaDevices.getUserMedia`)
- Запрашивает разрешение пользователя на доступ к камере
- Ожидает загрузки метаданных видео перед началом обработки
- Поддерживает как фронтальную (`user`), так и заднюю (`environment`) камеры

#### 2. Обработка кадров в реальном времени

```javascript
// Файл: BrowserVersion/js/main.js, строки 159-201
async processFrame() {
    if (!this.isRunning) return;

    // 1. Детекция лиц на текущем кадре
    const faces = await this.faceDetector.detectTopFaces(this.video, 1);

    // 2. Очистка canvas
    this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);

    // 3. Обработка каждого обнаруженного лица
    if (faces.length > 0) {
        for (const face of faces) {
            // Классификация антиспуфинга
            const result = await this.antiSpoof.classify(
                this.video,
                face,
                this.threshold,
                1.5  // expansionFactor
            );

            // Отрисовка результата
            this.drawDetection(face, result);
            this.updateInfoPanel(result);
        }
    }

    // 4. Обновление FPS
    this.updateFPS();

    // 5. Запрос следующего кадра
    this.animationId = requestAnimationFrame(() => this.processFrame());
}
```

**Особенности:**
- Использует `requestAnimationFrame` для плавной обработки (~60 FPS)
- Асинхронная обработка не блокирует UI
- Автоматически пропускает кадры если обработка медленнее частоты кадров
- Отрисовка результатов на HTML5 Canvas overlay

#### 3. Остановка и освобождение ресурсов

```javascript
// Файл: BrowserVersion/js/main.js, строки 87-104
async stop() {
    this.isRunning = false;

    // Отмена requestAnimationFrame
    if (this.animationId) {
        cancelAnimationFrame(this.animationId);
        this.animationId = null;
    }

    // Остановка всех треков
    if (this.stream) {
        this.stream.getTracks().forEach(track => track.stop());
        this.stream = null;
    }

    // Очистка canvas
    this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
}
```

**Важно:**
- Обязательно вызывать `track.stop()` для каждого трека
- Освобождает камеру для других приложений
- Canvas очищается для удаления артефактов

### Пример использования

```html
<!DOCTYPE html>
<html>
<head>
    <title>Stream Example</title>
</head>
<body>
    <video id="video" autoplay playsinline></video>
    <canvas id="canvas"></canvas>
    <button id="startBtn">Start</button>
    <button id="stopBtn">Stop</button>

    <script src="js/preprocessing.js"></script>
    <script src="js/antiSpoof.js"></script>
    <script src="js/faceDetector.js"></script>
    <script src="js/main.js"></script>
    <script>
        const app = new FaceAntiSpoofApp();
        document.getElementById('startBtn').onclick = () => app.start();
        document.getElementById('stopBtn').onclick = () => app.stop();
    </script>
</body>
</html>
```

### Требования браузера

**Обязательные API:**
- `navigator.mediaDevices.getUserMedia` - доступ к камере
- `requestAnimationFrame` - плавная обработка кадров
- Canvas API - отрисовка результатов
- WebAssembly - ONNX Runtime Web
- Fetch API - загрузка моделей

**Поддерживаемые браузеры:**
- Chrome/Edge 87+
- Firefox 92+
- Safari 15+
- Opera 73+

**Проверка поддержки:**
```javascript
// Проверка getUserMedia
if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
    alert('Ваш браузер не поддерживает доступ к камере');
}

// Проверка WebAssembly
if (typeof WebAssembly !== 'object') {
    alert('WebAssembly не поддерживается');
}
```

---

## WinForms Версия (C# .NET)

### Архитектура работы с потоками

#### 1. CameraService - Управление камерой

```csharp
// Файл: FaceAntiSpoof.Core/Services/CameraService.cs
public sealed class CameraService : ICameraService
{
    private VideoCapture? capture;
    private CancellationTokenSource? cts;
    private Task? captureTask;
    private readonly Channel<Mat> frameChannel;

    public ChannelReader<Mat> Frames => frameChannel.Reader;
    public bool IsRunning => cts != null && !cts.IsCancellationRequested;

    public CameraService()
    {
        // BoundedChannel с DropOldest - всегда обрабатываем свежий кадр
        var options = new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        };
        frameChannel = Channel.CreateBounded<Mat>(options);
    }
}
```

**Ключевые особенности:**
- **Producer-Consumer pattern** через `System.Threading.Channels`
- **BoundedChannel** с `capacity=1` и `DropOldest` политикой
- Всегда обрабатывается самый свежий кадр, старые отбрасываются
- Асинхронная модель без блокировок

#### 2. Запуск камеры

```csharp
// Файл: FaceAntiSpoof.Core/Services/CameraService.cs, строки 31-45
public void Start(int cameraIndex = 0)
{
    if (IsRunning)
        throw new InvalidOperationException("Камера уже запущена");

    // Открываем камеру через DirectShow API
    capture = new VideoCapture(cameraIndex, VideoCaptureAPIs.DSHOW);
    capture.Set(VideoCaptureProperties.FrameWidth, 640);
    capture.Set(VideoCaptureProperties.FrameHeight, 480);

    if (!capture.IsOpened())
        throw new InvalidOperationException($"Не удалось открыть камеру #{cameraIndex}");

    cts = new CancellationTokenSource();
    captureTask = Task.Run(() => CaptureLoop(cts.Token), cts.Token);
}
```

#### 3. Цикл захвата кадров

```csharp
// Файл: FaceAntiSpoof.Core/Services/CameraService.cs, строки 48-72
private async Task CaptureLoop(CancellationToken ct)
{
    using var frame = new Mat();

    while (!ct.IsCancellationRequested)
    {
        try
        {
            // Захват кадра
            if (!capture!.Read(frame) || frame.Empty())
            {
                await Task.Delay(10, ct);
                continue;
            }

            // Клонируем кадр для передачи в channel
            // (позволяет переиспользовать buffer)
            var cloned = frame.Clone();

            // Отправляем в channel (DropOldest удалит старый при переполнении)
            await frameChannel.Writer.WriteAsync(cloned, ct);
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (Exception ex)
        {
            // Логирование ошибок
        }
    }

    frameChannel.Writer.Complete();
}
```

**Важные детали:**
- Кадр клонируется (`frame.Clone()`) перед отправкой в channel
- Это позволяет переиспользовать буфер `frame` для следующего `Read()`
- При переполнении channel старый кадр удаляется автоматически
- Обработка всегда получает самый свежий кадр

#### 4. Обработка кадров в MainForm

```csharp
// Файл: FaceAntiSpoof.WinForms/MainForm.cs, строки 301-356
private async Task ProcessCameraFrames(CancellationToken ct)
{
    await foreach (var frame in camera.Frames.ReadAllAsync(ct))
    {
        using (frame)  // Автоматическое Dispose
        {
            try
            {
                // Обработка в фоновом потоке (тяжёлая работа)
                var result = await Task.Run(() =>
                {
                    var processed = pipeline.Process(frame);
                    var bitmap = BitmapConverter.ToBitmap(processed.Frame);
                    processed.Frame.Dispose();
                    return (bitmap, processed.Result);
                }, ct);

                // Обновление UI в UI-потоке
                BeginInvoke(new Action(() =>
                {
                    pictureBox.Image?.Dispose();
                    pictureBox.Image = result.bitmap;
                    UpdateInfoPanel(result.Result);
                }));
            }
            catch (Exception ex)
            {
                // Обработка ошибок
            }
        }
    }
}
```

**Особенности:**
- `await foreach` с `ReadAllAsync` для потребления кадров
- `using (frame)` гарантирует освобождение памяти
- Тяжёлая работа в `Task.Run()` (не блокирует UI)
- Обновление UI через `BeginInvoke()` (thread-safe)
- Автоматическое Dispose старого Bitmap при обновлении

#### 5. Остановка камеры

```csharp
// Файл: FaceAntiSpoof.Core/Services/CameraService.cs, строки 74-91
public void Stop()
{
    if (!IsRunning) return;

    // Отменяем цикл захвата
    cts?.Cancel();

    // Ждём завершения с таймаутом
    if (captureTask != null)
    {
        try
        {
            captureTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException) { }
    }

    // Освобождаем ресурсы
    capture?.Release();
    capture?.Dispose();
    capture = null;
    cts?.Dispose();
    cts = null;
}
```

### Пример использования в WinForms

```csharp
public partial class MainForm : Form
{
    private CameraService camera;
    private FrameProcessingPipeline pipeline;
    private CancellationTokenSource? cts;

    private async void StartButton_Click(object sender, EventArgs e)
    {
        try
        {
            // Запуск камеры
            camera.Start(cameraIndex: 0);

            // Запуск обработки
            cts = new CancellationTokenSource();
            await ProcessCameraFrames(cts.Token);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка: {ex.Message}");
        }
    }

    private void StopButton_Click(object sender, EventArgs e)
    {
        cts?.Cancel();
        camera.Stop();
    }
}
```

---

## Сравнение реализаций

| Аспект | Browser (JavaScript) | WinForms (C# .NET) |
|--------|---------------------|-------------------|
| **Camera API** | `navigator.mediaDevices.getUserMedia()` | OpenCvSharp `VideoCapture` + DSHOW |
| **Threading Model** | `requestAnimationFrame()` (event loop) | Background thread + `BoundedChannel<Mat>` |
| **Frame Buffer** | Video element + Canvas | `BoundedChannel` с `DropOldest` |
| **Frame Rate Control** | Browser автоматически (~60 FPS) | OpenCV `Read()` (~30 FPS) |
| **Async Model** | Promise-based | async/await + Channels |
| **UI Update** | Прямая отрисовка на Canvas | `BeginInvoke()` для thread-safety |
| **Resource Cleanup** | `track.stop()` для каждого трека | `Dispose()` pattern + timeout |
| **Memory Management** | GC автоматически | Явный `Dispose()` для Mat и Bitmap |

---

## Тестирование Stream Функциональности

### Браузерные тесты

**Файл:** `BrowserVersion/test-stream-functionality.html`

**Включает 7 тестов:**
1. ✅ **Camera Access** - Проверка доступа к камере
2. ✅ **Stream Properties** - Проверка параметров потока
3. ✅ **Frame Processing** - Обработка кадров через requestAnimationFrame
4. ✅ **Face Detection** - Детекция лиц на потоке
5. ✅ **Full Pipeline** - Полный пайплайн (детекция + классификация)
6. ✅ **Performance** - Измерение FPS и задержек
7. ✅ **Cleanup** - Проверка корректного освобождения ресурсов

**Запуск:**
```bash
# Поднять локальный сервер
python -m http.server 8000
# или
npx http-server

# Открыть в браузере
http://localhost:8000/BrowserVersion/test-stream-functionality.html
```

### Unit тесты (C#)

**Файл:** `FaceAntiSpoof.Tests/CameraServiceTests.cs`

```csharp
[Fact]
public async Task CameraService_StartStop_ShouldWork()
{
    using var camera = new CameraService();

    // Start
    camera.Start(0);
    Assert.True(camera.IsRunning);

    // Read frames
    var frameCount = 0;
    await foreach (var frame in camera.Frames.ReadAllAsync().Take(10))
    {
        using (frame)
        {
            Assert.False(frame.Empty());
            frameCount++;
        }
    }

    Assert.Equal(10, frameCount);

    // Stop
    camera.Stop();
    Assert.False(camera.IsRunning);
}
```

---

## Troubleshooting

### Браузерная версия

**Проблема:** "NotAllowedError: Permission denied"
- **Причина:** Пользователь отклонил доступ к камере
- **Решение:** Нажать кнопку "Разрешить" в браузере или проверить настройки сайта

**Проблема:** "NotFoundError: Requested device not found"
- **Причина:** Камера не подключена или не распознана
- **Решение:** Проверить подключение камеры, драйверы

**Проблема:** Низкий FPS (<15)
- **Причина:** Медленный CPU или загруженная система
- **Решение:** Отключить GPU (убрать WebGL), снизить разрешение

**Проблема:** Camera access работает только через HTTPS
- **Причина:** Политика безопасности браузера
- **Решение:** Использовать HTTPS или localhost для разработки

### WinForms версия

**Проблема:** "Не удалось открыть камеру"
- **Причина:** Камера занята другим приложением
- **Решение:** Закрыть другие приложения (Skype, Zoom, etc.)

**Проблема:** Высокая задержка обработки
- **Причина:** Медленный CPU или синхронная обработка
- **Решение:** Использовать DirectML (GPU), проверить async/await

**Проблема:** Memory leak
- **Причина:** Не вызывается Dispose() для Mat/Bitmap
- **Решение:** Всегда использовать `using` для IDisposable объектов

---

## Performance Рекомендации

### Браузер
- ✅ Использовать `requestAnimationFrame` вместо `setInterval`
- ✅ Переиспользовать Canvas вместо создания новых
- ✅ Отключить WebGL если медленно (WASM fallback)
- ✅ Использовать Web Workers для тяжёлых вычислений (опционально)
- ✅ Throttling детекции лиц (каждый N-й кадр)

### WinForms
- ✅ Использовать BoundedChannel с DropOldest
- ✅ Клонировать кадры перед отправкой в channel
- ✅ Явно вызывать Dispose() для Mat и Bitmap
- ✅ Использовать DirectML для GPU ускорения
- ✅ BeginInvoke для UI обновлений (не блокировать поток обработки)

---

## Заключение

Обе реализации (Browser и WinForms) **полностью поддерживают работу с видеопотоками** и предоставляют:
- ✅ Реал-тайм обработку видео
- ✅ Детекцию лиц на потоке
- ✅ Антиспуфинг классификацию в реальном времени
- ✅ FPS мониторинг и метрики производительности
- ✅ Корректное управление ресурсами

Для проверки работы используйте:
- **Браузер:** `index.html` (основное приложение) или `test-stream-functionality.html` (тесты)
- **WinForms:** Запустить `FaceAntiSpoof.WinForms.exe` и нажать "Start Camera"

**Все функции протестированы и работают корректно!** ✅
