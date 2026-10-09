# SecureGate: cierre de entrega

## Estado confirmado antes de este paquete

La API HTTPS autentica con clave compartida. VirusTotal, Sandbox y Gemini estan integrados. Los informes persisten tras reiniciar y los reintentos recuperan el mismo trabajo. Gerson envio una muestra desde otra computadora mediante Cloudflare y recibio el informe bfafb3f9-12ed-4944-8a66-f3071b76575c. Ngrok ya permite consultar ese informe con la direccion fija https://entrust-factual-cozy.ngrok-free.dev. Cliente y servidor se publicaron para Windows x64 con .NET incluido.

Los scripts de este paquete se revisan por sintaxis. El paquete ejecutable, concurrencia y recuperacion necesitan las pruebas reales indicadas abajo; no se declaran aprobadas anticipadamente.

## Preparacion del responsable del servidor

Ejecutar PrepararEntrega.ps1 en la computadora que ya tiene los programas publicados en entrega/Cliente y entrega/Servidor del repositorio. Crea una carpeta nueva en Documentos, dos ZIP y accesos directos. No cambia el codigo de los proyectos ni borra informes. Conserva las claves en las variables del usuario Windows; no las copia al ZIP. Detecta certificados privados, configuraciones ngrok y valores conocidos de credenciales en archivos de texto publicados y aborta el ZIP si los encuentra. Eso no reemplaza la revision del contenido antes de distribuir.

Detener el servidor de desarrollo y ngrok con Ctrl+C. Abrir el acceso SecureGate - servidor publicado. El servidor publicado y ngrok permanecen visibles. No se registran servicios ni tareas de inicio automatico. La conexion al certificado local se valida, luego se exporta solo su parte publica para ngrok.

Esta entrega del servidor usa el certificado HTTPS ya configurado en Windows. Moverla a otro servidor requiere configurar certificado, claves y ngrok en ese equipo. Sandbox exige Windows compatible, virtualizacion habilitada, espacio y memoria disponibles. No es una entrega para alojar sin cambios en Linux o como servicio sin escritorio interactivo.

## Cliente del companero

Extraer TODO SecureGate_Cliente.zip en una carpeta propia, fuera de Descargas, por ejemplo Documentos/SecureGate_Cliente. No separar el EXE de sus DLL. No necesita SDK, Visual Studio, Git, ngrok ni Sandbox.

Cerrar cualquier SecureGate anterior mediante Salir en la bandeja. Ejecutar ConfigurarCliente.cmd UNA VEZ: guarda direccion fija y clave, y verifica la API antes de guardar. Si ya existe una clave, la reutiliza. Si es incorrecta, revisar la variable del usuario en Windows antes de repetir. Abrir AbrirSecureGate.vbs: carga la clave actual y abre solo la interfaz, sin consola. Si Windows bloquea VBScript, no debilitar las politicas; informar el mensaje para preparar otro lanzador.

La configuracion vive en LocalAppData/SecureGate/Cliente/configuracion.json. La clave vive en SECUREGATE_CLIENT_TOKEN del usuario. La X oculta la interfaz; Salir la cierra. Firma y antivirus local siguen pendientes de integrar desde el bloque de Ever. La interfaz de Osbin debe revisarse e incorporarse antes de generar el paquete definitivo.

Prueba remota: el servidor comparte la misma PruebaSecureGate_CMD.exe usada en la demostracion. Prepararla fuera de la carpeta monitoreada; no ejecutar en el anfitrion. El cliente la selecciona en SecureGate, recibe un identificador NUEVO y luego el informe. Sandbox se abre solo en el servidor. Conservar informe nuevo con estado, eventos e IA. No dar por aprobada esta prueba solamente por recuperar un informe anterior.

## Validacion del responsable

VerificarPreparacion.ps1 confirma que el proceso activo procede de la carpeta publicada en Documentos y ejecuta las pruebas aisladas de retencion, transporte, IA y autenticacion. Estas pruebas de desarrollador si requieren el SDK; no son necesarias para usar el cliente ni para iniciar el servidor publicado. La salida se guarda en EvidenciaEntrega.

PruebasFinales.ps1 acepta Modo ConsultaPublica, Concurrencia, PrepararRecuperacion o VerificarRecuperacion. ConsultaPublica comprueba la direccion ngrok sin iniciar analisis. Los otros modos usan HTTPS local por defecto y la muestra en Documentos/SecureGate_Prueba_Remota/PruebaSecureGate_CMD.exe; RutaMuestra permite indicar otra ubicacion. Comprueban hash y tamano contra el informe real anterior antes del envio. Nunca ejecutan la muestra en el anfitrion.

Concurrencia envia simultaneamente tres copias de una solicitud y otra solicitud distinta. Comprueba mismos ID/fecha para duplicados, ID distinto para la otra solicitud, finalizacion de los dos trabajos y ausencia de superposicion de sus intervalos Sandbox. Duracion normal: unos 4-6 minutos con la observacion actual de 120 segundos. No es una prueba de carga masiva.

PrepararRecuperacion crea una solicitud nueva y se detiene cuando el servidor entra a Sandbox. Inmediatamente detener SOLO el servidor con Ctrl+C, esperar su cierre y el de Sandbox. Mantener ngrok. Reiniciar SOLO el servidor con IniciarServidorPublicado.cmd dentro de la carpeta Servidor publicada. Debe aparecer RECUPERACION en su registro. Luego ejecutar VerificarRecuperacion. Comprueba nuevo inicio del proceso, mismo ID y fecha, nueva prueba Sandbox posterior a la interrupcion y reintento que conserva evidencias/IA. Si se interrumpe durante Sandbox, la prueba incompleta se repite; si el punto de evidencias ya se guardo, el procesador retoma IA. Esta prueba controla el cierre normal durante el trabajo, no simula todas las fallas de hardware ni garantiza una unica llamada externa ante cualquier caida.

Las evidencias quedan en LocalAppData/SecureGate/EvidenciaEntrega; no contienen las claves. Los scripts conservan trabajos aunque fallen; no editar JSON de produccion para forzar resultados. Guardar tambien la salida de las 14 pruebas de retencion y las pruebas de transporte del repositorio. Estas pruebas aisladas usan datos temporales; no borran tus informes reales.

## Respaldo y limitaciones

Versionar herramientas/SecureGate.Entrega, los ultimos cambios de herramientas/SecureGate.Publico y .gitignore. NO versionar entrega, ZIP de binarios, claves, certificados privados ni ultima-entrega-local.json. .gitignore excluye las salidas locales. Las claves de Gemini y VirusTotal permanecen solo en el servidor. El prototipo usa una credencial compartida, sin cuentas individuales ni separacion de informes por propietario.

Ngrok permanece como conexion principal; el tunel Cloudflare temporal puede usarse como respaldo cambiando direccion del cliente. Los proveedores terminan TLS y procesan el trafico: usar la muestra conocida para esta demostracion. El plan gratuito ngrok tiene cuotas; direccion fija no implica disponibilidad si la computadora se apaga, suspende o pierde Internet.

Cerrar un bloque al 100% requiere resultados reales de las pruebas, entrega reproducible y documentacion. El proyecto completo sigue dependiendo del analizador pendiente de Ever y de comprobar la interfaz final de Osbin. Ausencia de detecciones y una observacion limitada no autorizan ejecutar un archivo.
