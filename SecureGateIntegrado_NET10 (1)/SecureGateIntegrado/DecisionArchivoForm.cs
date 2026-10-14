using SecureGate.Proteccion;

namespace SecureGate;

internal enum AccionDecisionArchivo { Mantener, Devolver, DevolverYAbrir, Eliminar, LiberarConRiesgo }

internal sealed class DecisionArchivoForm : Form
{
    internal AccionDecisionArchivo Accion { get; private set; } = AccionDecisionArchivo.Mantener;
    internal DecisionArchivoForm(string nombre, TipoResultadoDecision tipo, Action verInforme, bool permiteDevolucion = true)
    {
        Text = "SecureGate — resultado de tu descarga";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        Font = new Font("Segoe UI", 11F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ShowInTaskbar = true; TopMost = true;
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize = new Size(640, 0);
        bool limpio = tipo == TipoResultadoDecision.SinAmenazasReportadas;
        var tabla = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill,
            ColumnCount = 1, Padding = new Padding(24) };
        void Fila(Control control) { tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize)); tabla.Controls.Add(control); }
        Fila(new Label { Text = limpio ? "Análisis terminado" : tipo == TipoResultadoDecision.Riesgo
            ? "Se encontraron señales de riesgo" : "El análisis tiene comprobaciones pendientes",
            AutoSize = true, MaximumSize = new Size(650, 0), Font = new Font(Font, FontStyle.Bold),
            ForeColor = limpio ? Color.FromArgb(21, 128, 61) : Color.FromArgb(180, 45, 30),
            Margin = new Padding(0, 0, 0, 14) });
        Fila(new Label { Text = nombre, AutoSize = true, MaximumSize = new Size(650, 0),
            Margin = new Padding(0, 0, 0, 14) });
        Fila(new Label { AutoSize = true, MaximumSize = new Size(650, 0),
            Text = limpio ? "No se reportaron amenazas en las comprobaciones realizadas. Podés devolver el archivo a su carpeta o devolverlo y abrirlo."
                : tipo == TipoResultadoDecision.Riesgo ? "Recomendamos eliminar este archivo. Si decidís conservarlo fuera de cuarentena, tendrás que aceptar expresamente el riesgo."
                : "No se completaron todas las comprobaciones. Recomendamos mantener el archivo retenido y revisar el informe.",
            Margin = new Padding(0, 0, 0, 14) });
        Fila(new Label { Text = "El archivo sigue en cuarentena hasta que decidás. El análisis no garantiza seguridad.",
            AutoSize = true, MaximumSize = new Size(650, 0), Margin = new Padding(0, 0, 0, 20) });
        var botones = new FlowLayoutPanel { AutoSize = true, MaximumSize = new Size(680, 0),
            FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Dock = DockStyle.Fill };
        Button Boton(string texto, AccionDecisionArchivo accion)
        {
            var boton = new Button { Text = texto, AutoSize = true, Padding = new Padding(12, 8, 12, 8),
                Margin = new Padding(0, 0, 8, 8), Name = accion.ToString() };
            boton.Click += (_, _) => { Accion = accion; DialogResult = DialogResult.OK; Close(); };
            botones.Controls.Add(boton); return boton;
        }
        if (limpio)
        {
            Boton("Devolver a su carpeta", AccionDecisionArchivo.Devolver);
            Boton("Devolver y abrir", AccionDecisionArchivo.DevolverYAbrir);
        }
        else Boton("Eliminar archivo", AccionDecisionArchivo.Eliminar);
        var mantener = Boton("Mantener retenido", AccionDecisionArchivo.Mantener);
        if (!limpio && tipo != TipoResultadoDecision.NoFinalizado && permiteDevolucion)
            Boton("Liberar bajo mi responsabilidad", AccionDecisionArchivo.LiberarConRiesgo);
        Fila(botones);
        var informe = new LinkLabel { Text = "Ver informe completo", AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        informe.LinkClicked += (_, _) => verInforme();
        Fila(informe);
        Controls.Add(tabla);
        AcceptButton = mantener; CancelButton = mantener;
    }
}

internal sealed class ConfirmarRiesgoForm : Form
{
    internal ConfirmarRiesgoForm(string nombre, TipoResultadoDecision tipo)
    {
        Text = "SecureGate — confirmar devolución excepcional";
        StartPosition = FormStartPosition.CenterScreen; TopMost = true;
        AutoScaleMode = AutoScaleMode.Font; Font = new Font("Segoe UI", 11F);
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        var tabla = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false,
            AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(24) };
        tabla.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(580, 0),
            Text = nombre + "\n\n" + (tipo == TipoResultadoDecision.Riesgo
                ? "Este archivo tiene señales de riesgo. Devolverlo permitirá abrirlo en tu computadora y podría causar daños."
                : "El análisis tiene comprobaciones pendientes. No contamos con todas las evidencias previstas.") +
                "\n\nEsta acción devuelve el archivo, no lo ejecuta. Windows puede bloquearlo igualmente.",
            Margin = new Padding(0, 0, 0, 20) });
        var aceptar = new CheckBox { Name = "AceptarRiesgo", Text = "Entiendo el riesgo y acepto devolver este archivo bajo mi responsabilidad.",
            AutoSize = true, MaximumSize = new Size(580, 0), Margin = new Padding(0, 0, 0, 20) };
        tabla.Controls.Add(aceptar);
        var botones = new FlowLayoutPanel { AutoSize = true };
        var devolver = new Button { Name = "Confirmar", Text = "Sí, devolver el archivo", AutoSize = true,
            Padding = new Padding(10), Enabled = false, DialogResult = DialogResult.OK };
        var cancelar = new Button { Text = "Cancelar", AutoSize = true, Padding = new Padding(10),
            DialogResult = DialogResult.Cancel };
        aceptar.CheckedChanged += (_, _) => devolver.Enabled = aceptar.Checked;
        botones.Controls.AddRange(new Control[] { cancelar, devolver });
        tabla.Controls.Add(botones); Controls.Add(tabla);
        AcceptButton = cancelar; CancelButton = cancelar;
    }
}
