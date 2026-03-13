using Xunit;
using FaceAntiSpoof.Core.Infrastructure;

namespace FaceAntiSpoof.Tests;

/// <summary>
/// Тесты для TensorHelper (Softmax).
/// Порядок логитов модели: индекс 0 = real, индекс 1 = spoof.
/// </summary>
public class TensorHelperTests
{
    /// <summary>
    /// Одинаковые логиты (0, 0) → обе вероятности = 0.5.
    /// </summary>
    [Fact]
    public void TestSoftmax_EqualLogits()
    {
        // Act — logitReal=0, logitSpoof=0
        var (real, spoof) = TensorHelper.Softmax(logitReal: 0f, logitSpoof: 0f);

        // Assert
        Assert.Equal(0.5f, real, precision: 4);
        Assert.Equal(0.5f, spoof, precision: 4);
    }

    /// <summary>
    /// Логиты (10, 0) → вероятность real ≈ 1.0.
    /// </summary>
    [Fact]
    public void TestSoftmax_StrongReal()
    {
        // Act — logitReal=10, logitSpoof=0
        var (real, spoof) = TensorHelper.Softmax(logitReal: 10f, logitSpoof: 0f);

        // Assert
        Assert.True(real > 0.999f, $"Ожидалось real ≈ 1.0, получено {real}");
        Assert.True(spoof < 0.001f, $"Ожидалось spoof ≈ 0.0, получено {spoof}");
    }

    /// <summary>
    /// Логиты (0, 10) → вероятность spoof ≈ 1.0.
    /// </summary>
    [Fact]
    public void TestSoftmax_StrongSpoof()
    {
        // Act — logitReal=0, logitSpoof=10
        var (real, spoof) = TensorHelper.Softmax(logitReal: 0f, logitSpoof: 10f);

        // Assert
        Assert.True(spoof > 0.999f, $"Ожидалось spoof ≈ 1.0, получено {spoof}");
        Assert.True(real < 0.001f, $"Ожидалось real ≈ 0.0, получено {real}");
    }

    /// <summary>
    /// Очень большие логиты (1000, 1001) → результат не NaN и не Inf.
    /// </summary>
    [Fact]
    public void TestSoftmax_NumericalStability()
    {
        // Act
        var (real, spoof) = TensorHelper.Softmax(logitReal: 1001f, logitSpoof: 1000f);

        // Assert — результат не должен быть NaN или Infinity
        Assert.False(float.IsNaN(real), "Real не должен быть NaN");
        Assert.False(float.IsNaN(spoof), "Spoof не должен быть NaN");
        Assert.False(float.IsInfinity(real), "Real не должен быть Infinity");
        Assert.False(float.IsInfinity(spoof), "Spoof не должен быть Infinity");

        // Сумма вероятностей должна быть ≈ 1.0
        Assert.Equal(1.0f, real + spoof, precision: 4);
    }
}
