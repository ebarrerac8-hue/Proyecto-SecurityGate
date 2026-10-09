using System;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace SecureGate
{
    public class HistoryForm : Form
    {
        private readonly ListView.ListViewItemCollection _items;
        private readonly DataGridView _grid;
        private readonly Label _infoLabel;

        public HistoryForm(ListView.ListViewItemCollection items)
        {
            _items = items;
            Text = "Historial — SecureGate";
            StartPosition = FormStartPosition.CenterParent;
            Width = 1000;
            Height = 680;
            Font = new Font("Segoe UI", 10F);
            BackColor = Color.FromArgb(245, 247, 250);
            Padding = new Padding(0);

            // Encabezado azul
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = Color.FromArgb(37, 99, 235)
            };

            var titleLabel = new Label
            {
                Text = "Historial de Análisis",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(20, 0, 0, 0)
            };
            header.Controls.Add(titleLabel);
            Controls.Add(header);

            // Panel principal
            var container = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(245, 247, 250),
                Padding = new Padding(16)
            };

            // Información superior
            _infoLabel = new Label
            {
                Text = "Cargando historial...",
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(55, 65, 81),
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 50,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var separator1 = new Panel { Height = 12, Dock = DockStyle.Top, BackColor = Color.FromArgb(245, 247, 250) };

            // Grid de historial
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BorderStyle = BorderStyle.None,
                BackgroundColor = Color.White,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(229, 231, 235),
                    ForeColor = Color.FromArgb(17, 24, 39),
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleLeft
                },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    ForeColor = Color.FromArgb(55, 65, 81),
                    BackColor = Color.White,
                    SelectionBackColor = Color.FromArgb(219, 234, 254),
                    SelectionForeColor = Color.FromArgb(17, 24, 39),
                    Padding = new Padding(4)
                }
            };
            _grid.Columns.Add("Nombre", "Nombre");
            _grid.Columns.Add("Origen", "Origen");
            _grid.Columns.Add("Fecha", "Fecha");
            _grid.Columns.Add("Estado", "Estado");

            var separator2 = new Panel { Height = 12, Dock = DockStyle.Top, BackColor = Color.FromArgb(245, 247, 250) };

            // Panel de acciones
            var actionsPanel = new Panel { Height = 48, Dock = DockStyle.Bottom, BackColor = Color.FromArgb(245, 247, 250) };

            var exportBtn = new Button
            {
                Text = "📥 Exportar Todo (.txt)",
                ForeColor = Color.White,
                BackColor = Color.FromArgb(37, 99, 235),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F),
                Height = 40,
                Width = 200,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            exportBtn.FlatAppearance.BorderSize = 0;
            exportBtn.Click += ExportAll_Click;

            var closeBtn = new Button
            {
                Text = "Cerrar",
                ForeColor = Color.FromArgb(55, 65, 81),
                BackColor = Color.FromArgb(229, 231, 235),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F),
                Height = 40,
                Width = 100,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.Click += (_, _) => Close();
            closeBtn.Margin = new Padding(8, 0, 0, 0);

            var flowActions = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
            flowActions.Controls.Add(closeBtn);
            flowActions.Controls.Add(exportBtn);

            actionsPanel.Controls.Add(flowActions);

            // Armar contenedor
            container.Controls.Add(_grid);
            container.Controls.Add(separator2);
            container.Controls.Add(_infoLabel);
            container.Controls.Add(separator1);

            Controls.Add(actionsPanel);
            Controls.Add(container);

            Load += HistoryForm_Load;
        }


        private void HistoryForm_Load(object? sender, EventArgs e)
        {
            try
            {
                int totalItems = 0;
                int limpios = 0;
                int sospechosos = 0;
                int maliciosos = 0;

                foreach (ListViewItem i in _items)
                {
                    var nombre = i.SubItems.Count > 0 ? i.SubItems[0].Text : i.Text;
                    var origen = i.SubItems.Count > 1 ? i.SubItems[1].Text : string.Empty;
                    var fecha = i.SubItems.Count > 2 ? i.SubItems[2].Text : string.Empty;
                    var estado = i.SubItems.Count > 3 ? i.SubItems[3].Text : string.Empty;

                    int idx = _grid.Rows.Add(nombre, origen, fecha, estado);
                    totalItems++;

                    // Contar estados y colorear
                    if (estado.Contains("Limpio"))
                    {
                        limpios++;
                        if (_grid.Rows[idx] is DataGridViewRow row)
                            row.DefaultCellStyle.BackColor = Color.FromArgb(240, 253, 244);
                    }
                    else if (estado.Contains("Malicioso"))
                    {
                        maliciosos++;
                        if (_grid.Rows[idx] is DataGridViewRow row)
                            row.DefaultCellStyle.BackColor = Color.FromArgb(254, 242, 242);
                    }
                    else if (estado.Contains("Sospechoso"))
                    {
                        sospechosos++;
                        if (_grid.Rows[idx] is DataGridViewRow row)
                            row.DefaultCellStyle.BackColor = Color.FromArgb(255, 252, 235);
                    }
                }

                var sb = new StringBuilder();
                sb.Append($"📊 Total: {totalItems}  |  ✅ Limpios: {limpios}  |  ⚠️ Sospechosos: {sospechosos}  |  ❌ Maliciosos: {maliciosos}");
                _infoLabel.Text = sb.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "No se pudo cargar el historial: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportAll_Click(object? sender, EventArgs e)
        {
            try
            {
                using var dlg = new SaveFileDialog { Filter = "Texto|*.txt", FileName = "SecureGate_Historial.txt" };
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                var sb = new StringBuilder();
                sb.AppendLine("═══════════════════════════════════════════");
                sb.AppendLine("     HISTORIAL DE ANÁLISIS - SecureGate    ");
                sb.AppendLine("═══════════════════════════════════════════");
                sb.AppendLine();
                sb.AppendLine("Archivos analizados:");
                sb.AppendLine("─────────────────────────────────────────");

                foreach (DataGridViewRow r in _grid.Rows)
                {
                    var nombre = r.Cells[0].Value?.ToString() ?? "";
                    var origen = r.Cells[1].Value?.ToString() ?? "";
                    var fecha = r.Cells[2].Value?.ToString() ?? "";
                    var estado = r.Cells[3].Value?.ToString() ?? "";
                    sb.AppendLine($"{nombre} | {origen} | {fecha} | {estado}");
                }

                System.IO.File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show(this, "Historial exportado correctamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "No se pudo exportar el historial: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
