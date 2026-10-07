namespace SecureGate.Contratos;

public interface IAnalizadorLocal
{
    Task<ResultadoAnalisisLocal> AnalizarAsync(
        Guid archivoId,
        CancellationToken cancellationToken = default);
}