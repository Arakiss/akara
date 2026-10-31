# Revisión de la interfaz de producción 0.6

La interfaz inspeccionada es `WorkbenchView`, el mismo control WPF que abre la aplicación portátil. El boceto anterior no se usa como prueba de implementación. Esta revisión acredita composición, datos y funcionamiento observados; no sustituye el criterio estético del usuario.

## Evidencia

La matriz privada permanece fuera del repositorio, en los outputs locales de QA. Se han renderizado Equipo a 1280×900, 1600×1000 y 3440×1440, la lista inicial de personajes, Habilidades, Personaje, Mercenario, Mundo, Taller, Biblioteca y Cambios. La escala automática en ultrapanorámico es 1,5. Se ha inspeccionado además una copia privada de un personaje existente, sin escribir en ella durante las capturas.

Los sidecars identifican `PresentationFramework`, `Akara.Desktop.WorkbenchView`, dimensiones reales iguales a las solicitadas, `native: true`, `browser: false` y `visibleWindowCreated: false`. La demostración ficticia contiene 66 de 66 imágenes originales resueltas; la copia privada, 69 de 69. La portada contiene nombres privados y no se publica. Las dos capturas públicas corresponden a un personaje ficticio:

- [Equipo, 1600×1000](../../assets/screenshots/native-equipment.png).
- [Habilidades, 1600×1000](../../assets/screenshots/native-skills.png).

## Seis pases

| Pase | Resultado inspeccionado |
|---|---|
| Lectura | Una cabecera con marca, menú y versión; una barra de trabajo; pestañas; títulos de panel discretos. Selección de personaje y objeto visibles. Propiedades completas con desplazamiento y acciones fuera del viewport. |
| Coherencia | Grafito, gris, marfil y bronce contenido. Sin retratos ni piezas generadas. Exocet original leído localmente al preparar el personaje; Segoe UI para controles y propiedades. El arranque sin recursos cargados usa una fuente de reserva. |
| Semántica | Diez ranuras, dos anillos, conjuntos I/II, inventario y alijo personal separados. El árbol consulta todas las páginas del perfil; la muestra local contiene 1478 únicos. Los valores de piezas proceden del personaje; las entradas de catálogo no fingen mostrar una tirada. |
| Acabado | Sin fondo blanco en listas o árbol desactivados. Equipo e inventario no se solapan en la ventana reducida. El nombre largo de Tal Rasha ocupa dos líneas; sus propiedades siguen accesibles con scroll. A 3440 hay cuadrícula adicional del alijo y escala 1,5. |
| Contexto | Menús reales, teclado y controles sin arrastre, galería con selección y doble clic. La lista completa de objetos sigue disponible como desplegable secundario. Lista inicial con búsqueda por nombre o carpeta y apertura explícita. |
| Integridad | Ninguna captura abre una ventana, inyecta entrada o escribe partidas. Las pruebas usan copias aisladas. Conservados revisión, copias, checksum, roundtrip, conflictos y bloqueo del guardado con el juego abierto. No se acredita con esto una nueva prueba dentro del motor del juego. |

## Comportamiento

Las pruebas nativas añaden tres flujos de producción: filtrar y abrir desde la lista inicial; seleccionar una pieza del alijo y obtener su ficha completa; abrir la rama de bases del árbol y comprobar que contiene todas las páginas del perfil. Navegar y consultar no cambia el borrador. Se mantienen las pruebas de hover, edición, habilidades, conjuntos de armas, mercenario, biblioteca, guardado y restauración.

El paquete incluye runtime, CLI y documentación del tributo. Sus identificadores técnicos AKARA y su carpeta local se conservan para mantener perfiles, copias y comandos existentes.
