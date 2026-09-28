# Armas y duende en ForestScene

## Historia

`StoryDirector` (en `Assets/01_Scripts/Forest/Story`) se crea solo al cargar ForestScene y cuenta la historia por capítulos. Cada capítulo cambia la dinámica del juego:

| Capítulo | Historia | Objetivo | Qué cambia en el juego |
|---|---|---|---|
| Prólogo · Tres noches | Mateo, tu hermano, entró en Shadowwood hace tres noches. Su linterna estaba junto a la mesa del cazador Elías Varga. | Toma un arma de la mesa. | Solo hay duendes dormidos; zombis y hombre lobo desactivados. |
| I · Los que duermen | En la cabaña del cazador hay luz. | Llega a la cabaña sin despertar a los duendes. | Sigilo: si despiertas a uno, aviso «Te han oído». |
| II · El diario del cazador | Los duendes robaron las páginas del diario que explican la maldición. | Elimina 3 duendes. | El marcador señala al duende más cercano; la noche se oscurece. |
| III · Los que no descansan | «Cuando la sangre del bosque se derrama, los muertos despiertan.» | Elimina 5 zombis. | Los zombis salen sin parar (uno cada 6 s, hasta 3 a la vez); más oscuridad y niebla. |
| IV · Luna de sangre | El cazador es el hombre lobo; Mateo está en el sótano. | Acaba con el Hombre Lobo. | Aparece el jefe; luna de sangre: niebla y luz rojas. Los zombis dejan de salir. |
| Amanecer | Los muertos vuelven a la tierra; Mateo está vivo. | — | Amanece, los zombis que quedan caen, los duendes no reaparecen; «Fin» y vuelta al menú. |

Se muestra con una tarjeta de capítulo y una campana, narración con efecto de máquina de escribir en un panel que sigue suavemente la mirada, el objetivo arriba y un rombo rojo con la distancia sobre el lugar o el enemigo objetivo. Se ven siempre por encima de la escena. Al morir, la primera vez, se escucha «Todavía no. Mateo te necesita». Los textos están en `StoryDirector.Story()`.

## Sonidos

Los archivos están en `Assets/05_Sounds`. Los sonidos generales se asignan en `Assets/05_Sounds/Resources/GameAudio.asset`; los de cada enemigo, en la sección **Sonidos** de su `GoblinSettings` (`Assets/SO_`).

| Sonido | Dónde suena |
|---|---|
| `disparo` | Cada disparo del revólver, en 3D desde el cañón (tono ligeramente variable). |
| `duende-durmiendo` | Ronquido en bucle de cada duende dormido, en 3D; cada uno empieza en un punto distinto para que no ronquen al unísono. |
| `goblin-voice-excited…` | Grito del duende al despertar y, de vez en cuando, mientras te persigue. |
| `Risa_diabólica…` | Risa del duende al atacarte o golpearte, y desde detrás de ti cuando mueres. |
| `goblin-death` | Muerte del duende. |
| `zombie` | Gruñidos del zombi al despertar, mientras persigue y al atacar, y su muerte (más grave). |
| `haunting-wolf-song` | Aullido del Hombre Lobo al aparecer y al morir (más grave). |
| `cansado` | Respiración agotada del jugador con menos del 35 % de vida; más fuerte cuanto menos vida queda. |
| `Voces_susurrantes…` | Susurros alrededor del jugador cada 45 a 100 s, y detrás de ti cuando «la tierra se abre» (capítulo III). |
| `scary_music` | Música del bosque, en bucle y en streaming. |
| `universfield-paranormal…` | Música del menú y del combate final (Luna de sangre). Al amanecer la música se apaga. |

Cada enemigo tiene un tono de voz ligeramente distinto. Los efectos se precargan y son mono para sonar bien en 3D; las músicas se reproducen en streaming. El volumen general de música y efectos se ajusta en `GameAudio`.

## Menú de inicio

`Assets/00_Scenes/MainMenu.unity` es la primera escena en Build Settings, seguida de `ForestScene`. Se crea sola la primera vez que Unity compila; para regenerarla usa el menú **Shadowwood > Crear o actualizar menu de inicio**. Contiene el rig VR, un suelo invisible y `ShadowwoodMenu`, que construye al empezar un panel de 3,4 m a 2,6 m delante del jugador:

