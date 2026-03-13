using Xunit;
using OpenCvSharp;
using FaceAntiSpoof.Core.Preprocessing;

namespace FaceAntiSpoof.Tests;

/// <summary>
/// Тесты для ImagePreprocessor и BoundingBoxHelper.
/// </summary>
public class PreprocessingTests
{
    /// <summary>
    /// CropFace создаёт квадратный кроп с padding.
    /// BBox (100, 100, 50, 50) с коэффициентом 1.5 → кроп 75×75.
    /// </summary>
    [Fact]
    public void TestCropFaceSquareOutput()
    {
        // Arrange
        using var frame = new Mat(480, 640, MatType.CV_8UC3, new Scalar(100, 150, 200));
        var bbox = new Rect(100, 100, 50, 50);
        float expansionFactor = 1.5f;

        // Act
        using var crop = BoundingBoxHelper.CropFace(frame, bbox, expansionFactor);

        // Assert — результат должен быть квадратным, размер = max(50,50) * 1.5 = 75
        Assert.Equal(75, crop.Width);
        Assert.Equal(75, crop.Height);
    }

    /// <summary>
    /// BBox у края изображения → CropFace не падает и создаёт квадратный кроп с padding.
    /// </summary>
    [Fact]
    public void TestCropFaceEdge()
    {
        // Arrange — bbox у верхнего левого угла
        using var frame = new Mat(100, 100, MatType.CV_8UC3, new Scalar(50, 50, 50));
        var bbox = new Rect(0, 0, 50, 50);
        float expansionFactor = 2.0f;

        // Act
        using var crop = BoundingBoxHelper.CropFace(frame, bbox, expansionFactor);

        // Assert — результат квадратный, размер = 50 * 2.0 = 100
        Assert.Equal(crop.Width, crop.Height);
        Assert.Equal(100, crop.Width);
    }

    /// <summary>
    /// Проверяем что выходной буфер заполняется, длина = 1×3×128×128 = 49152.
    /// </summary>
    [Fact]
    public void TestPreprocessOutputLength()
    {
        // Arrange
        int expectedLength = 1 * 3 * 128 * 128;
        using var mat = new Mat(200, 200, MatType.CV_8UC3, new Scalar(100, 150, 200));
        var bbox = new Rect(10, 10, 100, 100);
        var buffer = new float[expectedLength];

        // Act
        ImagePreprocessor.PreprocessWithExpansion(mat, bbox, 1.5f, buffer);

        // Assert
        Assert.Equal(ImagePreprocessor.TensorLength, expectedLength);
        Assert.Equal(expectedLength, buffer.Length);
        // Проверяем что буфер заполнен (не все нули)
        Assert.True(buffer.Any(v => v > 0), "Буфер не должен быть пустым");
    }

    /// <summary>
    /// Подаём однородный Mat → значения в буфере нормализованы.
    /// </summary>
    [Fact]
    public void TestPreprocessNormalizationRange()
    {
        // Arrange
        using var mat = new Mat(200, 200, MatType.CV_8UC3, new Scalar(128, 128, 128));
        var bbox = new Rect(25, 25, 100, 100);
        var buffer = new float[ImagePreprocessor.TensorLength];

        // Act
        ImagePreprocessor.PreprocessWithExpansion(mat, bbox, 1.0f, buffer);

        // Assert — все значения должны быть в диапазоне [0.0, 1.0]
        foreach (var val in buffer)
        {
            Assert.InRange(val, 0.0f, 1.0f);
        }
    }

    /// <summary>
    /// Создаём Mat BGR с известными значениями →
    /// проверяем корректность CHW-транспозиции. После BGR→RGB: каналы меняются местами.
    /// </summary>
    [Fact]
    public void TestPreprocessTransposeChannels()
    {
        // Arrange — BGR: B=10, G=20, R=30 → после BGR→RGB: channel 0=R=30, channel 1=G=20, channel 2=B=10
        byte bVal = 10, gVal = 20, rVal = 30;
        using var mat = new Mat(200, 200, MatType.CV_8UC3, new Scalar(bVal, gVal, rVal));
        var bbox = new Rect(25, 25, 150, 150);
        var buffer = new float[ImagePreprocessor.TensorLength];

        // Act
        ImagePreprocessor.PreprocessWithExpansion(mat, bbox, 1.0f, buffer);

        int planeSize = 128 * 128;

        // Assert — CHW: канал 0 (R в RGB), канал 1 (G), канал 2 (B)
        // Центральные пиксели должны содержать значения оригинала,
        // крайние могут содержать padding. Проверяем центральный пиксель.
        int centerIdx = 64 * 128 + 64; // Примерно центр
        float expectedR = rVal / 255.0f;
        float expectedG = gVal / 255.0f;
        float expectedB = bVal / 255.0f;

        Assert.Equal(expectedR, buffer[centerIdx], precision: 2);
        Assert.Equal(expectedG, buffer[planeSize + centerIdx], precision: 2);
        Assert.Equal(expectedB, buffer[2 * planeSize + centerIdx], precision: 2);
    }
}
