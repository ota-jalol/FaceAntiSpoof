/**
 * ImagePreprocessor - JavaScript port of C# ImagePreprocessor
 * Порт из FaceAntiSpoof.Core/Preprocessing/ImagePreprocessor.cs
 *
 * Пайплайн предобработки:
 * 1. Квадратный crop с BORDER_REFLECT_101 padding
 * 2. Letterbox resize до 128×128 с сохранением пропорций
 * 3. Нормализация /255 → [0.0, 1.0]
 * 4. HWC → CHW преобразование
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
        // 1. Создаём квадратный crop с reflection padding
        const faceCrop = this.cropFaceWithReflection(source, bbox, expansionFactor);

        // 2. Letterbox resize до 128×128
        const letterboxed = this.letterboxResize(faceCrop, this.INPUT_SIZE);

        // 3. Получаем пиксели и конвертируем в CHW + нормализация
        const tensor = this.convertToTensorCHW(letterboxed);

        return tensor;
    }

    /**
     * Создаёт квадратный crop лица с reflection padding
     * Порт из BoundingBoxHelper.CropFace()
     */
    static cropFaceWithReflection(source, bbox, expansionFactor) {
        const canvas = document.createElement('canvas');
        const ctx = canvas.getContext('2d');

        // Получаем размеры источника
        const originalWidth = source.videoWidth || source.width;
        const originalHeight = source.videoHeight || source.height;

        const w = bbox.width;
        const h = bbox.height;

        // Квадрат по максимальной стороне
        const maxDim = Math.max(w, h);
        const centerX = bbox.x + w / 2;
        const centerY = bbox.y + h / 2;

        const cropSize = Math.floor(maxDim * expansionFactor);
        let x = Math.floor(centerX - cropSize / 2);
        let y = Math.floor(centerY - cropSize / 2);

        // Клиппинг к границам кадра
        const cropX1 = Math.max(0, x);
        const cropY1 = Math.max(0, y);
        const cropX2 = Math.min(originalWidth, x + cropSize);
        const cropY2 = Math.min(originalHeight, y + cropSize);

        // Padding для лиц у краёв кадра
        const topPad = Math.max(0, -y);
        const leftPad = Math.max(0, -x);
        const bottomPad = Math.max(0, (y + cropSize) - originalHeight);
        const rightPad = Math.max(0, (x + cropSize) - originalWidth);

        // Размер canvas с padding
        canvas.width = cropSize;
        canvas.height = cropSize;

        // Применяем BORDER_REFLECT_101 padding
        if (topPad > 0 || leftPad > 0 || bottomPad > 0 || rightPad > 0) {
            this.applyReflectionPadding(ctx, source,
                cropX1, cropY1, cropX2 - cropX1, cropY2 - cropY1,
                leftPad, topPad, rightPad, bottomPad, cropSize);
        } else {
            // Прямое копирование без padding
            ctx.drawImage(source, cropX1, cropY1, cropX2 - cropX1, cropY2 - cropY1,
                          0, 0, cropSize, cropSize);
        }

        return canvas;
    }

    /**
     * Применяет отражающий padding (BORDER_REFLECT_101)
     */
    static applyReflectionPadding(ctx, source, sx, sy, sw, sh,
                                   leftPad, topPad, rightPad, bottomPad, cropSize) {
        // Центральная область
        ctx.drawImage(source, sx, sy, sw, sh, leftPad, topPad, sw, sh);

        // Top padding (отражаем сверху)
        if (topPad > 0) {
            const reflectHeight = Math.min(topPad, sh);
            ctx.save();
            ctx.scale(1, -1);
            ctx.drawImage(source, sx, sy, sw, reflectHeight,
                         leftPad, -topPad, sw, reflectHeight);
            ctx.restore();
        }

        // Bottom padding (отражаем снизу)
        if (bottomPad > 0) {
            const reflectHeight = Math.min(bottomPad, sh);
            const sourceY = sy + sh - reflectHeight;
            const destY = topPad + sh;
            ctx.save();
            ctx.scale(1, -1);
            ctx.drawImage(source, sx, sourceY, sw, reflectHeight,
                         leftPad, -(destY + reflectHeight), sw, reflectHeight);
            ctx.restore();
        }

        // Left padding (отражаем слева)
        if (leftPad > 0) {
            const reflectWidth = Math.min(leftPad, sw);
            ctx.save();
            ctx.scale(-1, 1);
            ctx.drawImage(source, sx, sy, reflectWidth, sh,
                         -leftPad, topPad, reflectWidth, sh);
            ctx.restore();
        }

        // Right padding (отражаем справа)
        if (rightPad > 0) {
            const reflectWidth = Math.min(rightPad, sw);
            const sourceX = sx + sw - reflectWidth;
            const destX = leftPad + sw;
            ctx.save();
            ctx.scale(-1, 1);
            ctx.drawImage(source, sourceX, sy, reflectWidth, sh,
                         -(destX + reflectWidth), topPad, reflectWidth, sh);
            ctx.restore();
        }

        // Corner padding (углы с двойным отражением)
        // Top-left corner
        if (topPad > 0 && leftPad > 0) {
            const w = Math.min(leftPad, sw);
            const h = Math.min(topPad, sh);
            ctx.save();
            ctx.scale(-1, -1);
            ctx.drawImage(source, sx, sy, w, h, -(leftPad), -(topPad), w, h);
            ctx.restore();
        }

        // Top-right corner
        if (topPad > 0 && rightPad > 0) {
            const w = Math.min(rightPad, sw);
            const h = Math.min(topPad, sh);
            const sourceX = sx + sw - w;
            const destX = leftPad + sw;
            ctx.save();
            ctx.scale(-1, -1);
            ctx.drawImage(source, sourceX, sy, w, h, -(destX + w), -(topPad), w, h);
            ctx.restore();
        }

        // Bottom-left corner
        if (bottomPad > 0 && leftPad > 0) {
            const w = Math.min(leftPad, sw);
            const h = Math.min(bottomPad, sh);
            const sourceY = sy + sh - h;
            const destY = topPad + sh;
            ctx.save();
            ctx.scale(-1, -1);
            ctx.drawImage(source, sx, sourceY, w, h, -(leftPad), -(destY + h), w, h);
            ctx.restore();
        }

        // Bottom-right corner
        if (bottomPad > 0 && rightPad > 0) {
            const w = Math.min(rightPad, sw);
            const h = Math.min(bottomPad, sh);
            const sourceX = sx + sw - w;
            const sourceY = sy + sh - h;
            const destX = leftPad + sw;
            const destY = topPad + sh;
            ctx.save();
            ctx.scale(-1, -1);
            ctx.drawImage(source, sourceX, sourceY, w, h, -(destX + w), -(destY + h), w, h);
            ctx.restore();
        }
    }

    /**
     * Letterbox resize с сохранением пропорций
     * Порт из ImagePreprocessor.LetterboxResize()
     */
    static letterboxResize(sourceCanvas, newSize) {
        const oldW = sourceCanvas.width;
        const oldH = sourceCanvas.height;

        const ratio = newSize / Math.max(oldH, oldW);
        const scaledH = Math.floor(oldH * ratio);
        const scaledW = Math.floor(oldW * ratio);

        const canvas = document.createElement('canvas');
        canvas.width = newSize;
        canvas.height = newSize;
        const ctx = canvas.getContext('2d');

        // Выбор интерполяции (в браузере всегда bilinear, но можем использовать imageSmoothingQuality)
        ctx.imageSmoothingEnabled = true;
        ctx.imageSmoothingQuality = ratio > 1.0 ? 'high' : 'medium'; // LANCZOS4 vs AREA

        // Вычисляем padding для центрирования
        const deltaW = newSize - scaledW;
        const deltaH = newSize - scaledH;
        const top = Math.floor(deltaH / 2);
        const left = Math.floor(deltaW / 2);

        // Применяем reflection padding к фону
        // (упрощённая версия, заполняем чёрным, т.к. модель нормализована)
        ctx.fillStyle = '#000000';
        ctx.fillRect(0, 0, newSize, newSize);

        // Рисуем масштабированное изображение
        ctx.drawImage(sourceCanvas, 0, 0, oldW, oldH, left, top, scaledW, scaledH);

        // Применяем reflect padding для границ (упрощённо)
        // Для полной точности нужно отражать края, но это добавляет сложности
        // В продакшене можно улучшить эту часть

        return canvas;
    }

    /**
     * Конвертирует canvas в Float32Array CHW тензор с нормализацией
     * Порт из ImagePreprocessor.PreprocessWithExpansion() шаг 4
     */
    static convertToTensorCHW(canvas) {
        const ctx = canvas.getContext('2d');
        const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
        const pixels = imageData.data; // RGBA format

        const size = this.INPUT_SIZE;
        const tensor = new Float32Array(this.TENSOR_LENGTH);

        // HWC → CHW + нормализация /255
        // Canvas даёт RGBA, нам нужен RGB
        for (let c = 0; c < 3; c++) { // R, G, B (игнорируем A)
            const channelOffset = c * size * size;
            for (let h = 0; h < size; h++) {
                const rowOffset = h * size;
                for (let w = 0; w < size; w++) {
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
