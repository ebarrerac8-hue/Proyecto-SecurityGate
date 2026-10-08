using System.Net;

namespace SecureGate.Cliente;

public sealed class ErrorClienteAnalisis : Exception
{
    public string Codigo { get; }
    public HttpStatusCode? EstadoHttp { get; }
    public bool Reintentable { get; }

    public ErrorClienteAnalisis(string codigo, string mensaje,
        HttpStatusCode? estadoHttp = null, bool reintentable = false,
        Exception? innerException = null) : base(mensaje, innerException)
    {
        Codigo = codigo;
        EstadoHttp = estadoHttp;
        Reintentable = reintentable;
    }
}
