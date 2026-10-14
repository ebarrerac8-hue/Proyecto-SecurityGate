namespace SecureGate.Cliente;

// Se usa desde el hilo de interfaz. Mantiene identidad incluso mientras un cuadro está abierto.
public sealed class ColaDecisionesCliente
{
    private readonly Queue<EnvioCliente> _pendientes = new();
    private readonly HashSet<Guid> _identidades = new();
    public int Count => _pendientes.Count;
    public bool Encolar(EnvioCliente envio)
    {
        if (!envio.DecisionUsuarioPendiente || (!envio.Terminado && envio.UltimoError is null) ||
            !_identidades.Add(envio.Solicitud.SolicitudId)) return false;
        _pendientes.Enqueue(envio);
        return true;
    }
    public bool TryTomar(out EnvioCliente? envio) => _pendientes.TryDequeue(out envio);
    public void Finalizar(EnvioCliente envio) => _identidades.Remove(envio.Solicitud.SolicitudId);
}
