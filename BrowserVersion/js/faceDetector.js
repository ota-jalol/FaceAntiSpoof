/**
 * FaceDetectorService - JavaScript implementation using BlazeFace
 * Альтернатива C# FaceDetectorService (Haar Cascade)
 *
 * Использует BlazeFace от TensorFlow.js для быстрой детекции лиц
 * BlazeFace быстрее Haar Cascade и работает в реальном времени в браузере
 */

class FaceDetectorService {
    constructor() {
        this.model = null;
        this.isInitialized = false;
        this.minConfidence = 0.5;
    }

    /**
     * Инициализация модели детекции
     * @param {number} minConfidence - Минимальная уверенность детекции
     */
    async initialize(minConfidence = 0.5) {
        try {
            console.log('Загрузка модели детекции лиц BlazeFace...');
            this.minConfidence = minConfidence;

            // Загрузка BlazeFace (лёгкая и быстрая модель)
            this.model = await blazeface.load();

            console.log('BlazeFace загружен успешно');
            this.isInitialized = true;
            return true;
        } catch (error) {
            console.error('Ошибка загрузки BlazeFace:', error);
            throw error;
        }
    }

    /**
     * Детекция лиц в видео/изображении
     * Порт из FaceDetectorService.Detect()
     *
     * @param {HTMLVideoElement|HTMLCanvasElement} source - Источник видео
     * @returns {Promise<Array>} - Массив {x, y, width, height, confidence, landmarks}
     */
    async detect(source) {
        if (!this.isInitialized) {
            throw new Error('Модель не инициализирована. Вызовите initialize() сначала.');
        }

        try {
            // BlazeFace детекция
            const predictions = await this.model.estimateFaces(source, false);

            if (!predictions || predictions.length === 0) {
                return [];
            }

            // Конвертируем результаты в формат, аналогичный C# FaceDetectionResult
            const results = [];
            for (const pred of predictions) {
                // BlazeFace возвращает [topLeft, bottomRight]
                const topLeft = pred.topLeft;
                const bottomRight = pred.bottomRight;

                const x = Math.floor(topLeft[0]);
                const y = Math.floor(topLeft[1]);
                const width = Math.floor(bottomRight[0] - topLeft[0]);
                const height = Math.floor(bottomRight[1] - topLeft[1]);

                // Проверка минимальной уверенности
                const confidence = pred.probability ? pred.probability[0] : 1.0;
                if (confidence < this.minConfidence) {
                    continue;
                }

                // Landmarks (6 точек от BlazeFace)
                const landmarks = pred.landmarks || null;

                results.push({
                    x,
                    y,
                    width,
                    height,
                    confidence,
                    landmarks
                });
            }

            return results;
        } catch (error) {
            console.error('Ошибка детекции:', error);
            return [];
        }
    }

    /**
     * Детекция с ограничением на максимальное количество лиц
     * @param {HTMLVideoElement|HTMLCanvasElement} source
     * @param {number} maxFaces - Максимальное количество лиц
     */
    async detectTopFaces(source, maxFaces = 1) {
        const allFaces = await this.detect(source);

        // Сортируем по confidence и берём топ N
        allFaces.sort((a, b) => b.confidence - a.confidence);
        return allFaces.slice(0, maxFaces);
    }

    /**
     * Освобождение ресурсов
     */
    dispose() {
        if (this.model) {
            this.model.dispose();
            this.model = null;
            this.isInitialized = false;
        }
    }
}

/**
 * Альтернативная реализация с использованием MediaPipe Face Detection (опционально)
 * Раскомментируйте, если хотите использовать MediaPipe вместо BlazeFace
 */
/*
class FaceDetectorServiceMediaPipe {
    constructor() {
        this.detector = null;
        this.isInitialized = false;
    }

    async initialize() {
        // Требует подключения @mediapipe/face_detection
        const vision = await FilesetResolver.forVisionTasks(
            "https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision/wasm"
        );

        this.detector = await FaceDetector.createFromOptions(vision, {
            baseOptions: {
                modelAssetPath: `https://storage.googleapis.com/mediapipe-models/face_detector/blaze_face_short_range/float16/1/blaze_face_short_range.tflite`,
                delegate: "GPU"
            },
            runningMode: "VIDEO"
        });

        this.isInitialized = true;
    }

    async detect(video) {
        if (!this.isInitialized) return [];

        const detections = this.detector.detectForVideo(video, performance.now());

        return detections.detections.map(det => {
            const bbox = det.boundingBox;
            return {
                x: bbox.originX,
                y: bbox.originY,
                width: bbox.width,
                height: bbox.height,
                confidence: det.categories[0].score,
                landmarks: det.keypoints
            };
        });
    }
}
*/
