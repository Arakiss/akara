# AKARA: verificación del núcleo

2026-10-31. Implementación congelada para empaquetar con .NET10.0.403; compilación sin advertencias ni errores.

Se ejecutaron38 verificaciones de integración mediante `tests/ForjaHoradrica.Tests.csproj`. Las entradas reales sólo se leyeron y copiaron a un directorio temporal; todos los guardados/reemplazos/restauraciones ocurrieron en dicho directorio.

- Hechicera y Brujo identificados y conservados con roundtrip binario idéntico. Edición de nivel de Brujo con crecimiento y presupuesto de sus tablas.
- Operación vacía y formulario completo con nivel sin cambiar exportan bytes idénticos. XP intermedia17 conservada.
- Exceso de atributos, límite nativo de habilidades, prerequisitos, reducción de nivel con skills omitidas y ranuras incompatibles rechazados.
- Preset Orbe validado: base926,5vida,96puntos, Anya consumido en tres dificultades, botón derecho y Teletransporte, SpiritID20635, Um y máscaras parciales de Tal.
- Reaplicar preset conserva el equipo previo en el alijo y evita duplicar Antorcha/Annihilus activos.
- MercenarioActII nivel85 con XP74562000 y equipo Infinity/Fortitude etéreos, Andariel/Ral: binario estable, equipo conservado y ninguna propiedad ED duplicada tras lectura.
- Guardia de juego abierto, conflicto de hash, backup byteidéntico, sustitución atómica y restauración al original comprobados con temporales.
- Las dos entradas reales permanecen byteidénticas al terminar.

Smoke HTTP real: bootstrap sin token→401, origen ajeno→403; apertura, búsqueda de Oculus y preview nativo funcionan. El frontend realiza QA visual y prueba el cierre autorizado del host. El ejecutable final, sus recursos embebidos y su arranque portátil se verifican después del publish por el agente principal.

Limitaciones explícitas: escritura de versiones97–105 requiere roundtrip idéntico con el perfil seleccionado; fixtures reales comprobados de versión105 para Sorceress/Warlock. Algunas propiedades poco frecuentes del catálogo aún no están soportadas por generación; el catálogo identifica esas filas y la vista previa rechaza cualquier función no implementada en vez de omitirla. Las estadísticas derivadas de equipo las calcula el juego; la UI muestra recursos base. No se convierten versiones ni se modifica el alijo compartido.
