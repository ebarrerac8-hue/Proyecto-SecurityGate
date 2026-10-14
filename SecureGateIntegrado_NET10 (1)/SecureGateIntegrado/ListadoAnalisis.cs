using System.Drawing;
using System.Text;
using System.Windows.Forms;
using SecureGate.Cliente;
using SecureGate.Contratos;

namespace SecureGate;

internal static class ListadoAnalisis
{
    internal static void Construir(Form ventana, string titulo, string exportacion,
        EventHandler exportar, out DataGridView grid, out Label resumen, Control? filtros = null)
    {
        ventana.SuspendLayout();
        ventana.Font = new Font("Segoe UI", 10F);
        ventana.AutoScaleMode = AutoScaleMode.Font;
        ventana.StartPosition = FormStartPosition.CenterParent;
        ventana.ClientSize = new Size(1060, 650);
        ventana.MinimumSize = new Size(800, 520);
        ventana.BackColor = Color.FromArgb(245, 247, 250);
        var tabla = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6,
            Padding = new Padding(12)
        };
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tabla.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tabla.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
        tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var encabezado = new Label
        {
            Text = titulo, AutoSize = true, Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            BackColor = Color.FromArgb(37, 99, 235), ForeColor = Color.White,
            Padding = new Padding(12), Margin = new Padding(0, 0, 0, 8)
        };
        resumen = new Label
        {
            AutoSize = true, Dock = DockStyle.Fill, Text = "Cargando…",
            Padding = new Padding(4, 8, 4, 12), Margin = Padding.Empty
        };
        grid = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
            AllowUserToDeleteRows = false, RowHeadersVisible = false,
            MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            EnableHeadersVisualStyles = false, ShowCellToolTips = true,
            BackgroundColor = Color.White, BorderStyle = BorderStyle.FixedSingle,
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(229, 231, 235),
                ForeColor = Color.FromArgb(17, 24, 39),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Padding = new Padding(4, 8, 4, 8),
                WrapMode = DataGridViewTriState.True
            },
            DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.White, ForeColor = Color.FromArgb(55, 65, 81),
                SelectionBackColor = Color.FromArgb(219, 234, 254),
                SelectionForeColor = Color.FromArgb(17, 24, 39),
                Padding = new Padding(4), WrapMode = DataGridViewTriState.True
            }
        };
        string[] nombres = { "Nombre", "Ubicación de la muestra", "Fecha", "Estado del archivo", "Resultado", "Procesamiento" };
        float[] pesos = { 20, 22, 14, 12, 20, 12 };
        for (int i = 0; i < nombres.Length; i++)
        {
            grid.Columns.Add("Columna" + i, nombres[i]);
            grid.Columns[i].MinimumWidth = i == 2 ? 125 : 130;
            grid.Columns[i].FillWeight = pesos[i];
        }
        var grupo = new GroupBox
        {
            Text = "Datos completos de la fila seleccionada", Dock = DockStyle.Fill,
            Padding = new Padding(8), Margin = new Padding(0, 8, 0, 4)
        };
        var detalle = new TextBox
        {
            Dock = DockStyle.Fill, ReadOnly = true, Multiline = true,
            WordWrap = true, ScrollBars = ScrollBars.Vertical,
            BackColor = Color.White, Text = "Seleccioná una fila para ver sus datos completos."
        };
        grupo.Controls.Add(detalle);
        var listado = grid;
        listado.SelectionChanged += (_, _) =>
        {
            if (listado.CurrentRow is not { } fila)
            { detalle.Text = "No hay una fila seleccionada."; return; }
            var texto = new StringBuilder();
            for (int i = 0; i < listado.Columns.Count; i++)
                texto.AppendLine(listado.Columns[i].HeaderText + ": " + fila.Cells[i].Value);
            detalle.Text = texto.ToString();
        };
        var botones = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft, WrapContents = false,
            Padding = new Padding(0, 6, 0, 0)
        };
        var cerrar = new Button
        {
            Text = "Cerrar", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12, 6, 12, 6), DialogResult = DialogResult.Cancel
        };
        var guardar = new Button
        {
            Text = exportacion, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12, 6, 12, 6), BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White, FlatStyle = FlatStyle.Flat
        };
        cerrar.Click += (_, _) => ventana.Close();
        guardar.Click += exportar;
        botones.Controls.Add(cerrar);
        botones.Controls.Add(guardar);
        ventana.CancelButton = cerrar;
        tabla.Controls.Add(encabezado, 0, 0);
        tabla.Controls.Add(resumen, 0, 1);
        if (filtros is not null) tabla.Controls.Add(filtros, 0, 2);
        tabla.Controls.Add(grid, 0, 3);
        tabla.Controls.Add(grupo, 0, 4);
        tabla.Controls.Add(botones, 0, 5);
        ventana.Controls.Add(tabla);
        var etiqueta = resumen;
        tabla.SizeChanged += (_, _) =>
        {
            int ancho = Math.Max(200, tabla.ClientSize.Width - tabla.Padding.Horizontal - 10);
            etiqueta.MaximumSize = new Size(ancho, 0);
            encabezado.MaximumSize = new Size(ancho, 0);
        };
        ventana.ResumeLayout(true);
    }

    internal static string Cargar(IEnumerable<ListViewItem> items, DataGridView grid)
    {
        var filas = items.ToList();
        grid.Rows.Clear();
        int sinAmenazas = 0, amenazas = 0, precaucion = 0, pendientes = 0;
        foreach (var item in filas)
        {
            string Campo(int i) => item.SubItems.Count > i ? item.SubItems[i].Text : "";
            var envio = item.Tag as EnvioCliente;
            string resultado = envio is null ? PresentacionAnalisis.EnCurso : PresentacionAnalisis.Resultado(envio);
            if (resultado == PresentacionAnalisis.SinAmenazas) sinAmenazas++;
            else if (resultado == PresentacionAnalisis.Amenaza) amenazas++;
            else if (resultado == PresentacionAnalisis.Precaucion) precaucion++;
            else pendientes++;
            int indice = grid.Rows.Add(Campo(0), Campo(1), Campo(2),
                envio is null ? Campo(3) : PresentacionAnalisis.EstadoArchivo(envio),
                resultado, envio is null ? "Sin datos" : PresentacionAnalisis.Procesamiento(envio));
            var fila = grid.Rows[indice];
            foreach (DataGridViewCell celda in fila.Cells) celda.ToolTipText = celda.Value?.ToString() ?? "";
            if (resultado == PresentacionAnalisis.Amenaza)
                fila.DefaultCellStyle.BackColor = Color.FromArgb(254, 242, 242);
            else if (resultado == PresentacionAnalisis.Precaucion)
                fila.DefaultCellStyle.BackColor = Color.FromArgb(255, 252, 235);
        }
        if (grid.Rows.Count > 0)
        {
            grid.ClearSelection();
            grid.CurrentCell = grid.Rows[0].Cells[0];
            grid.Rows[0].Selected = true;
        }
        return $"Sin amenazas reportadas: {sinAmenazas} | Precaución: {precaucion} | Amenazas: {amenazas} | Pendientes/sin resultado: {pendientes}";
    }

    internal static void Exportar(Form ventana, DataGridView grid, string resumen, string nombre)
    {
        using var dialogo = new SaveFileDialog { Filter = "Texto|*.txt", FileName = nombre };
        if (dialogo.ShowDialog(ventana) != DialogResult.OK) return;
        var texto = new StringBuilder("SECUREGATE — " + ventana.Text + "\n\n");
        texto.AppendLine(resumen);
        foreach (DataGridViewRow fila in grid.Rows)
        {
            texto.AppendLine();
            for (int i = 0; i < grid.Columns.Count; i++)
                texto.AppendLine(grid.Columns[i].HeaderText + ": " + fila.Cells[i].Value);
        }
        File.WriteAllText(dialogo.FileName, texto.ToString(), Encoding.UTF8);
        MessageBox.Show(ventana, "Exportación completada.", "SecureGate");
    }
}
