namespace SecureGate.Monitoreo
{
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
        public EstadoSandbox Estado { get; set; }

        public string Mensaje { get; set; }

        public int? IdProceso { get; set; }

        public ResultadoSandbox()
        {
            Mensaje = string.Empty;
        }
    }
}