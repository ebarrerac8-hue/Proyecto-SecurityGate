using System.Windows.Forms;

namespace SecureGate;

public class ReportsForm : Form
{
    private readonly ListView.ListViewItemCollection _items;
    private readonly DataGridView _grid;
    private readonly Label _resumen;

    public ReportsForm(ListView.ListViewItemCollection items)
    {
        _items = items;
        Text = "Reportes — SecureGate";
        ListadoAnalisis.Construir(this, "Reportes de análisis", "Exportar resumen TXT",
            Exportar, out _grid, out _resumen);
        Load += (_, _) =>
        {
            try { _resumen.Text = ListadoAnalisis.Cargar(_items, _grid); }
            catch (Exception error) { MessageBox.Show(this, "No se pudo cargar: " + error.Message); }
        };
    }

    private void Exportar(object? sender, EventArgs e)
    {
        try { ListadoAnalisis.Exportar(this, _grid, _resumen.Text, "SecureGate_Resumen.txt"); }
        catch (Exception error) { MessageBox.Show(this, "No se pudo exportar: " + error.Message); }
    }
}
