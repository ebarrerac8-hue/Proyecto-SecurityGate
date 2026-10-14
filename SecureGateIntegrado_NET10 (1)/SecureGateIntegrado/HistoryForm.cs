using System.Windows.Forms;

namespace SecureGate;

public class HistoryForm : Form
{
    private readonly ListView.ListViewItemCollection _items;
    private readonly DataGridView _grid;
    private readonly Label _resumen;

    public HistoryForm(ListView.ListViewItemCollection items)
    {
        _items = items;
        Text = "Historial — SecureGate";
        ListadoAnalisis.Construir(this, "Historial de análisis", "Exportar historial TXT",
            Exportar, out _grid, out _resumen);
        Load += (_, _) =>
        {
            try { _resumen.Text = ListadoAnalisis.Cargar(_items, _grid); }
            catch (Exception error) { MessageBox.Show(this, "No se pudo cargar: " + error.Message); }
        };
    }

    private void Exportar(object? sender, EventArgs e)
    {
        try { ListadoAnalisis.Exportar(this, _grid, _resumen.Text, "SecureGate_Historial.txt"); }
        catch (Exception error) { MessageBox.Show(this, "No se pudo exportar: " + error.Message); }
    }
}
