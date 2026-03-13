using OpenCvSharp;

namespace FaceAntiSpoof.Core.Models;

/// <summary>
/// Составной результат обработки одного кадра.
/// </summary>
public sealed class FrameProcessingResult
{
    /// <summary>
    /// Кадр с наложенными рамками и метками (готов к отображению).
    /// Вызывающий код отвечает за Dispose.
    /// </summary>
    public Mat AnnotatedFrame { get; init; } = null!;

    /// <summary>
    /// Результаты по каждому обнаруженному лицу.
    /// </summary>
    public IReadOnlyList<FaceClassificationEntry> Faces { get; init; } = [];

    /// <summary>
    /// Общее время обработки кадра в миллисекундах.
    /// </summary>
    public double ProcessingTimeMs { get; init; }
}

/// <summary>
/// Результат детекции и классификации одного лица в кадре.
/// </summary>
public sealed class FaceClassificationEntry
{
    /// <summary>
    /// Результат детекции (BBox, confidence, landmarks).
    /// </summary>
    public FaceDetectionResult Detection { get; init; } = null!;

    /// <summary>
    /// Результат антиспуфинга (Real/Spoof, вероятности).
    /// </summary>
    public AntiSpoofResult AntiSpoof { get; init; } = null!;
}
