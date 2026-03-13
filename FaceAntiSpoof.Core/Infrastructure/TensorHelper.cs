using Microsoft.ML.OnnxRuntime;

namespace FaceAntiSpoof.Core.Infrastructure;

/// <summary>
/// Вспомогательные методы для работы с OrtValue-тензорами.
/// </summary>
public static class TensorHelper
{
    /// <summary>
    /// Создаёт входной OrtValue из преаллоцированного float-буфера.
    /// </summary>
    public static OrtValue CreateInputTensor(float[] buffer, long[] shape)
    {
        return OrtValue.CreateTensorValueFromMemory(buffer, shape);
    }

    /// <summary>
    /// Применяет Softmax к двум логитам с вычитанием max для численной стабильности.
    /// Порядок логитов модели MiniFASNetV2SE: индекс 0 = real, индекс 1 = spoof.
    /// </summary>
    /// <param name="logitReal">Логит класса real (индекс 0).</param>
    /// <param name="logitSpoof">Логит класса spoof (индекс 1).</param>
    /// <returns>Вероятности (real, spoof).</returns>
    public static (float Real, float Spoof) Softmax(float logitReal, float logitSpoof)
    {
        float maxVal = Math.Max(logitReal, logitSpoof);
        float expReal = MathF.Exp(logitReal - maxVal);
        float expSpoof = MathF.Exp(logitSpoof - maxVal);
        float sum = expReal + expSpoof;
        return (expReal / sum, expSpoof / sum);
    }
}
