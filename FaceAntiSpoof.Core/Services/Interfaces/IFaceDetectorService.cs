using OpenCvSharp;
using FaceAntiSpoof.Core.Models;

namespace FaceAntiSpoof.Core.Services.Interfaces;

/// <summary>
/// Контракт сервиса детекции лиц.
/// </summary>
public interface IFaceDetectorService : IDisposable
{
    /// <summary>
    /// Обнаруживает лица на изображении.
    /// </summary>
    /// <param name="frame">Входной кадр (BGR).</param>
    /// <returns>Список обнаруженных лиц.</returns>
    IReadOnlyList<FaceDetectionResult> Detect(Mat frame);
}
