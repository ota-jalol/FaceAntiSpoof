using System.Diagnostics;
using OpenCvSharp;
using FaceAntiSpoof.Core.Models;
using FaceAntiSpoof.Core.Services.Interfaces;

namespace FaceAntiSpoof.Core.Services;

/// <summary>
/// Оркестратор: для каждого кадра выполняет детекцию → кроп → антиспуфинг → аннотацию.
/// </summary>
public sealed class FrameProcessingPipeline : IDisposable
{
    private readonly IFaceDetectorService faceDetector;
    private readonly IAntiSpoofService antiSpoof;
    private float threshold = 0.5f;

    public float Threshold
    {
        get => threshold;
        set => threshold = value;
    }

    public FrameProcessingPipeline(IFaceDetectorService faceDetector, IAntiSpoofService antiSpoof)
    {
        this.faceDetector = faceDetector;
        this.antiSpoof = antiSpoof;
    }

    /// <summary>
    /// Обрабатывает один кадр: детекция лиц → антиспуфинг → отрисовка оверлеев.
    /// </summary>
    /// <param name="frame">Входной BGR-кадр. Не модифицируется.</param>
    /// <returns>Результат с аннотированным кадром (вызывающий код отвечает за Dispose).</returns>
    public FrameProcessingResult Process(Mat frame)
    {
        var sw = Stopwatch.StartNew();

        // 1. Детекция лиц
        var detections = faceDetector.Detect(frame);

        // 2. Классификация каждого лица
        var faces = new List<FaceClassificationEntry>(detections.Count);
        foreach (var detection in detections)
        {
            var spoofResult = antiSpoof.Classify(frame, detection.BoundingBox, threshold);
            faces.Add(new FaceClassificationEntry
            {
                Detection = detection,
                AntiSpoof = spoofResult
            });
        }

        // 3. Аннотация кадра
        var annotated = frame.Clone();
        DrawOverlays(annotated, faces);

        sw.Stop();

        return new FrameProcessingResult
        {
            AnnotatedFrame = annotated,
            Faces = faces,
            ProcessingTimeMs = sw.Elapsed.TotalMilliseconds
        };
    }

    private static void DrawOverlays(Mat frame, List<FaceClassificationEntry> faces)
    {
        foreach (var face in faces)
        {
            var bbox = face.Detection.BoundingBox;
            var result = face.AntiSpoof;

            // Зелёная рамка = реальное лицо, красная = спуфинг
            var color = result.IsReal
                ? new Scalar(0, 255, 0)   // Зелёный (BGR)
                : new Scalar(0, 0, 255);  // Красный (BGR)

            Cv2.Rectangle(frame, bbox, color, 2);

            // Текстовая метка с вероятностью
            string label = $"{result.Label}: {result.RealProbability:P1}";
            int baseline;
            var textSize = Cv2.GetTextSize(label, HersheyFonts.HersheySimplex, 0.6, 2, out baseline);

            // Фон под текстом
            var textBg = new Rect(bbox.X, bbox.Y - textSize.Height - 10, textSize.Width + 4, textSize.Height + 8);
            Cv2.Rectangle(frame, textBg, color, -1); // Заливка

            Cv2.PutText(frame, label,
                new Point(bbox.X + 2, bbox.Y - 5),
                HersheyFonts.HersheySimplex, 0.6,
                new Scalar(255, 255, 255), 2);
        }
    }

    public void Dispose()
    {
        faceDetector.Dispose();
        antiSpoof.Dispose();
    }
}
