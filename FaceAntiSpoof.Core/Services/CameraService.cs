using System.Threading.Channels;
using OpenCvSharp;
using FaceAntiSpoof.Core.Services.Interfaces;

namespace FaceAntiSpoof.Core.Services;

/// <summary>
/// Захват видеопотока с камеры через OpenCvSharp VideoCapture.
/// Кадры передаются через BoundedChannel с политикой DropOldest.
/// </summary>
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
        frameChannel = Channel.CreateBounded<Mat>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true
        });
    }

    public void Start(int cameraIndex = 0)
    {
        if (IsRunning)
            return;

        // CAP_DSHOW для стабильной работы на Windows
        capture = new VideoCapture(cameraIndex, VideoCaptureAPIs.DSHOW);
        capture.Set(VideoCaptureProperties.FrameWidth, 640);
        capture.Set(VideoCaptureProperties.FrameHeight, 480);

        if (!capture.IsOpened())
            throw new InvalidOperationException($"Не удалось открыть камеру с индексом {cameraIndex}.");

        cts = new CancellationTokenSource();
        captureTask = Task.Run(() => CaptureLoop(cts.Token));
    }

    private void CaptureLoop(CancellationToken ct)
    {
        using var frame = new Mat();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (capture!.Read(frame) && !frame.Empty())
                {
                    // Clone — чтобы frame мог быть перезаписан на следующей итерации
                    // При DropOldest старый Mat теряется — допустимо для real-time
                    frameChannel.Writer.TryWrite(frame.Clone());
                }
                else
                {
                    // Небольшая пауза при неудачном чтении, чтобы не грузить CPU
                    Thread.Sleep(10);
                }
            }
        }
        finally
        {
            frameChannel.Writer.TryComplete();
        }
    }

    public void Stop()
    {
        cts?.Cancel();
    }

    public void Dispose()
    {
        Stop();
        try
        {
            captureTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Игнорируем OperationCanceledException
        }
        capture?.Dispose();
        cts?.Dispose();
    }
}
