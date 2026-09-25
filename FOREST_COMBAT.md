# Armas y duende en ForestScene

## Controles y equipo

Las tres armas son objetos guardados en `ForestScene` sobre la mesa `MESA`: el arco atras, el hacha y el revolver delante. Acercate a la mesa y agarra un arma con **Grip** (el agarre lateral del control). Al soltarla cae sobre la mesa o el suelo; ya no reaparece junto a la camara.

- **Arco:** agarra el cuerpo con una mano. Acerca la otra al centro de la cuerda, mantén Grip y tira hacia atrás. Suelta Grip de esa segunda mano para disparar. La flecha se coloca automáticamente; no hay límite de flechas. Soltar el cuerpo o apartar excesivamente la mano cancela el disparo.
- **Hacha:** agarra el mango y golpea con la cabeza. Necesita velocidad de movimiento; tocar o mantener el hacha sobre el enemigo no produce daño continuo. No se desgasta.
- **Revólver:** agarra la empuñadura y pulsa el gatillo para disparar. La bala (`Bullet`, modelo `TripoModels/bala`) se instancia en el punto `Muzzle` en la punta del cañon y vuela a 40 m/s para que se vea. Deja detrás una estela luminosa tipo estrella fugaz (`shootingStarTrail` en `WeaponProjectile`) que se desvanece en 0,3 s en el punto donde terminó el disparo. Un disparo por pulsación, sin recarga ni límite de munición. En el editor tambien dispara con **F** mientras lo sostienes.

El agarre de armas es fijo: una pulsacion de Grip toma el arma y queda en la mano al soltar el boton; la siguiente pulsacion de Grip la suelta. Asi se puede disparar sin mantener Grip. El tamaño del arma no cambia al sostenerla (`trackScale` desactivado; antes el joystick la escalaba).

Las armas se agarran siempre hacia la mano, aunque se tomen con el rayo a distancia (`farAttachMode = Near`, forzado en `WeaponGrip`). Cada arma tiene su propio agarre (`WeaponHandPoses`): con mandos, `VRControllerHand` coloca la mano sobre la empuñadura del arma y cierra los dedos con la pose de esa arma; con seguimiento de manos, `HandGripPose` aplica la misma pose a los dedos. Revólver: agarre de pistola con el índice en el gatillo, que se dobla al apretarlo. Hacha: puño cerrado en el mango. Arco: solo se puede agarrar con la **mano izquierda**; mientras lo sostienes, la **mano derecha** lleva una flecha pinzada entre pulgar e índice, y al tomar la cuerda la mano se engancha en ella y la flecha pasa a la cuerda. Tras disparar, la siguiente flecha aparece en la mano al terminar la espera. Cada `WeaponGrip` tiene `gripStyle`, `handPositionOffset` y `handYawOffset` para afinar el agarre en el inspector (la mano izquierda usa el reflejo). Las manos de los mandos ya no desaparecen por pérdidas breves de seguimiento (`trackingLostGrace`, 2 s) ni mientras sostienen algo. En el XR Interaction Simulator, cuyas manos capturadas señalan con el índice, la mano libre se muestra relajada; con seguimiento de manos real se respetan tus dedos.

| Arma | Daño | Nota |
|---|---:|---|
| Revólver | 100 | Un impacto mata al duende con 100 de vida. |
| Arco | Hasta 55 | Daño y velocidad aumentan con el estiramiento, máximo 55 cm. |
| Hacha | 12,5 a 50 | Requiere un golpe de al menos 1,2 m/s; 25 a 3 m/s y el doble a 6 m/s. Breve espera entre impactos. |

Los ajustes están en `Assets/SO_/Weapons`. Los cinco prefabs están en `Assets/02_Prefabs/Weapons`: VRBow, VRAxe, VRRevolver, Arrow y Bullet. Los modelos originales permanecen en TripoModels. La cuerda es un LineRenderer de Unity con tres puntos y un agarre independiente. El cuerpo del arco permanece rígido porque el modelo no tiene huesos para doblar las palas. Se revisó el ejemplo `VR-Archery-in-Unity-2022-main` que dejaste en la raiz del proyecto; el arco del juego conserva su implementacion compatible con el XR Interaction Toolkit actual y toma la distancia de estiramiento al soltar la cuerda.

Los proyectiles comprueban todo el trayecto entre fotogramas para evitar atravesar enemigos o paredes finas a alta velocidad. No dañan al propietario. Las flechas quedan clavadas temporalmente y los proyectiles se eliminan automáticamente.

En el XR Interaction Simulator, selecciona el mando con Tab y usa G para Grip y T para el gatillo. Haz clic en Game para que reciba el teclado. Con el visor, usa los botones fisicos equivalentes.

