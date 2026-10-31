# Revisión del concepto 0.4

Estado: **reviewed · preproducción**, después de inspeccionar la imagen completa y los tres recortes de Heimr registrados abajo. Inspección independiente de [concept-candidate.png](concept-candidate.png), salida ImageGen de 1586 × 992, frente a [brief.md](brief.md) y [prompt-concept.txt](prompt-concept.txt). La composición sirve como referencia de organización; quedan hallazgos semánticos abiertos. Esta revisión no acredita una aplicación implementada ni aprueba los píxeles como recurso de producción.

## Composición y oficio

La dirección resulta más próxima a una herramienta de edición: búsqueda, piezas, contenedores y propiedades comparten el campo de trabajo. La cabecera compacta y las pestañas horizontales dejan que los objetos expliquen el producto. El marco gris, la tipografía clara y el azul de las propiedades establecen una jerarquía comprensible sin depender de una gran marca ornamental.

- **Lectura:** el recorrido catálogo → pieza → propiedades es claro. El objeto seleccionado tiene nombre, base y propiedades juntos. Las acciones aparecen cerca del contexto de edición.
- **Topología del equipo:** se reconocen las diez ranuras: yelmo, amuleto, dos manos, armadura, guantes, botas, cinturón y dos anillos. Los conjuntos I/II tienen un selector propio.
- **Identidad:** el pequeño símbolo de la cabecera y los objetos bastan para dar carácter. Los marcos no compiten con las ilustraciones.
- **Oficio:** los bordes y las columnas del inspector son coherentes a escala completa. Sin embargo, los fondos conservan una textura diagonal y variaciones de brillo; los botones dorados tienen degradado. Para cumplir el brief deben convertirse en superficies planas y oro limitado al estado seleccionado y a la acción principal.

La densidad mejora, pero el boceto no reproduce las medidas propuestas. El catálogo ocupa aproximadamente 425 px porque muestra árbol y resultados en dos subpaneles; el inspector ronda 375 px y su miniatura supera los 64 px pedidos. La cabecera, comandos y pestañas consumen cerca de 147 px, frente a los 114 px del brief. Conviene conservar la organización y ajustar esas proporciones en controles reales.

## Hallazgos semánticos

| Región | Hallazgo y consecuencia | Corrección de implementación |
|---|---|---|
| Cabecera, pestañas, catálogo e inspector | La interfaz está en inglés: Open, Clone, Review, Save, Item Catalog, Properties, etc. La línea «50% Better Chance of Getting Magic Items» tampoco puede adoptarse como una plantilla de traducción. | Autoría nativa en español: Abrir, Clonar, Revisar cambios, Guardar con copia, Catálogo de objetos y Propiedades. Los nombres propios de los objetos se resuelven según el perfil; no se traducen por conjetura. |
| Inspector de The Oculus: requisitos | El boceto muestra 58 de Fuerza. La base resuelta por el perfil local actual no tiene requisito de Fuerza ni Destreza. El nivel 42 sí coincide con la definición del único, pero esto no valida el resto de las cifras pintadas. | Leer requisitos y base desde el DTO del objeto. Mostrar ausencia de requisito con una formulación clara; no importar 58 ni otras cifras del dibujo. |
| Inspector de The Oculus: huecos | «Socketed (1)» aparece junto a tres círculos vacíos. No se distingue entre capacidad de la base, huecos creados y contenido insertado. | Representar los huecos reales del objeto y sus hijos. La capacidad máxima de una base no prueba cuántos huecos tiene esa pieza. Contador, iconos y contenido deben describir el mismo estado. |
| Inspector: propiedades y bloque Advanced | Daño, durabilidad, tiradas, nivel 87 y fingerprint son valores ilustrativos. La imagen presenta una combinación concreta de propiedades como si describiera la pieza seleccionada. | Conservar los valores, capas y tiradas del objeto real. Generar todas las filas desde Core, incluidos modificadores activados y propiedades largas. Nivel de objeto y fingerprint no se reutilizan como datos de demostración de una partida real. |
| Inventario central | El dibujo permite contar ocho columnas y nueve filas, frente al ejemplo de diez por ocho solicitado. Las ilustraciones no aportan una especificación fiable de sus rectángulos de ocupación. | Tomar filas y columnas del perfil y cada huella del DTO. Usar celdas regulares, contener el arte en su huella y validar límites y solapamientos. Diez por ocho tampoco debe convertirse en una regla universal para otros perfiles o contenedores. |
| Catálogo: Catalysts | La categoría seleccionada mezcla Oculus y Eschuta con Windforce/Faith y armas de asta como Infinity o Tomb Reaver. La lista contradice el filtro visible. | Las categorías, tipos y búsquedas deben filtrar registros reales. Distinguir bases, únicos, conjuntos y palabras rúnicas según las capacidades del catálogo. Una categoría visual no puede inventar una taxonomía incompatible con los datos. |
| Selección y acciones | Oculus está resaltado en el catálogo, pero no hay un estado equivalente claro en la pieza equipada. El inspector combina propiedades de una instancia con acciones Edit/Move/Duplicate. No queda claro si se está mirando una plantilla o una pieza poseída. | Identificar el origen de la selección. Catálogo: previsualizar y añadir una pieza. Objeto poseído: seleccionar su ID y editar, mover o duplicar esa instancia. Mantener la selección sincronizada entre cuadrícula, equipo, lista e inspector. |
| Barra de comandos y estado | «No pending changes» convive con Guardar destacado. El boceto no expresa revisión pendiente, personaje nuevo, juego abierto o una operación en curso. | Mantener la máquina de estados existente. Guardar con copia solo se habilita cuando corresponde; mostrar por qué está deshabilitado. Un personaje nuevo debe indicar que sigue en memoria y necesita revisión y guardado. |
| Bandeja Reusable Items | La bandeja presenta antorcha, pociones, pergamino y llave como si existiera una colección reutilizable con acciones propias. Los píxeles no definen persistencia, pertenencia ni comportamiento. | Respaldarla con un modelo y acciones concretos antes de mostrarla. Si no entra en el alcance, retirarla. No sustituirla por una fila decorativa ni confundirla con el inventario, la biblioteca de builds o el portapapeles de objetos. |

