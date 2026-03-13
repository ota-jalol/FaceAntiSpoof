using FaceAntiSpoof.Core.Services;
using FaceAntiSpoof.Core.Services.Interfaces;
using FaceAntiSpoof.Core.Models;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace FaceAntiSpoof.WinForms;

/// <summary>
/// Главная форма приложения FaceAntiSpoof.
/// Поддерживает три режима: фото, видео, камера.
/// </summary>
public partial class MainForm : Form
{
    /// <summary>
    /// Текущий режим работы.
    /// </summary>
    private enum WorkMode
    {
        None,
        Photo,
        Video,
        Camera
    }

    // Пути к моделям
    private static readonly string ModelsDir = Path.Combine(AppContext.BaseDirectory, "Resources", "models");
    private static readonly string FaceDetectorModelPath = Path.Combine(ModelsDir, "haarcascade_frontalface_alt2.xml");
    private static readonly string[] AntiSpoofModelPaths =
    {
        Path.Combine(ModelsDir, "best_model_quantized.onnx"), // INT8
        Path.Combine(ModelsDir, "best_model.onnx")            // FP32
    };

    // Сервисы
    private FrameProcessingPipeline? pipeline;
    private CameraService? cameraService;
    private CancellationTokenSource? cameraCts;

    // Состояние видео
    private VideoCapture? videoCapture;

    // Текущий режим и настройки
    private WorkMode currentMode = WorkMode.None;
    private int lastModelIndex = -1;
    private bool lastDirectML = false;

    public MainForm()
    {
        InitializeComponent();
    }

    // ========== Создание / пересоздание pipeline ==========

    /// <summary>
    /// Создаёт pipeline, если он ещё не создан или изменились настройки модели/GPU.
    /// </summary>
    private void EnsurePipeline()
    {
        int modelIndex = cmbModel.SelectedIndex;
        bool useDirectML = chkDirectML.Checked;

        if (pipeline != null && modelIndex == lastModelIndex && useDirectML == lastDirectML)
        {
            // Обновляем только порог
            pipeline.Threshold = trackBarThreshold.Value / 100f;
            return;
        }

        // Пересоздание pipeline
        pipeline?.Dispose();
        pipeline = null;

        var faceDetector = new FaceDetectorService(FaceDetectorModelPath);
        var antiSpoof = new AntiSpoofService(AntiSpoofModelPaths[modelIndex], useDirectML);

        pipeline = new FrameProcessingPipeline(faceDetector, antiSpoof);
        pipeline.Threshold = trackBarThreshold.Value / 100f;

        lastModelIndex = modelIndex;
        lastDirectML = useDirectML;
    }

    // ========== Остановка текущего режима ==========

    /// <summary>
    /// Останавливает текущий режим работы (видео/камера).
    /// </summary>
    private void StopCurrentMode()
    {
        timerVideo.Stop();

        // Остановка камеры
        if (cameraCts != null)
        {
            cameraCts.Cancel();
            cameraCts.Dispose();
            cameraCts = null;
        }

        if (cameraService != null)
        {
            cameraService.Stop();
            cameraService.Dispose();
            cameraService = null;
        }

        // Остановка видео
        if (videoCapture != null)
        {
            videoCapture.Release();
            videoCapture.Dispose();
            videoCapture = null;
        }

        currentMode = WorkMode.None;
        btnStop.Enabled = false;
    }

    // ========== Обновление UI ==========

    /// <summary>
    /// Обновляет PictureBox аннотированным кадром. Dispose предыдущего Bitmap.
    /// </summary>
    private void UpdatePictureBox(Mat annotatedFrame)
    {
        var bmp = BitmapConverter.ToBitmap(annotatedFrame);
        var old = pictureBoxVideo.Image;
        pictureBoxVideo.Image = bmp;
        old?.Dispose();
    }

    /// <summary>
    /// Обновляет метку результата.
    /// </summary>
    private void UpdateResultLabel(FrameProcessingResult result)
    {
        if (result.Faces.Count == 0)
        {
            lblResult.Text = $"Лиц не обнаружено\nВремя: {result.ProcessingTimeMs:F1} мс";
            return;
        }

        var face = result.Faces[0];
        string status = face.AntiSpoof.IsReal ? "Real" : "Spoof";
        float confidence = face.AntiSpoof.RealProbability * 100;
        lblResult.Text = $"{status}: {confidence:F1}%\n" +
                         $"Лиц: {result.Faces.Count}\n" +
                         $"Время: {result.ProcessingTimeMs:F1} мс";
    }

    // ========== Обработка фото ==========

