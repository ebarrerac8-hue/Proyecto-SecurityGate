using SecureGate.Proteccion;

namespace SecureGate;

internal enum AccionDecisionArchivo { Mantener, Devolver, DevolverYAbrir, Eliminar, LiberarConRiesgo, Reintentar }

// Controles estándar de Windows: contenido blanco y una única fila de acciones a la derecha.
internal static class EstiloDialogoArchivo
{
    internal static TableLayoutPanel Preparar(Form ventana, string nombre, string titulo, string mensaje, bool advertencia)
    {
        ventana.Text = "SecureGate";
        ventana.StartPosition = FormStartPosition.CenterScreen;
        ventana.AutoScaleMode = AutoScaleMode.Dpi;
        ventana.AutoScaleDimensions = new SizeF(96, 96);
        ventana.Font = SystemFonts.MessageBoxFont;
        ventana.FormBorderStyle = FormBorderStyle.FixedDialog;
        ventana.MaximizeBox = false; ventana.MinimizeBox = false;
        ventana.ShowInTaskbar = true; ventana.TopMost = true;
        ventana.AutoSize = true; ventana.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        ventana.BackColor = SystemColors.Window;
        var raiz = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill,
            MinimumSize = new Size(560, 0), Margin = Padding.Empty, Padding = Padding.Empty };
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var cuerpo = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill,
            Padding = new Padding(20, 22, 20, 16), Margin = Padding.Empty };
        cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
        cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var imagen = new PictureBox { Image = (advertencia ? SystemIcons.Warning : SystemIcons.Information).ToBitmap(),
            Size = new Size(32, 32), SizeMode = PictureBoxSizeMode.Zoom, Margin = Padding.Empty };
        imagen.Disposed += (_, _) => imagen.Image?.Dispose();
        cuerpo.Controls.Add(imagen, 0, 0);
        var textos = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill,
            Margin = Padding.Empty, Padding = Padding.Empty };
        void Agregar(Control control) { textos.RowStyles.Add(new RowStyle(SizeType.AutoSize)); textos.Controls.Add(control); }
        Agregar(new Label { Name = "Titulo", Text = titulo, AutoSize = true, MaximumSize = new Size(450, 0),
            Font = new Font("Segoe UI", 11F), ForeColor = SystemColors.HotTrack, Margin = new Padding(0, 0, 0, 12) });
        var archivo = new Label { Name = "NombreArchivo", Text = nombre, AutoSize = false,
            AutoEllipsis = true, Size = new Size(450, 22), Margin = new Padding(0, 0, 0, 8) };
        Agregar(archivo);
        var ayuda = new ToolTip(); ayuda.SetToolTip(archivo, nombre);
        ventana.Disposed += (_, _) => ayuda.Dispose();
        Agregar(new Label { Text = mensaje, AutoSize = true, MaximumSize = new Size(450, 0),
            Margin = new Padding(0, 0, 0, 12) });
        cuerpo.Controls.Add(textos, 1, 0);
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize)); raiz.Controls.Add(cuerpo);
        ventana.Controls.Add(raiz);
        return textos;
    }

    internal static Button Boton(string texto, string nombre) => new()
    {
        Text = texto, Name = nombre, AutoSize = false, Size = new Size(140, 30),
        Margin = new Padding(8, 0, 0, 0), Padding = Padding.Empty, UseVisualStyleBackColor = true
    };

    internal static void Pie(Form ventana, IEnumerable<Button> botones)
    {
        var raiz = (TableLayoutPanel)ventana.Controls[0];
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
        raiz.Controls.Add(new Panel { Height = 1, Dock = DockStyle.Fill, Margin = Padding.Empty,
            BackColor = SystemColors.ControlLight });
        var pie = new FlowLayoutPanel { Name = "Botones", AutoSize = true, Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft, WrapContents = false,
            Padding = new Padding(20, 12, 20, 12), Margin = Padding.Empty, BackColor = SystemColors.Control };
        foreach (var boton in botones.Reverse()) pie.Controls.Add(boton);
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize)); raiz.Controls.Add(pie);
        bool ajustando = false;
        void AjustarBotones()
        {
            if (ajustando || ventana.IsDisposed) return;
            ajustando = true;
            try
            {
                var acciones = pie.Controls.OfType<Button>().ToArray();
                if (acciones.Length == 0) return;
                float escala = ventana.DeviceDpi / 96f;
                var medidas = acciones.Select(b => TextRenderer.MeasureText(b.Text, b.Font,
                    Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding)).ToArray();
                int ancho = Math.Max((int)Math.Ceiling(140 * escala),
                    medidas.Max(m => m.Width) + (int)Math.Ceiling(24 * escala));
                int alto = Math.Max((int)Math.Ceiling(30 * escala),
                    medidas.Max(m => m.Height) + (int)Math.Ceiling(10 * escala));
                foreach (var boton in acciones)
                    if (boton.Size != new Size(ancho, alto)) boton.Size = new Size(ancho, alto);
                int requerido = acciones.Sum(b => b.Width + b.Margin.Horizontal) + pie.Padding.Horizontal;
                var minimo = new Size(Math.Max((int)Math.Ceiling(560 * escala), requerido), 0);
                if (raiz.MinimumSize != minimo) raiz.MinimumSize = minimo;
            }
            finally { ajustando = false; }
        }
        pie.Layout += (_, _) => AjustarBotones();
        ventana.FontChanged += (_, _) => AjustarBotones();
        ventana.Shown += (_, _) => AjustarBotones();
        ventana.DpiChanged += (_, _) => AjustarBotones();
        AjustarBotones();
    }
}

