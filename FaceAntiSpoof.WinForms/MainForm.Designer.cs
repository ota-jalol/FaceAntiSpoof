namespace FaceAntiSpoof.WinForms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
            CleanupResources();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        // Основные контролы
        pictureBoxVideo = new PictureBox();
        panelControls = new Panel();
        btnOpenPhoto = new Button();
        btnOpenVideo = new Button();
        btnStartCamera = new Button();
        btnStop = new Button();
        cmbCamera = new ComboBox();
        lblCamera = new Label();
        cmbModel = new ComboBox();
        lblModel = new Label();
        trackBarThreshold = new TrackBar();
        lblThreshold = new Label();
        lblThresholdValue = new Label();
        lblResult = new Label();
        chkDirectML = new CheckBox();
        timerVideo = new System.Windows.Forms.Timer(components);

        ((System.ComponentModel.ISupportInitialize)pictureBoxVideo).BeginInit();
        ((System.ComponentModel.ISupportInitialize)trackBarThreshold).BeginInit();
        panelControls.SuspendLayout();
        SuspendLayout();

        // panelControls
        panelControls.Dock = DockStyle.Left;
        panelControls.Width = 240;
        panelControls.Padding = new Padding(10);
        panelControls.AutoScroll = true;
        panelControls.BackColor = System.Drawing.Color.FromArgb(245, 245, 245);
        panelControls.Controls.Add(chkDirectML);
        panelControls.Controls.Add(lblResult);
        panelControls.Controls.Add(lblThresholdValue);
        panelControls.Controls.Add(trackBarThreshold);
        panelControls.Controls.Add(lblThreshold);
        panelControls.Controls.Add(cmbModel);
        panelControls.Controls.Add(lblModel);
        panelControls.Controls.Add(cmbCamera);
        panelControls.Controls.Add(lblCamera);
        panelControls.Controls.Add(btnStop);
        panelControls.Controls.Add(btnStartCamera);
        panelControls.Controls.Add(btnOpenVideo);
        panelControls.Controls.Add(btnOpenPhoto);

        // btnOpenPhoto
        btnOpenPhoto.Dock = DockStyle.Top;
        btnOpenPhoto.Height = 36;
        btnOpenPhoto.Text = "Открыть фото";
        btnOpenPhoto.Margin = new Padding(0, 0, 0, 4);
        btnOpenPhoto.Click += BtnOpenPhoto_Click;

        // btnOpenVideo
        btnOpenVideo.Dock = DockStyle.Top;
        btnOpenVideo.Height = 36;
        btnOpenVideo.Text = "Открыть видео";
        btnOpenVideo.Click += BtnOpenVideo_Click;

        // btnStartCamera
        btnStartCamera.Dock = DockStyle.Top;
        btnStartCamera.Height = 36;
        btnStartCamera.Text = "Запустить камеру";
        btnStartCamera.Click += BtnStartCamera_Click;

        // btnStop
        btnStop.Dock = DockStyle.Top;
        btnStop.Height = 36;
        btnStop.Text = "Остановить";
        btnStop.Enabled = false;
        btnStop.Click += BtnStop_Click;

        // lblCamera
        lblCamera.Dock = DockStyle.Top;
        lblCamera.Text = "Камера:";
        lblCamera.Height = 20;
        lblCamera.Padding = new Padding(0, 8, 0, 0);

        // cmbCamera
        cmbCamera.Dock = DockStyle.Top;
        cmbCamera.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbCamera.Items.AddRange(new object[] { "Камера 0", "Камера 1", "Камера 2", "Камера 3", "Камера 4" });
        cmbCamera.SelectedIndex = 0;

        // lblModel
        lblModel.Dock = DockStyle.Top;
        lblModel.Text = "Модель:";
        lblModel.Height = 20;
        lblModel.Padding = new Padding(0, 8, 0, 0);

        // cmbModel
        cmbModel.Dock = DockStyle.Top;
        cmbModel.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbModel.Items.AddRange(new object[] { "INT8 (600 КБ)", "FP32 (1.82 МБ)" });
        cmbModel.SelectedIndex = 0;

        // lblThreshold
        lblThreshold.Dock = DockStyle.Top;
        lblThreshold.Text = "Порог решения:";
        lblThreshold.Height = 20;
        lblThreshold.Padding = new Padding(0, 8, 0, 0);

        // trackBarThreshold
        trackBarThreshold.Dock = DockStyle.Top;
        trackBarThreshold.Minimum = 0;
        trackBarThreshold.Maximum = 100;
        trackBarThreshold.Value = 50;
        trackBarThreshold.TickFrequency = 10;
        trackBarThreshold.Height = 45;
        trackBarThreshold.ValueChanged += TrackBarThreshold_ValueChanged;

        // lblThresholdValue
        lblThresholdValue.Dock = DockStyle.Top;
        lblThresholdValue.Text = "0.50";
        lblThresholdValue.Height = 20;
        lblThresholdValue.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;

        // chkDirectML
        chkDirectML.Dock = DockStyle.Top;
        chkDirectML.Text = "DirectML (GPU)";
        chkDirectML.Height = 28;
        chkDirectML.Padding = new Padding(0, 8, 0, 0);

        // lblResult
        lblResult.Dock = DockStyle.Top;
        lblResult.Text = "Готово к работе";
        lblResult.Height = 80;
        lblResult.Padding = new Padding(0, 12, 0, 0);
        lblResult.Font = new System.Drawing.Font("Segoe UI", 9.5f);

        // pictureBoxVideo
        pictureBoxVideo.Dock = DockStyle.Fill;
        pictureBoxVideo.SizeMode = PictureBoxSizeMode.Zoom;
        pictureBoxVideo.BackColor = System.Drawing.Color.Black;

        // timerVideo
        timerVideo.Interval = 33;
        timerVideo.Tick += TimerVideo_Tick;

        // MainForm
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new System.Drawing.Size(1024, 700);
        Controls.Add(pictureBoxVideo);
        Controls.Add(panelControls);
        Text = "FaceAntiSpoof";
        StartPosition = FormStartPosition.CenterScreen;
        FormClosing += MainForm_FormClosing;

        ((System.ComponentModel.ISupportInitialize)pictureBoxVideo).EndInit();
        ((System.ComponentModel.ISupportInitialize)trackBarThreshold).EndInit();
        panelControls.ResumeLayout(false);
        ResumeLayout(false);
    }

    #endregion

    private PictureBox pictureBoxVideo;
    private Panel panelControls;
    private Button btnOpenPhoto;
    private Button btnOpenVideo;
    private Button btnStartCamera;
    private Button btnStop;
    private ComboBox cmbCamera;
    private Label lblCamera;
    private ComboBox cmbModel;
    private Label lblModel;
    private TrackBar trackBarThreshold;
    private Label lblThreshold;
    private Label lblThresholdValue;
    private Label lblResult;
    private CheckBox chkDirectML;
    private System.Windows.Forms.Timer timerVideo;
}
