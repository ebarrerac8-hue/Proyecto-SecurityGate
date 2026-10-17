using SecureGate.Cliente;
using SecureGate.Contratos;

namespace SecureGate;

/// <summary>
/// Ventana visual para el informe completo de un análisis SecureGate.
/// </summary>
internal sealed class InformeVisualForm : Form
{
    // ── Paleta ──────────────────────────────────────────
    private static readonly Color Azul       = Color.FromArgb(37,  99, 235);
    private static readonly Color AzulClaro  = Color.FromArgb(219, 234, 254);
    private static readonly Color FondoApp   = Color.FromArgb(245, 247, 250);
    private static readonly Color Blanco     = Color.White;
    private static readonly Color Gris       = Color.FromArgb(107, 114, 128);
    private static readonly Color GrisOscuro = Color.FromArgb(17,  24,  39);
    private static readonly Color GrisBorde  = Color.FromArgb(229, 231, 235);
    private static readonly Color Gris2Fondo = Color.FromArgb(243, 244, 246);
    private static readonly Color Verde      = Color.FromArgb(5,  150, 105);
    private static readonly Color VerdeFondo = Color.FromArgb(209, 250, 229);
    private static readonly Color Rojo       = Color.FromArgb(220,  38,  38);
    private static readonly Color RojoFondo  = Color.FromArgb(254, 226, 226);
    private static readonly Color Amarillo   = Color.FromArgb(202, 138,   4);
    private static readonly Color AmarFondo  = Color.FromArgb(254, 249, 195);

    private readonly EnvioCliente _envio;
    private Panel _scrollArea = null!;

    internal InformeVisualForm(EnvioCliente envio)
    {
        _envio = envio;
        BuildWindow();
        Populate();
    }

    // ────────────────────────────────────────────────────
    //  Estructura de la ventana
    // ────────────────────────────────────────────────────
    private void BuildWindow()
    {
        Text            = "Informe de SecureGate";
        Size            = new Size(840, 700);
        MinimumSize     = new Size(680, 500);
        StartPosition   = FormStartPosition.CenterScreen;
        BackColor       = FondoApp;
        Font            = new Font("Segoe UI", 10F);

        // ── Encabezado ──────────────────────────────────
        var header = new TableLayoutPanel
        {
            Dock        = DockStyle.Top,
            Height      = 78,
            BackColor   = Azul,
            ColumnCount = 2,
            RowCount    = 2,
            Padding     = new Padding(16, 10, 16, 10)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 45));

        var icoLabel = new Label
        {
            Text      = "🛡",
            Font      = new Font("Segoe UI", 22F),
            ForeColor = Blanco,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock      = DockStyle.Fill
        };
        header.Controls.Add(icoLabel, 0, 0);
        header.SetRowSpan(icoLabel, 2);

        var lblTitulo = new Label
        {
            Text      = "Informe de análisis",
            Font      = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = Blanco,
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft
        };
        var lblArchivo = new Label
        {
            Text      = _envio.Solicitud.AnalisisLocal.Archivo.NombreOriginal,
            Font      = new Font("Segoe UI", 9F),
            ForeColor = Color.FromArgb(191, 219, 254),
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft
        };
        header.Controls.Add(lblTitulo,  1, 0);
        header.Controls.Add(lblArchivo, 1, 1);
        Controls.Add(header);

