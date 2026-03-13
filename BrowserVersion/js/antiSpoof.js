/**
 * AntiSpoofService - JavaScript port of C# AntiSpoofService
 * Порт из FaceAntiSpoof.Core/Services/AntiSpoofService.cs
 *
 * Использует ONNX Runtime Web для инференса модели MiniFASNetV2SE
 */

class AntiSpoofService {
    constructor() {
        this.session = null;
        this.inputName = null;
        this.outputName = null;
        this.isInitialized = false;
    }

    /**
     * Инициализация ONNX сессии
     * @param {string} modelPath - Путь к ONNX модели
     * @param {boolean} useGpu - Использовать GPU через WebGL
     */
    async initialize(modelPath, useGpu = true) {
        try {
            console.log('Загрузка модели антиспуфинга:', modelPath);

            // Настройка ONNX Runtime
            // WebGL может иметь проблемы с некоторыми операторами (например, BatchNormalization)
            // Используем только WASM для стабильности
            const options = {
                executionProviders: ['wasm'],
                graphOptimizationLevel: 'all',
                enableCpuMemArena: true,
                enableMemPattern: true,
            };

            // Создание сессии (аналог OnnxSessionManager)
            this.session = await ort.InferenceSession.create(modelPath, options);

            // Получаем имена входов/выходов
            this.inputName = this.session.inputNames[0];
            this.outputName = this.session.outputNames[0];

            console.log(`Модель загружена. Вход: ${this.inputName}, Выход: ${this.outputName}`);
            console.log('Execution providers:', this.session.executionProviders);

            this.isInitialized = true;
            return true;
        } catch (error) {
            console.error('Ошибка загрузки модели:', error);
            throw error;
        }
    }

    /**
     * Классификация лица (real/spoof)
     * Порт из AntiSpoofService.Classify()
     *
     * @param {HTMLVideoElement|HTMLCanvasElement} source - Источник видео
     * @param {Object} bbox - Bounding box {x, y, width, height}
     * @param {number} threshold - Порог для классификации (default 0.5)
     * @param {number} expansionFactor - Коэффициент расширения bbox
     * @returns {Promise<Object>} - {isReal, realProb, spoofProb, inferenceTime}
     */
    async classify(source, bbox, threshold = 0.5, expansionFactor = 1.5) {
        if (!this.isInitialized) {
            throw new Error('Модель не инициализирована. Вызовите initialize() сначала.');
        }

        const startTime = performance.now();

        try {
            // 1. Предобработка: crop → letterbox → normalize → CHW
            const inputTensor = ImagePreprocessor.preprocess(source, bbox, expansionFactor);

            // 2. Создание ONNX тензора
            const tensor = new ort.Tensor('float32', inputTensor, [1, 3, 128, 128]);

            // 3. Инференс
            const feeds = { [this.inputName]: tensor };
            const results = await this.session.run(feeds);
            const output = results[this.outputName];

            // 4. Получение логитов
            const logits = output.data; // Float32Array [logit_real, logit_spoof]
            const logitReal = logits[0];
            const logitSpoof = logits[1];

            // 5. Softmax (индекс 0 = real, индекс 1 = spoof)
            const { realProb, spoofProb } = this.softmax(logitReal, logitSpoof);

            const inferenceTime = performance.now() - startTime;

            // 6. Классификация по порогу
            const isReal = realProb >= threshold;

            return {
                isReal,
                realProb,
                spoofProb,
                inferenceTime: inferenceTime.toFixed(2)
            };

        } catch (error) {
            console.error('Ошибка при классификации:', error);
            throw error;
        }
    }

    /**
     * Softmax с численной стабильностью
     * Порт из TensorHelper.Softmax()
     *
     * @param {number} logitReal - Логит для класса "real"
     * @param {number} logitSpoof - Логит для класса "spoof"
     * @returns {Object} - {realProb, spoofProb}
     */
    softmax(logitReal, logitSpoof) {
        // Вычитаем max для численной стабильности
        const maxVal = Math.max(logitReal, logitSpoof);
        const expReal = Math.exp(logitReal - maxVal);
        const expSpoof = Math.exp(logitSpoof - maxVal);
        const sum = expReal + expSpoof;

        return {
            realProb: expReal / sum,
            spoofProb: expSpoof / sum
        };
    }

    /**
     * Освобождение ресурсов
     */
    dispose() {
        if (this.session) {
            // ONNX Runtime Web автоматически управляет памятью
            this.session = null;
            this.isInitialized = false;
        }
    }

    /**
     * Проверка поддержки WebGL
     */
    static isWebGLSupported() {
        try {
            const canvas = document.createElement('canvas');
            const gl = canvas.getContext('webgl') || canvas.getContext('experimental-webgl');
            return gl instanceof WebGLRenderingContext;
        } catch (e) {
            return false;
        }
    }
}
