using OpenCvSharp;

namespace FaceAntiSpoof.Core.Preprocessing;

/// <summary>
/// Утилиты для работы с ограничивающими прямоугольниками лиц.
/// Портировано из Python: src/inference/preprocess.py → crop()
/// </summary>
public static class BoundingBoxHelper
{
    /// <summary>
    /// Создаёт квадратный кроп лица с расширением и отражающим padding.
    /// Порт Python-функции crop(): квадрат по max(w,h) * expansionFactor,
    /// padding через BORDER_REFLECT_101 для лиц у края кадра.
    /// </summary>
    /// <param name="frame">Исходный кадр.</param>
    /// <param name="bbox">BBox лица (x, y, w, h).</param>
    /// <param name="expansionFactor">Коэффициент расширения (1.5 по умолчанию).</param>
    /// <returns>Квадратный кроп лица с padding.</returns>
    public static Mat CropFace(Mat frame, Rect bbox, float expansionFactor = 1.5f)
    {
        int originalHeight = frame.Rows;
        int originalWidth = frame.Cols;

        int w = bbox.Width;
        int h = bbox.Height;

        // Квадрат по максимальной стороне
        int maxDim = Math.Max(w, h);
        float centerX = bbox.X + w / 2f;
        float centerY = bbox.Y + h / 2f;

        int cropSize = (int)(maxDim * expansionFactor);
        int x = (int)(centerX - cropSize / 2f);
        int y = (int)(centerY - cropSize / 2f);

        // Область кропа с клиппингом
        int cropX1 = Math.Max(0, x);
        int cropY1 = Math.Max(0, y);
        int cropX2 = Math.Min(originalWidth, x + cropSize);
        int cropY2 = Math.Min(originalHeight, y + cropSize);

        // Padding для лиц у краёв кадра
        int topPad = Math.Max(0, -y);
        int leftPad = Math.Max(0, -x);
        int bottomPad = Math.Max(0, (y + cropSize) - originalHeight);
        int rightPad = Math.Max(0, (x + cropSize) - originalWidth);

        Mat cropped;
        if (cropX2 > cropX1 && cropY2 > cropY1)
        {
            var cropRect = new Rect(cropX1, cropY1, cropX2 - cropX1, cropY2 - cropY1);
            cropped = new Mat(frame, cropRect);
        }
        else
        {
            cropped = new Mat(0, 0, frame.Type());
        }

        // Отражающий padding (как в Python: BORDER_REFLECT_101)
        var result = new Mat();
        Cv2.CopyMakeBorder(cropped, result, topPad, bottomPad, leftPad, rightPad, BorderTypes.Reflect101);
        cropped.Dispose();

        return result;
    }
}
