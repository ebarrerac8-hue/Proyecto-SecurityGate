namespace Seguregate;

public enum EstadoSandbox
{
    Cancelado,
    WindowsRequerido,
    LanzadorNoEncontrado,
    ConfiguracionInvalida,
    AccesoDenegado,
    InicioSolicitado,
    Error
}

public class ResultadoSandbox
{
    public EstadoSandbox Estado { get; init; }

    public string Mensaje { get; init; } = "";

   
    public int? IdProceso { get; init; }
}
