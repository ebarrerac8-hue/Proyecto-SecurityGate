namespace SecureGate
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            panelLeft = new Panel();
            btnConfig = new Button();
            btnReports = new Button();
            btnHistory = new Button();
            btnAnalyze = new Button();
            btnHome = new Button();
            logoPanel = new Panel();
            pictureBoxLogo = new PictureBox();
            lblAppName = new Label();
            panelTop = new Panel();
            lblTitle = new Label();
            panelRight = new Panel();
            groupBoxSummary = new GroupBox();
            tableSummary = new TableLayoutPanel();
            lblName = new Label();
            lblType = new Label();
            lblSize = new Label();
            lblHash = new Label();
            lblSignature = new Label();
            lblReputation = new Label();
            groupBoxSandbox = new GroupBox();
            lblSandboxTitle = new Label();
            lblSandboxStatus = new Label();
            btnConfigureSandbox = new Button();
            panelMain = new Panel();
            groupBoxRecent = new GroupBox();
            listViewRecent = new ListView();
            columnHeaderName = new ColumnHeader();
            columnHeaderPath = new ColumnHeader();
            columnHeaderDate = new ColumnHeader();
            columnHeaderStatus = new ColumnHeader();
            dragPanel = new Panel();
            pictureBoxIcon = new PictureBox();
            selectFileButton = new Button();
            lblDrag = new Label();
            lblSubtitle = new Label();
            openFileDialog1 = new OpenFileDialog();
            panelLeft.SuspendLayout();
            logoPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBoxLogo).BeginInit();
            panelTop.SuspendLayout();
            panelRight.SuspendLayout();
            groupBoxSummary.SuspendLayout();
            tableSummary.SuspendLayout();
            panelMain.SuspendLayout();
            groupBoxRecent.SuspendLayout();
            dragPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBoxIcon).BeginInit();
            SuspendLayout();
            // 
            // panelLeft
            // 
            panelLeft.BackColor = Color.FromArgb(17, 24, 39);
            panelLeft.Controls.Add(btnConfig);
            panelLeft.Controls.Add(btnReports);
            panelLeft.Controls.Add(btnHistory);
            panelLeft.Controls.Add(btnAnalyze);
            panelLeft.Controls.Add(btnHome);
            panelLeft.Controls.Add(logoPanel);
            panelLeft.Dock = DockStyle.Left;
            panelLeft.Location = new Point(0, 0);
            panelLeft.Name = "panelLeft";
            panelLeft.Size = new Size(240, 650);
            panelLeft.TabIndex = 0;
            // 
            // btnConfig
            // 
            btnConfig.BackColor = Color.FromArgb(17, 24, 39);
            btnConfig.Cursor = Cursors.Hand;
            btnConfig.Dock = DockStyle.Bottom;
            btnConfig.FlatAppearance.BorderSize = 0;
            btnConfig.FlatStyle = FlatStyle.Flat;
            btnConfig.ForeColor = Color.FromArgb(209, 213, 219);
            btnConfig.Location = new Point(0, 602);
            btnConfig.Name = "btnConfig";
            btnConfig.Padding = new Padding(16, 0, 0, 0);
            btnConfig.Size = new Size(240, 48);
            btnConfig.TabIndex = 0;
            btnConfig.Text = "⚙️  Configuración";
            btnConfig.TextAlign = ContentAlignment.MiddleLeft;
            btnConfig.UseVisualStyleBackColor = false;
            btnConfig.Click += btnConfig_Click;
            // 
            // btnReports
            // 
            btnReports.BackColor = Color.FromArgb(17, 24, 39);
            btnReports.Cursor = Cursors.Hand;
            btnReports.Dock = DockStyle.Top;
            btnReports.FlatAppearance.BorderSize = 0;
            btnReports.FlatStyle = FlatStyle.Flat;
            btnReports.ForeColor = Color.FromArgb(209, 213, 219);
            btnReports.Location = new Point(0, 234);
            btnReports.Name = "btnReports";
            btnReports.Padding = new Padding(16, 0, 0, 0);
            btnReports.Size = new Size(240, 52);
            btnReports.TabIndex = 1;
            btnReports.Text = "📊 Reportes";
            btnReports.TextAlign = ContentAlignment.MiddleLeft;
            btnReports.UseVisualStyleBackColor = false;
            // 
            // btnHistory
            // 
            btnHistory.BackColor = Color.FromArgb(17, 24, 39);
            btnHistory.Dock = DockStyle.Top;
            btnHistory.FlatStyle = FlatStyle.Flat;
            btnHistory.ForeColor = Color.FromArgb(209, 213, 219);
            btnHistory.Location = new Point(0, 186);
            btnHistory.Name = "btnHistory";
            btnHistory.Padding = new Padding(16, 0, 0, 0);
            btnHistory.Size = new Size(240, 48);
            btnHistory.TabIndex = 2;
            btnHistory.Text = "📜 Historial";
            btnHistory.TextAlign = ContentAlignment.MiddleLeft;
            btnHistory.UseVisualStyleBackColor = false;
            // 
            // btnAnalyze
            // 
            btnAnalyze.BackColor = Color.FromArgb(17, 24, 39);
            btnAnalyze.Dock = DockStyle.Top;
            btnAnalyze.FlatStyle = FlatStyle.Flat;
            btnAnalyze.ForeColor = Color.FromArgb(209, 213, 219);
            btnAnalyze.Location = new Point(0, 138);
            btnAnalyze.Name = "btnAnalyze";
            btnAnalyze.Padding = new Padding(16, 0, 0, 0);
            btnAnalyze.Size = new Size(240, 48);
            btnAnalyze.TabIndex = 3;
            btnAnalyze.Text = "🔍 Analizar archivo";
            btnAnalyze.TextAlign = ContentAlignment.MiddleLeft;
            btnAnalyze.UseVisualStyleBackColor = false;
            // 
            // btnHome
            // 
            btnHome.BackColor = Color.FromArgb(37, 99, 235);
            btnHome.Cursor = Cursors.Hand;
            btnHome.Dock = DockStyle.Top;
            btnHome.FlatAppearance.BorderSize = 0;
            btnHome.FlatStyle = FlatStyle.Flat;
            btnHome.ForeColor = Color.White;
            btnHome.Location = new Point(0, 90);
            btnHome.Name = "btnHome";
            btnHome.Padding = new Padding(16, 0, 0, 0);
            btnHome.Size = new Size(240, 48);
            btnHome.TabIndex = 4;
            btnHome.Text = "🏠 Inicio";
            btnHome.TextAlign = ContentAlignment.MiddleLeft;
            btnHome.UseVisualStyleBackColor = false;
            // 
            // logoPanel
            // 
            logoPanel.BackColor = Color.FromArgb(17, 24, 39);
            logoPanel.Controls.Add(pictureBoxLogo);
            logoPanel.Controls.Add(lblAppName);
            logoPanel.Dock = DockStyle.Top;
            logoPanel.Location = new Point(0, 0);
            logoPanel.Name = "logoPanel";
            logoPanel.Padding = new Padding(12);
            logoPanel.Size = new Size(240, 90);
            logoPanel.TabIndex = 5;
            // 
            // pictureBoxLogo
            // 
            pictureBoxLogo.BackColor = Color.Transparent;
            pictureBoxLogo.Image = (Image)resources.GetObject("pictureBoxLogo.Image");
            pictureBoxLogo.Location = new Point(12, 20);
            pictureBoxLogo.Name = "pictureBoxLogo";
            pictureBoxLogo.Size = new Size(40, 40);
            pictureBoxLogo.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBoxLogo.TabIndex = 1;
            pictureBoxLogo.TabStop = false;
            pictureBoxLogo.Click += pictureBoxLogo_Click;
            // 
            // lblAppName
            // 
            lblAppName.AutoSize = true;
            lblAppName.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            lblAppName.ForeColor = Color.White;
            lblAppName.Location = new Point(64, 24);
            lblAppName.Name = "lblAppName";
            lblAppName.Size = new Size(142, 32);
            lblAppName.TabIndex = 0;
            lblAppName.Text = "SecureGate";
            // 
            // panelTop
            // 
            panelTop.BackColor = Color.FromArgb(37, 99, 235);
            panelTop.Controls.Add(lblTitle);
            panelTop.Dock = DockStyle.Top;
            panelTop.Location = new Point(240, 0);
            panelTop.Name = "panelTop";
            panelTop.Size = new Size(860, 80);
            panelTop.TabIndex = 2;
            // 
            // lblTitle
            // 
            lblTitle.AutoSize = true;
            lblTitle.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(16, 15);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(366, 41);
            lblTitle.TabIndex = 0;
            lblTitle.Text = "Bienvenido a SecureGate";
            lblTitle.Click += lblTitle_Click;
            // 
            // panelRight
            // 
            panelRight.BackColor = Color.WhiteSmoke;
            panelRight.Controls.Add(groupBoxSummary);
            panelRight.Dock = DockStyle.Right;
            panelRight.Location = new Point(780, 80);
            panelRight.Name = "panelRight";
            panelRight.Padding = new Padding(12);
            panelRight.Size = new Size(320, 570);
            panelRight.TabIndex = 1;
            // 
            // groupBoxSummary
            // 
            groupBoxSummary.Controls.Add(tableSummary);
            groupBoxSummary.Dock = DockStyle.Top;
            groupBoxSummary.Location = new Point(12, 12);
            groupBoxSummary.Name = "groupBoxSummary";
            groupBoxSummary.Size = new Size(296, 260);
            groupBoxSummary.TabIndex = 0;
            groupBoxSummary.TabStop = false;
            groupBoxSummary.Text = "Resumen del análisis";
            // 
            // tableSummary
            // 
            tableSummary.ColumnCount = 1;
            tableSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
            tableSummary.Controls.Add(lblName, 0, 0);
            tableSummary.Controls.Add(lblType, 0, 1);
            tableSummary.Controls.Add(lblSize, 0, 2);
            tableSummary.Controls.Add(lblHash, 0, 3);
            tableSummary.Controls.Add(lblSignature, 0, 4);
            tableSummary.Controls.Add(lblReputation, 0, 5);
            tableSummary.Dock = DockStyle.Fill;
            tableSummary.Location = new Point(3, 23);
            tableSummary.Name = "tableSummary";
            tableSummary.Padding = new Padding(8);
            tableSummary.RowCount = 6;
            tableSummary.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            tableSummary.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            tableSummary.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            tableSummary.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            tableSummary.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            tableSummary.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            tableSummary.Size = new Size(290, 234);
            tableSummary.TabIndex = 0;
            // 
            // lblName
            // 
            lblName.AutoSize = true;
            lblName.Location = new Point(11, 8);
            lblName.Name = "lblName";
            lblName.Size = new Size(77, 20);
            lblName.TabIndex = 0;
            lblName.Text = "Nombre: -";
            // 
            // lblType
            // 
            lblType.AutoSize = true;
            lblType.Location = new Point(11, 28);
            lblType.Name = "lblType";
            lblType.Size = new Size(52, 20);
            lblType.TabIndex = 1;
            lblType.Text = "Tipo: -";
            // 
            // lblSize
            // 
            lblSize.AutoSize = true;
            lblSize.Location = new Point(11, 48);
            lblSize.Name = "lblSize";
            lblSize.Size = new Size(74, 20);
            lblSize.TabIndex = 2;
            lblSize.Text = "Tamaño: -";
            // 
            // lblHash
            // 
            lblHash.AutoSize = true;
            lblHash.Location = new Point(11, 68);
            lblHash.Name = "lblHash";
            lblHash.Size = new Size(118, 20);
            lblHash.TabIndex = 3;
            lblHash.Text = "Hash SHA-256: -";
            // 
            // lblSignature
            // 
            lblSignature.AutoSize = true;
            lblSignature.Location = new Point(11, 88);
            lblSignature.Name = "lblSignature";
            lblSignature.Size = new Size(106, 20);
            lblSignature.TabIndex = 4;
            lblSignature.Text = "Firma digital: -";
            lblSignature.Click += lblSignature_Click;
            // 
            // lblReputation
            // 
            lblReputation.AutoSize = true;
            lblReputation.Location = new Point(11, 108);
            lblReputation.Name = "lblReputation";
            lblReputation.Size = new Size(97, 20);
            lblReputation.TabIndex = 5;
            lblReputation.Text = "Reputación: -";
            // 
            // groupBoxSandbox
            // 
            groupBoxSandbox.Location = new Point(0, 0);
            groupBoxSandbox.Name = "groupBoxSandbox";
            groupBoxSandbox.Size = new Size(200, 100);
            groupBoxSandbox.TabIndex = 0;
            groupBoxSandbox.TabStop = false;
            // 
            // lblSandboxTitle
            // 
            lblSandboxTitle.Location = new Point(0, 0);
            lblSandboxTitle.Name = "lblSandboxTitle";
            lblSandboxTitle.Size = new Size(100, 23);
            lblSandboxTitle.TabIndex = 0;
            // 
            // lblSandboxStatus
            // 
            lblSandboxStatus.Location = new Point(0, 0);
            lblSandboxStatus.Name = "lblSandboxStatus";
            lblSandboxStatus.Size = new Size(100, 23);
            lblSandboxStatus.TabIndex = 0;
            // 
            // btnConfigureSandbox
            // 
            btnConfigureSandbox.Location = new Point(0, 0);
            btnConfigureSandbox.Name = "btnConfigureSandbox";
            btnConfigureSandbox.Size = new Size(75, 23);
            btnConfigureSandbox.TabIndex = 0;
            // 
            // panelMain
            // 
            panelMain.BackColor = Color.FromArgb(245, 247, 250);
            panelMain.Controls.Add(groupBoxRecent);
            panelMain.Controls.Add(dragPanel);
            panelMain.Dock = DockStyle.Fill;
            panelMain.Location = new Point(240, 80);
            panelMain.Name = "panelMain";
            panelMain.Padding = new Padding(16);
            panelMain.Size = new Size(540, 570);
            panelMain.TabIndex = 0;
            // 
            // groupBoxRecent
            // 
            groupBoxRecent.Controls.Add(listViewRecent);
            groupBoxRecent.Dock = DockStyle.Fill;
            groupBoxRecent.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            groupBoxRecent.ForeColor = Color.FromArgb(55, 65, 81);
            groupBoxRecent.Location = new Point(16, 316);
            groupBoxRecent.Name = "groupBoxRecent";
            groupBoxRecent.Padding = new Padding(8);
            groupBoxRecent.Size = new Size(508, 238);
            groupBoxRecent.TabIndex = 0;
            groupBoxRecent.TabStop = false;
            groupBoxRecent.Text = "📋 Archivos recientes";
            // 
            // listViewRecent
            // 
            listViewRecent.BackColor = Color.White;
            listViewRecent.Columns.AddRange(new ColumnHeader[] { columnHeaderName, columnHeaderPath, columnHeaderDate, columnHeaderStatus });
            listViewRecent.Dock = DockStyle.Fill;
            listViewRecent.ForeColor = Color.FromArgb(55, 65, 81);
            listViewRecent.FullRowSelect = true;
            listViewRecent.Location = new Point(8, 31);
            listViewRecent.Name = "listViewRecent";
            listViewRecent.Size = new Size(492, 199);
            listViewRecent.TabIndex = 0;
            listViewRecent.UseCompatibleStateImageBehavior = false;
            listViewRecent.View = View.Details;
            listViewRecent.SelectedIndexChanged += listViewRecent_SelectedIndexChanged;
            // 
            // columnHeaderName
            // 
            columnHeaderName.Text = "Nombre";
            columnHeaderName.Width = 160;
            // 
            // columnHeaderPath
            // 
            columnHeaderPath.Text = "Ruta";
            columnHeaderPath.Width = 260;
            // 
            // columnHeaderDate
            // 
            columnHeaderDate.Text = "Fecha";
            columnHeaderDate.Width = 140;
            // 
            // columnHeaderStatus
            // 
            columnHeaderStatus.Text = "Estado";
            columnHeaderStatus.Width = 100;
            // 
            // dragPanel
            // 
            dragPanel.AllowDrop = true;
            dragPanel.BackColor = Color.White;
            dragPanel.Controls.Add(pictureBoxIcon);
            dragPanel.Controls.Add(selectFileButton);
            dragPanel.Controls.Add(lblDrag);
            dragPanel.Dock = DockStyle.Top;
            dragPanel.Location = new Point(16, 16);
            dragPanel.Name = "dragPanel";
            dragPanel.Size = new Size(508, 300);
            dragPanel.TabIndex = 1;
            dragPanel.DragDrop += dragPanel_DragDrop;
            dragPanel.DragEnter += dragPanel_DragEnter;
            // 
            // pictureBoxIcon
            // 
            pictureBoxIcon.BackColor = Color.Transparent;
            pictureBoxIcon.Image = (Image)resources.GetObject("pictureBoxIcon.Image");
            pictureBoxIcon.Location = new Point(218, 16);
            pictureBoxIcon.Name = "pictureBoxIcon";
            pictureBoxIcon.Size = new Size(88, 88);
            pictureBoxIcon.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBoxIcon.TabIndex = 2;
            pictureBoxIcon.TabStop = false;
            // 
            // selectFileButton
            // 
            selectFileButton.Anchor = AnchorStyles.None;
            selectFileButton.BackColor = Color.FromArgb(37, 99, 235);
            selectFileButton.FlatAppearance.BorderSize = 0;
            selectFileButton.FlatStyle = FlatStyle.Flat;
            selectFileButton.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            selectFileButton.ForeColor = Color.White;
            selectFileButton.Location = new Point(178, 233);
            selectFileButton.Name = "selectFileButton";
            selectFileButton.Size = new Size(160, 36);
            selectFileButton.TabIndex = 0;
            selectFileButton.Text = "📁 Seleccionar archivo";
            selectFileButton.UseVisualStyleBackColor = false;
            selectFileButton.Click += selectFileButton_Click;
            // 
            // lblDrag
            // 
            lblDrag.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lblDrag.Font = new Font("Segoe UI", 13F);
            lblDrag.ForeColor = Color.FromArgb(55, 65, 81);
            lblDrag.Location = new Point(20, 100);
            lblDrag.Name = "lblDrag";
            lblDrag.Size = new Size(468, 64);
            lblDrag.TabIndex = 1;
            lblDrag.Text = "Arrastra un archivo aquí\no selecciona un archivo desde tu equipo";
            lblDrag.TextAlign = ContentAlignment.MiddleCenter;
            lblDrag.Click += lblDrag_Click;
            // 
            // lblSubtitle
            // 
            lblSubtitle.Location = new Point(0, 0);
            lblSubtitle.Name = "lblSubtitle";
            lblSubtitle.Size = new Size(100, 23);
            lblSubtitle.TabIndex = 0;
            // 
            // openFileDialog1
            // 
            openFileDialog1.Filter = "Archivos de interés|*.exe;*.msi;*.bat;*.ps1;*.zip;*.*";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1100, 650);
            Controls.Add(panelMain);
            Controls.Add(panelRight);
            Controls.Add(panelTop);
            Controls.Add(panelLeft);
            Name = "Form1";
            Text = "SecureGate";
            Load += Form1_Load;
            panelLeft.ResumeLayout(false);
            logoPanel.ResumeLayout(false);
            logoPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBoxLogo).EndInit();
            panelTop.ResumeLayout(false);
            panelTop.PerformLayout();
            panelRight.ResumeLayout(false);
            groupBoxSummary.ResumeLayout(false);
            tableSummary.ResumeLayout(false);
            tableSummary.PerformLayout();
            panelMain.ResumeLayout(false);
            groupBoxRecent.ResumeLayout(false);
            dragPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pictureBoxIcon).EndInit();
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panelLeft;
        private System.Windows.Forms.Panel logoPanel;
        private System.Windows.Forms.Label lblAppName;
        private System.Windows.Forms.Button btnConfig;
        private System.Windows.Forms.Button btnReports;
        private System.Windows.Forms.Button btnHistory;
        private System.Windows.Forms.Button btnAnalyze;
        private System.Windows.Forms.Button btnHome;
        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Panel panelRight;
        private System.Windows.Forms.Panel panelMain;
        private System.Windows.Forms.Panel dragPanel;
        private System.Windows.Forms.Label lblDrag;
        private System.Windows.Forms.Button selectFileButton;
        private System.Windows.Forms.Label lblSubtitle;
        private System.Windows.Forms.GroupBox groupBoxRecent;
        private System.Windows.Forms.ListView listViewRecent;
        private System.Windows.Forms.ColumnHeader columnHeaderName;
        private System.Windows.Forms.ColumnHeader columnHeaderPath;
        private System.Windows.Forms.ColumnHeader columnHeaderDate;
        private System.Windows.Forms.ColumnHeader columnHeaderStatus;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.GroupBox groupBoxSummary;
        private System.Windows.Forms.TableLayoutPanel tableSummary;
        private System.Windows.Forms.Label lblName;
        private System.Windows.Forms.Label lblType;
        private System.Windows.Forms.Label lblSize;
        private System.Windows.Forms.Label lblHash;
        private System.Windows.Forms.Label lblSignature;
        private System.Windows.Forms.Label lblReputation;
        private System.Windows.Forms.PictureBox pictureBoxIcon;
        private System.Windows.Forms.GroupBox groupBoxSandbox;
        private System.Windows.Forms.Label lblSandboxTitle;
        private System.Windows.Forms.Label lblSandboxStatus;
        private System.Windows.Forms.Button btnConfigureSandbox;
        private System.Windows.Forms.PictureBox pictureBoxLogo;
        private System.Windows.Forms.Button changeLogoButton;
    }
}
