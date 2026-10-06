namespace SecureGate.Contratos;

public class SolicitudSandbox
{
    public Guid SolicitudId { get; set; }
    public string RutaMuestraServidor { get; set; } = string.Empty;
    public string Sha256Esperado { get; set; } = string.Empty;
    public TimeSpan TiempoMaximoEjecucion { get; set; } = TimeSpan.FromSeconds(45);
    public bool PermitirRed { get; set; } = false;
}
