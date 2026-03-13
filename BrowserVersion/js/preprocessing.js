/**
 * ImagePreprocessor - Optimized JavaScript port avoiding canvas artifacts
 * Улучшенная версия без множественных canvas операций
 *
 * Пайплайн предобработки:
 * 1. Прямое чтение пикселей из источника
 * 2. Квадратный crop с BORDER_REFLECT_101 padding
 * 3. Letterbox resize до 128×128 с сохранением пропорций
 * 4. Нормализация /255 → [0.0, 1.0]
 * 5. HWC → CHW преобразование
 */

class ImagePreprocessor {
    static INPUT_SIZE = 128;
    static CHANNELS = 3;
    static TENSOR_LENGTH = 1 * ImagePreprocessor.CHANNELS * ImagePreprocessor.INPUT_SIZE * ImagePreprocessor.INPUT_SIZE;

    /**
     * Полная предобработка изображения лица для модели MiniFASNet
     * @param {HTMLVideoElement|HTMLCanvasElement} source - Источник видео/canvas
     * @param {Object} bbox - Bounding box {x, y, width, height}
     * @param {number} expansionFactor - Коэффициент расширения bbox (default 1.5)
     * @returns {Float32Array} - Тензор в формате CHW [1, 3, 128, 128]
     */
    static preprocess(source, bbox, expansionFactor = 1.5) {
        // Получаем размеры источника
        const originalWidth = source.videoWidth || source.width;
        const originalHeight = source.videoHeight || source.height;

        // 1. Читаем пиксели из источника ОДИН РАЗ
        const sourceCanvas = document.createElement('canvas');
        sourceCanvas.width = originalWidth;
        sourceCanvas.height = originalHeight;
        const sourceCtx = sourceCanvas.getContext('2d', { willReadFrequently: true });
        sourceCtx.drawImage(source, 0, 0);
        const sourceImageData = sourceCtx.getImageData(0, 0, originalWidth, originalHeight);
        const sourcePixels = sourceImageData.data;

        // 2. Вычисляем параметры квадратного кропа
        const w = bbox.width;
        const h = bbox.height;
        const maxDim = Math.max(w, h);
        const centerX = bbox.x + w / 2;
        const centerY = bbox.y + h / 2;
        const cropSize = Math.floor(maxDim * expansionFactor);
        const x = Math.floor(centerX - cropSize / 2);
        const y = Math.floor(centerY - cropSize / 2);

        // 3. Создаём crop с reflection padding напрямую в память
        const croppedPixels = this.cropWithReflectionPadding(
            sourcePixels, originalWidth, originalHeight,
            x, y, cropSize
        );

        // 4. Letterbox resize до 128×128
        const resizedPixels = this.letterboxResize(croppedPixels, cropSize, this.INPUT_SIZE);

        // 5. Конвертируем в CHW тензор с нормализацией
        const tensor = this.convertRGBAToCHW(resizedPixels, this.INPUT_SIZE);

        return tensor;
    }

    /**
     * Выполняет crop с reflection padding без canvas
     * @param {Uint8ClampedArray} sourcePixels - Исходные пиксели в формате RGBA
     * @param {number} sourceWidth - Ширина источника
     * @param {number} sourceHeight - Высота источника
     * @param {number} x - X координата начала кропа
     * @param {number} y - Y координата начала кропа
     * @param {number} cropSize - Размер квадратного кропа
     * @returns {Uint8ClampedArray} - Кропнутые пиксели в формате RGBA
     */
    static cropWithReflectionPadding(sourcePixels, sourceWidth, sourceHeight, x, y, cropSize) {
        const croppedPixels = new Uint8ClampedArray(cropSize * cropSize * 4);

        for (let h = 0; h < cropSize; h++) {
            for (let w = 0; w < cropSize; w++) {
                // Вычисляем координаты в исходном изображении
                let srcX = x + w;
                let srcY = y + h;

                // Применяем BORDER_REFLECT_101
                if (srcX < 0) {
                    srcX = -srcX - 1;
                } else if (srcX >= sourceWidth) {
                    srcX = 2 * sourceWidth - srcX - 1;
                }

                if (srcY < 0) {
                    srcY = -srcY - 1;
                } else if (srcY >= sourceHeight) {
                    srcY = 2 * sourceHeight - srcY - 1;
                }

                // Клиппинг на всякий случай
                srcX = Math.max(0, Math.min(sourceWidth - 1, srcX));
                srcY = Math.max(0, Math.min(sourceHeight - 1, srcY));

                // Копируем пиксель
                const srcIndex = (srcY * sourceWidth + srcX) * 4;
                const dstIndex = (h * cropSize + w) * 4;

                croppedPixels[dstIndex] = sourcePixels[srcIndex];         // R
                croppedPixels[dstIndex + 1] = sourcePixels[srcIndex + 1]; // G
                croppedPixels[dstIndex + 2] = sourcePixels[srcIndex + 2]; // B
                croppedPixels[dstIndex + 3] = 255;                         // A
            }
        }

        return croppedPixels;
    }