## Vida, muerte y recuperacion

La vida del jugador es una barra verde en la esquina inferior izquierda de la vista (`HudHealthBar`, hija de la camara y dibujada encima de la escena) que se acorta al recibir dano. Cuando llega a cero, el duende deja de atacar y las armas dejan de hacer dano: no es un bloqueo de la IA. Ahora aparece el aviso **Sin vida**. Pulsa **A o X** en los controles para recuperar la vida y seguir la prueba; en el editor/simulador tambien funciona **F8**. Esta accion solo funciona estando muerto y no reinicia la escena ni revive al duende.

## Jugador utilizado

La escena contiene dos rigs: `Forest VR Player`, desactivado, y `XR Origin Hands (XR Rig)`, activo. Se han conservado sus estados. El spawner apunta al rig **activo**, con vida y las tres armas conectadas. `Forest VR Player` también tiene preparado el equipo si decides usarlo posteriormente. Si cambias de rig, actualiza la referencia Player del Goblin Spawner y mantén solamente uno activo.

## Spawn y estados del duende

`SPAWN_DUENDE` está asignado explícitamente al spawner en la posición que guardaste `(3.23, 2, -9.28)`. No necesita moverse ni cambiar de padre. Se mantiene el contenedor opcional `Goblin Spawn Points` para futuros puntos.

Se mantiene **un solo duende vivo** y la espera de **20 minutos desde su muerte**. Tras la espera solo aparece si el jugador vivo y activo está a 25 metros o menos de un punto. El reloj es tiempo de juego y no persiste al cerrar la escena.

1. Aparece en Sleep o Relaxing. Estos dos archivos contienen poses de un fotograma, mantenidas en bucle.
2. Detecta proximidad en 360° a 8 m, con línea de visión, y reproduce Getting Up.
3. Ya levantado, ve en un sector frontal de 110° hasta 15 m. Persigue al jugador visible; al perderlo conserva su última posición durante 4 segundos, no conoce su posición nueva a través de paredes.
4. Ataca únicamente a 1,5 m o menos. Alterna el ataque rápido (20 de daño) y el lento (30), con tiempos de impacto distintos. Volver a comprobar distancia, orientación y obstáculos en el instante del golpe permite esquivarlo.
5. Si recibe daño por detrás mientras camina o está en idle, reproduce IfAttackBack y gira hacia el origen del golpe.
6. Al morir reproduce Death, deja de atacar y desactiva su colisión. El cadáver se elimina después de 8 segundos.

Selecciona `SPAWN_DUENDE` con **Gizmos** activado para ver el círculo de despertar y el sector frontal. Selecciona el duende durante Play para ver también el alcance de ataque. Los valores y clips se editan en `Assets/SO_/ForestGoblinSettings.asset`.

La persecución utiliza CharacterController y colisiones; no incluye búsqueda de caminos alrededor de árboles o paredes. Para esa navegación hará falta preparar el NavMesh del mapa.

## Suelo irregular

`Assets/SO_/ForestLocomotionSettings.asset` configura pendientes de hasta 60°, escalones de 40 cm, tolerancia de colisión de 4 cm y movimiento mínimo de cero. La recuperación del jugador deja de devolverlo a una posición antigua por bajar una pendiente o quedar ligeramente bajo el punto de suelo muestreado; sigue recuperándolo ante penetraciones importantes o al salir del terreno jugable. No se han cambiado las acciones de Oculus ni eliminado las colisiones del mapa.

La luz exclusiva del editor permanece funcionando como antes. La linterna durante el juego ahora abre 110 grados y alcanza 40 metros; su intensidad baja de 150 a 8 para reducir la sobreexposicion cercana.

## Verificación

Los scripts se compilan y la escena se abre en Unity 6000.5.6f1 en una copia temporal. Se verifican las referencias al jugador activo, al spawn y a las armas, y la compatibilidad de los clips con el esqueleto.

Las pruebas reproducibles están en `Assets/01_Scripts/Forest/Editor`. Las entradas `ForestCombatValidation.RunBatch` y `ForestGameplayValidation.RunBatch` son exclusivamente para un proyecto desechable: entran en Play y cierran ese editor al terminar. Cubren el combate, la cuerda, el daño, obstáculos, visión, estados, pendientes y reaparición.

Se muestran los avisos de vida, F8 recupera al jugador muerto y el duende reanuda sus ataques. La linterna ampliada fue revisada visualmente. Las armas ahora estan colocadas en la escena sobre MESA.

Queda pendiente la comprobación ergonómica con tu visor y controles físicos: altura de los soportes, comodidad del agarre y ubicación de los anclajes se pueden ajustar en los componentes/prefabs sin regenerar los modelos.
