# Revisión visual de la UI nativa 0.4

Estado: **reviewed**. Esta revisión distingue las capturas de controles WPF del [boceto de preproducción](concept-review.md). Se inspeccionaron imágenes completas, metadatos nativos y el informe del ejecutable distribuido. El agente revisor no utilizó entradas de escritorio ni modificó partidas. La ejecución del paquete y de sus pruebas corresponde a root; sus resultados comprobados se recogen al final.

## Evidencia inspeccionada

| Vista | Evidencia local relativa | Alcance |
|---|---|---|
| Objetos, 1280 × 900 | [equipment1280.png](../../artifacts/native-04-ui-review/captures/equipment1280.png) y [polish-equipment1280.png](../../artifacts/native-04-ui-review/captures/polish-equipment1280.png) | Encaje mínimo, diez ranuras, contenedor, inspector y colores antes/después del ajuste. |
| Objetos, 1600 × 1000 | [equipment1600.png](../../artifacts/native-04-ui-review/captures/equipment1600.png) | Distribución de paneles, selección y propiedades largas. Sus colores son anteriores al shader final. |
| Objetos, 3440 × 1440 | [equipment3440.png](../../artifacts/native-04-ui-review/captures/equipment3440.png) y [polish-sequential-equipment3440.png](../../artifacts/native-04-ui-review/captures/polish-sequential-equipment3440.png) | Espacio ultrawide y primera versión del alijo simultáneo. La alineación del alijo en estas imágenes precede a la corrección posterior de root. |
| Habilidades, 1280 × 900 | [skills1280.png](../../artifacts/native-04-ui-review/captures/skills1280.png) y [polish-skills1280.png](../../artifacts/native-04-ui-review/captures/polish-skills1280.png) | Treinta habilidades completas, iconos originales, nombres de árboles y aclaración de puntos invertidos. |
| Habilidades, 1600 × 1000 | [skills1600.png](../../artifacts/native-04-ui-review/captures/skills1600.png) | Densidad y correspondencia entre iconos, nombres y controles. |

También se inspeccionó la quinta captura del shader, `real-art-fifth-shader.png`, procedente de una fixture privada. Se conserva fuera del repositorio y no se incorpora aquí: sirve para contrastar el color final, no como captura pública de demostración. No contiene evidencia sustituible por el boceto.

Los metadatos revisados identifican `PresentationFramework`, `Akara.Desktop.WorkbenchView`, tamaño real igual al solicitado, `native: true`, `browser: false` y ausencia de ventana visible durante la captura. Esto acredita el origen WPF de esas imágenes; no prueba interacción, persistencia o lectura de todos los recursos.

## Composición y encaje

La distribución real cumple la dirección de herramienta compacta: catálogo a la izquierda, equipo y contenedor en el centro, propiedades a la derecha. Los controles usan superficies planas y las ilustraciones dan la identidad de Diablo. La marca ocupa una parte pequeña de la cabecera. La selección de la pieza se reconoce tanto en el equipo como en la tabla; el inspector corresponde a esa instancia.

A 1280 × 900 están visibles las diez ranuras completas y el inventario de diez columnas por ocho filas. El arte se contiene dentro de las celdas. La tabla inferior y las propiedades disponen de desplazamiento visible; el proc largo ocupa dos líneas y no invade el panel vecino. Los botones del inspector quedan accesibles y la barra de estado permanece separada del contenido.

A 1600 × 1000 las columnas se leen con holgura y las propiedades permanecen cerca de su miniatura. No se observó recorte accidental de texto ni superposición de las piezas en las vistas inspeccionadas. El catálogo muestra nombres y niveles; los códigos de base son útiles para trabajo experto, aunque mostrar nombres de base y reservar los códigos para información avanzada sería una mejora posterior de lectura.

En la primera versión ultrawide, equipo e inventario quedaban a la izquierda y el alijo aparecía muy alejado a la derecha. Root corrigió su alineación para mantener ambos contenedores juntos. La captura final del paquete, inspeccionada posteriormente con una fixture privada, confirma que el alijo empieza junto al inventario y conserva sus dimensiones y piezas legibles. Las imágenes de la tabla anterior conservan la colocación previa. El alijo está denominado «Alijo personal»; no se presenta un contenedor compartido inexistente.

## Objetos y color originales

Las imágenes reales sustituyen de forma reconocible las ilustraciones generadas: la silueta de Oculus, el escudo, los tomos, el cubo, los talismanes y las piezas de equipo corresponden a recursos del juego. La procedencia se comunica en el inspector y el código distingue una imagen original disponible de un marcador neutral.

El primer grupo de capturas mostraba el yelmo con tono marrón. En la quinta captura del shader se observa el verde propio de la variante y se conserva el negro/púrpura de la armadura de conjunto; los talismanes azules mantienen detalle interior. No se observa el efecto plano del intento anterior. Esta comparación visual confirma la corrección de esos ejemplos; no sustituye la comprobación de lectura CASC ni acredita por sí sola todas las variantes de únicos y conjuntos.

