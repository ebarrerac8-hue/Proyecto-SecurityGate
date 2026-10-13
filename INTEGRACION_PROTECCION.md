SecureGate: retencion y firma conectadas al formulario y monitor.
Requiere las tres fases anteriores aplicadas y probadas: cuarentena,
Authenticode y puente con Cliente. Instalar en integracion/ever-...

Se respalda el codigo y se aplican cambios acotados con comprobacion de hashes.
Compila la solucion y corre pruebas de seguridad y cliente sin red real.
Conserva los arreglos previos de autenticacion estricta, consulta VT solo servidor,
recuperacion de resultados y acciones de liberar/eliminar deshabilitadas.
El cliente funciona sin exigir administrador; no se cambian certificados.

Configuracion adaptable con Guardar/Cancelar y retencion optativa para la
puesta en marcha. En ajustes existentes sin RetencionAutomatica se conserva false.
Con retencion activa, el archivo se retiene ANTES de esperar al servidor;
recuperacion de registros/envios al iniciar y firma local visible en el informe.
No libera automaticamente ningun archivo. No integra Defender todavia.
Retencion no es bloqueo global ni elimina el intervalo del FileSystemWatcher.
Pendientes: permisos/control de ejecucion, autorizacion de acciones, Defender,
acabado visual de la pantalla principal y pruebas finales de todos los casos.

ProbarInterfaz.ps1 -Modo Nueva crea datos aislados, configura un monitor de
prueba, verifica acceso a la API y lanza el EXE compilado sin consola cliente.
Al confirmarse el monitor copia cmd.exe de Windows a la carpeta artificial.
El servidor de la MISMA computadora debe reconocer el hash como demostracion.
No ejecutar esa copia en el anfitrion. Sandbox se ejecuta en el servidor.
El script no inicia ni apaga servidor/tunel y no activa servicios de arranque.
ProbarInterfaz.ps1 -Modo Reabrir usa la misma prueba, sin copiar otra muestra.
VerificarInterfaz.ps1 valida el envio real; -Modo Recuperacion comprueba un
proceso nuevo con iguales identificadores, fecha local e informe.
Datos habituales y pruebas separados por SECUREGATE_CLIENT_DATA_DIR solo
heredado en el proceso de prueba; no se guarda como variable User.

No hace merge/commit/push. Sintaxis revisada en Linux; pruebas Windows al aplicar.
