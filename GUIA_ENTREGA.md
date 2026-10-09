# SecureGate: guía del servidor y cliente

## 1. Alcance de esta entrega

El servidor recibe archivos por una API HTTPS autenticada, verifica tamaño y SHA-256, registra solicitudes, coordina una cola de análisis, consulta VirusTotal, solicita la prueba aislada y genera una evaluación por reglas. Gemini explica evidencias y limitaciones; no modifica la decisión ni autoriza ejecutar o liberar archivos.

La credencial de esta entrega es compartida entre los clientes del laboratorio. No identifica personas, no establece roles y permite consultar los informes de esa instancia. La individualización de credenciales y la separación de informes por propietario serían una ampliación del prototipo; no deben afirmarse como implementadas.

La comprobación Authenticode, Microsoft Defender, cuarentena y liberación del cliente pertenecen al bloque de Ever. Su integración real continúa pendiente mientras no entregue esos resultados. La observación de Sandbox corresponde al bloque de Gerson; el servidor coordina y conserva sus informes.

## 2. Requisitos

- Servidor Windows con SDK .NET 10, Windows Sandbox habilitado y sus requisitos de recursos disponibles.
- Cliente Windows para la interfaz WinForms; SDK .NET 10 al ejecutarla desde el código fuente.
- Acceso de Internet del servidor a VirusTotal y Gemini. La red de Sandbox permanece deshabilitada.
- Para dos computadoras: red local de laboratorio, IP privada estable del servidor y confianza explícita en su certificado.

## 3. Código y secretos

Trabajar desde la versión que incluya Gemini, HTTPS, autenticación, retención y transporte de red. No basta clonar un `main` al que todavía no se fusionaron estos cambios.

Variables del usuario Windows del servidor:

| Variable | Uso |
|---|---|
| `SECUREGATE_VIRUSTOTAL_API_KEY` | Consulta de reputación por hash |
| `SECUREGATE_GEMINI_API_KEY` | Explicación de evidencias |
| `SECUREGATE_GEMINI_MODEL` | Modelo probado: `gemini-3.5-flash-lite` |
| `SECUREGATE_CLIENT_TOKEN` | Credencial compartida, 32 bytes aleatorios en Base64 |
| `SECUREGATE_BIND_IP` | IP privada para habilitar la escucha remota; vacía mantiene localhost |
| `SECUREGATE_CERT_THUMBPRINT` | Huella del certificado con clave privada en CurrentUser/My |

El cliente necesita únicamente `SECUREGATE_CLIENT_TOKEN`, confianza en el certificado y la URL del servidor. No necesita claves de VirusTotal o Gemini. Las variables de usuario no son un almacén cifrado de secretos. No copiar claves a código, Git, informes ni capturas.

## 4. Inicio local

Preparar confianza en el certificado local con `dotnet dev-certs https --trust`. Los instaladores de HTTPS realizan este paso y generan la credencial.

Desde la raíz del repositorio:

```powershell
& ".\herramientas\SecureGate.Red\IniciarServidor.ps1"
```

Sin variables de red, el servidor escucha en `https://localhost:5443`. Las rutas `/`, `/api/estado` y `/api/analisis` requieren autorización Bearer. El puerto HTTP 5080 ya no se utiliza.

En otra ventana cargar la credencial antes de ejecutar la interfaz:

```powershell
$env:SECUREGATE_CLIENT_TOKEN = [Environment]::GetEnvironmentVariable("SECUREGATE_CLIENT_TOKEN", "User")
dotnet run --project ".\SecureGateIntegrado_NET10 (1)\SecureGateIntegrado\SecureGateIntegrado.csproj"
```

Cerrar SecureGate mediante Salir en su icono de bandeja; la X puede ocultar la ventana. Detener el servidor mediante Ctrl+C.

## 5. Configuración entre dos computadoras

1. Completar las pruebas locales de retención y HTTPS antes de habilitar la red.
2. En el servidor identificar la IPv4 de la conexión de laboratorio. No usar la IP de un adaptador virtual por error. Mantener esa dirección estable durante la demostración.
3. Detener el servidor. Abrir PowerShell como administrador **con el mismo usuario** que ejecutará SecureGate. Ejecutar:

```powershell
& ".\herramientas\SecureGate.Red\PrepararServidor.ps1" -IPServidor "IP_REAL_DEL_SERVIDOR"
```

