using System;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace SecureGate
{
    public class ReportsForm : Form
    {
        private readonly ListView.ListViewItemCollection _items;
        private readonly DataGridView _grid;
        private readonly Button _exportBtn;
        private readonly Label _statsLabel;
        private int _totalAnalisis;
        private int _limpios;
        private int _sospechosos;
        private int _maliciosos;

        public ReportsForm(ListView.ListViewItemCollection items)
        {
            _items = items;
            Text = "Reportes — SecureGate";
            StartPosition = FormStartPosition.CenterParent;
            Width = 1000;
            Height = 720;
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
                Text = "Reportes de Análisis",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(20, 0, 0, 0)
            };
            header.Controls.Add(titleLabel);
            Controls.Add(header);

            // Panel principal con contenido
            var container = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(245, 247, 250), Padding = new Padding(16) };

            // Tarjeta de estadísticas
            var statsPanel = new Panel
            {
                Height = 100,
                Dock = DockStyle.Top,
                BackColor = Color.White,
                Padding = new Padding(12)
            };
            statsPanel.BorderStyle = BorderStyle.FixedSingle;

            _statsLabel = new Label
            {
                Text = "Cargando estadísticas...",
                Font = new Font("Segoe UI", 11F),
                ForeColor = Color.FromArgb(55, 65, 81),
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft
            };
            statsPanel.Controls.Add(_statsLabel);

            var separator1 = new Panel { Height = 12, Dock = DockStyle.Top, BackColor = Color.FromArgb(245, 247, 250) };

            // Grid de detalles
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
            _exportBtn = new Button
            {
                Text = "📥 Exportar Resumen (.txt)",
                ForeColor = Color.White,
                BackColor = Color.FromArgb(37, 99, 235),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F),
                Height = 40,
                Width = 200,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            _exportBtn.FlatAppearance.BorderSize = 0;
            _exportBtn.Click += ExportBtn_Click;

            var btnClose = new Button
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
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (_, _) => Close();
            btnClose.Margin = new Padding(8, 0, 0, 0);

            var flowActions = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
            flowActions.Controls.Add(btnClose);
            flowActions.Controls.Add(_exportBtn);

            actionsPanel.Controls.Add(flowActions);

            // Armar contenedor
            container.Controls.Add(_grid);
            container.Controls.Add(separator2);
            container.Controls.Add(statsPanel);
            container.Controls.Add(separator1);

            Controls.Add(actionsPanel);
            Controls.Add(container);

            Load += ReportsForm_Load;
        }


        private void ReportsForm_Load(object? sender, EventArgs e)
        {
            try
            {
                var rows = _items.Cast<ListViewItem>().Select(i => new
                {
                    Nombre = i.SubItems.Count > 0 ? i.SubItems[0].Text : i.Text,
                    Origen = i.SubItems.Count > 1 ? i.SubItems[1].Text : string.Empty,
                    Fecha = i.SubItems.Count > 2 ? i.SubItems[2].Text : string.Empty,
                    Estado = i.SubItems.Count > 3 ? i.SubItems[3].Text : string.Empty
                }).ToList();

                _totalAnalisis = rows.Count;
                var byEstado = rows.GroupBy(r => string.IsNullOrWhiteSpace(r.Estado) ? "Sin estado" : r.Estado)
                    .Select(g => new { Estado = g.Key, Conteo = g.Count() })
                    .ToList();

                _limpios = byEstado.FirstOrDefault(x => x.Estado.Contains("Limpio"))?.Conteo ?? 0;
                _sospechosos = byEstado.FirstOrDefault(x => x.Estado.Contains("Sospechoso"))?.Conteo ?? 0;
                _maliciosos = byEstado.FirstOrDefault(x => x.Estado.Contains("Malicioso"))?.Conteo ?? 0;

                var sb = new StringBuilder();
                sb.AppendLine($"📊 Total de análisis: {_totalAnalisis}");
                sb.AppendLine($"✅ Limpios: {_limpios}");
                sb.AppendLine($"⚠️ Sospechosos: {_sospechosos}");
                sb.AppendLine($"❌ Maliciosos: {_maliciosos}");
                _statsLabel.Text = sb.ToString();

                // Colorear filas según estado
                foreach (var r in rows)
                {
                    int idx = _grid.Rows.Add(r.Nombre, r.Origen, r.Fecha, r.Estado);
                    if (_grid.Rows[idx] is DataGridViewRow row)
                    {
                        if (r.Estado.Contains("Limpio"))
                            row.DefaultCellStyle.BackColor = Color.FromArgb(240, 253, 244);
                        else if (r.Estado.Contains("Malicioso"))
                            row.DefaultCellStyle.BackColor = Color.FromArgb(254, 242, 242);
                        else if (r.Estado.Contains("Sospechoso"))
                            row.DefaultCellStyle.BackColor = Color.FromArgb(255, 252, 235);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Error al generar reportes: " + ex.Message);
            }
        }

        private void ExportBtn_Click(object? sender, EventArgs e)
        {
            try
            {
                using var dlg = new SaveFileDialog { Filter = "Texto|*.txt", FileName = "SecureGate_Resumen.txt" };
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                var sb = new StringBuilder();
                sb.AppendLine("═══════════════════════════════════════════");
                sb.AppendLine("     RESUMEN DE ANÁLISIS - SecureGate     ");
                sb.AppendLine("═══════════════════════════════════════════");
                sb.AppendLine();
                sb.AppendLine($"📊 Total de análisis: {_totalAnalisis}");
                sb.AppendLine($"✅ Limpios: {_limpios}");
                sb.AppendLine($"⚠️  Sospechosos: {_sospechosos}");
                sb.AppendLine($"❌ Maliciosos: {_maliciosos}");
                sb.AppendLine();
                sb.AppendLine("Detalle de archivos analizados:");
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
                MessageBox.Show(this, "Resumen exportado correctamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "No se pudo exportar: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
