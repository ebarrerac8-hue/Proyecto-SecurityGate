using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using SecureGate.Cliente;

namespace SecureGate;

public class HistoryForm : Form
{
    private readonly List<ListViewItem> _items;
    private readonly DataGridView _grid;
    private readonly TextBox _buscar;
    private readonly Label _contador;
    private readonly Button _limpiar;
    private readonly Button _detalles;
    private readonly ContextMenuStrip _menuFiltros = new();
    private string _consulta = "";
    private string? _estado;
    private string? _resultado;
    private DateTime? _desde;
    private DateTime? _hasta;
    private bool _porNombre;
    private bool _ascendente;

    public HistoryForm(ListView.ListViewItemCollection items)
    {
        _items = items.Cast<ListViewItem>().ToList();
        Text = "Historial — SecureGate";
        Font = new Font("Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Font;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1000, 620);
        MinimumSize = new Size(740, 440);
        BackColor = Color.FromArgb(245, 247, 250);

        // Solo la tabla recibe el espacio flexible. Las barras no pueden desplazarla.
        var raiz = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize, Padding = new Padding(12)
        };
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        var titulo = new Label
        {
            Text = "Historial de archivos", Dock = DockStyle.Fill, AutoSize = false,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold), ForeColor = Color.White,
            BackColor = Color.FromArgb(37, 99, 235), TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 12, 0), Margin = new Padding(0, 0, 0, 6)
        };
        var herramientas = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1,
            Padding = new Padding(0, 6, 0, 6), Margin = Padding.Empty
        };
        herramientas.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        herramientas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        herramientas.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        herramientas.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _buscar = new TextBox { PlaceholderText = "Buscar archivo por nombre…",
            Dock = DockStyle.Fill, Margin = new Padding(0, 3, 6, 3) };
        var buscar = new Button { Text = "Buscar", AutoSize = true, Dock = DockStyle.Fill,
            Padding = new Padding(10, 0, 10, 0), Margin = new Padding(0, 0, 6, 0) };
        _limpiar = new Button { Text = "Limpiar filtros", AutoSize = true,
            Dock = DockStyle.Fill, Padding = new Padding(10, 0, 10, 0), Margin = Padding.Empty };
        herramientas.Controls.Add(_buscar, 0, 0);
        herramientas.Controls.Add(buscar, 1, 0);
        herramientas.Controls.Add(_limpiar, 2, 0);
        _contador = new Label { Dock = DockStyle.Fill, AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Margin = Padding.Empty };

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill, AutoSize = false, ReadOnly = true,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
            MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None, BackgroundColor = Color.White,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            EnableHeadersVisualStyles = false, ShowCellToolTips = true,
            BorderStyle = BorderStyle.FixedSingle, Margin = Padding.Empty,
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                BackColor = Color.FromArgb(229, 231, 235), ForeColor = Color.FromArgb(17, 24, 39),
                Padding = new Padding(6, 8, 6, 8), WrapMode = DataGridViewTriState.True
            },
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Padding = new Padding(6), WrapMode = DataGridViewTriState.False,
                ForeColor = Color.FromArgb(55, 65, 81),
                SelectionBackColor = Color.FromArgb(219, 234, 254),
                SelectionForeColor = Color.FromArgb(17, 24, 39)
            }
        };
        string[] encabezados = { "Nombre ↕", "Fecha ▾", "Estado del archivo ▾", "Resultado ▾" };
        float[] pesos = { 34, 19, 17, 30 };
        for (int i = 0; i < encabezados.Length; i++)
        {
            int col = _grid.Columns.Add("Columna" + i, encabezados[i]);
            _grid.Columns[col].FillWeight = pesos[i];
            _grid.Columns[col].MinimumWidth = i == 1 ? 150 : 120;
            _grid.Columns[col].SortMode = DataGridViewColumnSortMode.Programmatic;
        }
        var pie = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1,
            Padding = new Padding(0, 8, 0, 0), Margin = Padding.Empty };
        pie.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        pie.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++) pie.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _detalles = new Button { Text = "Ver detalles", AutoSize = true, Dock = DockStyle.Fill,
            Padding = new Padding(10, 0, 10, 0), Enabled = false };
        var exportar = new Button { Text = "Exportar TXT", AutoSize = true, Dock = DockStyle.Fill,
            Padding = new Padding(10, 0, 10, 0) };
        var cerrar = new Button { Text = "Cerrar", AutoSize = true, Dock = DockStyle.Fill,
            Padding = new Padding(10, 0, 10, 0), DialogResult = DialogResult.Cancel };
        pie.Controls.Add(_detalles, 1, 0);
        pie.Controls.Add(exportar, 2, 0);
        pie.Controls.Add(cerrar, 3, 0);
        raiz.Controls.Add(titulo, 0, 0);
        raiz.Controls.Add(herramientas, 0, 1);
        raiz.Controls.Add(_contador, 0, 2);
        raiz.Controls.Add(_grid, 0, 3);
        raiz.Controls.Add(pie, 0, 4);
        Controls.Add(raiz);
        CancelButton = cerrar;

        void Buscar() { _consulta = _buscar.Text.Trim(); Actualizar(); }
        buscar.Click += (_, _) => Buscar();
        _buscar.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Buscar(); }
        };
        _limpiar.Click += (_, _) =>
        {
            _buscar.Clear(); _consulta = ""; _estado = null; _resultado = null;
            _desde = null; _hasta = null; Actualizar();
        };
        cerrar.Click += (_, _) => Close();
        exportar.Click += (_, _) =>
        {
            try { ListadoAnalisis.Exportar(this, _grid, _contador.Text, "SecureGate_Historial_Filtrado.txt"); }
            catch (Exception error) { MessageBox.Show(this, "No se pudo exportar: " + error.Message); }
        };
        _detalles.Click += (_, _) => MostrarDetalles();
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) MostrarDetalles(); };
        _grid.SelectionChanged += (_, _) => _detalles.Enabled = _grid.SelectedRows.Count > 0;
        _grid.ColumnHeaderMouseClick += Encabezado;
        Load += (_, _) => Actualizar();
        void AjustarBarras()
        {
            raiz.SuspendLayout();
            herramientas.SuspendLayout();
            pie.SuspendLayout();
            float escala = DeviceDpi / 96f;
            Size Preparar(Button boton)
            {
                boton.MinimumSize = Size.Empty;
                boton.MaximumSize = Size.Empty;
                Size preferido = boton.GetPreferredSize(Size.Empty);
                int altoTexto = TextRenderer.MeasureText(boton.Text, boton.Font).Height;
                preferido.Height = Math.Max(preferido.Height, altoTexto + (int)(14 * escala));
                boton.AutoSize = false;
                boton.TextAlign = ContentAlignment.MiddleCenter;
                return preferido;
            }
            Size bBuscar = Preparar(buscar);
            Size bLimpiar = Preparar(_limpiar);
            herramientas.ColumnStyles[1].SizeType = SizeType.Absolute;
            herramientas.ColumnStyles[1].Width = bBuscar.Width + buscar.Margin.Horizontal;
            herramientas.ColumnStyles[2].SizeType = SizeType.Absolute;
            herramientas.ColumnStyles[2].Width = bLimpiar.Width + _limpiar.Margin.Horizontal;
            int altoBusqueda = Math.Max(bBuscar.Height + buscar.Margin.Vertical,
                bLimpiar.Height + _limpiar.Margin.Vertical);
            altoBusqueda = Math.Max(altoBusqueda, _buscar.PreferredHeight + _buscar.Margin.Vertical);
            raiz.RowStyles[0].Height = TextRenderer.MeasureText(titulo.Text, titulo.Font).Height +
                titulo.Margin.Vertical + (int)(20 * escala);
            raiz.RowStyles[1].Height = altoBusqueda + herramientas.Padding.Vertical;
            raiz.RowStyles[2].Height = TextRenderer.MeasureText("Mostrando archivos", Font).Height + (int)(12 * escala);
            int altoPie = 0;
            int columna = 1;
            foreach (Button boton in new[] { _detalles, exportar, cerrar })
            {
                Size preferido = Preparar(boton);
                pie.ColumnStyles[columna].SizeType = SizeType.Absolute;
                pie.ColumnStyles[columna++].Width = preferido.Width + boton.Margin.Horizontal;
                altoPie = Math.Max(altoPie, preferido.Height + boton.Margin.Vertical);
            }
            raiz.RowStyles[4].Height = altoPie + pie.Padding.Vertical;
            herramientas.ResumeLayout(true);
            pie.ResumeLayout(true);
            raiz.ResumeLayout(true);
        }
        DpiChanged += (_, _) => AjustarBarras();
        Shown += (_, _) =>
        {
            float escala = DeviceDpi / 96f;
            AjustarBarras();
            _grid.RowTemplate.Height = TextRenderer.MeasureText("Archivo", Font).Height + (int)(14 * escala);
            foreach (DataGridViewRow fila in _grid.Rows) fila.Height = _grid.RowTemplate.Height;
            var area = Screen.FromControl(this).WorkingArea;
            MinimumSize = new Size(Math.Min((int)(740 * escala), area.Width - 24),
                Math.Min((int)(440 * escala), area.Height - 24));
            Size = new Size(Math.Min(Width, area.Width - 40), Math.Min(Height, area.Height - 40));
        };
    }

    private static string Nombre(ListViewItem item) => item.SubItems.Count > 0 ? item.SubItems[0].Text : item.Text;
    private static DateTime? Fecha(ListViewItem item)
    {
        if (item.Tag is EnvioCliente envio) return envio.FechaCreacionUtc.LocalDateTime;
        return item.SubItems.Count > 2 && DateTime.TryParse(item.SubItems[2].Text,
            CultureInfo.CurrentCulture, DateTimeStyles.None, out var fecha) ? fecha : null;
    }
    private static string Estado(ListViewItem item) => item.Tag is EnvioCliente envio
        ? PresentacionAnalisis.EstadoArchivo(envio) : "Sin datos";
    private static string Resultado(ListViewItem item) => item.Tag is EnvioCliente envio
        ? PresentacionAnalisis.Resultado(envio) : PresentacionAnalisis.EnCurso;

    private void Actualizar()
    {
        var seleccionada = _grid.SelectedRows.Count > 0 ? _grid.SelectedRows[0].Tag as ListViewItem : null;
        var visibles = _items.Where(item =>
            Nombre(item).Contains(_consulta, StringComparison.OrdinalIgnoreCase) &&
            (_estado is null || Estado(item) == _estado) &&
            (_resultado is null || Resultado(item) == _resultado) &&
            (_desde is null || Fecha(item)?.Date >= _desde.Value.Date) &&
            (_hasta is null || Fecha(item)?.Date <= _hasta.Value.Date));
        IOrderedEnumerable<ListViewItem> ordenadas = _porNombre
            ? (_ascendente ? visibles.OrderBy(Nombre, StringComparer.CurrentCultureIgnoreCase)
                : visibles.OrderByDescending(Nombre, StringComparer.CurrentCultureIgnoreCase))
            : (_ascendente ? visibles.OrderBy(Fecha) : visibles.OrderByDescending(Fecha));
        _grid.Rows.Clear();
        DataGridViewRow? seleccionar = null;
        foreach (var item in ordenadas)
        {
            int i = _grid.Rows.Add(Nombre(item), Fecha(item)?.ToString("g") ?? "Sin fecha", Estado(item), Resultado(item));
            var fila = _grid.Rows[i];
            fila.Tag = item;
            foreach (DataGridViewCell celda in fila.Cells) celda.ToolTipText = celda.Value?.ToString() ?? "";
            if (Resultado(item) == PresentacionAnalisis.Amenaza)
                fila.DefaultCellStyle.BackColor = Color.FromArgb(254, 242, 242);
            else if (Resultado(item) == PresentacionAnalisis.Precaucion)
                fila.DefaultCellStyle.BackColor = Color.FromArgb(255, 252, 235);
            if (ReferenceEquals(item, seleccionada)) seleccionar = fila;
        }
        if (_grid.Rows.Count > 0)
        {
            _grid.ClearSelection();
            seleccionar ??= _grid.Rows[0];
            _grid.CurrentCell = seleccionar.Cells[0];
            seleccionar.Selected = true;
        }
        _detalles.Enabled = _grid.Rows.Count > 0;
        var filtros = new List<string>();
        if (_consulta.Length > 0) filtros.Add("Nombre: " + _consulta);
        if (_estado is not null) filtros.Add("Estado: " + _estado);
        if (_resultado is not null) filtros.Add("Resultado: " + _resultado);
        if (_desde is not null) filtros.Add("Desde: " + _desde.Value.ToString("d"));
        if (_hasta is not null) filtros.Add("Hasta: " + _hasta.Value.ToString("d"));
        _contador.Text = $"Mostrando {_grid.Rows.Count} de {_items.Count} archivos" +
            (filtros.Count == 0 ? "" : " | " + string.Join(" | ", filtros));
        _limpiar.Enabled = filtros.Count > 0;
        _grid.Columns[0].HeaderText = _porNombre ? (_ascendente ? "Nombre ↑" : "Nombre ↓") : "Nombre ↕";
        _grid.Columns[1].HeaderText = "Fecha" + (_desde is not null || _hasta is not null ? " •" : "") + " ▾";
        _grid.Columns[2].HeaderText = "Estado del archivo" + (_estado is not null ? " •" : "") + " ▾";
        _grid.Columns[3].HeaderText = "Resultado" + (_resultado is not null ? " •" : "") + " ▾";
    }

    private void Encabezado(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.ColumnIndex == 0)
        {
            _ascendente = !_porNombre || !_ascendente; _porNombre = true; Actualizar(); return;
        }
        var menu = _menuFiltros;
        menu.Close();
        var anteriores = menu.Items.Cast<ToolStripItem>().ToArray();
        menu.Items.Clear();
        foreach (var elemento in anteriores) elemento.Dispose();
        void Opcion(string texto, bool seleccionada, Action accion)
        {
            var opcion = new ToolStripMenuItem(texto) { Checked = seleccionada };
            opcion.Click += (_, _) => accion();
            menu.Items.Add(opcion);
        }
        if (e.ColumnIndex == 1)
        {
            Opcion("Más recientes primero", !_porNombre && !_ascendente,
                () => { _porNombre = false; _ascendente = false; Actualizar(); });
            Opcion("Más antiguos primero", !_porNombre && _ascendente,
                () => { _porNombre = false; _ascendente = true; Actualizar(); });
            menu.Items.Add(new ToolStripSeparator());
            Opcion("Filtrar por fechas…", _desde is not null || _hasta is not null, ElegirFechas);
            Opcion("Quitar filtro de fecha", false, () => { _desde = null; _hasta = null; Actualizar(); });
        }
        else if (e.ColumnIndex == 2)
        {
            Opcion("Todos los estados", _estado is null, () => { _estado = null; Actualizar(); });
            foreach (string valor in new[] { "Retenido", "Liberado", "Eliminado", "Sin retención" })
                Opcion(valor, _estado == valor, () => { _estado = valor; Actualizar(); });
        }
        else if (e.ColumnIndex == 3)
        {
            Opcion("Todos los resultados", _resultado is null, () => { _resultado = null; Actualizar(); });
            foreach (string valor in new[] { PresentacionAnalisis.SinAmenazas, PresentacionAnalisis.Amenaza,
                PresentacionAnalisis.Precaucion, PresentacionAnalisis.Pendientes, PresentacionAnalisis.EnCurso })
                Opcion(valor, _resultado == valor, () => { _resultado = valor; Actualizar(); });
        }
        else return;
        var rectangulo = _grid.GetCellDisplayRectangle(e.ColumnIndex, -1, true);
        menu.Show(_grid, new Point(rectangulo.Left, rectangulo.Bottom));
    }

    private void ElegirFechas()
    {
        using var ventana = new Form { Text = "Filtrar por fechas", StartPosition = FormStartPosition.CenterParent,
            Font = Font, AutoScaleMode = AutoScaleMode.Font, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false, MaximizeBox = false };
        var tabla = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16) };
        var desde = new DateTimePicker { Format = DateTimePickerFormat.Short, ShowCheckBox = true,
            Value = _desde ?? DateTime.Today, Checked = _desde.HasValue, Width = 210 };
        var hasta = new DateTimePicker { Format = DateTimePickerFormat.Short, ShowCheckBox = true,
            Value = _hasta ?? DateTime.Today, Checked = _hasta.HasValue, Width = 210 };
        tabla.Controls.Add(new Label { Text = "Desde", AutoSize = true }, 0, 0);
        tabla.Controls.Add(desde, 1, 0);
        tabla.Controls.Add(new Label { Text = "Hasta", AutoSize = true }, 0, 1);
        tabla.Controls.Add(hasta, 1, 1);
        var ayuda = new Label { Text = "Marque la casilla de la fecha que desea aplicar.", AutoSize = true,
            Margin = new Padding(3, 12, 3, 12) };
        tabla.Controls.Add(ayuda, 0, 2); tabla.SetColumnSpan(ayuda, 2);
        var aplicar = new Button { Text = "Aplicar", AutoSize = true };
        var cancelar = new Button { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
        tabla.Controls.Add(aplicar, 0, 3); tabla.Controls.Add(cancelar, 1, 3);
        aplicar.Click += (_, _) =>
        {
            if (desde.Checked && hasta.Checked && desde.Value.Date > hasta.Value.Date)
            { MessageBox.Show(ventana, "La fecha Desde debe ser anterior o igual a Hasta."); return; }
            _desde = desde.Checked ? desde.Value.Date : null;
            _hasta = hasta.Checked ? hasta.Value.Date : null;
            ventana.DialogResult = DialogResult.OK;
        };
        ventana.Controls.Add(tabla); ventana.AcceptButton = aplicar; ventana.CancelButton = cancelar;
        if (ventana.ShowDialog(this) == DialogResult.OK) Actualizar();
    }

    private void MostrarDetalles()
    {
        if (_grid.SelectedRows.Count == 0 || _grid.SelectedRows[0].Tag is not ListViewItem item) return;
        string texto = item.Tag is EnvioCliente envio
            ? "Ubicación de la muestra: " + envio.RutaArchivo + "\n\n" + Form1.CrearInforme(envio)
            : string.Join(Environment.NewLine, item.SubItems.Cast<ListViewItem.ListViewSubItem>().Select(s => s.Text));
        using var ventana = new Form { Text = "Detalles del archivo — SecureGate", Font = Font,
            StartPosition = FormStartPosition.CenterParent, Size = new Size(850, 620), MinimumSize = new Size(600, 400) };
        var contenido = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
            ScrollBars = ScrollBars.Both, WordWrap = false, Text = texto, Font = new Font("Consolas", 10F) };
        ventana.Controls.Add(contenido);
        ventana.ShowDialog(this);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) _menuFiltros.Dispose();
        base.Dispose(disposing);
    }

}