- Fondo: `Assets/05_UI/Textures/ShadowwoodMenu.png` (el arte con el título SHADOWWOOD).
- Tipografía: Cinzel Decorative (botones y títulos) y Cinzel (textos), romanas en mayúsculas como el título; licencia libre OFL en `Assets/05_UI/Resources/Fonts/OFL.txt`.
- Terror: aparece desde negro, el farol del arte parpadea, la niebla se desplaza y cada 8 a 15 s el farol se apaga a tirones y la vista se tiñe de rojo con un latido. De fondo suena un zumbido grave con viento, generado por código. Al señalar un botón, el texto se vuelve rojo sangre, crece, tiembla y se subraya.
- En el editor, Play empieza siempre desde el menú aunque tengas abierta ForestScene (`EditorSceneManager.playModeStartScene`). Para probar el bosque directamente, desmarca **Shadowwood > Empezar Play siempre desde el menu**. En el simulador, **Enter** equivale a JUGAR y **Esc** cierra los controles, porque el simulador desactiva el ratón sobre la UI.
- Botones: **JUGAR** (funde a negro y carga ForestScene), **CONTROLES** (panel con los controles) y **SALIR**. Se usan con el rayo de los mandos o, en el simulador, con el ratón.

## Controles y equipo

Las tres armas son objetos guardados en `ForestScene` sobre la mesa `MESA`: el arco atras, el hacha y el revolver delante. Acercate a la mesa y agarra un arma con **Grip** (el agarre lateral del control). Al soltarla cae sobre la mesa o el suelo; ya no reaparece junto a la camara.

- **Arco:** hecho según el proyecto de referencia *Bow and Arrow* de Fist Full of Shrimp (modelos `PlankBow` y `Arrow`, material `String`, licencia MIT en `Assets/02_Prefabs/Weapons/BowAndArrow`). Agarra el arco: queda en la mano izquierda y una flecha espera en el centro del arco, sobre la cuerda. Con manos seguidas, el arco se coloca en la palma de la mano izquierda (el mango dentro del puño, el lado del pulgar hacia arriba); con mandos usa el agarre normal. La cuerda se agarra como cualquier objeto, de cerca o con el rayo: con la mano derecha tómala (Grip mantenido) y tira hacia atrás desde donde la agarraste: la cuerda y la flecha se mueven con la mano entre `Resting Nock` (reposo) y `Pull End` (tensión máxima). Suelta Grip para disparar esa flecha con la estela de estrella fugaz (1,6 veces más ancha que la de la bala) y una luz cálida que viaja en la punta e ilumina el bosque oscuro a su paso; al impactar, estela y luz se apagan en el sitio (`trailWidth` y `glowLight` en el prefab `Arrow`); la fuerza depende de cuánto tensaste (mínimo 20 %). Tras el disparo aparece otra flecha en el arco al terminar la espera. Soltar el arco o alejar demasiado la mano cancela el disparo.
- **Hacha:** agarra el mango y golpea con la cabeza. Necesita velocidad de movimiento; tocar o mantener el hacha sobre el enemigo no produce daño continuo. No se desgasta.
- **Revólver:** agarra la empuñadura y pulsa el gatillo para disparar. La bala (`Bullet`, modelo `TripoModels/bala`) se instancia en el punto `Muzzle` en la punta del cañon y vuela a 40 m/s para que se vea. La bala es 2,5 veces más grande que antes (escala 0,1 del modelo). Deja detrás dos estelas (`shootingStarTrail` en `WeaponProjectile`): una estrella fugaz ancha y brillante (9 cm, blanca a naranja, 0,6 s) y una línea trazadora fina que sigue a la bala desde el cañón y dura 2,5 s, para ver hacia dónde fue el disparo. Al impactar, ambas se quedan en el sitio y se desvanecen. Un disparo por pulsación, sin recarga ni límite de munición. En el editor tambien dispara con **F** mientras lo sostienes.

El agarre de armas es fijo: una pulsacion de Grip toma el arma y queda en la mano al soltar el boton; la siguiente pulsacion de Grip la suelta. Asi se puede disparar sin mantener Grip. El tamaño del arma no cambia al sostenerla (`trackScale` desactivado; antes el joystick la escalaba).

