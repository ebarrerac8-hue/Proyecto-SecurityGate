using SecureGate.Cliente;
using SecureGate.Contratos;
using SecureGate.Proteccion;

namespace SecureGate;

public partial class Form1
{
    private readonly ColaDecisionesCliente _decisiones = new();
    private bool _decisionVisible;
    private ProgresoArchivoForm? _progresoActual;
    private bool _procesandoDecisiones;

    private void EncolarDecision(EnvioCliente envio)
    {
        if (_cerrado || !EsMuestraRetenida(envio) || !_decisiones.Encolar(envio)) return;
        ProcesarDecisiones();
    }

    private void ProcesarDecisiones()
    {
        if (_cerrado || _procesandoDecisiones || _decisionVisible || _cola.CurrentCount == 0 || _decisiones.Count == 0) return;
        _procesandoDecisiones = true;
        EnInterfaz(async () =>
        {
            try
            {
                while (!_cerrado && _cola.CurrentCount > 0 && _decisiones.TryTomar(out var envio))
                {
                    try
                    {
                        if (envio!.DecisionUsuarioPendiente && EsMuestraRetenida(envio))
                            await MostrarDecisionAsync(envio);
                    }
                    finally { _decisiones.Finalizar(envio!); }
                }
            }
            catch (Exception error)
            {
                if (!_cerrado) Notificar("La decisión no se completó. El archivo permanece retenido: " + error.Message);
            }
            finally { _procesandoDecisiones = false; }
        });
    }

    private async Task MostrarDecisionAsync(EnvioCliente envio)
    {
        if (_decisionVisible || _cerrado || !EsMuestraRetenida(envio) || !await _cola.WaitAsync(0)) return;
        _decisionVisible = true;
        try
        {
            // El diálogo guarda su propio envío: otro archivo no puede cambiar el destinatario de una acción.
            var tipo = envio.Resultado is { } r ? PoliticaDecisionAnalisis.Clasificar(r) : TipoResultadoDecision.NoFinalizado;
            using var dialogo = new DecisionArchivoForm(envio.Solicitud.AnalisisLocal.Archivo.NombreOriginal,
                tipo, () => MostrarInforme(envio), envio.Resultado?.Estado == EstadoAnalisis.Completado);
            dialogo.ShowDialog(); // Ventana independiente y central, incluso con el cliente oculto.
            if (dialogo.Accion == AccionDecisionArchivo.Reintentar)
            {
                // Reanudar después de liberar el turno; conserva SolicitudId/AnalisisId.
                BeginInvoke(new Action(async () => await ReanudarEnvioAsync(envio)));
                return;
            }
            await AplicarDecisionConTurnoAsync(envio, dialogo.Accion);
        }
        catch (OperationCanceledException) when (_salida.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (!_cerrado) MessageBox.Show(error.Message + "\nConsulte el estado del archivo en SecureGate.",
                "No se completó la decisión", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _decisionVisible = false;
            _cola.Release();
            if (!_cerrado) { ActualizarFila(envio); MostrarRegistro(envio); ProcesarDecisiones(); }
        }
    }

    private async Task AplicarDecisionAsync(EnvioCliente envio, AccionDecisionArchivo accion)
    {
        if (_cerrado || !await _cola.WaitAsync(0)) return;
        try { await AplicarDecisionConTurnoAsync(envio, accion); }
        catch (OperationCanceledException) when (_salida.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (!_cerrado) MessageBox.Show(error.Message, "No se completó la decisión", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _cola.Release();
            if (!_cerrado) { ActualizarFila(envio); MostrarRegistro(envio); ProcesarDecisiones(); }
        }
    }

    private async Task AplicarDecisionConTurnoAsync(EnvioCliente envio, AccionDecisionArchivo accion)
    {
        if (accion == AccionDecisionArchivo.Mantener)
        {
            await GuardarDecisionAsync(envio, "MantenerRetenido");
            return;
        }
        if (!EsMuestraRetenida(envio)) throw new InvalidOperationException("La muestra ya no está retenida.");
        var archivo = envio.Solicitud.AnalisisLocal.Archivo;
        var gestor = new GestorCuarentena(EntornoCliente.CarpetaCuarentena);
        if (accion == AccionDecisionArchivo.Eliminar)
        {
            if (MessageBox.Show("¿Desea eliminar definitivamente " + archivo.NombreOriginal + "? El informe se conservará.",
                "Eliminar archivo", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            var operacion = await gestor.EliminarAsync(archivo.ArchivoId, _salida.Token);
            if (!operacion.Realizada) throw new InvalidOperationException(operacion.Mensaje);
            envio.EstadoLocal = "Archivo eliminado; informe conservado";
            await GuardarDecisionAsync(envio, "Eliminar");
            MessageBox.Show("Archivo eliminado. El informe se conserva.", "SecureGate");
            return;
        }
        if (envio.AnalisisId is not { } id) throw new InvalidOperationException("Todavía no hay un informe final.");
        string direccion = NormalizarServidor(envio.Servidor);
        if (!_clientes.TryGetValue(direccion, out var cliente))
        {
            cliente = new ClienteAnalisisHttp(new Uri(direccion));
            _clientes.Add(direccion, cliente);
        }
        bool excepcion = accion == AccionDecisionArchivo.LiberarConRiesgo;
        var permiso = await AutorizacionLiberacion.SolicitarConfirmacionAsync(cliente, archivo.ArchivoId,
            id, archivo.Sha256, excepcion, resultado =>
            {
                if (!excepcion) return true; // El botón Devolver ya es una decisión explícita.
                using var confirmacion = new ConfirmarRiesgoForm(archivo.NombreOriginal,
                    PoliticaDecisionAnalisis.Clasificar(resultado));
                return confirmacion.ShowDialog() == DialogResult.OK;
            }, _salida.Token);
        if (permiso is null) return; // Cancelar nunca devuelve ni consume una autorización.
        var registro = await gestor.ConsultarRegistroAsync(archivo.ArchivoId, _salida.Token);
        _monitor.RegistrarRestauracion(registro.RutaOriginal, registro.Sha256);
        var liberacion = await gestor.LiberarAutorizadoAsync(permiso, _salida.Token);
        if (!liberacion.Realizada) throw new InvalidOperationException(liberacion.Mensaje);
        envio.EstadoLocal = "Archivo liberado bajo responsabilidad; informe conservado";
        await GuardarDecisionAsync(envio, accion.ToString());
        if (accion == AccionDecisionArchivo.DevolverYAbrir)
        {
            try
            {
                var restaurado = await gestor.ConsultarRegistroAsync(archivo.ArchivoId, _salida.Token);
                AbridorArchivoLiberado.Abrir(restaurado);
            }
            catch (Exception error)
            {
                MessageBox.Show("El archivo fue devuelto, pero no se pudo abrir: " + error.Message +
                    "\nUbicación: " + registro.RutaOriginal, "SecureGate", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        else MessageBox.Show("Archivo movido a su carpeta de descarga.\nUbicación: " + registro.RutaOriginal + "\nNo se ha ejecutado.", "SecureGate");
    }

    private async Task GuardarDecisionAsync(EnvioCliente envio, string decision)
    {
        envio.DecisionUsuarioPendiente = false;
        envio.DecisionUsuario = decision;
        envio.FechaDecisionUsuarioUtc = DateTimeOffset.UtcNow;
        envio.UltimoError = null;
        await _envios.GuardarAsync(envio, _salida.Token);
        await _historialPersistencia.ActualizarRegistroAsync(envio);
    }
}