La restricción se mantiene: **ningún objeto generado por IA en runtime**. Si falta una imagen original, el resultado aceptable es un marcador neutral con estado explícito. El atlas generado queda fuera de los recursos del ensamblado. El logo y los controles de la aplicación se evalúan por separado de los objetos del juego.

## Habilidades

Las tres columnas muestran las treinta habilidades completas a 1280 × 900 y 1600 × 1000. Los iconos originales de 32 px permiten reconocerlas sin depender únicamente del nombre. Los encabezados provisionales se sustituyeron por los nombres de árbol. No hay un inspector de objetos ocupando el espacio de esta página.

La versión de pulido añade «Puntos invertidos; el juego suma las bonificaciones del equipo». Esto cierra la ambigüedad entre puntos editables y nivel efectivo dentro del juego. Los máximos del perfil y los puntos disponibles siguen visibles. El comportamiento de los botones al llegar al límite, los requisitos y deshacer/rehacer requieren las pruebas funcionales; una captura no puede confirmarlos.

## Hallazgos y disposición

| Hallazgo | Disposición |
|---|---|
| Objetos generados y siluetas que cambiaban con la ubicación. | La nueva ruta utiliza recursos locales; se conserva una comprobación de identidad al mover la pieza. La exclusión del atlas y la lectura real se verifican en NativeSmoke. |
| Yelmo marrón y transformaciones de color demasiado planas. | Corregidos en la quinta captura del shader inspeccionada. Las capturas anteriores se conservan solo como evidencia de disposición. |
| Árboles provisionales y falta de iconos. | Cerrados visualmente en las capturas reales de Habilidades. |
| Confusión entre puntos invertidos y bonificaciones del equipo. | Cerrada visualmente en `polish-skills1280.png`. |
| «Nivel» ambiguo en el encabezado del objeto. | Cerrado también por inspección de la captura final del paquete: muestra «nivel de objeto». |
| Separación excesiva entre inventario y alijo en ultrawide. | Cerrado por inspección de la captura final del paquete a 3440 × 1440. Las capturas de desarrollo enlazadas conservan la colocación previa. |
| Verificación del ejecutable distribuido y concurrencia de caché. | Informe final inspeccionado: tres checks estructurales y 58 privados, sin fallos ni omisiones. Los tres metadatos del reproductor concurrente muestran todos los objetos resueltos. |

No se observan bloqueos de composición en los tamaños inspeccionados. El informe del paquete verifica la ruta CASC, la exclusión del atlas generado y las garantías de edición cubiertas por el runner. La revisión visual se limita a las vistas y fixtures descritas; no representa una prueba de todas las clases dentro del motor del juego.

## Comprobaciones preparadas para el paquete

NativeSmoke comprueba que no hay un `items-atlas` embebido y que el decodificador rechaza cabeceras y payloads truncados mediante bytes de prueba, sin distribuir assets del juego. Después de abrir una copia privada, comprueba la lectura de recursos originales para Oculus, Eschuta, Harlequin Crest y piezas de Tal Rasha que realmente estén presentes en esa fixture.

La comprobación consulta directamente LocalGameArt: exige un bitmap decodificado con píxeles visibles y no acepta el marcador de sustitución. Si no existe una instalación CASC local o la pieza no está en la fixture, informa una omisión explícita. Si CASC está instalado y falta el arte de una pieza crítica presente, el resultado es un fallo con diagnóstico. Las cachés de prueba permanecen en el temporal privado y los servicios de persistencia utilizan copias aisladas.

## Cierre con el paquete final

Se inspeccionaron las capturas finales `real-equipment1600.png`, `real-equipment3440.png` y `real-skills1600.png`, generadas por el ejecutable distribuido sobre una fixture privada y conservadas en los outputs privados de QA. No se incorporan esas imágenes ni la fixture al repositorio. Confirman los colores finales, la distinción entre Oculus y Eschuta, el alijo contiguo al inventario en ultrawide, el encabezado «nivel de objeto» y la explicación de puntos invertidos.

**Layout final de Objetos y Habilidades: aprobado en las resoluciones inspeccionadas.** Se comprobaron legibilidad y encaje, topología de ranuras/celdas, proximidad de inventario y alijo, separación de selección y propiedades y ausencia de paneles ajenos en Habilidades. Esta aprobación corresponde a la UI real, no al concepto generado.

El informe final del ejecutable self-contained 0.4 registra **3 comprobaciones estructurales y 58/58 comprobaciones privadas**, con **0 fallos y 0 omisiones**. Se leyeron sus resultados, incluidos los cuatro checks de arte original para las piezas críticas presentes en la fixture, la exclusión del atlas y el rechazo de sprites truncados. También conserva los checks de fuente intacta, exportación, guardado privado, copia de seguridad y restauración.

El reproductor de concurrencia ejecutado por root utilizó tres procesos, dos con caché compartida y otro con caché separada. Los tres registros inspeccionados informan **69/69 imágenes originales**, correspondientes a 55 claves distintas, sin sustituciones. Esto aporta evidencia del caso concurrente probado; no se extrapola a todos los fallos posibles del sistema de archivos.

SHA256 del ejecutable verificado: `2588B7321EA165DF9B637601F7F0B1E6D4DF7673A35C13AEAA2885A98E6250C6`.
