using OpenCvSharp;

namespace FaceAntiSpoof.Core.Models;

/// <summary>
/// Результат детекции одного лица на изображении.
/// </summary>
public sealed class FaceDetectionResult
{
    /// <summary>
    /// Ограничивающий прямоугольник лица.
    /// </summary>
    public Rect BoundingBox { get; init; }

    /// <summary>
    /// Уверенность детектора (0.0 – 1.0).
    /// </summary>
    public float Confidence { get; init; }

    /// <summary>
    /// Координаты 5 лицевых ландмарков (правый глаз, левый глаз, нос, правый угол рта, левый угол рта).
    /// Может быть null, если детектор не возвращает ландмарки.
    /// </summary>
    public Point2f[]? Landmarks { get; init; }
}