Las armas se agarran siempre hacia la mano, aunque se tomen con el rayo a distancia (`farAttachMode = Near`, forzado en `WeaponGrip`). Cada arma tiene su propio agarre (`WeaponHandPoses`): con mandos, `VRControllerHand` coloca la mano sobre la empuñadura del arma y cierra los dedos con la pose de esa arma; con seguimiento de manos, `HandGripPose` aplica la misma pose a los dedos. Revólver: agarre de pistola con el índice en el gatillo, que se dobla al apretarlo. Hacha: puño cerrado en el mango. Arco: siempre queda en la **mano izquierda**; si lo agarras con la derecha, pasa solo a la izquierda al instante (siempre que la izquierda esté libre); la **mano derecha** no lleva ninguna flecha: al tomar la cuerda la mano se engancha en ella y, cuando la cuerda está tensada (8 cm o más), la flecha aparece colocada en la cuerda, en el centro del arco; al soltar la cuerda se lanza. Cada `WeaponGrip` tiene `gripStyle`, `handPositionOffset` y `handYawOffset` para afinar el agarre en el inspector (la mano izquierda usa el reflejo). Sin arma, las manos están siempre abiertas con todos los dedos extendidos (también al inicio y en el simulador); los botones Grip y gatillo ya no las cierran ni levantan el índice. Las armas conservan en la mano el mismo tamaño que tienen sobre la mesa (`heldScale` en `WeaponGrip`; 0 = tamaño de la mesa, 0,8 = 80 %, etc.). Las manos de los mandos ya no desaparecen por pérdidas breves de seguimiento (`trackingLostGrace`, 2 s) ni mientras sostienen algo. En el XR Interaction Simulator, cuyas manos capturadas señalan con el índice, la mano libre se muestra relajada; con seguimiento de manos real se respetan tus dedos.

| Arma | Daño | Nota |
|---|---:|---|
| Revólver | 150 | Un impacto mata al duende con 100 de vida. |
| Arco | Hasta 100 | Daño y velocidad aumentan con la tensión de la cuerda (máxima en `Pull End`). |
| Hacha | 40 | El filo hace daño tras recorrer una distancia al golpear; mantenerlo apoyado no repite el daño. Hay que separarlo y dar otro hachazo para volver a golpear, respetando la breve espera entre impactos. |

Los ajustes están en `Assets/SO_/Weapons`. Los cinco prefabs están en `Assets/02_Prefabs/Weapons`: VRBow, VRAxe, VRRevolver, Arrow y Bullet. Los modelos originales permanecen en TripoModels. La cuerda es un LineRenderer de Unity con tres puntos y un agarre independiente. El cuerpo del arco permanece rígido porque el modelo no tiene huesos para doblar las palas. Se revisó el ejemplo `VR-Archery-in-Unity-2022-main` que dejaste en la raiz del proyecto; el arco del juego conserva su implementacion compatible con el XR Interaction Toolkit actual y toma la distancia de estiramiento al soltar la cuerda.

Los proyectiles comprueban todo el trayecto entre fotogramas para evitar atravesar enemigos o paredes finas a alta velocidad. No dañan al propietario. Las flechas quedan clavadas temporalmente y los proyectiles se eliminan automáticamente.

En el XR Interaction Simulator, selecciona el mando con Tab y usa G para Grip y T para el gatillo. Haz clic en Game para que reciba el teclado. Con el visor, usa los botones fisicos equivalentes.

## Enemigos: Duende, Zombie y Hombre Lobo

| Enemigo | Rol | Vida | Daño | Barra |
|---|---|---:|---|---|
| Duende | El más débil | 100 | 10 / 30 | Verde |
| Zombie | Intermedio | 200 | 20 (ataque) / 35 (mordida al cuello) | Verde |
| Hombre Lobo | Jefe final | 1000 | 35 | Roja y 1,4 veces más grande |

Los tres usan `GoblinActor` con su propio `GoblinSettings` (`Assets/SO_/ForestGoblinSettings`, `ForestZombieSettings`, `ForestWerewolfSettings`). Sobre la cabeza de cada uno aparece su nombre y debajo su barra de vida (`EnemyNameplate`), siempre mirando al jugador; se oculta al morir o a más de 40 m. El nombre, el color y el tamaño de la barra están en `displayName`, `healthBarColor` y `nameplateScale` de cada configuración.

Aparición (`respawnSeconds` y `maxAlive` en cada configuración): ForestScene guarda un `Zombie Spawner` conectado a `Spawn Zombie` y un `Hombre Lobo Spawner` conectado a `Spawn Wolf`, ambos cerca de `SPAWN_DUENDE`. Cada uno instancia exclusivamente en su empty. El zombie añade uno cada 5 minutos hasta 3 vivos; el lobo mantiene uno y reaparece 5 minutos después de morir. El `Goblin Spawner` y sus 10 duendes repartidos por el mapa siguen igual.