No se aprueban ilustraciones individuales a partir de este boceto. La mezcla de objetos pintados y sprites de aspecto clásico funciona como intención de reconocimiento, pero el arte del concepto no demuestra anatomía, resolución, procedencia o consistencia de un atlas de producción. La restricción posterior del usuario es **cero objetos generados por IA**: tampoco se reutiliza el atlas generado de 0.3. Los objetos de la aplicación deben proceder de la instalación local mediante CASC, con la variante correspondiente a la base y al único/conjunto, y sus transformaciones nativas cuando correspondan. Conservar la identidad de una pieza al pasar del equipo al alijo. Una base desconocida conserva el marcador neutral.

## Inspección explícita de los recortes

| Recorte de Heimr inspeccionado | Lectura y oficio | Semántica y decisión |
|---|---|---|
| [commands-and-tabs.png](audit/crops/jamella-modern-layout-study/commands-and-tabs.png) | Marca pequeña, iconos monocromos y pestaña activa reconocible. La línea de separación funciona. El brillo del botón Guardar y el alto de las franjas siguen siendo mayores que la dirección plana y compacta pedida. | Confirmados interfaz inglesa y Guardar destacado sin explicar el estado de revisión. Trasladar la jerarquía y sustituir textos, estado y medidas por controles reales. |
| [paperdoll-and-inventory.png](audit/crops/jamella-modern-layout-study/paperdoll-and-inventory.png) | Diez ranuras distinguibles; se reconocen un par de guantes y un par de botas. Equipo y contenedor se pueden relacionar sin recorrer la ventana. La identidad de Diablo proviene de las piezas. | Confirmadas ocho columnas y nueve filas ilustradas. Las huellas, objetos y píxeles son demostrativos. Implementar el mapa real de celdas y usar exclusivamente objetos originales extraídos de la instalación. |
| [properties.png](audit/crops/jamella-modern-layout-study/properties.png) | Valores alineados y secciones reconocibles. La miniatura consume más espacio que los 64 px del brief; un proc largo necesitará varias líneas. | Confirmados requisito ficticio de Fuerza 58 y ambigüedad de «Socketed (1)» con tres círculos vacíos. Propiedades, requisitos, huecos, nivel y fingerprint deben leerse del objeto seleccionado. |

El repaso de los recortes confirma los hallazgos de la imagen completa; no los cierra. El estado `reviewed` permite desarrollar la dirección en preproducción. El concepto permanece fuera de los recursos de la interfaz y no se eleva a `approved`.

## Contrato para la UI nativa

