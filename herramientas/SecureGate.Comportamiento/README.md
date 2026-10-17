# Prueba controlada del comportamiento

La evaluación del servidor aplica reglas a la observación de Sandbox. No requiere que Defender ni VirusTotal detecten la muestra para elevar la evaluación a `Sospechoso`.

| Código | Evidencia necesaria | Interpretación |
|---|---|---|
| SG-B001 | Descendiente atribuido a la muestra que inicia PowerShell con `EncodedCommand` o abreviación admitida | Orden codificada; requiere revisión, no confirma malware |
| SG-B002 | Cambio observado en HKCU Run/RunOnce cuyo comando apunta exactamente a `C:\Prueba\muestra.exe` | Inicio automático relacionado con la muestra; autor de la escritura no confirmado |
| SG-B003 | Descendiente atribuido que solicita borrado de copias o registros mediante vssadmin, wbadmin o wevtutil | Orden observada; no confirma éxito |

Las reglas verifican identidad, hash, ventana temporal y raíz de la muestra. Los procesos requieren cadena de padres documentada. Archivos temporales y procesos ordinarios no bastan para activar estas reglas. Una detección de amenaza por antivirus o reputación conserva su prioridad. Los informes históricos anteriores a esta versión no se reutilizan como observaciones actuales.

Gemini recibe los hallazgos y sus identificadores de eventos, evalúa su relevancia y devuelve observaciones estructuradas con referencias. El servidor valida que las referencias de comportamiento correspondan a hallazgos reales. La IA no elimina señales de reglas ni concede permisos de ejecución. Sus interpretaciones pueden ser incorrectas.

## Preparación en Windows

Actualizar **servidor y cliente**, con ambos cerrados antes de publicar. El servidor debe recibir el nuevo Observador.ps1. Generar después el paquete privado del cliente con la conexión configurada.

Desde la raíz del repositorio, generar una muestra benigna fuera de Descargas:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\herramientas\SecureGate.Comportamiento\PrepararMuestra.ps1 -Destino "$env:TEMP\SecureGate_Laboratorio\PruebaComportamiento.exe"
```

Con servidor y cliente actualizados y ejecutándose, copiar ese EXE a Descargas. No ejecutarlo manualmente. SecureGate debe retenerlo y enviarlo a análisis. Si antivirus o reputación interceptan el archivo, registrar esa intervención; no desactivar protecciones ni presentar ese caso como prueba de Sandbox.

La muestra distribuida solo realiza acciones al ejecutarse como WDAGUtilityAccount en `C:\Prueba\muestra.exe`, con solicitud presente. En el invitado crea temporalmente una entrada Run que apunta a sí misma y un proceso PowerShell con una orden codificada que escribe un marcador de texto. Finalmente retira su propia entrada y marcador. No ejecuta órdenes destructivas. Al abrirla accidentalmente fuera del invitado termina sin realizar estas acciones.

Resultado esperado: SG-B001 y SG-B002 con eventos identificados en Motivos, sección de comportamiento e informe conservado. La firma ausente puede producir otra señal independiente: **esa señal no prueba el análisis de comportamiento**. Para la demostración mostrar los códigos SG-B y sus eventos, no solo el color o el título de riesgo. SG-B003 se verifica con evidencia simulada; nunca se ejecutan órdenes de borrado en las pruebas.

## Verificación automatizada y alcance

Las pruebas cubren señales positivas, casos ordinarios, atribución inválida, identidad ajena, eventos fuera de plazo, serialización y referencias de IA válidas e inválidas. CI también ejecuta el colector real sobre una muestra benigna en un Windows efímero y exige ambas señales; usa un binario QA distinto y no distribuible. **Esta prueba no inicia Windows Sandbox ni sustituye la comprobación final en el equipo de entrega.**

La observación usa muestreo: puede perder procesos breves y cambios que ocurran entre muestras. Solo compara HKCU Run/RunOnce y observa archivos del perfil; no cubre todo el registro, la red ni el sistema. No atribuye el autor de eventos de archivo o de registro. Los datos del invitado no son resistentes a manipulación. La ausencia de estas señales no garantiza seguridad ni constituye detección universal de malware.