El script comprueba que la IP pertenece al equipo y que la conexión está marcada como privada. Crea un certificado de servidor de 60 días con SAN para localhost y la IP. Guarda la clave privada no exportable en el usuario Windows, exporta únicamente un `.cer` público y crea una regla TCP 5443 limitada a esa dirección, perfil privado y subred local.

4. Reiniciar el servidor desde PowerShell normal con `herramientas/SecureGate.Red/IniciarServidor.ps1`; comprobar la escucha local y la IP de red. Entregar al cliente el `.cer` público y verificar su SHA-256 contra la huella mostrada en el servidor. Entregar también la credencial compartida mediante un canal privado. El script del cliente la solicita sin mostrarla. Nunca entregar la clave privada del certificado, ni las claves de proveedores.
5. Cerrar la interfaz del cliente y ejecutar desde su cuenta Windows normal:

```powershell
& ".\herramientas\SecureGate.Red\PrepararCliente.ps1" `
    -IPServidor "IP_REAL_DEL_SERVIDOR" `
    -Certificado "RUTA_AL_CERTIFICADO_PUBLICO.cer" `
    -Sha256Certificado "HUELLA_SHA256_MOSTRADA_EN_EL_SERVIDOR"
```

El script importa el certificado público después de comparar la huella y prueba la API antes de guardar la credencial y dirección del cliente. Reiniciar el servidor con el script de esta entrega para cargar las variables de red; debe estar iniciado al ejecutar la prueba de conexión del cliente.

6. En el segundo equipo ejecutar:

```powershell
& ".\herramientas\SecureGate.Red\ProbarRemoto.ps1" -IPServidor "IP_REAL_DEL_SERVIDOR"
```

Usa el informe existente `e4ead7cb-21c5-4e34-967b-199658834363`; puede indicarse otro con `-AnalisisId`. Verifica 401 sin credencial, 401 incorrecta, HTTPS 200 con credencial válida, informe con IA y 404. No analiza otra muestra. Ejecutarla desde el servidor con su propia IP no cuenta como prueba entre dos equipos.

7. Abrir la interfaz del cliente, enviar una muestra de demostración conocida y confirmar recepción, procesamiento, resultado e historial. Esta última prueba valida también la subida real desde otro equipo y sí puede iniciar Sandbox en el servidor. No ejecutarla en el anfitrión.

Si la IP cambia, el certificado anterior deja de corresponder a la nueva URL: volver a preparar servidor y cliente. No deshabilitar la validación TLS para resolverlo. Este certificado y esta regla son de laboratorio; no se configura publicación en Internet.

## 6. Persistencia, reintentos y retención

Los informes se guardan en `%LOCALAPPDATA%\SecureGate\Servidor\Trabajos`; las muestras en `Muestras`; las copias aisladas en `SesionesSandbox`. La configuración del cliente y su historial se guardan en `%LOCALAPPDATA%\SecureGate\Cliente`.

Con la retención instalada, copias de trabajos terminados vencen a los 7 días; archivos abandonados sin registro, a las 24 horas. La limpieza corre al iniciar y cada 60 minutos. Conserva los informes y solicitudes sin vencimiento en esta versión, y protege trabajos activos. Suspende la pasada si no puede leer todos los registros. No limpia sesiones mientras haya Sandbox abierto.

`FechaEliminacionMuestraServidorUtc` indica que la copia del servidor ya no está disponible. Reenviar la misma SolicitudId y datos recupera el mismo AnalisisId sin repetir análisis. Una solicitud nueva necesita otra SolicitudId. Los metadatos diferentes con un identificador reutilizado producen HTTP 409.

Configurar los plazos en la sección `Servidor` de appsettings.json, preservando sus demás secciones. Si se cambia `CarpetaDatos`, ajustar también el gestor de Sandbox, que actualmente escribe sesiones en la ruta predeterminada.

## 7. Pruebas antes de entregar

```powershell
dotnet build .\SecureGate.Equipo.slnx
dotnet run --project .\pruebas\SecureGate.Retencion.Pruebas\SecureGate.Retencion.Pruebas.csproj
dotnet run --project .\pruebas\SecureGate.Transporte.Pruebas\SecureGate.Transporte.Pruebas.csproj
dotnet run --project .\pruebas\SecureGate.Seguridad.Pruebas\SecureGate.Seguridad.Pruebas.csproj
dotnet run --project .\pruebas\SecureGate.Ia.Pruebas\SecureGate.Ia.Pruebas.csproj
dotnet run --project .\pruebas\SecureGate.Cliente.Pruebas\SecureGate.Cliente.Pruebas.csproj -- --autopruebas
dotnet run --project .\pruebas\SecureGate.Cliente.Pruebas\SecureGate.Cliente.Pruebas.csproj -- --persistencia
```

Las pruebas simuladas no reemplazan la demostración real. Conservar salidas de compilación, retención, HTTPS, reenvío, reinicio y conexión entre dos equipos. Probar además una interrupción con trabajo en proceso y dos solicitudes concurrentes antes de declarar cerrada la operación del servidor.

## 8. Interpretación de resultados

`Completado` significa que terminó el procesamiento, no que el archivo sea seguro. `Incompleto` sigue siendo correcto si faltan firma, antivirus u observación suficiente. Cero detecciones de VirusTotal no demuestra seguridad. Gemini ofrece apoyo explicativo y sus referencias se verifican; esto no garantiza la exactitud de sus interpretaciones.

El observador actual cubre parcialmente procesos y archivos del perfil del invitado, no todo el registro ni conexiones de red. Puede perder procesos breves y no identifica el autor de todos los eventos. Las evidencias del invitado no resisten necesariamente manipulación por software hostil. La demostración con cmd.exe y acciones prefijadas no equivale a analizar un instalador arbitrario.

## 9. Diagnóstico y recuperación

- HTTP 401: cargar o corregir la credencial del cliente; no repetir automáticamente.
- Error TLS: verificar IP, certificado, huella, confianza y fecha de expiración.
- Conexión rechazada: revisar servidor iniciado, IP asignada, perfil privado y regla TCP 5443.
- IA 503 o tiempo agotado: conservar el informe por reglas; no repetir Sandbox para diagnosticar IA. El modelo probado es Flash-Lite con límite de 20 segundos.
- LIMPIEZA suspendida: revisar registros dañados, permisos y enlaces. No borrar informes para ocultar el problema.
- Después de cambiar código: volver a compilar y ejecutar las pruebas del bloque afectado.

Para volver al modo local, ejecutar `DesactivarRemoto.ps1` como administrador con el mismo usuario y reiniciar mediante el script de servidor. Se retira la regla del laboratorio y las variables de escucha remota. El certificado permanece en Windows; no se eliminan otros certificados.

## 10. Referencias técnicas

- [Certificados de servidor con SAN](https://learn.microsoft.com/en-us/powershell/module/pki/new-selfsignedcertificate)
- [Reglas de Windows Firewall](https://learn.microsoft.com/en-us/powershell/module/netsecurity/new-netfirewallrule)
- [HTTPS con ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl)

Esta guía describe el código preparado. La configuración remota y sus resultados solo deben marcarse como verificados después de ejecutarlos en los equipos Windows correspondientes.

## Demostracion publica entre dos computadoras

Prueba realizada el 8 de octubre de 2026: el cliente de Gerson envio
PruebaSecureGate_CMD.exe desde otra computadora a la API publica.
El servidor proceso la muestra y el cliente recibio el informe.

AnalisisId: bfafb3f9-12ed-4944-8a66-f3071b76575c.
Sandbox: completada, muestra ejecutada, observador iniciado,
red deshabilitada y 49 eventos registrados.
VirusTotal: 0 motores maliciosos, 0 sospechosos y 71 sin deteccion.
Gemini: explicacion completada con referencias, aproximadamente 5,5 segundos.
Evaluacion general: Incompleto; firma y antivirus local no realizados.

Scripts: herramientas/SecureGate.Publico.
IniciarServidorPublico.ps1 inicia la API local HTTPS.
IniciarTunel.ps1 obtiene una URL publica temporal.
ConfigurarClientePublico.ps1 configura y comprueba el cliente remoto.
ProbarAPI.ps1 verifica autenticacion y consulta de informes.

Mantener servidor y tunel abiertos durante la prueba.
La URL cambia al reiniciar el tunel.
Compartir solamente la credencial del cliente por un medio privado.
Las claves de Gemini y VirusTotal permanecen en el servidor.
La muestra de demostracion debe proceder del cmd.exe del servidor.
No ejecutar la muestra en el anfitrion.

Esta prueba demuestra conectividad y procesamiento remoto reales.
No constituye un despliegue permanente ni completa los analisis pendientes.