- Construir cabecera, comandos, pestañas, listas, cuadrículas, divisores, selección, texto y propiedades con controles nativos. El PNG es una referencia de diseño; no se incorpora como fondo, mapa de clics o imagen con datos superpuestos.
- Cargar los objetos originales desde CASC local y resolver las variantes de únicos/conjuntos mediante los datos del perfil. Los iconos de habilidades también deben ser originales; se prevén a 32 px. No incluir el atlas generado ni sus recortes en los recursos de la aplicación.
- Limitar catálogo e inspector a anchos útiles, usar separadores realmente arrastrables y definir mínimos. El centro recibe el espacio restante. El ancho adicional debe favorecer el trabajo con contenedores y herramientas, sin márgenes enormes ni escalado indefinido de objetos.
- A 1280 × 900, permitir colapsar paneles auxiliares o reorganizar el centro y ofrecer desplazamiento visible. Conservar texto de 13–14 px; no resolver la falta de espacio reduciendo toda la interfaz.
- Distinguir el conjunto que se está viendo del conjunto activo al cargar la partida. Cambiar de vista I/II no debe modificar el estado del personaje por sí solo.
- Mostrar los contenedores admitidos por el perfil. «Alijo» debe representar el personal si ese es el único soportado; no añadir un alijo compartido implícito.
- Mantener las propiedades largas legibles con ajuste de línea y desplazamiento. La tabla corta de Oculus no justifica alturas fijas que corten procs, capas o afijos de otros objetos.
- Conservar los controles alternativos al arrastre, foco visible y nombres accesibles de ranuras e iconos. La ilustración del equipo no sustituye la selección por teclado ni las acciones de mover e importar.
- En Habilidades y otras pestañas, liberar el espacio de los paneles de objetos. Comprobar los tres árboles, las 30 habilidades y los controles de puntos en la UI real, sin un inspector de Oculus ajeno a esa tarea.
- Mantener revisión, deshacer/rehacer, conflictos de origen, guardado con copia y protección del juego abierto. La nueva distribución no altera sus garantías.

## Puerta de revisión siguiente

La siguiente evidencia debe ser la UI WPF real a 1280 × 900, 1600 × 1000 y 3440 × 1440, con personaje sintético para capturas públicas. Revisar un contenedor lleno, una pieza seleccionada desde cada origen, nombres y propiedades largos, huecos reales y estados de guardado habilitado/deshabilitado. Comprobar también que Habilidades recupera el espacio del inspector.

El boceto queda **revisado como dirección de preproducción**. Los hallazgos de esta tabla deben resolverse en la implementación y comprobarse sobre capturas reales antes de aprobar el resultado de la aplicación.

## Captura de la UI real: Habilidades

Inspeccionada por separado [actual-skills1600.png](actual-skills1600.png), de 1600 × 1000. Esta evidencia corresponde a controles de la aplicación; no se confunde con el boceto. La revisión es visual: no se ha interactuado con la ventana ni se han probado comandos.

La página usa el ancho disponible y elimina los paneles de objetos. Se ven las treinta habilidades completas, agrupadas en tres columnas con diez filas, sin recortes a este tamaño. Los contadores están alineados y los botones de ajuste permanecen cerca del final de cada fila. El grafito es plano, los encabezados son legibles y la barra inferior distingue «Borrador sin guardar» del mensaje de creación en memoria. La reserva de espacio bajo las listas es razonable; no hace falta estirar las filas para rellenarla.

Prioridades para la siguiente pasada:

1. **Identificar los árboles y reconocer habilidades.** «Árbol 1/2/3» son encabezados provisionales. Sustituirlos por nombres propios del árbol de la clase y añadir los iconos originales de 32 px previstos. La correspondencia debe venir de los datos de cada clase; no fijar Fuego/Rayos/Frío para las ocho clases. Este trabajo ya está anunciado, por lo que se registra como pendiente conocido.
2. **Explicar qué mide el contador.** La cifra editable representa puntos invertidos, mientras que el juego puede mostrar un nivel mayor con bonificaciones de equipo. Añadir una indicación como «Puntos invertidos» o equivalente y mantener el máximo del perfil. Cualquier total con equipo necesita una fuente de cálculo fiable; no inferirlo del boceto.
3. **Verificar los límites de los controles.** En la captura, los botones de incremento junto a 25 puntos son visualmente muy parecidos al resto. La imagen no permite concluir si están habilitados. Comprobar que un máximo alcanzado tiene una respuesta clara y que cero puntos disponibles, requisitos incumplidos o un borrador inválido se explican sin botones que aparenten actuar y no hagan nada. No se ha probado su comportamiento en esta revisión visual.
4. **Conservar el significado del guardado.** El botón superior dice «Guardar»; la operación existente crea una copia de seguridad. Preferir el nombre explícito «Guardar con copia» y conservar sus condiciones de habilitación. El estado inferior actual sí explica que el personaje sigue en memoria.
5. **Revisar un detalle de la fila de pestañas.** En su extremo superior derecho aparece una pastilla gris vertical aislada, aproximadamente 14 × 27 px, sin función visible en la captura. Determinar si es un elemento de desplazamiento involuntario y corregirlo si no corresponde a contenido que realmente desborde.

Quedan pendientes las capturas reales de 1280 × 900 y 3440 × 1440. Deben comprobar especialmente la cercanía entre nombre y contador, la visibilidad de las treinta filas, los nombres de árbol largos y el tamaño de los iconos. La captura de Habilidades no acredita todavía el catálogo, los objetos originales, el inspector ni los estados interactivos de las otras páginas.
