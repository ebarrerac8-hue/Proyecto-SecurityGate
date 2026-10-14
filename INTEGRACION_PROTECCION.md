# Integración de protección — 14 de octubre de 2026

La rama `integracion/ever-20261012_234403` incorpora cuarentena persistente, permisos NTFS, Authenticode de Windows, Microsoft Defender, cliente automático, liberación autorizada, eliminación, presentación de informes, filtros de historial y reutilización Sandbox. Sustituye la documentación inicial que todavía indicaba Defender y liberación como pendientes.

## Evidencia comprobada en Windows

- Cuarentena: metadatos, bytes, SHA-256, recuperación de retenciones e integridad antes de operar.
- Authenticode: firma válida y ausente; Defender real sobre la copia retenida de una muestra benigna.
- Detección positiva del componente real de Defender con EICAR: `Completada`, `AmenazaDetectada`, `Virus:DOS/EICAR_Test_File`. La muestra de prueba no se ejecutó ni se envió al servidor y se retiró al finalizar. No equivale a una prueba positiva completa del recorrido interfaz/servidor.
- Liberación: cancelar conserva la muestra; autenticar restaura sin ejecutar; autorización de un solo uso; recuperación de interrupciones; destino ocupado no se sobrescribe; bytes y evaluación conservados. También pasó con Sandbox reutilizado.
- Eliminación: confirmación/cancelación, informe conservado y acciones deshabilitadas después de terminar.
- Historial y reportes: filtros, búsqueda, persistencia y exportaciones TXT/JSON contrastadas con el resultado almacenado.
- Reutilización real: origen `343e73ff-3b64-4ba0-a88e-c26702f7ec89`; nueva solicitud `f9e4e8df-207b-45f6-909a-81c0e3999b77`, con las fechas/eventos originales y nuevas comprobaciones antivirus/VirusTotal.
- Descargas habituales: análisis `af19317e-5762-46a2-8b27-8a39bf240cf7`; copia retenida, hash validado e informe conservado después de reiniciar.
- Entrega publicada `SecureGate_Entrega_20261014_023926`: HTTPS, consulta por ngrok de trabajos anteriores y análisis nuevo `2ad7f56d-d3bb-49dd-9a9c-f537f3fc7161`, desde un archivo temporal renombrado a `.exe`; retención, firma, Defender e IA completados con Sandbox histórico.
- Los accesos de Cliente, Servidor y Túnel público de esa entrega se probaron con inicio manual.

## Cierre del alcance y redacción

El monitor, selector manual y API comparten `AlcanceAnalisis`: únicamente `.exe`. Se rechazan formatos fuera del alcance antes de una nueva retención manual. El monitor ignora scripts y documentos, pero conserva el historial y las muestras retenidas anteriormente.

Las instrucciones y evidencias para Gemini distinguen los límites de `Incompleto` de la liberación excepcional con comprobaciones y autenticación. La IA sigue sin conceder autorización. Los informes anteriores no se reescriben; el cambio de redacción se comprueba en análisis nuevos.

El registro `SANDBOX RESULTADO` incluye si la observación fue reutilizada, para no confundir el cierre de esa etapa con una ejecución nueva.

Cierre comprobado en Windows: compilación y pruebas aprobadas; cliente y servidor republicados en la entrega existente, conservando accesos e iconos. El análisis nuevo a2fb16a7-1b38-4de9-a59a-8d4607f8dbb1 terminó con retención, firma válida, Defender sin detecciones, Sandbox reutilizado y Gemini completado. E0001 y la explicación real distinguen los límites de Incompleto de la liberación excepcional.

## Pruebas y límites

`SecureGate.Alcance.Pruebas` comprueba el monitor real con archivos inertes: creación `.EXE`, renombrado `.exe.crdownload` a `.exe`, exclusión de `.ps1`/PDF y exclusión de archivos preexistentes. Las pruebas de IA inspeccionan las reglas enviadas y siguen rechazando referencias inventadas, cambios de evaluación y aprobaciones de ejecución.

No hay bloqueo global del doble clic: existe un intervalo entre descarga y retención. El cliente debe permanecer abierto con monitoreo y retención activos. Sandbox observa parcialmente y sin red. La ausencia de detecciones no demuestra seguridad. No se probó una amenaza real ejecutándose en el recorrido completo y no se desactivó Defender para forzar esa prueba.

No fusionar ciegamente el PR antiguo de `trabajo/proteccion`: la rama de integración conserva correcciones posteriores y el arranque WinForms. La incorporación a `main` debe usar esta rama probada.
