/**
 * Main Application - Orchestrates face detection and anti-spoof pipeline
 * Порт из FrameProcessingPipeline и UI логики WinForms
 */

class FaceAntiSpoofApp {
    constructor() {
        // Сервисы
        this.faceDetector = new FaceDetectorService();
        this.antiSpoof = new AntiSpoofService();

        // DOM элементы
        this.video = document.getElementById('video');
        this.canvas = document.getElementById('canvas');
        this.ctx = this.canvas.getContext('2d');
        this.statusElement = document.getElementById('status');

        // UI элементы
        this.startButton = document.getElementById('startButton');
        this.stopButton = document.getElementById('stopButton');
        this.useGpuCheckbox = document.getElementById('useGpu');
        this.thresholdSlider = document.getElementById('threshold');
        this.thresholdValue = document.getElementById('thresholdValue');

        // Info панель
        this.fpsElement = document.getElementById('fps');
        this.faceCountElement = document.getElementById('faceCount');
        this.realProbElement = document.getElementById('realProb');
        this.spoofProbElement = document.getElementById('spoofProb');
        this.classificationElement = document.getElementById('classification');
        this.inferenceTimeElement = document.getElementById('inferenceTime');

        // Состояние
        this.isRunning = false;
        this.stream = null;
        this.animationId = null;
        this.threshold = 0.5;
        this.useGpu = true;

        // FPS счётчик
        this.frameCount = 0;
        this.lastFpsUpdate = performance.now();
        this.fps = 0;

        this.setupEventListeners();
    }

    setupEventListeners() {
        this.startButton.addEventListener('click', () => this.start());
        this.stopButton.addEventListener('click', () => this.stop());

        this.thresholdSlider.addEventListener('input', (e) => {
            this.threshold = parseFloat(e.target.value);
            this.thresholdValue.textContent = this.threshold.toFixed(2);
        });

        this.useGpuCheckbox.addEventListener('change', (e) => {
            this.useGpu = e.target.checked;
        });
    }

    async start() {
        try {
            this.updateStatus('Инициализация...');
            this.startButton.disabled = true;

            // 1. Инициализация камеры
            await this.initializeCamera();

            // 2. Инициализация моделей
            this.updateStatus('Загрузка моделей...');
            await this.initializeModels();

            // 3. Запуск обработки
            this.isRunning = true;
            this.stopButton.disabled = false;
            this.updateStatus('Работает');
            this.processFrame();

        } catch (error) {
            console.error('Ошибка запуска:', error);
            this.updateStatus('Ошибка: ' + error.message);
            this.startButton.disabled = false;
        }
    }

    async stop() {
        this.isRunning = false;
        this.stopButton.disabled = true;
        this.startButton.disabled = false;

        if (this.animationId) {
            cancelAnimationFrame(this.animationId);
            this.animationId = null;
        }

        if (this.stream) {
            this.stream.getTracks().forEach(track => track.stop());
            this.stream = null;
        }

        this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
        this.updateStatus('Остановлено');
    }

    async initializeCamera() {
        try {
            const constraints = {
                video: {
                    width: { ideal: 640 },
                    height: { ideal: 480 },
                    facingMode: 'user'
                }
            };

            this.stream = await navigator.mediaDevices.getUserMedia(constraints);
            this.video.srcObject = this.stream;

            // Ждём загрузки метаданных видео
            await new Promise((resolve) => {
                this.video.onloadedmetadata = () => {
                    this.video.play();
                    resolve();
                };
            });

            // Настройка canvas
            this.canvas.width = this.video.videoWidth;
            this.canvas.height = this.video.videoHeight;

            console.log(`Камера инициализирована: ${this.video.videoWidth}x${this.video.videoHeight}`);
        } catch (error) {
            throw new Error('Не удалось получить доступ к камере: ' + error.message);
        }
    }

    async initializeModels() {
        try {
            // Проверка поддержки WebGL
            const webglSupported = AntiSpoofService.isWebGLSupported();
            if (this.useGpu && !webglSupported) {
                console.warn('WebGL не поддерживается, используется CPU');
                this.useGpu = false;
            }

            // Инициализация детектора лиц
            await this.faceDetector.initialize(0.5);

            // Инициализация антиспуфинга
            await this.antiSpoof.initialize('models/best_model_quantized.onnx', this.useGpu);

            console.log('Все модели загружены');
        } catch (error) {
            throw new Error('Ошибка загрузки моделей: ' + error.message);
        }
    }

