using OpenCvSharp;
using FaceAntiSpoof.Core.Models;
using FaceAntiSpoof.Core.Services.Interfaces;

namespace FaceAntiSpoof.Core.Services;

/// <summary>
/// Детекция лиц с использованием CascadeClassifier (Haar-каскад).
/// Используется вместо FaceDetectorYN, т.к. objdetect модуль
/// не скомпилирован в OpenCvSharp4.runtime.win.
/// </summary>
public sealed class FaceDetectorService : IFaceDetectorService
{
    private readonly CascadeClassifier cascade;
    private readonly int minFaceSize;

    /// <param name="modelPath">Путь к XML-файлу Haar-каскада (haarcascade_frontalface_alt2.xml).</param>
    /// <param name="minFaceSize">Минимальный размер лица в пикселях.</param>
    public FaceDetectorService(string modelPath, int minFaceSize = 60)
    {
        cascade = new CascadeClassifier(modelPath);
        if (cascade.Empty())
            throw new InvalidOperationException($"Не удалось загрузить каскад: {modelPath}");
        this.minFaceSize = minFaceSize;
    }

    public IReadOnlyList<FaceDetectionResult> Detect(Mat frame)
    {
        using var gray = new Mat();
        Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.EqualizeHist(gray, gray);

        var faces = cascade.DetectMultiScale(
            gray,
            scaleFactor: 1.1,
            minNeighbors: 5,
            flags: HaarDetectionTypes.ScaleImage,
            minSize: new Size(minFaceSize, minFaceSize));

        if (faces.Length == 0)
            return [];

        var results = new List<FaceDetectionResult>(faces.Length);
        foreach (var rect in faces)
        {
            results.Add(new FaceDetectionResult
            {
                BoundingBox = rect,
                Confidence = 1.0f, // Haar-каскад не возвращает confidence
                Landmarks = null
            });
        }

        return results;
    }

    public void Dispose()
    {
        cascade.Dispose();
    }
}
