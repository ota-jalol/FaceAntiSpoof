using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;
using FaceAntiSpoof.Core.Infrastructure;
using FaceAntiSpoof.Core.Models;
using FaceAntiSpoof.Core.Preprocessing;
using FaceAntiSpoof.Core.Services.Interfaces;

namespace FaceAntiSpoof.Core.Services;

/// <summary>
/// Сервис антиспуфинга на базе MiniFASNetV2SE.
/// InferenceSession создаётся однократно, буферы преаллоцированы.
/// </summary>
public sealed class AntiSpoofService : IAntiSpoofService
{
    private readonly OnnxSessionManager sessionManager;
    private readonly float[] inputBuffer;
    private readonly float expansionFactor;

    private static readonly long[] InputShape = [1, 3, ImagePreprocessor.InputSize, ImagePreprocessor.InputSize];

    /// <param name="modelPath">Путь к ONNX-модели антиспуфинга.</param>
    /// <param name="useDirectML">Использовать GPU через DirectML.</param>
    /// <param name="expansionFactor">Коэффициент расширения BBox (1.5 по умолчанию).</param>
    public AntiSpoofService(string modelPath, bool useDirectML = false, float expansionFactor = 1.5f)
    {
        sessionManager = new OnnxSessionManager(modelPath, useDirectML);
        inputBuffer = new float[ImagePreprocessor.TensorLength];
        this.expansionFactor = expansionFactor;
    }

    public AntiSpoofResult Classify(Mat frame, Rect bbox, float threshold = 0.5f)
    {
        // Предобработка: квадратный crop → letterbox resize → normalize → CHW
        ImagePreprocessor.PreprocessWithExpansion(frame, bbox, expansionFactor, inputBuffer);

        // Создание входного тензора
        using var inputOrt = TensorHelper.CreateInputTensor(inputBuffer, InputShape);
        var inputs = new Dictionary<string, OrtValue> { { sessionManager.InputName, inputOrt } };

        // Инференс
        using var results = sessionManager.Session.Run(new RunOptions(), inputs, sessionManager.OutputNames);
        var logits = results[0].GetTensorDataAsSpan<float>();

        // Softmax: индекс 0 = real, индекс 1 = spoof (проверено по Python-исходнику)
        var (realProb, spoofProb) = TensorHelper.Softmax(logits[0], logits[1]);

        return new AntiSpoofResult(realProb >= threshold, realProb, spoofProb);
    }

    public void Dispose()
    {
        sessionManager.Dispose();
    }
}
