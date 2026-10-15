namespace SecureGate;

// Ventana no modal: minimizar o cerrar jamás cancela el análisis.
internal sealed class ProgresoArchivoForm : Form
{
    private readonly Label _estado;
    internal ProgresoArchivoForm(string nombre)
    {
        var textos = EstiloDialogoArchivo.Preparar(this, nombre, "Analizando archivo",
            "El archivo está retenido mientras se realiza el análisis.\nPuede minimizar o cerrar esta ventana. El análisis continuará en segundo plano.", false);
        MinimizeBox = true;
        _estado = new Label { Name = "EstadoProgreso", Text = "Esperando análisis…", AutoSize = true,
            MaximumSize = new Size(450, 0), Margin = new Padding(0, 0, 0, 12) };
        textos.Controls.Add(_estado);
        textos.Controls.Add(new ProgressBar { Style = ProgressBarStyle.Marquee, Width = 440, Height = 16,
            Margin = new Padding(0, 0, 0, 16) });
        Resize += (_, _) => { if (WindowState == FormWindowState.Minimized) Hide(); };
    }
    internal void Actualizar(string estado) { if (!IsDisposed) _estado.Text = estado; }
}
