# SecureGate

Prototipo universitario para Windows, desarrollado en C# .NET 10 con Windows Forms y ASP.NET Core. Detecta ejecutables `.exe` nuevos en una carpeta monitoreada, los retiene y combina Authenticode, Microsoft Defender, VirusTotal, observación en Windows Sandbox y una explicación de Gemini basada en evidencias.

## Alcance y comportamiento

- El monitor, la selección manual y la recepción de archivos de la API admiten exclusivamente `.exe`, sin distinguir mayúsculas. PDF, MSI y scripts no se retienen automáticamente en esta versión.
- Con el cliente abierto, monitoreo activo y retención habilitada, el monitor detecta creaciones y renombrados en la carpeta elegida, sin recorrer subcarpetas ni analizar retroactivamente los archivos existentes.
- Espera que el archivo se estabilice y mueve su contenido a cuarentena antes de esperar su turno de análisis. Registra nombre, extensión, ubicación original, tamaño, SHA-256 y estado. Las muestras retenidas tienen permisos NTFS que restringen la ejecución directa.
- Hay un intervalo entre la aparición del archivo y su retención. No existe un interceptor global del doble clic ni protección cuando el cliente está cerrado. La ausencia de detecciones no garantiza seguridad.
- El cliente consulta estados e informes. Detener consulta detiene la operación del cliente; no cancela un trabajo que ya procesa el servidor.

## Cliente y servidor

El cliente necesita la dirección HTTPS y la credencial de acceso configuradas. No inicia ngrok ni Windows Sandbox. El servidor necesita Windows Sandbox disponible, Defender para el análisis local del cliente, acceso a VirusTotal/Gemini y sus variables de entorno. El túnel público se ejecuta en la computadora del servidor.

Para la demostración se inicia manualmente: **Servidor → Túnel público → Cliente**. Las ventanas del servidor y ngrok permanecen visibles. No se instala inicio automático con Windows.

La entrega Windows x64 se genera con `dotnet publish -c Release -r win-x64 --self-contained true`. Cliente y Servidor tienen carpetas separadas; el servidor debe incluir `Assets/Guest/Observador.ps1`. No distribuir claves, certificados privados ni muestras con la entrega.

## Persistencia y recuperación

Se utilizan archivos JSON y muestras en disco, no una base de datos SQL:

| Datos habituales | Carpeta |
|---|---|
| Configuración, envíos e informes del cliente | `%LOCALAPPDATA%\SecureGate\Cliente` |
| Registros y muestras retenidas | `%LOCALAPPDATA%\SecureGate\Cuarentena` |
| Trabajos y datos del servidor por defecto | `%LOCALAPPDATA%\SecureGate\Servidor` |

Los registros permiten recuperar operaciones después de reiniciar. La misma `SolicitudId` recupera el trabajo existente; reutilizarla con otro archivo se rechaza. Los informes históricos conservan la evaluación y explicación originales. Actualizar el programa no los reescribe.

## Reutilización por hash

Una solicitud nueva puede reutilizar observaciones Sandbox compatibles del mismo SHA-256, tamaño y extensión. Se conservan las fechas y eventos originales y se registra el análisis de origen. VirusTotal y el antivirus local se comprueban nuevamente; Gemini recibe la procedencia histórica.

La política exige identidad, integridad y comprobaciones compatibles; rechaza amenazas, señales sospechosas, observaciones insuficientes y versiones incompatibles. La vigencia predeterminada de Sandbox es 30 días desde el fin original; reutilizarla no renueva ese plazo. Después de 24 horas exige además un informe encontrado de VirusTotal actualizado. Los valores son configurables en `OpcionesServidor`.

## Resultados y acciones

- **Informe completo disponible**: se completaron las comprobaciones previstas. No significa que el archivo sea seguro.
- **Sin amenazas reportadas**: las capas consultadas no reportaron amenazas.
- **Evaluación técnica Incompleto**: las evidencias tienen límites. Sandbox observa durante un tiempo limitado y sin red; no demuestra todos los comportamientos posibles.
- Gemini explica evidencias. No concede permisos ni determina que el usuario pueda ejecutar o liberar el archivo.
- La liberación excepcional es independiente: valida el informe y la identidad/integridad, exige antivirus local completado sin detecciones, rechaza amenazas detectadas, requiere aceptación de riesgo y autenticación de la cuenta Windows. Restaura la muestra sin ejecutarla y no modifica la evaluación técnica. La autorización es temporal y de un solo uso.
- Eliminar una muestra conserva el informe. Cancelar conserva la retención. Después de liberar o eliminar se deshabilitan las acciones que ya no corresponden.

Las retenciones e informes de formatos anteriores permanecen en el historial; el nuevo alcance no los libera ni elimina automáticamente.

## Claves y transporte

Las credenciales se configuran fuera del repositorio: `SECUREGATE_CLIENT_TOKEN`, `SECUREGATE_VIRUSTOTAL_API_KEY`, `SECUREGATE_GEMINI_API_KEY` y `SECUREGATE_GEMINI_MODEL`. El cliente usa solamente la credencial de acceso. El prototipo usa un token compartido, no cuentas independientes ni roles de servidor. La API escucha HTTPS local en el puerto 5443; el túnel puede exponer esa API con la dirección configurada.

## Compilación y pruebas

Desde PowerShell en la raíz del repositorio:

```powershell
dotnet build SecureGate.Equipo.slnx -m:1
```

Los proyectos de `pruebas` son programas de consola: se ejecutan con `dotnet run --project <ruta-del-proyecto>`. Las pruebas automáticas habituales usan datos aislados y simuladores; las pruebas reales de Defender, interfaz y autorización Windows se identifican por separado. Consulte [INTEGRACION_PROTECCION.md](INTEGRACION_PROTECCION.md) para la evidencia y las limitaciones de la integración.

No declarar protección universal ni una garantía de ausencia de malware. La detección positiva de EICAR se validó con el componente real de Defender; el recorrido completo de la entrega se validó con una copia benigna de `cmd.exe`.
