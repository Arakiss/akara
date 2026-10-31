# AKARA — Jamella modernizado

## Intención

Que quien usó Jamella reconozca inmediatamente qué puede tocar: equipo, inventario, alijo, catálogo y propiedades a la vista. Tiene que sentirse como una herramienta precisa de escritorio para Diablo, con objetos reconocibles y edición directa.

## Investigación

Fuente primaria: Jamella's Diablo 2 Editor, código publicado por su autor el 17-03-2001, posterior a 3.0. Recursos de Inventory en pp. 273–274 y tooltips p. 245.
https://tristram-archives.github.io/diablo2_infodump/Jamella%20Diablo%20II%20Editor%20C%2B%2B/jamella-diablo-ii-source-code.pdf

Equipo y contenedores juntos; Selected Item y Built-In Item Tree integrados; creación desde catálogo, Copy Buffer, edición experta contextual. La referencia es la organización y cercanía de las acciones. No copiamos código ni confundimos Jamella con Hero Editor de ZonFire99.

## Firma visual

Aplicación nativa compacta. Grafito cálido y pizarra claramente separados. Texto claro Segoe UI, tamaños 13–14, títulos 18. Oro envejecido limitado a selección y una marca pequeña. Líneas de 1 px, esquinas de 3 px, iconos monocromos. La identidad de Diablo la dan los objetos y las cuadrículas. Serif solo en el nombre del objeto si aporta reconocimiento.

Nada de retratos generados, banners, tarjetas de dashboard, piedra falsa, filigrana, glow, degradados metálicos, enormes logotipos ni márgenes que vacíen el ultrawide.

## Estructura

- Barra nativa 32 px y comandos 44 px; selección de personaje, abrir/clonar, deshacer/rehacer, revisar y guardar.
- Pestañas horizontales 38 px.
- Objetos: catálogo buscable a la izquierda; equipo compacto e inventario/alijo en el centro; inspector de propiedades a la derecha, miniatura de 64 px y acciones visibles.
- Separadores arrastrables. Los paneles auxiliares desaparecen en otras pestañas.
- Habilidades: 3 árboles/columnas, 30 filas legibles, puntos alineados, controles compactos. Ningún Oculus ocupando esa pantalla.
- Barra de estado 28 px: estado del borrador, resultado y perfil.

## Invariantes semánticas y QA

Mantener las diez ranuras reales de equipo y los conjuntos I/II. No inventar equipamiento ni estadísticas. Dimensiones de contenedores tomadas del perfil. No presentar alijo compartido si solo existe el personal. Texto, estadísticas, selección y acciones siempre nativos y separados del arte. Captura pública solo con personaje sintético.

Revisar boceto entero, cabecera, equipo/inventario y propiedades. Marcar texto/filas inventadas del boceto como defectos y no trasladarlas al programa. Verificar después la UI real a 1280×900, 1600×1000 y 3440×1440. El boceto no cuenta como prueba de la aplicación.
