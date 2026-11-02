# Detección de cambios y guardado de habilidades — 0.6.1

En 0.6, escribir puntos en un campo de Habilidades no actualizaba el modelo hasta perder el foco. Además, Guardar permanecía desactivado hasta usar Revisar por separado, aunque el borrador ya tuviera cambios. La regresión se reprodujo en el árbol WPF real, sobre una copia privada, cambiando el texto sin abandonar el campo.

La versión 0.6.1 actualiza el borrador y el contador de puntos con cada valor entero introducido. Los campos respetan el bloqueo durante operaciones. Un campo vacío o no numérico muestra un error, invalida la revisión anterior y bloquea guardado, exportación y captura de configuraciones con valores anteriores.

La acción **Revisar y guardar…**, también en Ctrl+S, está disponible cuando hay cambios. El primer uso prepara la revisión y abre Cambios sin escribir. **Guardar** confirma el plan revisado. Las comprobaciones de puntos, requisitos, juego cerrado, origen sin cambios, respaldo y escritura atómica siguen en Core. El tooltip del botón explica por qué no está disponible.

La prueba nativa utiliza los controles de producción y comprueba detección sin cambio de foco, contador actualizado, primera acción sin escritura, errores de conversión y campo vacío, corrección que exige nueva revisión, guardado del valor exacto y restauración byte a byte de la copia aislada. Las capturas de edición detectada y valor inválido quedan en la carpeta temporal de evidencia privada, fuera del repositorio. La suite completa conserva las verificaciones de equipo, biblioteca, habilidades, conflictos y copias.