Estados según sus animaciones:
- **Zombie:** en reposo de pie (`zombie idle`); al detectar al jugador en su radio grita (`zombie scream`) y lo persigue caminando (`zombie walk`, 1,1 m/s) o corriendo si está a más de 5 m (`zombie run`, 2,6 m/s). Al quedar en 100 de vida o menos, pasa a `zombie crawl` o `running crawl` y se mueve más despacio. Alterna `zombie attack` y `zombie neck bite`. Muere con `zombie death`.
- **Hombre Lobo:** en reposo `WolfIdle`; persigue con `wolfwalk` (1,8 m/s) o `WolfRun` (5 m/s), ataca con `WolfAttack`, si lo golpean por la espalda se gira con `wolf Right Turn 90` y muere con `wolfdied`. Nunca hay más de un lobo.

Prefabs: `Assets/02_Prefabs/Enemy/ForestZombie.prefab` y `ForestWerewolf.prefab`. Los spawners y sus dos puntos están guardados en ForestScene. Puedes mover los empties; no se generan puntos alternativos. Al seleccionarlos, sus gizmos muestran el radio de detección y el cono de visión. El menú **Forest VR > Enemigos > Crear zombie y hombre lobo y colocarlos en la escena** reutiliza esos puntos y no crea otros si faltan.

## Recorrido, zonas de spawn, páginas y puerta

- **Recorrido:** MESA 1 (inicio del jugador) tiene el hacha; MESA 2 el arco; MESA 3 el revólver. La historia (`StoryDirector`) sigue ese orden: hacha y páginas de los duendes → arco y zombis → revólver y Hombre Lobo → entrar en la cabaña.
- **Zonas de spawn (`SpawnZone`):** en cada punto de spawn (SPAWN_DUENDE, Spawn Zombie, Spawn Wolf) se configuran `radius` (radio del círculo) y `count` (cuántos enemigos). Aparecen dentro del círculo, separados (`spacing`), solo sobre suelo verde, nunca en el agua ni fuera del mapa, ni encima del jugador. Un enemigo muerto se repone tras el tiempo de reaparición. Se ven en el editor como círculos celestes con la etiqueta "Zona: N x Enemigo". Para más zonas del mismo enemigo, añade otro empty con `SpawnZone` a la lista `Spawn Points` de su spawner. Los duendes ya no se reparten por todo el mapa (`mapPopulation` 0): solo salen de sus zonas.
- **Agua y bordes (`PlayArea`, `PlayAreaGuard`):** el jugador no puede entrar en el lago ni salir de los bordes de `FloorWithLake`; se le devuelve al último sitio válido y aparece "No deberías pasar por ahí.". Los enemigos también evitan el agua al perseguir.
- **Páginas (`PagePickup`):** cada duende que muere suelta una página mientras falten (3). Flota sobre el suelo sin caer, brilla y se recoge al pasar por encima; el objetivo cuenta (n/3). El modelo final se asigna en `ForestGoblinSettings` > Page Prefab (vacío = hoja provisional). Sonido opcional en `GameAudio` > Page Pickup.
- **Hacha:** el punto de daño (`Blade Hit Point` del prefab VRAxe) está en el centro del filo; antes estaba fuera de la hoja, junto al mango.
- **Puerta de la cabaña (`SceneDoor`):** el empty `Puerta Cabaña` está en el porche (lado este de Casita Final). Cerrada hasta el final de la historia; al entrar carga `InteriorHouse`. Para las builds, añade InteriorHouse en File > Build Profiles (en el editor funciona igual).
- Menú **Forest VR > Recorrido > Colocar armas, zonas de spawn y puerta de la cabaña**: vuelve a hacer esta preparación en la escena abierta sin duplicar nada.

## Preparación automática de ForestScene

Al cargar `ForestScene`, `ForestSceneSetup` (datos en `Assets/Resources/ForestEnemyRoster.asset`) prepara la escena sin tocarla a mano:

