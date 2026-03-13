using System.Runtime.InteropServices;
using OpenCvSharp;

namespace FaceAntiSpoof.Core.Preprocessing;

/// <summary>
/// Предобработка кропа лица для подачи в модель MiniFASNetV2SE.
/// Портировано из Python: src/inference/preprocess.py → preprocess()
/// Пайплайн: Crop (квадрат) → Letterbox resize 128×128 → BGR→RGB → /255 → HWC→CHW.
/// </summary>
public static class ImagePreprocessor
{
    public const int InputSize = 128;
    public const int Channels = 3;
    public const int TensorLength = 1 * Channels * InputSize * InputSize;

    /// <summary>
    /// Полная предобработка: квадратный кроп → letterbox resize → нормализация → CHW.
    /// </summary>
    /// <param name="bgrFrame">Входной кадр в формате BGR.</param>
    /// <param name="bbox">BBox лица (x, y, w, h).</param>
    /// <param name="expansionFactor">Коэффициент расширения BBox.</param>
    /// <param name="outputBuffer">Преаллоцированный буфер float[1×3×128×128].</param>
    public static void PreprocessWithExpansion(Mat bgrFrame, Rect bbox, float expansionFactor, float[] outputBuffer)
    {
        // 1. Квадратный кроп с отражающим padding (как в Python crop())
        using var faceCrop = BoundingBoxHelper.CropFace(bgrFrame, bbox, expansionFactor);

        // 2. BGR → RGB (Python детектит на RGB и кропит из RGB)
        using var rgb = new Mat();
        Cv2.CvtColor(faceCrop, rgb, ColorConversionCodes.BGR2RGB);

        // 3. Letterbox resize с сохранением пропорций (как в Python preprocess())
        using var letterboxed = LetterboxResize(rgb, InputSize);

        // 4. HWC → CHW + нормализация /255
        int totalBytes = InputSize * InputSize * Channels;
        byte[] pixels = new byte[totalBytes];
        Marshal.Copy(letterboxed.Data, pixels, 0, totalBytes);

        for (int c = 0; c < Channels; c++)
        {
            int channelOffset = c * InputSize * InputSize;
            for (int h = 0; h < InputSize; h++)
            {
                int rowOffset = h * InputSize;
                for (int w = 0; w < InputSize; w++)
                {
                    int hwcIndex = (rowOffset + w) * Channels + c;
                    outputBuffer[channelOffset + rowOffset + w] = pixels[hwcIndex] / 255.0f;
                }
            }
        }
    }

    /// <summary>
    /// Letterbox resize: масштабирование с сохранением пропорций + отражающий padding.
    /// Порт Python: preprocess() из src/inference/preprocess.py.
    /// </summary>
    private static Mat LetterboxResize(Mat img, int newSize)
    {
        int oldH = img.Rows;
        int oldW = img.Cols;

        float ratio = (float)newSize / Math.Max(oldH, oldW);
        int scaledH = (int)(oldH * ratio);
        int scaledW = (int)(oldW * ratio);

        // Выбор интерполяции как в Python
        var interpolation = ratio > 1.0f
            ? InterpolationFlags.Lanczos4
            : InterpolationFlags.Area;

        using var resized = new Mat();
        Cv2.Resize(img, resized, new Size(scaledW, scaledH), interpolation: interpolation);

        // Отражающий padding до квадрата newSize×newSize
        int deltaW = newSize - scaledW;
        int deltaH = newSize - scaledH;
        int top = deltaH / 2;
        int bottom = deltaH - top;
        int left = deltaW / 2;
        int right = deltaW - left;

        var result = new Mat();
        Cv2.CopyMakeBorder(resized, result, top, bottom, left, right, BorderTypes.Reflect101);

        return result;
    }
}