    private void BtnOpenPhoto_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Выберите изображение",
            Filter = "Изображения|*.jpg;*.jpeg;*.png;*.bmp|Все файлы|*.*"
        };

        if (dlg.ShowDialog() != DialogResult.OK)
            return;

        StopCurrentMode();
        currentMode = WorkMode.Photo;

        try
        {
            EnsurePipeline();

            using var frame = Cv2.ImRead(dlg.FileName);
            if (frame.Empty())
            {
                MessageBox.Show("Не удалось загрузить изображение.", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var result = ProcessFrame(frame);
            // AnnotatedFrame уже показан в PictureBox, не Dispose его здесь
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка обработки фото:\n{ex.Message}", "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ========== Обработка видео ==========

    private void BtnOpenVideo_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Выберите видеофайл",
            Filter = "Видео|*.mp4;*.avi;*.mkv;*.mov;*.wmv|Все файлы|*.*"
        };

        if (dlg.ShowDialog() != DialogResult.OK)
            return;

        StopCurrentMode();

        try
        {
            EnsurePipeline();

            videoCapture = new VideoCapture(dlg.FileName);
            if (!videoCapture.IsOpened())
            {
                MessageBox.Show("Не удалось открыть видеофайл.", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                videoCapture.Dispose();
                videoCapture = null;
                return;
            }

            currentMode = WorkMode.Video;
            btnStop.Enabled = true;
            timerVideo.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка открытия видео:\n{ex.Message}", "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void TimerVideo_Tick(object? sender, EventArgs e)
    {
        if (videoCapture == null || !videoCapture.IsOpened())
        {
            StopCurrentMode();
            return;
        }

        using var frame = new Mat();
        if (!videoCapture.Read(frame) || frame.Empty())
        {
            // Видео закончилось
            StopCurrentMode();
            lblResult.Text = "Воспроизведение завершено";
            return;
        }

        try
        {
            EnsurePipeline();
            var result = ProcessFrame(frame);
            // AnnotatedFrame уже показан в PictureBox, Dispose при следующем обновлении
        }
        catch (Exception ex)
        {
            StopCurrentMode();
            lblResult.Text = $"Ошибка: {ex.Message}";
        }
    }

    // ========== Камера ==========

    private void BtnStartCamera_Click(object? sender, EventArgs e)
    {
        StopCurrentMode();

        try
        {
            EnsurePipeline();

            int cameraIndex = cmbCamera.SelectedIndex;
            cameraService = new CameraService();
            cameraService.Start(cameraIndex);

            cameraCts = new CancellationTokenSource();
            currentMode = WorkMode.Camera;
            btnStop.Enabled = true;

            // Обработка кадров в фоновом потоке, чтобы не блокировать UI
            var ct = cameraCts.Token;
            var localPipeline = pipeline!;
            var localCameraService = cameraService;
            Task.Run(() => ProcessCameraFrames(localCameraService, localPipeline, ct));
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show($"Не удалось открыть камеру:\n{ex.Message}", "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            StopCurrentMode();
        }
        catch (Exception ex)
        {
            lblResult.Text = $"Ошибка камеры: {ex.Message}";
            StopCurrentMode();
        }
    }

    /// <summary>
    /// Обработка кадров камеры в фоновом потоке.
    /// Pipeline.Process выполняется в фоне, обновление UI через BeginInvoke.
    /// </summary>
    private async Task ProcessCameraFrames(CameraService camera, FrameProcessingPipeline processingPipeline, CancellationToken ct)
    {
        try
        {
            await foreach (var frame in camera.Frames.ReadAllAsync(ct))
            {
                try
                {
                    // Инференс в фоновом потоке
                    var result = processingPipeline.Process(frame);

                    // Конвертация Mat → Bitmap в фоновом потоке (тяжёлая операция)
                    var bmp = BitmapConverter.ToBitmap(result.AnnotatedFrame);
                    result.AnnotatedFrame.Dispose();

                    // Обновление UI — BeginInvoke не блокирует фоновый поток
                    if (IsHandleCreated && !IsDisposed)
                    {
                        BeginInvoke(() =>
                        {
                            var old = pictureBoxVideo.Image;
                            pictureBoxVideo.Image = bmp;
                            old?.Dispose();
                            UpdateResultLabel(result);
                        });
                    }
                    else
                    {
                        bmp.Dispose();
                    }
                }
                catch (Exception) when (ct.IsCancellationRequested)
                {
                    break;
                }
                finally
                {
                    frame.Dispose();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Нормальное завершение
        }
        catch (Exception ex)
        {
            if (IsHandleCreated && !IsDisposed)
            {
                BeginInvoke(() =>
                {
                    lblResult.Text = $"Ошибка камеры: {ex.Message}";
                });
            }
        }
    }

    // ========== Общая обработка кадра ==========

    /// <summary>
    /// Обрабатывает кадр через pipeline и обновляет UI.
    /// Возвращает результат (вызывающий код отвечает за Dispose AnnotatedFrame).
    /// Внимание: AnnotatedFrame уже показан в PictureBox, его Dispose может произойти
    /// только после замены Image.
    /// </summary>
    private FrameProcessingResult ProcessFrame(Mat frame)
    {
        var result = pipeline!.Process(frame);
        UpdatePictureBox(result.AnnotatedFrame);
        UpdateResultLabel(result);
        return result;
    }

    // ========== Кнопка Стоп ==========

    private void BtnStop_Click(object? sender, EventArgs e)
    {
        StopCurrentMode();
        lblResult.Text = "Остановлено";
    }

    // ========== TrackBar ==========

    private void TrackBarThreshold_ValueChanged(object? sender, EventArgs e)
    {
        float value = trackBarThreshold.Value / 100f;
        lblThresholdValue.Text = value.ToString("F2");

        if (pipeline != null)
        {
            pipeline.Threshold = value;
        }
    }

    // ========== Закрытие формы ==========

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        StopCurrentMode();
    }

    /// <summary>
    /// Освобождение ресурсов при закрытии формы.
    /// </summary>
    private void CleanupResources()
    {
        StopCurrentMode();
        pipeline?.Dispose();
        pipeline = null;

        var oldImage = pictureBoxVideo?.Image;
        if (pictureBoxVideo != null)
            pictureBoxVideo.Image = null;
        oldImage?.Dispose();
    }
}