    async processFrame() {
        if (!this.isRunning) return;

        try {
            // 1. Детекция лиц
            const faces = await this.faceDetector.detectTopFaces(this.video, 1);
            this.faceCountElement.textContent = faces.length;

            // 2. Очистка canvas
            this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);

            // 3. Обработка каждого лица
            if (faces.length > 0) {
                for (const face of faces) {
                    // Классификация антиспуфинга
                    const result = await this.antiSpoof.classify(
                        this.video,
                        face,
                        this.threshold,
                        1.5 // expansionFactor
                    );

                    // Отрисовка результата
                    this.drawDetection(face, result);

                    // Обновление UI
                    this.updateInfoPanel(result);
                }
            } else {
                // Нет лиц
                this.resetInfoPanel();
            }

            // 4. Обновление FPS
            this.updateFPS();

        } catch (error) {
            console.error('Ошибка обработки кадра:', error);
        }

        // 5. Следующий кадр
        this.animationId = requestAnimationFrame(() => this.processFrame());
    }

    drawDetection(face, result) {
        const { x, y, width, height } = face;
        const { isReal, realProb, spoofProb } = result;

        // Цвет рамки в зависимости от классификации
        const color = isReal ? '#27ae60' : '#e74c3c';
        const label = isReal ? 'REAL' : 'SPOOF';

        // Рисуем bounding box
        this.ctx.strokeStyle = color;
        this.ctx.lineWidth = 3;
        this.ctx.strokeRect(x, y, width, height);

        // Рисуем background для текста
        const labelText = `${label} ${(realProb * 100).toFixed(1)}%`;
        this.ctx.font = 'bold 18px Arial';
        const textMetrics = this.ctx.measureText(labelText);
        const textHeight = 25;

        this.ctx.fillStyle = color;
        this.ctx.fillRect(x, y - textHeight, textMetrics.width + 10, textHeight);

        // Рисуем текст
        this.ctx.fillStyle = '#ffffff';
        this.ctx.fillText(labelText, x + 5, y - 7);

        // Рисуем confidence bar
        this.drawConfidenceBar(x, y + height + 5, width, realProb, spoofProb);
    }

    drawConfidenceBar(x, y, width, realProb, spoofProb) {
        const barHeight = 10;

        // Background
        this.ctx.fillStyle = 'rgba(255, 255, 255, 0.3)';
        this.ctx.fillRect(x, y, width, barHeight);

        // Real bar (green)
        const realWidth = width * realProb;
        this.ctx.fillStyle = '#27ae60';
        this.ctx.fillRect(x, y, realWidth, barHeight);

        // Spoof bar (red)
        const spoofWidth = width * spoofProb;
        this.ctx.fillStyle = '#e74c3c';
        this.ctx.fillRect(x + realWidth, y, spoofWidth, barHeight);

        // Border
        this.ctx.strokeStyle = '#ffffff';
        this.ctx.lineWidth = 1;
        this.ctx.strokeRect(x, y, width, barHeight);
    }

    updateInfoPanel(result) {
        const { isReal, realProb, spoofProb, inferenceTime } = result;

        this.realProbElement.textContent = (realProb * 100).toFixed(2) + '%';
        this.spoofProbElement.textContent = (spoofProb * 100).toFixed(2) + '%';
        this.inferenceTimeElement.textContent = inferenceTime + ' ms';

        // Classification с цветом
        this.classificationElement.textContent = isReal ? 'REAL' : 'SPOOF';
        this.classificationElement.className = 'value ' + (isReal ? 'real' : 'spoof');
    }

    resetInfoPanel() {
        this.realProbElement.textContent = 'N/A';
        this.spoofProbElement.textContent = 'N/A';
        this.classificationElement.textContent = 'N/A';
        this.classificationElement.className = 'value';
        this.inferenceTimeElement.textContent = 'N/A';
    }

    updateFPS() {
        this.frameCount++;
        const now = performance.now();
        const elapsed = now - this.lastFpsUpdate;

        if (elapsed >= 1000) {
            this.fps = Math.round((this.frameCount * 1000) / elapsed);
            this.fpsElement.textContent = this.fps;
            this.frameCount = 0;
            this.lastFpsUpdate = now;
        }
    }

    updateStatus(message) {
        this.statusElement.textContent = message;
    }
}

// Инициализация приложения при загрузке DOM
let app;
document.addEventListener('DOMContentLoaded', () => {
    console.log('Face Anti-Spoof Browser Application');
    console.log('Портировано из C# .NET WinForms проекта');

    app = new FaceAntiSpoofApp();

    // Проверка поддержки необходимых API
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
        alert('Ваш браузер не поддерживает доступ к камере');
    }

    if (!AntiSpoofService.isWebGLSupported()) {
        console.warn('WebGL не поддерживается, производительность может быть снижена');
    }
});

// Cleanup при закрытии страницы
window.addEventListener('beforeunload', () => {
    if (app && app.isRunning) {
        app.stop();
    }
});