    /**
     * Letterbox resize с сохранением пропорций и BORDER_REFLECT_101 padding
     * Точный порт из C# ImagePreprocessor.LetterboxResize()
     * @param {Uint8ClampedArray} sourcePixels - Исходные пиксели
     * @param {number} sourceSize - Размер исходного изображения
     * @param {number} targetSize - Целевой размер (128)
     * @returns {Uint8ClampedArray} - Resized пиксели
     */
    static letterboxResize(sourcePixels, sourceSize, targetSize) {
        // Вычисляем параметры resize
        const ratio = targetSize / sourceSize;
        const scaledSize = Math.floor(sourceSize * ratio);
        const top = Math.floor((targetSize - scaledSize) / 2);
        const left = top; // Для квадратного изображения top == left

        // Сначала применяем bilinear интерполяцию для центрального региона
        const scaledPixels = new Uint8ClampedArray(scaledSize * scaledSize * 4);

        for (let h = 0; h < scaledSize; h++) {
            for (let w = 0; w < scaledSize; w++) {
                // Вычисляем координаты в исходном изображении
                const srcX = w / ratio;
                const srcY = h / ratio;

                // Bilinear интерполяция
                const x0 = Math.floor(srcX);
                const y0 = Math.floor(srcY);
                const x1 = Math.min(x0 + 1, sourceSize - 1);
                const y1 = Math.min(y0 + 1, sourceSize - 1);

                const fx = srcX - x0;
                const fy = srcY - y0;

                // Индексы 4 соседних пикселей
                const idx00 = (y0 * sourceSize + x0) * 4;
                const idx01 = (y0 * sourceSize + x1) * 4;
                const idx10 = (y1 * sourceSize + x0) * 4;
                const idx11 = (y1 * sourceSize + x1) * 4;

                // Целевой индекс в scaledPixels
                const dstIndex = (h * scaledSize + w) * 4;

                // Интерполяция для каждого канала (RGB)
                for (let c = 0; c < 3; c++) {
                    const v00 = sourcePixels[idx00 + c];
                    const v01 = sourcePixels[idx01 + c];
                    const v10 = sourcePixels[idx10 + c];
                    const v11 = sourcePixels[idx11 + c];

                    const v0 = v00 * (1 - fx) + v01 * fx;
                    const v1 = v10 * (1 - fx) + v11 * fx;
                    const value = v0 * (1 - fy) + v1 * fy;

                    scaledPixels[dstIndex + c] = Math.round(value);
                }
                scaledPixels[dstIndex + 3] = 255; // Alpha
            }
        }

        // Применяем BORDER_REFLECT_101 padding как в C# (CopyMakeBorder)
        const resizedPixels = new Uint8ClampedArray(targetSize * targetSize * 4);

        for (let h = 0; h < targetSize; h++) {
            for (let w = 0; w < targetSize; w++) {
                // Координаты в scaledPixels (с учётом offset)
                let srcH = h - top;
                let srcW = w - left;

                // Применяем BORDER_REFLECT_101
                if (srcH < 0) {
                    srcH = -srcH - 1;
                } else if (srcH >= scaledSize) {
                    srcH = 2 * scaledSize - srcH - 1;
                }

                if (srcW < 0) {
                    srcW = -srcW - 1;
                } else if (srcW >= scaledSize) {
                    srcW = 2 * scaledSize - srcW - 1;
                }

                // Клиппинг на всякий случай
                srcH = Math.max(0, Math.min(scaledSize - 1, srcH));
                srcW = Math.max(0, Math.min(scaledSize - 1, srcW));

                // Копируем пиксель
                const srcIndex = (srcH * scaledSize + srcW) * 4;
                const dstIndex = (h * targetSize + w) * 4;

                resizedPixels[dstIndex] = scaledPixels[srcIndex];         // R
                resizedPixels[dstIndex + 1] = scaledPixels[srcIndex + 1]; // G
                resizedPixels[dstIndex + 2] = scaledPixels[srcIndex + 2]; // B
                resizedPixels[dstIndex + 3] = 255;                         // A
            }
        }

        return resizedPixels;
    }

    /**
     * Конвертирует RGBA в CHW Float32Array с нормализацией
     * Точный порт из C# ImagePreprocessor.PreprocessWithExpansion() шаги 4
     * @param {Uint8ClampedArray} pixels - Пиксели в формате RGBA
     * @param {number} size - Размер изображения (128)
     * @returns {Float32Array} - Тензор в формате CHW
     */
    static convertRGBAToCHW(pixels, size) {
        const tensor = new Float32Array(this.TENSOR_LENGTH);

        // HWC → CHW + нормализация /255
        // Точно как в C# коде (строки 41-53)
        for (let c = 0; c < 3; c++) {
            const channelOffset = c * size * size;
            for (let h = 0; h < size; h++) {
                const rowOffset = h * size;
                for (let w = 0; w < size; w++) {
                    // HWC индекс: (h * width + w) * channels + c
                    const pixelIndex = (h * size + w) * 4; // RGBA stride
                    const value = pixels[pixelIndex + c]; // R=0, G=1, B=2
                    tensor[channelOffset + rowOffset + w] = value / 255.0;
                }
            }
        }

        return tensor;
    }

    /**
     * Визуализация предобработанного изображения (для отладки)
     */
    static visualizeTensor(tensor, targetCanvas) {
        const size = this.INPUT_SIZE;
        targetCanvas.width = size;
        targetCanvas.height = size;
        const ctx = targetCanvas.getContext('2d');
        const imageData = ctx.createImageData(size, size);

        // CHW → RGBA для визуализации
        for (let h = 0; h < size; h++) {
            for (let w = 0; w < size; w++) {
                const pixelIndex = (h * size + w) * 4;
                for (let c = 0; c < 3; c++) {
                    const tensorIndex = c * size * size + h * size + w;
                    imageData.data[pixelIndex + c] = Math.floor(tensor[tensorIndex] * 255);
                }
                imageData.data[pixelIndex + 3] = 255; // Alpha
            }
        }

        ctx.putImageData(imageData, 0, 0);
    }
}
