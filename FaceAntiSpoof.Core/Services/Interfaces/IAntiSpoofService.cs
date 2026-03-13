using OpenCvSharp;
using FaceAntiSpoof.Core.Models;

namespace FaceAntiSpoof.Core.Services.Interfaces;

/// <summary>
/// Контракт сервиса антиспуфинга.
/// </summary>
public interface IAntiSpoofService : IDisposable
{
    /// <summary>
    /// Классифицирует кроп лица: реальное или спуфинг.
    /// </summary>
    /// <param name="frame">Полный кадр (BGR).</param>
    /// <param name="bbox">Ограничивающий прямоугольник лица.</param>
    /// <param name="threshold">Порог решения (по умолчанию 0.5).</param>
    /// <returns>Результат классификации.</returns>
    AntiSpoofResult Classify(Mat frame, Rect bbox, float threshold = 0.5f);
}
