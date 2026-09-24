# ForestScene: duende, vida y luz de edición

> Esta guía corresponde a la primera integración. La configuración actual del jugador, las armas y los estados ampliados está en **FOREST_COMBAT.md**.

## Uso

1. Abre `Assets/00_Scenes/ForestScene.unity`. Si ya estaba abierta, recarga la versión de disco conservando primero cualquier cambio tuyo sin guardar.
2. `Editor Preview Light` ilumina mientras editas. Su luz es temporal: se elimina al entrar a Play y se recrea al salir. El objeto tiene la etiqueta `EditorOnly` y no se incluye en builds. No cambia la iluminación ambiental existente.
3. Crea GameObjects vacíos como **hijos directos de `Goblin Spawn Points`**. Colócalos sobre el suelo, fuera de árboles y paredes. Su orientación indica hacia dónde aparece el duende. No se han añadido puntos de ejemplo.
4. Activa `Forest VR Player` cuando vayas a probar. Ya estaba desactivado en la escena y se ha conservado ese estado, junto con los controles y componentes VR.
5. En Play aparece un único duende entre los puntos activos a menos de 25 metros de la cámara del jugador. Sin puntos o con el jugador inactivo no aparece ninguno.

## Comportamiento y ajustes

`Assets/SO_/ForestGoblinSettings.asset` contiene los valores editables:

- Vida: jugador 100 (componente Health de la escena), duende 100.
- Zona de aparición: 25 m; detección: 15 m; ataque: 1,5 m; daño: 20.
- Reaparición: 1200 segundos de juego desde la muerte. El tiempo se pausa con `Time.timeScale = 0`. No se guarda al cerrar o recargar la escena.
- Si termina la espera sin jugador cerca, sigue esperando hasta que entre en la zona de algún punto.
- El cadáver permanece al menos 8 segundos, con su animación de muerte. Su colisión se desactiva.
- El jugador muerto no recibe más daño ni provoca nuevas apariciones. Se exponen eventos de muerte y cambio de vida; no se ha añadido una pantalla de derrota ni se alteran los controles del visor.

El duende persigue con CharacterController y respeta colliders. Es persecución directa, sin navegación alrededor de obstáculos: puede detenerse ante un árbol o pared. Para rutas complejas hará falta preparar navegación del mapa. Los golpes requieren línea de visión.

El prefab nuevo `Assets/02_Prefabs/Enemy/ForestGoblin.prefab` utiliza el modelo con esqueleto de `GoblingIdle.fbx`: el prefab Tripo original no tiene la jerarquía necesaria para estos clips. Se conserva el original. La variante mide 1,4 m y usa el material existente del duende. Las copias de clips de `Assets/03_Animations/ForestGoblin` permiten ajustar bucles y desplazamiento sin modificar los FBX originales.

Los scripts están en `Assets/01_Scripts/Forest`. Las futuras flechas pueden buscar `Health` con `GetComponentInParent<Health>()` al impactar y llamar a `TakeDamage(dano)`. En Play, el menú contextual del componente Health permite recibir 25 de daño para probar la muerte sin arco.

## Git

No había entradas de merge sin resolver. Se añadieron al ignore los dos archivos solicitados y se retiraron del índice, **conservando sus copias locales**:

- `Assets/XR/Settings/OpenXR Package Settings.asset.meta`
- `ProjectSettings/EditorBuildSettings.asset`

Las dos bajas aparecen preparadas en Git; no se creó commit. Al compartirlas, cada equipo deberá conservar/configurar sus ajustes locales de escenas y XR. No son archivos de rutas personales: contienen referencias y ajustes de Unity.

La ruta absoluta de Tripo realmente está en `Packages/manifest.json`, en `com.tripo3d.unitybridge` (`file:C:/Users/johnn/Downloads/Tripo3d_Unity_Bridge`). No se ha modificado ese paquete ni ignorado el manifiesto completo. Para compartirlo sin depender del usuario de Windows, habrá que distribuir el bridge mediante una ruta relativa común o un paquete compartido.

## Preparación del arco para la siguiente implementación

- Genera el **cuerpo del arco sin cuerda**, con la empuñadura y extremos bien definidos. Puede ser una sola malla.
- La **cuerda debe poder deformarse independientemente**: no debe quedar fusionada como geometría rígida del cuerpo. No hace falta generarla en Tripo; podemos dibujarla en Unity entre extremo superior, punto de agarre y extremo inferior.
- La **flecha es un modelo separado**, con punta hacia su eje longitudinal y escala coherente.
- Si quieres que las palas del arco se doblen, el cuerpo necesitará huesos o blend shapes; un modelo rígido sirve para la primera versión.
- Se configurarán anclajes para mano, extremos de cuerda y flecha, agarre con ambas manos, fuerza según estiramiento y daño al impactar. El arco aún no está implementado.

## Verificación

Compilación e importación con Unity 6000.5.6f1 en un proyecto temporal. Se comprobaron las rutas de los huesos de los cuatro clips contra el modelo animado. Las pruebas automatizadas en Play verifican vida, daño, muerte única, aparición única, puntos vacíos/lejanos, daño del ataque, espera de 1200 segundos y condición de zona tras vencer la espera. Para probar el vencimiento se adelanta el reloj interno; no se espera 20 minutos reales.

`ForestVR.Editor.ForestGameplayValidation.RunBatch` ejecuta las pruebas en un proyecto desechable con estos assets; entra en Play y cierra ese editor al finalizar. No debe ejecutarse en tu sesión de trabajo. Falta comprobar visualmente ForestScene y la experiencia con el visor físico.
