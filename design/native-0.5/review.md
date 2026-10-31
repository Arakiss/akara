# Revisión visual 0.5

Estado: **approved para el acabado visual inspeccionado**. Hallazgos visuales abiertos: **0**. La aprobación corresponde al árbol WPF real y a las vistas documentadas; la verificación del paquete y del comportamiento se registra por separado.

## Evidencia

Las capturas siguientes utilizan un personaje ficticio y son evidencia sintética apta para publicación. Se volvieron a inspeccionar las dos copias públicas refrescadas desde el paquete final. La matriz anterior permanece en `artifacts`, fuera del control de versiones. No se incorporan aquí partidas, nombres de usuarios, tablas ni recursos del juego.

| Captura inspeccionada | Comprobación |
|---|---|
| [native-equipment.png](../../assets/screenshots/native-equipment.png) | Captura pública final de Objetos a 1600 × 1000: jerarquía, geometría, navegación y acciones del inspector. |
| [native-skills.png](../../assets/screenshots/native-skills.png) | Captura pública final de Habilidades a 1600 × 1000: treinta entradas, iconos originales y navegación sans actualizada. |
| [final1600.png](../../artifacts/native-05-ui-review/captures/final1600.png) | Objetos a 1600 × 1000, escala 1: inventario con celdas de 44 px, navegación sans y nueva ficha de objeto. |
| [final3440.png](../../artifacts/native-05-ui-review/captures/final3440.png) | Objetos a 3440 × 1440, escala de apariencia 1,5: equipo, inventario y alijo personal contiguos. |
| [scale125-2200.png](../../artifacts/native-05-ui-review/captures/scale125-2200.png) | Objetos a 2200 × 1000, escala 1,25: legibilidad, desplazamiento del inspector y acciones visibles. |
| [long-chilling1280.png](../../artifacts/native-05-ui-review/captures/long-chilling1280.png) | Nombre largo real de un talismán a 1280 × 900: dos líneas completas, sin invadir miniatura ni metadatos. |
| [skills1600.png](../../artifacts/native-05-ui-review/captures/skills1600.png) | Treinta habilidades, iconos originales y coherencia material. Conserva la navegación anterior; no se usa para acreditar la tipografía final de las pestañas. |

Los metadatos revisados identifican `PresentationFramework`, `Akara.Desktop.WorkbenchView`, dimensiones reales iguales a las solicitadas, origen nativo, ausencia de navegador y ausencia de ventana visible durante la captura. La matriz y las copias públicas finales informan **66 imágenes originales de 66**; la matriz registra 48 claves de arte distintas. Las escalas de apariencia indicadas se comprobaron en sus sidecars. Los metadatos privados refrescados del paquete final informan **69 imágenes originales de 69** en las tres vistas solicitadas; no se convierten esas capturas en evidencia pública.

La evidencia de paquete sobre fixtures privadas, manifiestos y hashes corresponde a los [outputs externos del proyecto](../../../../outputs/), fuera del repositorio. Esta revisión no copia ni convierte esas fixtures en demostraciones públicas.

## Pases de calidad

| Pase | Comprobaciones y resultado |
|---|---|
| **Read — lectura y jerarquía** | Personaje y objeto seleccionado tienen identidad propia; los metadatos y valores usan una escala subordinada. La previsualización original alcanza un tamaño reconocible y «Editar objeto» se diferencia de las acciones secundarias. El listado inferior es auxiliar: a 1600 su altura ronda 185 px, frente a unos 460 px del espacio de armería. Resultado: conforme. |
| **World — coherencia material y escala** | Tinta, pizarra oscura, bordes y esquinas mantienen un tratamiento coherente entre equipo, contenedores e inspector. La luz superior es contenida y no introduce reflejos incompatibles con los controles. Los objetos conservan proporciones reconocibles al cambiar de zona y escala; los marcos no añaden perspectiva o volumen contradictorios. Anatomía y arquitectura de una escena no aplican: no hay retratos ni entorno representado. Resultado: conforme. |
| **Semantics — topología y datos** | Diez ranuras de equipo, dos anillos y selector I/II distinguibles. Inventario y alijo personal mantienen cuadrículas separadas; no se inventa un alijo compartido. Habilidades muestra treinta entradas y explica puntos invertidos frente a bonificaciones. Nombre, calidad, ubicación y nivel de objeto permanecen como texto de la interfaz. Resultado: conforme en las muestras inspeccionadas. |
| **Craft — ejecución** | Las piezas conservan siluetas y proporciones, con márgenes dentro de sus ranuras; no se observan puntas cortadas. El nombre largo ajusta a dos líneas. En escala 1,25 y en ultrawide, las propiedades usan un viewport con desplazamiento y las acciones permanecen fuera de él y visibles. El contenido avanzado que queda bajo el borde se alcanza mediante ese desplazamiento; no constituye un recorte accidental de controles. Resultado: conforme. |
| **Identity — continuidad** | Exocet Mixed Caps original se utiliza en marca, títulos e identidad del objeto; las pestañas finales y la lectura frecuente usan sans. Sus formas particulares pertenecen a la fuente auténtica y no son glifos generados. Tinta, superficies cálidas oscuras, marco contenido y divisor original conectan la armería con la ficha sin llenar los paneles de bordes dorados. Habilidades comparte ese lenguaje y libera los paneles de objetos. Resultado: conforme. |
| **Runtime — separación y procedencia** | Las imágenes inspeccionadas son renders del control WPF real. Texto, valores, selección y acciones permanecen separados del arte; los metadatos acreditan disponibilidad de las imágenes originales de las muestras. El ajuste de presentación conserva la geometría del objeto. La conservación byte a byte de bitmaps, el guardado y las garantías de Core requieren su verificación automatizada propia; esta inspección no los deduce de una captura. Resultado: conforme para la presentación revisada. |

