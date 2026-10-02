
namespace Seguregate
{
    public enum EstadoConsulta
    {
        NoDisponible,
        InformeDisponible,
        Desconocido
    }

    public class ResultadoReputacion
    {
        public string Sha256 { get; set; } = "";
        public EstadoConsulta Estado { get; set; }
        public string Mensaje { get; set; } = "";

        public int? Maliciosos { get; set; }
        public int? Sospechosos { get; set; }
        public int? SinDeteccion { get; set; }
    }
}