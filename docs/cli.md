# Automatizar AKARA

`akara-cli.exe` comparte el núcleo del editor visual. Funciona sin iniciar el navegador, sin servidor HTTP y sin instalar un SDK. Los comandos escriben JSON en stdout, los errores escriben JSON en stderr y no presentan preguntas interactivas.

```powershell
.\akara-cli.exe doctor
.\akara-cli.exe saves
.\akara-cli.exe inspect 'C:\Partidas\Example.d2s'
.\akara-cli.exe catalog 'C:\Partidas\Example.d2s' --type runeword --search Spirit
.\akara-cli.exe schema preview
```

El perfil instalado de Reimagined se detecta automáticamente. Usa `--excel 'C:\MiMod\data\global\excel'` para seleccionar las tablas de otro perfil y `--strings` si sus traducciones están en otra carpeta. `--data-dir` aísla el historial de respaldos, por ejemplo para pruebas.

## Planificar y aplicar en procesos separados

```powershell
.\akara-cli.exe plan 'C:\Partidas\Example.d2s' --changes '@changes.json' --out plan.json
.\akara-cli.exe validate plan.json
.\akara-cli.exe apply plan.json
```

Un plan persistente incluye la huella de la partida original, la huella de las tablas, los bytes candidatos y las diferencias. El comando `apply` verifica de nuevo esos datos, exige que Diablo II esté cerrado, crea y verifica un respaldo y reemplaza el archivo mediante una operación atómica. Si el juego ha guardado desde que se creó el plan, devuelve `source_changed`; crea un plan nuevo sobre la partida actual.

Para crear una copia nueva y conservar el original:

```powershell
.\akara-cli.exe export plan.json --out 'C:\Copias\Example.d2s'
.\akara-cli.exe clone 'C:\Partidas\Example.d2s' --name ExampleCopy --out 'C:\Partidas\ExampleCopy.d2s'
```

Las exportaciones utilizan creación exclusiva: una ruta existente provoca un error, nunca una sobrescritura. El nombre del archivo debe coincidir con el nombre interno del personaje. Guarda los planes y blueprints con el mismo cuidado que tus partidas: contienen datos privados del personaje.

## Contrato de entrada

`--changes` y otros argumentos JSON aceptan un archivo, `@archivo`, JSON literal o `-` para stdin. En PowerShell, usa comillas simples para JSON literal y para argumentos que empiecen por `@`.

```powershell
'{"fillTomes":true,"fillBelt":true}' | .\akara-cli.exe preview 'C:\Partidas\Example.d2s' --changes -
```

Los campos ausentes conservan el estado. `skills` reemplaza la asignación completa de puntos de la clase; incluye todas las habilidades que quieras conservar. Una vista previa inválida devuelve sus errores y código de salida 3, sin guardar nada. Usa `schema preview` para descubrir los campos de la versión instalada y los catálogos nativos para obtener IDs y nombres.

| Salida | Significado |
|---|---|
|0|Operación completada|
|1|Error inesperado|
|2|Uso incorrecto o JSON inválido|
|3|Validación rechazada|
|4|Conflicto de estado, perfil cambiado o juego abierto|
|5|Error de archivos o permisos|

Los argumentos desconocidos y repetidos se rechazan antes de realizar operaciones. No analices los mensajes de error para automatizar: usa `error.code` y el código de salida. Para inspeccionar todas las capacidades y esquemas disponibles, ejecuta `help` y `schema commands`.

## Crear personajes y lotes

```powershell
.\akara-cli.exe create --name Example --class Amazona --preset squad-amazon --out 'C:\Partidas\Example.d2s'
.\akara-cli.exe batch --manifest '@examples/automation/squad.json' --out-dir 'C:\Partidas\Escuadron'
```

`--class` acepta el nombre nativo, los nombres españoles habituales o un ID de 0 a 7. La creación empieza con un personaje nuevo de formato105, sin usar la partida de otra persona como plantilla. Usa `--level` para una creación sin preset o `--request @create.json` para proporcionar un `CreateRequest` completo.

El lote valida todas las construcciones y colisiones de nombres antes de crear archivos. Si falla una escritura, retira únicamente los archivos que acaba de crear y siguen coincidiendo con su hash; conserva cualquier archivo que haya cambiado. No sobrescribe personajes previos y exige que el juego esté cerrado.

