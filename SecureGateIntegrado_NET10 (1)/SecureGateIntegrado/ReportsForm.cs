using System.Drawing;
using System.Text;
using System.Windows.Forms;
using SecureGate.Cliente;
using SecureGate.Contratos;

namespace SecureGate;

public class ReportsForm : Form
{
    private readonly ListView.ListViewItemCollection _items;
    private readonly DataGridView _estadisticas;
    private readonly Label _resumen;

    public ReportsForm(ListView.ListViewItemCollection items)
    {
        _items = items;
        Text = "Reportes — SecureGate";
        Font = new Font("Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Font;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(880, 570);
        MinimumSize = new Size(740, 480);
        var tabla = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16),
            ColumnCount = 1, RowCount = 5 };
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tabla.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var titulo = new Label { Text = "Resumen de análisis", Dock = DockStyle.Fill, AutoSize = true,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold), Padding = new Padding(12),
            BackColor = Color.FromArgb(37, 99, 235), ForeColor = Color.White };
        _resumen = new Label { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(4, 12, 4, 12) };
        _estadisticas = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            DefaultCellStyle = new DataGridViewCellStyle { Padding = new Padding(6), WrapMode = DataGridViewTriState.True },
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { Padding = new Padding(6),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold) } };
        _estadisticas.Columns.Add("Evaluacion", "Resultado de las comprobaciones");
        _estadisticas.Columns.Add("Cantidad", "Cantidad");
        _estadisticas.Columns[0].FillWeight = 80;
        _estadisticas.Columns[1].FillWeight = 20;
        var aclaracion = new Label { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(4, 10, 4, 10),
            Text = "Estos totales resumen los informes guardados. La ausencia de detecciones no garantiza seguridad.\n" +
                "Para consultar un archivo individual y su estado de eliminación o liberación, seleccione «Abrir historial»." };
        var acciones = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var cerrar = new Button { Text = "Cerrar", AutoSize = true, Padding = new Padding(10, 6, 10, 6) };
        var exportar = new Button { Text = "Exportar resumen TXT", AutoSize = true, Padding = new Padding(10, 6, 10, 6) };
        cerrar.Click += (_, _) => Close();
        exportar.Click += Exportar;
        var historial = new Button { Text = "Abrir historial", AutoSize = true, Padding = new Padding(10, 6, 10, 6) };
        historial.Click += (_, _) =>
        {
            using var ventana = new HistoryForm(_items);
            ventana.ShowDialog(this);
        };
        acciones.Controls.AddRange(new Control[] { cerrar, exportar, historial });
        tabla.Controls.Add(titulo, 0, 0);
        tabla.Controls.Add(_resumen, 0, 1);
        tabla.Controls.Add(_estadisticas, 0, 2);
        tabla.Controls.Add(aclaracion, 0, 3);
        tabla.Controls.Add(acciones, 0, 4);
        Controls.Add(tabla);
        Load += (_, _) => Cargar();
        Shown += (_, _) =>
        {
            var area = Screen.FromControl(this).WorkingArea;
            Size = new Size(Math.Min(Width, area.Width - 40), Math.Min(Height, area.Height - 40));
        };
    }

    private void Cargar()
    {
        var grupos = new Dictionary<string, int>
        {
            [PresentacionAnalisis.SinAmenazas] = 0,
            [PresentacionAnalisis.Pendientes] = 0,
            [PresentacionAnalisis.Precaucion] = 0,
            [PresentacionAnalisis.Amenaza] = 0,
            [PresentacionAnalisis.EnCurso] = 0
        };
        int eliminados = 0, liberados = 0, completos = 0;
        foreach (ListViewItem item in _items)
        {
            var envio = item.Tag as EnvioCliente;
            string categoria = envio is null ? PresentacionAnalisis.EnCurso : PresentacionAnalisis.Resultado(envio);
            grupos[categoria]++;
            if (envio?.EstadoLocal.StartsWith("Archivo eliminado") == true) eliminados++;
            if (envio?.EstadoLocal.StartsWith("Archivo liberado") == true) liberados++;
            if (envio is not null && envio.Resultado?.Estado == EstadoAnalisis.Completado &&
                PresentacionAnalisis.Faltantes(envio).Count == 0) completos++;
        }
        foreach (var grupo in grupos) _estadisticas.Rows.Add(grupo.Key, grupo.Value);
        _resumen.Text = $"Informes guardados: {_items.Count} | Informes completos: {completos}\nMuestras eliminadas: {eliminados} | Liberadas con autorización: {liberados}";
    }

    private void Exportar(object? sender, EventArgs e)
    {
        using var dialogo = new SaveFileDialog { Filter = "Texto|*.txt", FileName = "SecureGate_Resumen.txt" };
        if (dialogo.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var texto = new StringBuilder("SECUREGATE — RESUMEN\n\n");
            texto.AppendLine(_resumen.Text);
            foreach (DataGridViewRow fila in _estadisticas.Rows)
                texto.AppendLine(fila.Cells[0].Value + ": " + fila.Cells[1].Value);
            texto.AppendLine("La ausencia de detecciones no garantiza seguridad.");
            File.WriteAllText(dialogo.FileName, texto.ToString(), Encoding.UTF8);
            MessageBox.Show(this, "Resumen exportado.");
        }
        catch (Exception error) { MessageBox.Show(this, "No se pudo exportar: " + error.Message); }
    }
}
