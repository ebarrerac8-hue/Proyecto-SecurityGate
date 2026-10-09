using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SecureGate.Contratos;
using SecureGate.Cliente;

namespace SecureGate
{
    public partial class LiberarForm : Form
    {
        private readonly EnvioCliente _archivo;
        private CheckBox chkAceptandoRiesgo;
        private Label lblDescripcion;
        private Label lblArchivo;
        private Button btnLiberar;
        private Button btnCancelar;

        public LiberarForm(EnvioCliente archivo)
        {
            _archivo = archivo ?? throw new ArgumentNullException(nameof(archivo));
            InitializeComponent();
            ConfigurarUI();
        }

        private void InitializeComponent()
        {
            Text = "Liberar archivo bajo responsabilidad";
            Width = 500;
            Height = 350;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(245, 247, 250);
            AutoScaleMode = AutoScaleMode.Font;
        }

        private void ConfigurarUI()
        {
            var panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(37, 99, 235)
            };

            var lblTitulo = new Label
            {
                Text = "⚠️  Liberar archivo bajo responsabilidad",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(15, 0, 15, 0)
            };
            panelHeader.Controls.Add(lblTitulo);
            Controls.Add(panelHeader);

            var panelContenido = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15),
                AutoScroll = true
            };

            lblArchivo = new Label
            {
                Text = $"Archivo: {_archivo.Solicitud.AnalisisLocal!.Archivo.NombreOriginal}",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 10, 0, 15)
            };
            panelContenido.Controls.Add(lblArchivo);

            lblDescripcion = new Label
            {
                Text = 
                    "No has permitido el análisis del archivo en SecureGate. Si deseas usar este archivo, debes aceptar bajo tu responsabilidad " +
                    "los riesgos que puedan derivarse de su ejecución.\n\n" +
                    "SecureGate no será responsable por cualquier daño causado por este archivo.",
                Font = new Font("Segoe UI", 9F),
                AutoSize = true,
                MaximumSize = new Size(450, 200),
                Margin = new Padding(0, 0, 0, 20)
            };
            lblDescripcion.Location = new Point(15, lblArchivo.Bottom + 15);
            panelContenido.Controls.Add(lblDescripcion);

            chkAceptandoRiesgo = new CheckBox
            {
                Text = "✓ Acepto los riesgos y asumo total responsabilidad",
                Font = new Font("Segoe UI", 9F),
                AutoSize = true,
                Padding = new Padding(25, 0, 0, 0),
                Location = new Point(15, lblDescripcion.Bottom + 20)
            };
            chkAceptandoRiesgo.CheckedChanged += (_, _) => btnLiberar.Enabled = chkAceptandoRiesgo.Checked;
            panelContenido.Controls.Add(chkAceptandoRiesgo);

            Controls.Add(panelContenido);

            var panelBotones = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 45,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            btnLiberar = new Button
            {
                Text = "📋 Liberar",
                Font = new Font("Segoe UI", 9F),
                Width = 100,
                Height = 35,
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false,
                Location = new Point(Width - 225, 5)
            };
            btnLiberar.FlatAppearance.BorderSize = 0;
            btnLiberar.Click += (_, _) => DialogResult = DialogResult.OK;
            panelBotones.Controls.Add(btnLiberar);

            btnCancelar = new Button
            {
                Text = "Cancelar",
                Font = new Font("Segoe UI", 9F),
                Width = 100,
                Height = 35,
                BackColor = Color.FromArgb(229, 231, 235),
                ForeColor = Color.FromArgb(17, 24, 39),
                FlatStyle = FlatStyle.Flat,
                Location = new Point(Width - 115, 5)
            };
            btnCancelar.FlatAppearance.BorderSize = 0;
            btnCancelar.Click += (_, _) => DialogResult = DialogResult.Cancel;
            panelBotones.Controls.Add(btnCancelar);

            Controls.Add(panelBotones);
        }
    }
}