## Hallazgos anteriores y disposición

| Hallazgo | Disposición final |
|---|---|
| Inventario pequeño dentro de un marco con espacio desaprovechado. | **Cerrado.** Celdas de 44 px a 1600 y escala adaptativa en ultrawide; inventario y alijo quedan agrupados sin el vacío anterior entre contenedores. |
| Exocet demasiado pequeño para navegación frecuente. | **Cerrado.** Las capturas finales de Objetos muestran pestañas sans legibles. La fuente original permanece en las voces de identidad. |
| Riesgo de desbordamiento con nombres largos. | **Cerrado.** La muestra real de talismán a 1280 conserva el nombre completo en dos líneas y mantiene las acciones accesibles. |
| Escala manual 150% con inspector recortado en una ventana insuficiente. | **Cerrado por prueba GUI reportada por root sobre el paquete final.** La ventana crece a 1740 × 1200, todos los botones permanecen visibles y se puede volver a Auto. Las capturas fuera de pantalla no se usan como prueba de esta corrección interactiva. |
| Barra de estado oculta por la barra de tareas al maximizar. | **Cerrado por prueba GUI reportada por root tras el ajuste a WorkArea.** La barra de estado completa queda visible sobre la barra de tareas. El tamaño exterior de WindowGraphicsCapture no se interpreta como una medida del área útil de contenido. |
| Columnas auxiliares comprimidas tras maximizar, cambiar de vista y restaurar. | **Cerrado por prueba GUI reportada por root sobre el paquete final.** Tras maximizar → Habilidades → Objetos → restaurar a 1600, el nombre ocupa el ancho restante y Ubicación/Calidad se mantienen completas y legibles. Este cierre corresponde a la prueba interactiva de root, sin una nueva revisión visual completa por este agente. |

No quedan cambios visuales requeridos por esta revisión. El resultado presenta una jerarquía de armería y ficha de objeto que supera el cambio de paleta: cambian la escala de las piezas, la prioridad del listado, la estructura del inspector y la lectura de los controles.

## Límites de la aprobación

Esta revisión fue estática y no utilizó entradas de escritorio. El hover de 120 ms fue declarado para el tema final; aquí no se presenta como una duración medida. La aprobación visual no sustituye una prueba de todas las clases dentro del motor del juego.

La inspección propia comprende imágenes, sidecars, informe del paquete y lectura de la guardia de escala. Las comprobaciones GUI de escala manual, retorno a Auto, maximización y navegación entre Habilidades y Objetos fueron realizadas y reportadas por root. Root también confirmó Guardar deshabilitado y ausencia de cambios en datos durante esa comprobación. No se atribuyen estas acciones al agente revisor ni se inventan mediciones de tiempo o de WorkArea. No se realizó ni se afirma una auditoría Heimr de estas capturas nativas.

## Verificación del paquete

Se inspeccionaron el [informe externo del paquete](../../../../outputs/AKARA-0.5-QA-438cc60ba6e74447ba1e5873c8e670db/native-test.json) y el detalle de su self-test privado, ejecutados por root sobre el EXE self-contained final. Registran **3 comprobaciones estructurales y 59/59 privadas**, con **0 fallos y 0 omisiones**.

Entre los checks leídos figuran la exclusión de retratos rechazados, la ausencia del atlas generado, la ausencia de fuentes y sprites locales del juego embebidos y el rechazo de sprites truncados. Estos resultados se atribuyen al paquete probado; no se deducen de su apariencia.

SHA256 del ejecutable final verificado: `2F5AB922899E01E7FD15BE05B4D0E07B43EEAD863562C671477BB43ADFA9216C`.