- **Zombie y Hombre Lobo** reciben su spawner junto a `SPAWN_DUENDE`, en el lado opuesto al inicio del jugador (zombie a unos 6 m del punto del duende, lobo a unos 10 m), sobre suelo libre. Si la escena ya tiene un spawner con esa configuración (hecho a mano o con el menú), se respeta ese. El anclaje y la distancia de cada uno se cambian en el roster (`anchorName`, `offset`).
- **Árboles:** solo bloquea el tronco (una cápsula por árbol, medida de cada modelo); el collider que cubría toda la copa se desactiva. Enemigos, balas y flechas chocan con los troncos.
- **Jugador:** `VRBodyBlocker` lo empuja fuera de los troncos, porque caminar con el visor (o WASD del simulador) mueve la cámara sin física.

Las flechas que se clavan en objetos estirados (el suelo `FloorWithLake` está escalado 8,9 x 1 x 8,3) ya no se emparentan con ellos, así que no se deforman ni parecen atravesar el suelo; quedan hundidas unos 6 cm. En enemigos siguen clavadas y se mueven con ellos.

La linterna se mantiene como estaba (cono de 110°, intensidad 8, alcance 40 m, niebla 0,025). El perfil URP "Performance" del proyecto ilumina cada objeto (el suelo entero es uno) con una única luz extra, así que la luz de las flechas tiene prioridad baja y nunca le quita la linterna al suelo. La barra de vida del jugador está en la esquina inferior izquierda.

## Vida, muerte y recuperacion

La vida del jugador es una barra verde en la esquina inferior izquierda de la vista (`HudHealthBar`, hija de la camara y dibujada encima de la escena) que se acorta al recibir dano. Cuando llega a cero, el duende deja de atacar y las armas dejan de hacer dano: no es un bloqueo de la IA. Ahora aparece el aviso **Sin vida**. Pulsa **A o X** en los controles para recuperar la vida y seguir la prueba; en el editor/simulador tambien funciona **F8**. Esta accion solo funciona estando muerto y no reinicia la escena ni revive al duende.

## Jugador utilizado

La escena contiene dos rigs: `Forest VR Player`, desactivado, y `XR Origin Hands (XR Rig)`, activo. Se han conservado sus estados. El spawner apunta al rig **activo**, con vida y las tres armas conectadas. `Forest VR Player` también tiene preparado el equipo si decides usarlo posteriormente. Si cambias de rig, actualiza la referencia Player del Goblin Spawner y mantén solamente uno activo.

## Spawn y estados del duende

`SPAWN_DUENDE` está asignado explícitamente al spawner en la posición que guardaste `(3.23, 2, -9.28)`. No necesita moverse ni cambiar de padre. Se mantiene el contenedor opcional `Goblin Spawn Points` para futuros puntos.

En los puntos del spawner hay **un solo duende vivo** y una espera de **20 minutos desde su muerte**. Tras la espera solo aparece si el jugador vivo y activo está a 60 metros o menos de un punto (antes 25). Además hay **10 duendes repartidos por todo el mapa** (`mapPopulation`), colocados al empezar en sitios libres del suelo: no en el agua, sobre árboles, rocas o la mesa, ni en pendientes de más de 25°. Quedan separados al menos 12 m entre sí y a 20 m o más del jugador. Cada uno reaparece en su sitio 20 minutos después de morir, nunca a menos de 20 m del jugador. El reloj es tiempo de juego y no persiste al cerrar la escena.

1. Aparece en Sleep o Relaxing. Estos dos archivos contienen poses de un fotograma, mantenidas en bucle.
2. **Dormido o descansando no se levanta ni te persigue** a menos que estés muy cerca: 3 m (antes 8), con línea de visión, o que lo golpees. Entonces reproduce Getting Up.
3. Ya levantado, ve en un sector frontal de 110° hasta 15 m. Persigue al jugador visible; al perderlo conserva su última posición durante 4 segundos, no conoce su posición nueva a través de paredes.
4. Ataca únicamente a 1,5 m o menos. Alterna el ataque rápido y el lento (nunca el mismo tres veces seguidas), con ligeras variaciones de velocidad. Las animaciones se mezclan con transiciones suaves en vez de cortes. Durante el amago sigue girando hacia ti y da un paso para alcanzarte; el daño se aplica en el momento del golpe, medido automáticamente en cada animación (cuando la mano llega más adelante). Volver a comprobar distancia, orientación y obstáculos en el instante del golpe permite esquivarlo.
5. Si recibe daño por detrás mientras camina o está en idle, reproduce IfAttackBack y gira hacia el origen del golpe.
6. Al morir reproduce Death, deja de atacar y desactiva su colisión. El cadáver se elimina después de 8 segundos.

