# AKARA: contrato de API local

El ejecutable abre `http://127.0.0.1:PUERTO/?token=NONCE`. Todas las API requieren `X-Forja-Token: NONCE`; sólo bootstrap admite también `?token=NONCE`. JSON camelCase. Errores: `{code,message,details?}`. El frontend no necesita servicios externos.

| Ruta | Contrato |
|---|---|
| GET `/api/bootstrap` | `{appName:"AKARA",version,gameRunning,profiles:[{id,name,kind,excelPath}],saves:[{path,name,profileId}],presets:[{id,name}],defaultProfileId}` |
| GET `/api/profiles` | `{profiles:[...]}` |
| POST `/api/profiles` | `{excelPath,stringsPath?,name?}` → perfil validado |
| POST `/api/open` | `{path,profileId?}` → inspección descrita debajo |
| GET `/api/catalog?sessionId=ID&type=unique&search=texto&offset=0&limit=100` | `{entries:[{id,name,code,type,levelRequired,supported}],total}`. Tipos unique,set,runeword,base,skills. Skills usa su DTO específico. |
| POST `/api/preview` | cuerpo descrito debajo → `{planId,canSave,validation:{errors:[],warnings:[]},diff:[{section,label,before,after}],summary:<inspección>}` |
| POST `/api/export` | `{sessionId,planId,destinationPath?}` → `{outputPath,hash}`. Sin ruta, crea copia en carpeta exports de datos de AKARA; nunca sobrescribe. |
| POST `/api/commit` | `{sessionId,planId,expectedHash}` → `{backupPath,outputPath,hash,summary}`. Bloquea si D2R está abierto o cambió el archivo desde la inspección. |
| GET `/api/backups?sessionId=ID` | `{entries:[{id,path,createdAt,hash}]}` |
| POST `/api/restore` | `{sessionId,backupId,expectedHash}` → igual que commit. Preserva backup de la versión desplazada. |
| POST `/api/shutdown` | `{}` → `200 {closed:true}` y cierra el host después de enviar la respuesta. Requiere token. |

Inspección:

```json
{"sessionId":"...","sourceHash":"...","sourcePath":"...","profileId":"...","fileName":"Example.d2s","character":{"name":"Example","classId":1,"className":"Sorceress","level":85,"experience":1050299747,"attributes":{"strength":124,"dexterity":25,"vitality":331,"energy":35,"gold":200000,"stashGold":2000000,"unspentStats":0,"unspentSkills":0},"vitals":{"life":926.5,"maxLife":926.5,"mana":203,"maxMana":203}},"skills":[{"id":64,"name":"Frozen Orb","points":25,"maxPoints":25,"requiredLevel":30,"prerequisites":[44],"tabId":3,"row":6,"column":2}],"items":[{"id":"i0","code":"oba","name":"The Oculus","quality":"unique","type":"unique","slot":"weapon","page":"equipped","x":0,"y":0,"width":1,"height":3,"stats":[],"sockets":[]}],"gameRunning":false,"validation":{"canEdit":true,"checksum":true,"roundtrip":true}}
```

Preview:

```json
{"sessionId":"...","name":"Example","level":85,"attributes":{"strength":124,"dexterity":25,"vitality":331,"energy":35,"gold":200000,"stashGold":2000000},"skills":[{"id":64,"points":25}],"gear":[{"type":"unique","name":"The Oculus","slot":"weapon","level":99,"sockets":[]}],"moveToStashItemIds":["i0"],"questRewards":{"skills":true,"stats":true,"anya":true},"unlockHell":true,"unlockWaypoints":true,"fillTomes":true,"fillBelt":true}
```

Campos opcionales ausentes preservan estado. `skills` presente reemplaza todos los hard points de la clase; enviar todos los puntos deseados. `attributes` permite parche parcial. `gear` añade objetos y, si lleva slot, mueve el equipo anterior al alijo. Tipos unique,set,runeword,magic,base. `runeword` necesita `baseCode`; magic necesita baseCode,prefix y suffix opcional. `page:"inventory"` activa charms; resto va al alijo. Engarces se especifican por código nativo, por ejemplo `sockets:["r22"]`. `moveToStashItemIds` nunca destruye objetos.

Preset `"preset":"orbe-tal-oculus"` aplica la construcción nativa validada de Example a una Hechicera del perfil Reimagined: nivel85, atributos/habilidades, recompensas, equipo y charms. Los cambios explícitos posteriores del mismo cuerpo prevalecen. Otros personajes reciben error de compatibilidad sin escritura.

`canSave:false` conserva errores visibles y bloquea export/commit; gameRunning bloquea sólo commit/restore, no preview/export. Cada preview válido sustituye el plan anterior de esa sesión; la UI debe usar el último planId. Un commit renueva sourceHash y devuelve inspección actualizada. La selección de una partida no escribe nada; los respaldos empiezan sólo al guardar/restaurar.

Ampliaciones implementadas: inspección incluye `progression:{statsPerLevel,skillsPerLevel,maxLevel,baseAttributes}`, `inventoryGrid`, `stashGrid`, `hotkeys`, `leftSkill`, `rightSkill` y `mercenary:{exists,dead,typeId,nameIndex,experience,items}`. Objetos incluyen `baseName`, `runewordName`, cantidades, durabilidad, bonus parciales, stats de palabra rúnica y sockets recursivos. Stats incluyen `label`, `displayValue` humano y `hidden`; no mostrar los pasivos internos con hidden=true.

Preview admite `mercenary:{hirelingId,level,resurrect}`, `gear[].mercSlot:"weapon"|"armor"|"helm"`, `gear[].ethereal:true`, `hotkeys:{"0":64}`, `leftSkill` y `rightSkill`. Un cambio de mercenario usa datos y crecimiento de hireling.txt; el equipo reemplazado se conserva en el alijo personal. Estos campos son opcionales y las ediciones generales preservan el mercenario y su equipo.

Exportar conserva el nombre del personaje como nombre de archivo, dentro de una subcarpeta única. Para renombrar un personaje se exporta una copia con el nombre nuevo; commit rechaza un nombre interno que no coincida con el archivo seleccionado. Enviar el nivel actual no reinicia XP: sólo un cambio efectivo de nivel establece el umbral de XP de la tabla.