internal sealed class DecisionArchivoForm : Form
{
    internal AccionDecisionArchivo Accion { get; private set; } = AccionDecisionArchivo.Mantener;
    internal DecisionArchivoForm(string nombre, TipoResultadoDecision tipo, Action verInforme, bool permiteDevolucion = true)
    {
        bool limpio = tipo == TipoResultadoDecision.SinAmenazasReportadas;
        var textos = EstiloDialogoArchivo.Preparar(this, nombre,
            limpio ? "Análisis completado" : tipo == TipoResultadoDecision.Riesgo ? "Señales de riesgo detectadas" : "Análisis incompleto",
            limpio ? "No se detectaron amenazas en las comprobaciones realizadas.\nEl archivo permanece retenido."
                : tipo == TipoResultadoDecision.Riesgo ? "Se recomienda eliminar el archivo por las señales de riesgo detectadas.\nEl archivo permanece retenido."
                : "No se pudo completar el análisis.\nEl archivo permanece bloqueado.", !limpio);
        textos.Controls.Add(new Label { Text = "El análisis no garantiza seguridad.", AutoSize = true,
            ForeColor = SystemColors.GrayText, Margin = new Padding(0, 0, 0, 12) });
        var informe = new LinkLabel { Text = "Ver informe", AutoSize = true, Margin = Padding.Empty };
        informe.LinkClicked += (_, _) => verInforme(); textos.Controls.Add(informe);
        var botones = new List<Button>();
        Button Agregar(string texto, AccionDecisionArchivo accion)
        {
            var boton = EstiloDialogoArchivo.Boton(texto, accion.ToString());
            boton.Click += (_, _) => { Accion = accion; DialogResult = DialogResult.OK; Close(); };
            botones.Add(boton); return boton;
        }
        if (limpio)
        {
            Agregar("Mover a Descargas", AccionDecisionArchivo.Devolver);
            Agregar("Abrir", AccionDecisionArchivo.DevolverYAbrir);
        }
        else if (tipo == TipoResultadoDecision.NoFinalizado)
            Agregar("Reintentar", AccionDecisionArchivo.Reintentar);
        else Agregar("Eliminar", AccionDecisionArchivo.Eliminar);
        Button? mantener = null;
        if (!limpio)
        {
            mantener = Agregar("Mantener bloqueado", AccionDecisionArchivo.Mantener);
            if (tipo != TipoResultadoDecision.NoFinalizado && permiteDevolucion)
                Agregar("Permitir de todos modos", AccionDecisionArchivo.LiberarConRiesgo);
        }
        EstiloDialogoArchivo.Pie(this, botones);
        // Enter no devuelve ni abre una muestra por accidente. X/Escape conservan el archivo.
        if (mantener is not null) { AcceptButton = mantener; CancelButton = mantener; }
        else KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        KeyPreview = true;
        Shown += (_, _) => informe.Focus();
    }
}

internal sealed class ConfirmarRiesgoForm : Form
{
    internal ConfirmarRiesgoForm(string nombre, TipoResultadoDecision tipo)
    {
        var textos = EstiloDialogoArchivo.Preparar(this, nombre, "Permitir archivo",
            (tipo == TipoResultadoDecision.Riesgo ? "Permitir este archivo puede causar daños."
                : "No se completaron todas las comprobaciones.") +
            "\nEl archivo se devolverá a su carpeta original sin ejecutarse.", true);
        var aceptar = new CheckBox { Name = "AceptarRiesgo", Text = "Acepto permitir el archivo bajo mi responsabilidad.",
            AutoSize = true, MaximumSize = new Size(450, 0), Margin = new Padding(0, 4, 0, 4) };
        textos.Controls.Add(aceptar);
        var restaurar = EstiloDialogoArchivo.Boton("Permitir", "Confirmar");
        restaurar.Enabled = false; restaurar.DialogResult = DialogResult.OK;
        var cancelar = EstiloDialogoArchivo.Boton("Cancelar", "Cancelar");
        cancelar.DialogResult = DialogResult.Cancel;
        aceptar.CheckedChanged += (_, _) => restaurar.Enabled = aceptar.Checked;
        EstiloDialogoArchivo.Pie(this, new[] { restaurar, cancelar });
        AcceptButton = cancelar; CancelButton = cancelar;
        Shown += (_, _) => cancelar.Focus();
    }
}
