# AKARA 0.5 — dirección visual

## Intención

Debe sentirse como una armería de Diablo terminada con el cuidado de una aplicación de escritorio actual. El personaje y sus piezas son el centro de la composición. La interfaz 0.4 resolvió la lectura de recursos, pero sus franjas grises, controles uniformes, tabla dominante y miniatura de inspector parecen una herramienta administrativa.

## Firma

- Obsidiana cálida, fondos de tinta y superficies de pizarra casi negra, separados por luz y bordes discretos.
- Exocet Blizzard Mixed Caps ORIGINAL, leído de la instalación, para marca, títulos y nombre de objeto. Segoe UI para controles, valores y textos largos.
- Bronce envejecido en estructura y selección; rojo de sello en Guardar. Azul de propiedades y colores de calidad procedentes del contenido.
- Detalles finitos de marco y esquinas, materiales con grano casi imperceptible, sombras cortas y luz superior. Ninguna textura detrás de texto editable que reduzca legibilidad.
- Jerarquía de 24/20/14/12 px; alineación y espacios de 4/8/12/16/24 px.

## Composición

Cabecera de marca pequeña pero reconocible; comandos con icono y etiqueta; pestañas con estados activos claros. Identidad del personaje integrada en el espacio de equipo. Equipo e inventario agrupados en un único panel de armería con títulos y marcos contenidos; no cajas flotantes sin relación. Vista seleccionada y contenedor diferenciados.

Inspector como ficha de una pieza: imagen original de tamaño legible, nombre y calidad jerarquizados, propiedades sin una línea administrativa por cada stat, acciones agrupadas al pie. El listado inferior es una herramienta de búsqueda secundaria; no debe dominar media ventana. Catálogo con búsqueda clara, filas cuidadas y menos ruido de códigos.

En Habilidades, iconos originales y tres árboles reconocibles sobre el mismo lenguaje material. Todas las pestañas y diálogos comparten tipografía, botones, foco, contraste y espaciado.

## Invariantes

Conservar íntegros los píxeles originales de objetos y habilidades, sus variantes, colores y límites de cuadrícula. No generar ni repintar objetos, retratos, estadísticas, nombres o runas. No distribuir fuentes ni recursos del juego: extracción opcional local y fallback explícito. Mantener controles nativos, edición, validación, copia de seguridad, teclado y acciones sin arrastre. No escribir partidas para una revisión visual.

## Revisión

El boceto será el propio árbol WPF con datos y gráficos reales. Inspeccionar las pantallas completas y las zonas de equipo, encabezado, catálogo e inspector. Revisar 1280×900, 1600×1000 y 3440×1440. Verificar originalidad de recursos, diez ranuras, treinta habilidades, propiedades largas y estados habilitado/deshabilitado. Las capturas públicas usan un personaje ficticio.
