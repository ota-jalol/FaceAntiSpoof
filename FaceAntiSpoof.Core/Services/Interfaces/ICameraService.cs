using System.Threading.Channels;
using OpenCvSharp;

namespace FaceAntiSpoof.Core.Services.Interfaces;

/// <summary>
/// Контракт сервиса захвата камеры.
/// </summary>
public interface ICameraService : IDisposable
{
    /// <summary>
    /// Запускает захват с указанной камеры.
    /// </summary>
    /// <param name="cameraIndex">Индекс камеры (0 = по умолчанию).</param>
    void Start(int cameraIndex = 0);

    /// <summary>
    /// Останавливает захват.
    /// </summary>
    void Stop();

    /// <summary>
    /// Канал для чтения кадров. Каждый полученный Mat должен быть Dispose после использования.
    /// </summary>
    ChannelReader<Mat> Frames { get; }

    /// <summary>
    /// Возвращает true, если камера запущена.
    /// </summary>
    bool IsRunning { get; }
}
