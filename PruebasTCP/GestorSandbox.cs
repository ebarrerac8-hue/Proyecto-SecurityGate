using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Xml;
using System.Xml.Linq;

namespace Seguregate;

public class GestorSandbox
{
    public bool TienePermisosAdministrador()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        using WindowsIdentity identidad = WindowsIdentity.GetCurrent();

        var usuario = new WindowsPrincipal(identidad);

        return usuario.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public string? BuscarLanzador()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        string carpetaWindows =
            Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        /
        string carpetaSistema =
            Environment.Is64BitOperatingSystem &&
            !Environment.Is64BitProcess
                ? "Sysnative"
                : "System32";

        string ruta = Path.Combine(
            carpetaWindows,
            carpetaSistema,
            "WindowsSandbox.exe"
        );

        return File.Exists(ruta) ? ruta : null;
    }

    public ResultadoSandbox Iniciar(
        string rutaConfiguracion,
        bool autorizadoPorUsuario)
    {
       
        if (!autorizadoPorUsuario)
        {
            return CrearResultado(
                EstadoSandbox.Cancelado,
                "El usuario no autorizó la prueba."
            );
        }

        if (!OperatingSystem.IsWindows())
        {
            return CrearResultado(
                EstadoSandbox.WindowsRequerido,
                "Esta función necesita Windows."
            );
        }

        try
        {
            string? lanzador = BuscarLanzador();

            if (lanzador is null)
            {
                return CrearResultado(
                    EstadoSandbox.LanzadorNoEncontrado,
                    "No se encontró Windows Sandbox. Revisá su instalación " +
                    "y activación en Características de Windows."
                );
            }

            string ruta = Path.GetFullPath(
                rutaConfiguracion.Trim().Trim('"')
            );

            if (!string.Equals(
                Path.GetExtension(ruta),
                ".wsb",
                StringComparison.OrdinalIgnoreCase))
            {
                return CrearResultado(
                    EstadoSandbox.ConfiguracionInvalida,
                    "Se necesita una configuración .wsb, no el ejecutable descargado."
                );
            }

            if (!File.Exists(ruta))
            {
                return CrearResultado(
                    EstadoSandbox.ConfiguracionInvalida,
                    "No se encontró el archivo de configuración."
                );
            }

            string? problema = ValidarConfiguracion(ruta);

            if (problema is not null)
            {
                return CrearResultado(
                    EstadoSandbox.ConfiguracionInvalida,
                    problema
                );
            }

            var inicio = new ProcessStartInfo
            {
                FileName = lanzador,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(ruta)!
            };

            
            inicio.ArgumentList.Add(ruta);

            using Process? proceso = Process.Start(inicio);

            return new ResultadoSandbox
            {
                Estado = EstadoSandbox.InicioSolicitado,
                Mensaje =
                    "Se solicitó la apertura de Windows Sandbox. " +
                    "Comprobá que aparezca su ventana; Windows puede " +
                    "mostrar un error durante el arranque.",
                IdProceso = proceso?.Id
            };
        }
        catch (UnauthorizedAccessException)
        {
            return CrearResultado(
                EstadoSandbox.AccesoDenegado,
                "Windows denegó el acceso a la configuración o al lanzador."
            );
        }
        catch (Win32Exception error)
        {
            EstadoSandbox estado = error.NativeErrorCode switch
            {
                5 => EstadoSandbox.AccesoDenegado,
                1223 => EstadoSandbox.Cancelado,
                _ => EstadoSandbox.Error
            };

            return CrearResultado(
                estado,
                $"Windows no pudo iniciar Sandbox: {error.Message}"
            );
        }
        catch (Exception error) when (
            error is IOException ||
            error is XmlException ||
            error is ArgumentException ||
            error is NotSupportedException ||
            error is InvalidOperationException)
        {
            return CrearResultado(
                EstadoSandbox.Error,
                $"No se pudo preparar la prueba: {error.Message}"
            );
        }
    }

    private static string? ValidarConfiguracion(string ruta)
    {
        var ajustes = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 1_000_000
        };

        using XmlReader lector = XmlReader.Create(ruta, ajustes);

        XDocument documento = XDocument.Load(lector);
        XElement? raiz = documento.Root;

        if (raiz is null || raiz.Name != "Configuration")
            return "La configuración debe tener una raíz <Configuration>.";

        
        var opcionesRed = raiz.Elements("Networking").ToList();

        if (opcionesRed.Count != 1 ||
            !string.Equals(
                opcionesRed[0].Value.Trim(),
                "Disable",
                StringComparison.OrdinalIgnoreCase))
        {
            return "La configuración debe incluir " +
                   "<Networking>Disable</Networking>.";
        }

        
        foreach (XElement carpeta in raiz
            .Elements("MappedFolders")
            .Elements("MappedFolder"))
        {
            var permisos = carpeta.Elements("ReadOnly").ToList();

            if (permisos.Count != 1 ||
                !string.Equals(
                    permisos[0].Value.Trim(),
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Las carpetas compartidas deben incluir " +
                       "<ReadOnly>true</ReadOnly>.";
            }
        }

        return null;
    }

    private static ResultadoSandbox CrearResultado(
        EstadoSandbox estado,
        string mensaje)
    {
        return new ResultadoSandbox
        {
            Estado = estado,
            Mensaje = mensaje
        };
    }
}