using System.Threading.Channels;

namespace SecureGate.Servidor;

public sealed class ColaAnalisis
{
    private readonly Channel<Guid> _canal =
        Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

    private readonly object _control = new();
    private readonly HashSet<Guid> _registrados = new();

    public bool Encolar(Guid analisisId)
    {
        if (analisisId == Guid.Empty)
        {
            throw new ArgumentException(
                "El identificador del análisis no puede estar vacío.",
                nameof(analisisId));
        }

        lock (_control)
        {
            if (!_registrados.Add(analisisId))
            {
                return false;
            }

            if (_canal.Writer.TryWrite(analisisId))
            {
                return true;
            }

            _registrados.Remove(analisisId);

            throw new InvalidOperationException(
                "No se pudo agregar el análisis a la cola.");
        }
    }

    public IAsyncEnumerable<Guid> LeerAsync(
        CancellationToken cancellationToken = default)
    {
        return _canal.Reader.ReadAllAsync(cancellationToken);
    }

    public void Liberar(Guid analisisId)
    {
        lock (_control)
        {
            _registrados.Remove(analisisId);
        }
    }
}