## Mundo y accesos rápidos

`world SAVE` muestra los identificadores y estados de misiones y puntos de ruta. `schema world` describe los cambios que puedes incluir en `PreviewRequest.world`. Las recompensas se reconcilian con los presupuestos de habilidades, atributos y vida; retirar una recompensa puede invalidar una asignación que la necesite.

`hotkeys` usa índices de 0 a 15. El valor nativo 32767 vacía una asignación. `hotkeySides` permite indicar `left` o `right` cuando importa el lado de lanzamiento, por ejemplo para mantener Concentración a la derecha mientras lanzas Martillo a la izquierda. Clonar conserva el progreso completo; una biblioteca de build conserva la XP del destino al mismo nivel y usa el umbral nativo si cambias el nivel.

## Taller y blueprints

```powershell
.\akara-cli.exe items list --save 'C:\Partidas\Example.d2s'
.\akara-cli.exe items export --save 'C:\Partidas\Example.d2s' --id i0 --out item.akara-item.json
.\akara-cli.exe items import --save 'C:\Partidas\Example.d2s' --blueprint '@item.akara-item.json' --out import-plan.json
.\akara-cli.exe schema design
.\akara-cli.exe items create --save 'C:\Partidas\Example.d2s' --changes '@design.json' --out craft-plan.json
.\akara-cli.exe items edit --save 'C:\Partidas\Example.d2s' --id i0 --changes '@design.json' --out edit-plan.json
```

Crear, importar o editar objetos produce un plan, sin cambiar la partida. Revisa el diff y aplica o exporta ese plan. Puedes elegir el destino con `--page`, `--owner`, `--slot` y `--weapon-set`. Crear e importar usan el alijo del personaje por defecto, con colocación automática; editar conserva su identidad y posición si no indicas otro destino. El diseño de edición es completo: especifica las propiedades que quieras conservar.

El diseño permite elegir una base existente, calidad, propiedades con valores humanos o nativos, afijos, condición etérea, durabilidad, cantidades y engarces. Usa `catalog --type base` y `catalog --type stat` para descubrir los datos del perfil y sus límites. El esquema `design` describe la estructura exacta. Las propiedades incompatibles o fuera de rango se rechazan.

Los blueprints contienen el objeto nativo, su descripción legible y las huellas del objeto y perfil. Importar verifica versión, tablas, checksum lógico y codificación estable, y asigna una identidad nueva a la copia. Los blueprints no contienen la cabecera del personaje. Crear un nuevo modelo visual, tipo base o definición global de único requiere modificar los datos del mod por separado.

## Biblioteca de builds

```powershell
.\akara-cli.exe loadouts export --save 'C:\Partidas\Example.d2s' --name 'Rayos y orbe' --out rays.akara-loadout.json
.\akara-cli.exe loadouts save --save 'C:\Partidas\Example.d2s' --name 'Rayos y orbe'
.\akara-cli.exe loadouts list
.\akara-cli.exe loadouts plan --save 'C:\Partidas\Example.d2s' --loadout '@rays.akara-loadout.json' --out loadout-plan.json
.\akara-cli.exe validate loadout-plan.json
```

Una configuración incluye ambos conjuntos de armas, mercenario, talismanes y, por defecto, nivel, atributos, habilidades, accesos rápidos y recompensas necesarias. Usa `--items-only` para omitir la parte del personaje. La clase, el formato y la huella del perfil deben coincidir.

Aplicar la configuración prepara una vista previa; el equipo anterior pasa al alijo si hay espacio. `--discard-previous` prepara su eliminación explícita en el plan. Revisa siempre ese diff antes de `apply`. La biblioteca de la interfaz visual y la CLI es la misma. `loadouts show ID`, `loadouts import --save SAVE --loadout @FILE` y `loadouts delete ID` permiten gestionarla sin navegador.

## Restaurar

```powershell
.\akara-cli.exe backups 'C:\Partidas\Example.d2s'
.\akara-cli.exe restore 'C:\Partidas\Example.d2s' --backup ID --expect SHA256_ACTUAL
```

Restaurar conserva otro respaldo del estado desplazado. No acepta un archivo arbitrario como respaldo: el identificador debe pertenecer al historial de esa partida.