        // ── Footer ──────────────────────────────────────
        var footer = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 52,
            BackColor = Blanco
        };
        footer.Paint += (_, e) =>
            e.Graphics.DrawLine(new Pen(GrisBorde, 1), 0, 0, footer.Width, 0);

        var btnExp = MakeBtn("📥  Exportar", Azul, Blanco);
        btnExp.Click += (_, _) => ExportarTxt();
        var btnClose = MakeBtn("Cerrar", GrisBorde, GrisOscuro);
        btnClose.Click += (_, _) => Close();

        footer.Controls.Add(btnClose);
        footer.Controls.Add(btnExp);
        footer.Resize += (_, _) =>
        {
            btnClose.Location = new Point(footer.Width - 120, 8);
            btnExp.Location   = new Point(footer.Width - 290, 8);
        };
        // Posición inicial
        btnClose.Location = new Point(700, 8);
        btnExp.Location   = new Point(530, 8);
        Controls.Add(footer);

        // ── Área de scroll ──────────────────────────────
        _scrollArea = new Panel
        {
            Dock       = DockStyle.Fill,
            AutoScroll = true,
            BackColor  = FondoApp,
            Padding    = new Padding(14, 12, 14, 12)
        };
        Controls.Add(_scrollArea);
    }

    // ────────────────────────────────────────────────────
    //  Relleno de secciones
    // ────────────────────────────────────────────────────
    private void Populate()
    {
        var items = new List<Control>();
        var arch  = _envio.Solicitud.AnalisisLocal.Archivo;
        var res   = _envio.Resultado;

        // 1. Archivo
        items.Add(Card("📁  Información del archivo",
            Row("Nombre",    arch.NombreOriginal),
            Row("Extensión", arch.Extension),
            Row("Tamaño",    $"{arch.TamanoBytes:N0} bytes  ({arch.TamanoBytes / 1048576.0:F2} MiB)"),
            Row("SHA-256",   arch.Sha256, mono: true),
            Row("Fecha",     arch.FechaRegistroUtc.ToLocalTime().ToString("g"))
        ));

        // 2. Estado
        var estColor = res is null ? Amarillo
            : res.Estado == EstadoAnalisis.Completado ? Verde
            : res.Estado == EstadoAnalisis.Fallido    ? Rojo : Gris;
        var estTexto = res is null ? "⏳  Esperando resultado del servidor"
            : LabelEstado(res.Estado);
        items.Add(StatusBadge(estTexto, estColor));

        // 3. Evaluación
        if (res is not null)
        {
            var (icon, col, fondo) = res.Evaluacion switch
            {
                EvaluacionRiesgo.AmenazaDetectada         => ("🚨", Rojo,     RojoFondo),
                EvaluacionRiesgo.Sospechoso               => ("⚠️", Amarillo, AmarFondo),
                EvaluacionRiesgo.SinIndicadoresDetectados => ("✅", Verde,    VerdeFondo),
                _                                         => ("❓", Gris,     Gris2Fondo)
            };
            items.Add(EvalCard(icon, LabelEval(res.Evaluacion), res.Resumen, col, fondo));

            if (res.Motivos.Count > 0)
                items.Add(BulletList("🔎  Motivos", res.Motivos, Azul));
            if (res.Limitaciones.Count > 0)
                items.Add(BulletList("⚙️  Limitaciones", res.Limitaciones, Gris));
        }

        // 4. Firma digial
        var firma = _envio.Solicitud.AnalisisLocal.FirmaDigital;
        items.Add(Card("🔏  Firma digital",
            Row("Estado",    firma.Estado.ToString()),
            Row("Firmante",  firma.Firmante ?? "—"),
            Row("Resultado", firma.Firma.ToString()),
            Row("Detalle",   firma.Detalle ?? "—")
        ));

        // 5. Antivirus
        var av = _envio.Solicitud.AnalisisLocal.Antivirus;
        items.Add(Card("🦠  Antivirus local (Defender)",
            Row("Estado",    av.Estado.ToString()),
            Row("Detección", av.Deteccion.ToString()),
            Row("Detalle",   av.Detalle ?? "—")
        ));

        // 6. VirusTotal
        if (res?.Reputacion is not null)
            items.Add(Card("🌐  Reputación VirusTotal",
                Row("Detalle", res.Reputacion.Detalle ?? "—")
            ));

        // 7. IA
        if (res?.Ia is not null)
        {
            var ia      = res.Ia;
            var inner   = new List<Control>();
            inner.Add(Row("Proveedor / Modelo", $"{ia.Proveedor}  —  {ia.Modelo}"));
            inner.Add(Row("Estado",             ia.Estado.ToString()));
            inner.Add(Row("Consulta",           ia.FechaConsultaUtc?.ToLocalTime().ToString("g") ?? "—"));
            inner.Add(HLine());

            if (!string.IsNullOrWhiteSpace(ia.Resumen))
                inner.Add(Quote(ia.Resumen, Azul, AzulClaro));

            inner.Add(Row("Comportamiento IA", ia.EvaluacionComportamiento == "SenalesSospechosas" ? "Señales que requieren revisión" : "Sin conclusión adicional"));
            foreach (var observacion in ia.ObservacionesComportamiento) inner.Add(ObsItem(observacion));
            if (ia.Observaciones.Count > 0)
            {
                inner.Add(SubLabel("Observaciones"));
                foreach (var obs in ia.Observaciones)
                    inner.Add(ObsItem(obs));
            }
            if (ia.Limitaciones.Count > 0)
                inner.Add(BulletList("Limitaciones IA", ia.Limitaciones, Gris));

            items.Add(CardFromList("🤖  Análisis de IA (Gemini)", inner));
        }

        // 8. Sandbox
        if (res?.Sandbox is not null)
        {
            var sb    = res.Sandbox;
            var inner = new List<Control>();
            inner.Add(Row("Estado",            sb.Estado.ToString()));
            inner.Add(Row("Muestra ejecutada", sb.MuestraEjecutada    ? "Sí" : "No"));
            inner.Add(Row("Observador",        sb.ObservadorIniciado  ? "Activo" : "No iniciado"));
            inner.Add(Row("Red habilitada",    sb.RedHabilitada       ? "Sí" : "No"));
            inner.Add(Row("Inicio",            sb.InicioUtc?.ToLocalTime().ToString("g") ?? "—"));
            inner.Add(Row("Fin",               sb.FinUtc?.ToLocalTime().ToString("g")   ?? "—"));

            if (sb.AnalisisOrigenId is not null)
                inner.Add(Quote(
                    $"Observación reutilizada del análisis {sb.AnalisisOrigenId}\n" +
                    $"Archivo de origen: {sb.NombreArchivoOrigen}", Amarillo, AmarFondo));

            if (sb.Eventos.Count > 0)
            {
                inner.Add(SubLabel($"Eventos ({sb.Eventos.Count})"));
                foreach (var ev in sb.Eventos)
                    inner.Add(EventItem(ev));
            }
            items.Add(CardFromList("🧪  Sandbox", inner));
        }

        // Espaciador final: evita que el último elemento quede tapado por el footer
        items.Add(new Panel { Height = 60, BackColor = FondoApp });

        // ── Montar ───────────────────────────────────────
        int pad = _scrollArea.Padding.Left;
        // Empezar con el padding superior para que la primera tarjeta
        // no quede tapada por el borde del panel
        int y = _scrollArea.Padding.Top;
        foreach (var ctrl in items)
        {
            ctrl.Top    = y;
            ctrl.Left   = pad;
            ctrl.Width  = _scrollArea.ClientSize.Width - pad * 2;
            ctrl.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            _scrollArea.Controls.Add(ctrl);
            y += ctrl.Height + 10;
        }

        _scrollArea.Resize += (_, _) =>
        {
            int w = _scrollArea.ClientSize.Width - pad * 2;
            foreach (Control c in _scrollArea.Controls) { c.Left = pad; c.Width = w; }
        };
    }

    // ────────────────────────────────────────────────────
    //  Constructores de controles
    // ────────────────────────────────────────────────────

    // Tarjeta con filas simples
    private static Panel Card(string titulo, params Control[] filas)
    {
        var p = WhitePanel();
        var lbl = SectionTitle(titulo);
        p.Controls.Add(lbl);
        int y = lbl.Bottom + 6;
        foreach (var f in filas)
        {
            StretchRow(f, p, y);
            y = f.Bottom + 3;
        }
        p.Height = y + 12;
        return p;
    }

    // Tarjeta con lista de controles arbitraria
    private static Panel CardFromList(string titulo, IEnumerable<Control> lista)
    {
        var p   = WhitePanel();
        var lbl = SectionTitle(titulo);
        p.Controls.Add(lbl);
        int y = lbl.Bottom + 6;
        foreach (var f in lista)
        {
            StretchRow(f, p, y);
            y = f.Bottom + 4;
        }
        p.Height = y + 12;
        return p;
    }

    // Tarjeta evaluación: ícono + título + descripción (sin cortes)
    private static Panel EvalCard(string icono, string titulo, string desc, Color col, Color fondo)
    {
        var p = new Panel { BackColor = fondo, AutoSize = false, Padding = new Padding(0) };

        // Usamos FlowLayoutPanel horizontal para evitar solapamiento
        var flow = new FlowLayoutPanel
        {
            FlowDirection  = FlowDirection.LeftToRight,
            AutoSize       = true,
            WrapContents   = false,
            Location       = new Point(12, 10),
            BackColor      = Color.Transparent
        };
        var lblIco = new Label
        {
            Text      = icono,
            Font      = new Font("Segoe UI Emoji", 14F),
            ForeColor = col,
            AutoSize  = true,
            Margin    = new Padding(0, 0, 8, 0),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var lblTit = new Label
        {
            Text      = titulo,
            Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = col,
            AutoSize  = true,
            Margin    = new Padding(0),
            TextAlign = ContentAlignment.MiddleLeft
        };
        flow.Controls.Add(lblIco);
        flow.Controls.Add(lblTit);
        p.Controls.Add(flow);

        if (!string.IsNullOrWhiteSpace(desc))
        {
            var lblDesc = new Label
            {
                Text         = desc,
                Font         = new Font("Segoe UI", 9.5F),
                ForeColor    = GrisOscuro,
                AutoSize     = true,
                MaximumSize  = new Size(750, 0),
                Location     = new Point(14, flow.Bottom + 6),
                BackColor    = Color.Transparent
            };
            p.Controls.Add(lblDesc);
            p.Height = lblDesc.Bottom + 14;
        }
        else
            p.Height = flow.Bottom + 14;

        return p;
    }

    // Badge de estado (línea única con color)
    private static Panel StatusBadge(string texto, Color col)
    {
        var p = WhitePanel();
        // Layout horizontal: bullet + texto
        var bullet = new Panel { BackColor = col, Size = new Size(6, 6), Location = new Point(14, 18) };
        var lbl = new Label
        {
            Text      = texto,
            Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = col,
            AutoSize  = true,
            Location  = new Point(26, 10)
        };
        p.Controls.Add(bullet);
        p.Controls.Add(lbl);
        p.Height = lbl.Bottom + 10;
        return p;
    }

    // Fila clave: valor
    private static Label Row(string key, string val, bool mono = false)
    {
        return new Label
        {
            Text         = $"{key}:   {val}",
            Font         = mono ? new Font("Consolas", 8.5F) : new Font("Segoe UI", 9.5F),
            ForeColor    = GrisOscuro,
            AutoSize     = true,
            MaximumSize  = new Size(780, 0),
            Padding      = new Padding(0, 2, 0, 2)
        };
    }

    private static Panel HLine()
        => new Panel { Height = 1, BackColor = GrisBorde, Margin = new Padding(0, 4, 0, 4) };

    private static Label SubLabel(string texto)
        => new Label
        {
            Text      = texto,
            Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Gris,
            AutoSize  = true,
            Padding   = new Padding(0, 6, 0, 2)
        };

    private static Panel Quote(string texto, Color borde, Color fondo)
    {
        var p = new Panel { BackColor = fondo, Padding = new Padding(12, 8, 12, 8) };
        p.Paint += (_, e) => e.Graphics.DrawLine(new Pen(borde, 4), 0, 0, 0, p.Height);
        var lbl = new Label
        {
            Text        = texto,
            Font        = new Font("Segoe UI", 9.5F, FontStyle.Italic),
            ForeColor   = GrisOscuro,
            AutoSize    = true,
            MaximumSize = new Size(740, 0),
            Location    = new Point(10, 8)
        };
        p.Controls.Add(lbl);
        p.Height = lbl.Bottom + 10;
        return p;
    }

    private static Panel BulletList(string titulo, IEnumerable<string> items, Color col)
    {
        var p   = WhitePanel();
        var lbl = SubLabel(titulo);
        StretchRow(lbl, p, 10);
        int y = lbl.Bottom + 4;
        foreach (var item in items)
        {
            var dot = new Panel { BackColor = col, Size = new Size(5, 5), Location = new Point(14, y + 8) };
            var li  = new Label
            {
                Text        = item,
                Font        = new Font("Segoe UI", 9.5F),
                ForeColor   = GrisOscuro,
                AutoSize    = true,
                MaximumSize = new Size(740, 0),
                Location    = new Point(24, y)
            };
            p.Controls.Add(dot);
            StretchRow(li, p, y);
            y = li.Bottom + 4;
        }
        p.Height = y + 10;
        return p;
    }

    private static Panel ObsItem(ObservacionIa obs)
    {
        var p = new Panel { BackColor = Gris2Fondo, Padding = new Padding(8) };
        var txtObs = new Label
        {
            Text        = "• " + obs.Texto,
            Font        = new Font("Segoe UI", 9.5F),
            ForeColor   = GrisOscuro,
            AutoSize    = true,
            MaximumSize = new Size(730, 0),
            Location    = new Point(8, 6)
        };
        p.Controls.Add(txtObs);
        int bottom = txtObs.Bottom;
        if (obs.Referencias.Count > 0)
        {
            var refs = new Label
            {
                Text        = "Referencias: " + string.Join(", ", obs.Referencias),
                Font        = new Font("Segoe UI", 8.5F),
                ForeColor   = Gris,
                AutoSize    = true,
                MaximumSize = new Size(730, 0),
                Location    = new Point(10, txtObs.Bottom + 2)
            };
            p.Controls.Add(refs);
            bottom = refs.Bottom;
        }
        p.Height = bottom + 10;
        return p;
    }

    private static Panel EventItem(EventoSandbox ev)
    {
        var (icono, col) = ev.Tipo switch
        {
            TipoEventoSandbox.ProcesoIniciado    => ("▶", Verde),
            TipoEventoSandbox.ProcesoFinalizado  => ("■", Gris),
            TipoEventoSandbox.ArchivoCreado      => ("+", Azul),
            TipoEventoSandbox.ArchivoModificado  => ("~", Amarillo),
            TipoEventoSandbox.ArchivoEliminado   => ("×", Rojo),
            TipoEventoSandbox.RegistroModificado => ("R", Amarillo),
            TipoEventoSandbox.ConexionIntentada  => ("W", Rojo),
            _                                    => ("•", Gris)
        };

        string detalle = $"{ev.NombreProceso}  {ev.Recurso}  {ev.Detalle}".Trim();
        // Una sola etiqueta con salto de línea: evita el problema de AutoSize
        // en FlowLayoutPanel que dejaba espacio vacío entre título y detalle
        string texto = string.IsNullOrEmpty(detalle)
            ? $"{icono}  {ev.Tipo}"
            : $"{icono}  {ev.Tipo}\n    {detalle}";

        var lbl = new Label
        {
            Text        = texto,
            ForeColor   = col,
            Font        = new Font("Segoe UI", 9F),
            AutoSize    = true,
            MaximumSize = new Size(740, 0),
            Location    = new Point(8, 6)
        };
        // El detalle va en color gris: usamos RichLabel emulado con dos labels simples
        var p = new Panel { BackColor = Gris2Fondo };
        p.Controls.Add(lbl);

        if (!string.IsNullOrEmpty(detalle))
        {
            // Línea 1: tipo (bold + color)
            var lblTipo = new Label
            {
                Text      = $"{icono}  {ev.Tipo}",
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = col,
                AutoSize  = true,
                Location  = new Point(8, 5)
            };
            // Línea 2: detalle (gris, justo debajo con poco margen)
            var lblDet = new Label
            {
                Text        = "    " + detalle,
                Font        = new Font("Segoe UI", 8.5F),
                ForeColor   = Gris,
                AutoSize    = true,
                MaximumSize = new Size(730, 0),
                Location    = new Point(8, 22)
            };
            p.Controls.Clear();
            p.Controls.Add(lblTipo);
            p.Controls.Add(lblDet);
            // Altura real: usamos GetPreferredSize para el label que puede envolver
            var sz = lblDet.GetPreferredSize(new Size(730, 0));
            p.Height = 22 + sz.Height + 8;
        }
        else
        {
            p.Controls.Clear();
            var lblTipo = new Label
            {
                Text = $"{icono}  {ev.Tipo}", Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = col, AutoSize = true, Location = new Point(8, 5)
            };
            p.Controls.Add(lblTipo);
            p.Height = 32;
        }
        return p;
    }

    // ────────────────────────────────────────────────────
    //  Helpers de layout
    // ────────────────────────────────────────────────────
    private static Panel WhitePanel()
    {
        var p = new Panel { BackColor = Blanco, Padding = new Padding(14, 10, 14, 10) };
        p.Paint += (_, e) => e.Graphics.DrawRectangle(
            new Pen(GrisBorde, 1), 0, 0, p.Width - 1, p.Height - 1);
        return p;
    }

    private static Label SectionTitle(string text) => new Label
    {
        Text      = text,
        Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
        ForeColor = GrisOscuro,
        AutoSize  = true,
        Location  = new Point(14, 10),
        MaximumSize = new Size(780, 0)
    };

    private static void StretchRow(Control c, Panel parent, int top)
    {
        c.Top    = top;
        c.Left   = 14;
        c.Width  = parent.Width - 28;
        c.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
        parent.Controls.Add(c);
        parent.Resize += (_, _) => c.Width = parent.Width - 28;
    }

    private static Button MakeBtn(string text, Color back, Color fore)
    {
        var b = new Button
        {
            Text = text, BackColor = back, ForeColor = fore,
            FlatStyle = FlatStyle.Flat, Height = 36, Width = 160,
            Font = new Font("Segoe UI", 9F)
        };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    // ────────────────────────────────────────────────────
    //  Labels de estado / evaluación
    // ────────────────────────────────────────────────────
    private static string LabelEstado(EstadoAnalisis e) => e switch
    {
        EstadoAnalisis.Completado => "✓  Análisis completado",
        EstadoAnalisis.Fallido    => "✗  Análisis fallido",
        EstadoAnalisis.EnProceso  => "…  En proceso",
        EstadoAnalisis.EnCola     => "○  En cola",
        EstadoAnalisis.Cancelado  => "-  Cancelado",
        _                         => e.ToString()
    };

    private static string LabelEval(EvaluacionRiesgo e) => e switch
    {
        EvaluacionRiesgo.AmenazaDetectada         => "Amenaza detectada",
        EvaluacionRiesgo.Sospechoso               => "Sospechoso",
        EvaluacionRiesgo.SinIndicadoresDetectados => "Sin indicadores detectados",
        _                                          => "Evaluación incompleta"
    };

    // ────────────────────────────────────────────────────
    //  Exportar
    // ────────────────────────────────────────────────────
    private void ExportarTxt()
    {
        using var dlg = new SaveFileDialog
        {
            Filter   = "Informe de texto|*.txt|JSON estructurado|*.json",
            FileName = "SecureGate_Informe.txt"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            string contenido = dlg.FilterIndex == 2 && _envio.Resultado is not null
                ? System.Text.Json.JsonSerializer.Serialize(_envio.Resultado,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true })
                : Form1.CrearInforme(_envio);
            File.WriteAllText(dlg.FileName, contenido, System.Text.Encoding.UTF8);
            MessageBox.Show(this, "Informe exportado correctamente.", "SecureGate",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Error al exportar: " + ex.Message);
        }
    }
}