Selecciona `SPAWN_DUENDE` con **Gizmos** activado para ver el círculo de despertar y el sector frontal. Selecciona el duende durante Play para ver también el alcance de ataque. Los valores y clips se editan en `Assets/SO_/ForestGoblinSettings.asset`.

La persecución utiliza CharacterController y colisiones; no incluye búsqueda de caminos alrededor de árboles o paredes. Para esa navegación hará falta preparar el NavMesh del mapa.

## Manchas de sangre al recibir daño

Cada golpe de un enemigo mancha de rojo el borde de la vista (`BloodScreen`, hija de la cámara, creada por `PlayerCombatStatus`). El centro queda siempre despejado: solo se pierde algo de visión periférica. Cada golpe añade intensidad y una o dos salpicaduras del lado del que vino el golpe, con un breve pulso. Tras 2,5 s sin recibir daño, las manchas se desvanecen en 2,5 s. Recuperar la vida (A / X o F8) las borra. Las texturas se generan por código y la barra de vida se dibuja por encima.

## Árboles y obstáculos

Los modelos de la naturaleza importan un collider de malla de todo el modelo; en los árboles eso incluía la copa. Al empezar, `ForestColliders` mide el tronco de cada árbol con rayos contra su propia malla y le pone una cápsula sólida (hasta 5 m de alto), desactivando el collider de la copa: el jugador y las armas ya no chocan con las hojas y los disparos no se quedan en el follaje. Flores, hierba, setas y piedrecitas dejan de bloquear. Rocas, puente y casa conservan su collider. Los duendes esquivan árboles, rocas y paredes: si el camino directo está bloqueado prueban direcciones a ±35°, ±70° y ±105° y rodean el obstáculo por el mismo lado.

## Nada cae infinitamente

El suelo `FloorWithLake` es un MeshCollider (una superficie sin grosor) y la mesa tiene un collider de 8 cm; con detección discreta, un arma que caía podía atravesarlos en un paso de física. Ahora:

- Las armas usan detección continua (`ContinuousSpeculative`) y no atraviesan la mesa ni el suelo.
- Al empezar, `GroundSafety` crea bajo todo el terreno un collider de seguridad de 2 m de grosor (`Ground Safety Collider`), 30 m más grande que el suelo.
- Si un arma soltada acaba bajo el suelo o fuera del mapa, vuelve a su sitio inicial en la mesa.
- Un enemigo que queda bajo el suelo vuelve a la superficie, y al morir su cuerpo se apoya sobre lo que tenga debajo en vez de flotar o hundirse.

El suelo se registra desde `VRGroundProbe` (campo Environment Root); si cambias de suelo, asígnalo ahí.

## Suelo irregular

`Assets/SO_/ForestLocomotionSettings.asset` configura pendientes de hasta 60°, escalones de 40 cm, tolerancia de colisión de 4 cm y movimiento mínimo de cero. La recuperación del jugador deja de devolverlo a una posición antigua por bajar una pendiente o quedar ligeramente bajo el punto de suelo muestreado; sigue recuperándolo ante penetraciones importantes o al salir del terreno jugable. No se han cambiado las acciones de Oculus ni eliminado las colisiones del mapa.

La luz exclusiva del editor permanece funcionando como antes. La linterna durante el juego ahora abre 110 grados y alcanza 40 metros; su intensidad baja de 150 a 8 para reducir la sobreexposicion cercana.

## Verificación

Los scripts se compilan y la escena se abre en Unity 6000.5.6f1 en una copia temporal. Se verifican las referencias al jugador activo, al spawn y a las armas, y la compatibilidad de los clips con el esqueleto.

Las pruebas reproducibles están en `Assets/01_Scripts/Forest/Editor`. Las entradas `ForestCombatValidation.RunBatch` y `ForestGameplayValidation.RunBatch` son exclusivamente para un proyecto desechable: entran en Play y cierran ese editor al terminar. Cubren el combate, la cuerda, el daño, obstáculos, visión, estados, pendientes y reaparición.

Se muestran los avisos de vida, F8 recupera al jugador muerto y el duende reanuda sus ataques. La linterna ampliada fue revisada visualmente. Las armas ahora estan colocadas en la escena sobre MESA.

Queda pendiente la comprobación ergonómica con tu visor y controles físicos: altura de los soportes, comodidad del agarre y ubicación de los anclajes se pueden ajustar en los componentes/prefabs sin regenerar los modelos.
