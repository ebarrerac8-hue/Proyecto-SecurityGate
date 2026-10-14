using System.Drawing;
using System.Windows.Forms;

namespace SecureGate;

public partial class Form1
{
    private void OrganizarPantalla(FlowLayoutPanel acciones)
    {
        SuspendLayout();
        var distribucion = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2,
            Margin = Padding.Empty, Padding = Padding.Empty
        };
        distribucion.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 255));
        distribucion.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        distribucion.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        distribucion.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        distribucion.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Clear();
        Controls.Add(distribucion);
        foreach (var panel in new[] { panelLeft, panelTop, panelMain, panelRight })
        {
            panel.Dock = DockStyle.Fill;
            panel.Margin = Padding.Empty;
        }
        distribucion.Controls.Add(panelLeft, 0, 0);
        distribucion.SetRowSpan(panelLeft, 2);
        distribucion.Controls.Add(panelTop, 1, 0);
        distribucion.SetColumnSpan(panelTop, 2);
        distribucion.Controls.Add(panelMain, 1, 1);
        distribucion.Controls.Add(panelRight, 2, 1);
        lblAppName.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        logoPanel.Controls.Clear();
        var marca = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
            Padding = new Padding(10), Margin = Padding.Empty };
        marca.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
        marca.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pictureBoxLogo.Dock = DockStyle.Fill;
        pictureBoxLogo.SizeMode = PictureBoxSizeMode.Zoom;
        lblAppName.Dock = DockStyle.Fill;
        lblAppName.AutoSize = true;
        lblAppName.AutoEllipsis = false;
        lblAppName.Text = "SecureGate";
        lblAppName.TextAlign = ContentAlignment.MiddleLeft;
        marca.Controls.Add(pictureBoxLogo, 0, 0);
        marca.Controls.Add(lblAppName, 1, 0);
        logoPanel.Controls.Add(marca);
        StartPosition = FormStartPosition.CenterScreen;
        lblTitle.AutoSize = false;
        lblTitle.Dock = DockStyle.Fill;
        lblTitle.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        lblTitle.TextAlign = ContentAlignment.MiddleLeft;
        lblTitle.Padding = new Padding(14, 6, 14, 6);

        panelMain.Padding = new Padding(12);
        groupBoxRecent.Text = "Archivos y resultados";
        listViewRecent.ShowItemToolTips = true;
        listViewRecent.HideSelection = false;
        columnHeaderName.Width = 190;
        columnHeaderPath.Width = 260;
        columnHeaderDate.Width = 155;
        columnHeaderStatus.Width = 290;
        columnHeaderPath.Text = "Ubicación de la muestra";
        dragPanel.Height = 140;

        panelRight.Padding = new Padding(10);
        panelRight.Controls.Clear();
        var contenido = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1, RowCount = 3, Padding = Padding.Empty
        };
        contenido.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++) contenido.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panelRight.Controls.Add(contenido);

        groupBoxSandbox.Controls.Clear();
        groupBoxSandbox.AutoSize = true;
        groupBoxSandbox.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        groupBoxSandbox.Dock = DockStyle.Fill;
        groupBoxSandbox.Padding = new Padding(10);
        var resultado = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 3
        };
        resultado.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++) resultado.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        foreach (var etiqueta in new[] { lblSandboxTitle, lblSandboxStatus })
        {
            etiqueta.AutoSize = true;
            etiqueta.Dock = DockStyle.Fill;
            etiqueta.Margin = new Padding(3, 6, 3, 8);
        }
        lblSandboxTitle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        btnConfigureSandbox.AutoSize = true;
        btnConfigureSandbox.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        btnConfigureSandbox.Padding = new Padding(8, 6, 8, 6);
        resultado.Controls.Add(lblSandboxTitle, 0, 0);
        resultado.Controls.Add(lblSandboxStatus, 0, 1);
        resultado.Controls.Add(btnConfigureSandbox, 0, 2);
        groupBoxSandbox.Controls.Add(resultado);
        contenido.Controls.Add(groupBoxSandbox, 0, 0);

        acciones.FlowDirection = FlowDirection.LeftToRight;
        acciones.WrapContents = true;
        acciones.Dock = DockStyle.Fill;
        acciones.Padding = new Padding(0, 6, 0, 6);
        foreach (Control control in acciones.Controls)
        {
            if (control is Button boton)
            {
                boton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                boton.Padding = new Padding(8, 6, 8, 6);
                boton.Margin = new Padding(3, 4, 3, 4);
            }
        }
        components ??= new System.ComponentModel.Container();
        var ayuda = new ToolTip(components);
        ayuda.SetToolTip(_pausar, "Detiene la espera del cliente. El trabajo del servidor puede continuar.");
        ayuda.SetToolTip(_reintentar, "Consulta el mismo trabajo; no crea un análisis nuevo.");
        _liberar.Text = "Liberar con autorización";
        contenido.Controls.Add(acciones, 0, 1);

        groupBoxSummary.Text = "Datos del archivo";
        groupBoxSummary.AutoSize = true;
        groupBoxSummary.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        groupBoxSummary.Dock = DockStyle.Fill;
        tableSummary.AutoSize = true;
        tableSummary.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        tableSummary.Dock = DockStyle.Top;
        foreach (var etiqueta in new[] { lblName, lblType, lblSize, lblHash, lblSignature, lblReputation })
            etiqueta.Dock = DockStyle.Fill;
        contenido.Controls.Add(groupBoxSummary, 0, 2);
        void AjustarTexto()
        {
            int ancho = Math.Max(150, panelRight.ClientSize.Width - panelRight.Padding.Horizontal - 42);
            foreach (var etiqueta in new[] { lblName, lblType, lblSize, lblHash,
                lblSignature, lblReputation, lblSandboxTitle, lblSandboxStatus })
                etiqueta.MaximumSize = new Size(ancho, 0);
        }
        panelRight.SizeChanged += (_, _) => AjustarTexto();
        Shown += (_, _) =>
        {
            var area = Screen.FromControl(this).WorkingArea;
            float escala = DeviceDpi / 96f;
            MinimumSize = new Size(Math.Min((int)(900 * escala), area.Width - 24),
                Math.Min((int)(500 * escala), area.Height - 24));
            Size = new Size(Math.Min((int)(1180 * escala), area.Width - 40),
                Math.Min((int)(680 * escala), area.Height - 40));
            Location = new Point(area.Left + (area.Width - Width) / 2,
                area.Top + (area.Height - Height) / 2);
            panelRight.AutoScrollPosition = Point.Empty;
            AjustarTexto();
        };
        // Medir la marca con el escalado real en vez de reservar un ancho fijo insuficiente.
        void AjustarMarca()
        {
            int requerido = TextRenderer.MeasureText(lblAppName.Text, lblAppName.Font).Width +
                pictureBoxLogo.Width + marca.Padding.Horizontal + 36;
            distribucion.ColumnStyles[0].Width = Math.Max((int)(220 * DeviceDpi / 96f), requerido);
        }
        Shown += (_, _) => AjustarMarca();
        DpiChanged += (_, _) => AjustarMarca();
        AjustarTexto();
        ResumeLayout(true);
    }
}
