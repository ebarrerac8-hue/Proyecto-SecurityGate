using System;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace SecureGate
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            PrepararIntegracion();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Aplicar estilo general
            this.Font = new Font("Segoe UI", 10F);
            lblTitle.ForeColor = Color.White;

            // Agregar hover effects a los botones
            AddButtonHoverEffects();

            // Cargar logo si existe en la carpeta de la aplicación (varios nombres posibles)
            string[] candidates = new string[] { "logo_custom.png", "logo1.png", "logo2.png", "modulos.png", "logo.png", "shield1.png", "shield2.png" };
            foreach (var name in candidates)
            {
                string p = Path.Combine(Application.StartupPath, name);
                if (File.Exists(p))
                {
                    try
                    {
                        using (var ms = new MemoryStream(File.ReadAllBytes(p)))
                        {
                            using var imagen = Image.FromStream(ms);
                            pictureBoxLogo.Image = new Bitmap(imagen);
                        }
                        break;
                    }
                    catch { }
                }
            }

            // Centrar el botón dentro del dragPanel
            CenterSelectButton();

            // Si no se cargó ninguna imagen externa, generar un icono por código para que siempre quede bien presentado
            if (pictureBoxLogo?.Image == null && pictureBoxIcon?.Image == null)
            {
                try
                {
                    pictureBoxIcon.Image = GenerateDefaultLogo(pictureBoxIcon.Width, pictureBoxIcon.Height);
                }
                catch { }
            }
        }

        private void AddButtonHoverEffects()
        {
            // Preparar efectos hover para botones
            var buttons = new[] { btnHome, btnAnalyze, btnHistory, btnReports, btnConfig };
            foreach (var btn in buttons)
            {
                btn.MouseEnter += (s, e) => ButtonMouseEnter((Button)s);
                btn.MouseLeave += (s, e) => ButtonMouseLeave((Button)s);
            }
        }

        private void ButtonMouseEnter(Button btn)
        {
            if (btn == btnHome)
            {
                btn.BackColor = Color.FromArgb(29, 78, 216); // Azul más oscuro
            }
            else
            {
                btn.BackColor = Color.FromArgb(31, 41, 55); // Gris más oscuro
            }
            btn.Cursor = Cursors.Hand;
        }

        private void ButtonMouseLeave(Button btn)
        {
            if (btn == btnHome)
            {
                btn.BackColor = Color.FromArgb(37, 99, 235); // Azul base
            }
            else
            {
                btn.BackColor = Color.FromArgb(17, 24, 39); // Negro base
            }
        }

        private void CenterSelectButton()
        {
            if (dragPanel == null || selectFileButton == null || lblDrag == null) return;
            // Centrar horizontal
            selectFileButton.Left = Math.Max(16, (dragPanel.ClientSize.Width - selectFileButton.Width) / 2);
            // Colocar cerca del centro vertical, dejando espacio para subtitle
            selectFileButton.Top = Math.Max(16, (dragPanel.ClientSize.Height / 2) + 20);
            // Ajustar lblDrag padding para que no tape el botón
            // Ajustar tamaño y posición del label para reservar espacio para el icono
            lblDrag.Width = Math.Max(200, dragPanel.ClientSize.Width - 40);
            lblDrag.Left = 20;
            // Posicionar icono fijo arriba y centrar horizontalmente
            if (pictureBoxIcon != null)
            {
                pictureBoxIcon.Left = Math.Max(16, (dragPanel.ClientSize.Width - pictureBoxIcon.Width) / 2);
                pictureBoxIcon.Top = 16; // fijo a 16px del borde superior
                pictureBoxIcon.BringToFront();
                // Colocar el label debajo del icono
                lblDrag.Top = pictureBoxIcon.Bottom + 12;
            }
        }

        private void changeLogoButton_Click(object sender, EventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "Imagenes|*.png;*.jpg;*.jpeg;*.bmp;*.ico|Todos|*.*";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    // Cargar imagen en memoria para evitar bloquear el archivo original
                    var bytes = File.ReadAllBytes(dlg.FileName);
                    using (var ms = new MemoryStream(bytes))
                    {
                        var img = Image.FromStream(ms);
                        // Liberar imagen anterior
                        var old = pictureBoxLogo.Image;
                        pictureBoxLogo.Image = new Bitmap(img);
                        old?.Dispose();
                    }

                    // Guardar copia local como logo_custom.png para futura carga automática
                    try
                    {
                        var dest = Path.Combine(Application.StartupPath, "logo_custom.png");
                        File.WriteAllBytes(dest, bytes);
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"No se pudo cargar el logo: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void selectFileButton_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                string path = openFileDialog1.FileName;
                ProcessFile(path);
            }
        }

        private void ProcessFile(string path)
        {
            _ = AnalizarAsync(path, automatico: false);
        }

        private void dragPanel_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void dragPanel_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    // Procesar el primer archivo soltado
                    ProcessFile(files[0]);
                }
            }
        }

        private void dragPanel_Paint(object sender, PaintEventArgs e)
        {
            // Dibujar marco punteado con esquinas redondeadas aproximadas
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect = dragPanel.ClientRectangle;
            rect.Inflate(-8, -8);
            using (var pen = new Pen(Color.FromArgb(199, 210, 254), 2))
            {
                pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                g.DrawRectangle(pen, rect);
            }
        }

        private Image GenerateDefaultLogo(int w, int h)
        {
            var bmp = new Bitmap(w, h);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                // Escalas
                float ww = w;
                float hh = h;

                // Dibujar escudo (como un rombo con base redondeada)
                var shieldPath = new System.Drawing.Drawing2D.GraphicsPath();
                shieldPath.AddBezier(ww * 0.1f, hh * 0.15f, ww * 0.5f, hh * -0.05f, ww * 0.9f, hh * 0.15f, ww * 0.9f, hh * 0.15f);
                shieldPath.AddLine(ww * 0.9f, hh * 0.15f, ww * 0.9f, hh * 0.5f);
                shieldPath.AddArc(ww * 0.2f, hh * 0.45f, ww * 0.6f, hh * 0.6f, 0, 180);
                shieldPath.CloseFigure();

                using (var brush = new SolidBrush(Color.FromArgb(7, 94, 177)))
                using (var pen = new Pen(Color.FromArgb(3, 55, 99), Math.Max(2, w / 32)))
                {
                    g.FillPath(brush, shieldPath);
                    g.DrawPath(pen, shieldPath);
                }

                // Dibujar candado central
                float lockW = ww * 0.42f;
                float lockH = hh * 0.38f;
                float lockX = (ww - lockW) / 2;
                float lockY = hh * 0.36f;

                var rectLock = new RectangleF(lockX, lockY, lockW, lockH);
                using (var brush = new SolidBrush(Color.White))
                using (var pen = new Pen(Color.FromArgb(3, 55, 99), Math.Max(2, w / 44)))
                using (var path = CreateRoundedRectanglePath(rectLock, 6f))
                {
                    g.FillPath(brush, path);
                    g.DrawPath(pen, path);
                }

                // Arco superior del candado
                var arcRect = new RectangleF(lockX + lockW * 0.18f, lockY - lockH * 0.45f, lockW * 0.64f, lockH * 0.9f);
                using (var penArc = new Pen(Color.FromArgb(7, 94, 177), Math.Max(3, w / 28)))
                {
                    g.DrawArc(penArc, arcRect, 200, 140);
                }

                // Agujero
                float holeR = Math.Max(3, w / 32);
                var holeCenter = new PointF(ww / 2, lockY + lockH * 0.55f);
                using (var brushHole = new SolidBrush(Color.FromArgb(3, 55, 99)))
                {
                    g.FillEllipse(brushHole, holeCenter.X - holeR, holeCenter.Y - holeR, holeR * 2, holeR * 2);
                }
            }

            return bmp;
        }

        private System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectanglePath(RectangleF rect, float radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            float diameter = radius * 2f;
            var arc = new RectangleF(rect.Location, new SizeF(diameter, diameter));

            // top-left arc
            path.AddArc(arc, 180, 90);

            // top-right arc
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);

            // bottom-right arc
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // bottom-left arc
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        private void dragPanel_Resize(object sender, EventArgs e)
        {
            CenterSelectButton();
            dragPanel.Invalidate();
        }

        private void lblTitle_Click(object sender, EventArgs e)
        {

        }

        private void btnConfig_Click(object sender, EventArgs e)
        {
            MostrarConfiguracion();
        }

        private void lblDrag_Click(object sender, EventArgs e)
        {

        }

        private void listViewRecent_SelectedIndexChanged(object sender, EventArgs e)
        {
            MostrarSeleccion();
        }

        private void lblSignature_Click(object sender, EventArgs e)
        {

        }

        private void pictureBoxLogo_Click(object sender, EventArgs e)
        {

        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }
    }
}
