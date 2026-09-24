# Jugador VR del bosque

ForestScene usa el rig XR de SampleScene. El jugador anterior está desactivado.
VRGroundProbe configura la altura y gravedad, y crea manos anatómicas articuladas de Unity XR
sobre ambos controles, ocultando sus modelos de mando. Usa un material de piel URP; la orientación se calcula desde muñeca, índice, medio y meñique de cada modelo. Gatillo dobla el índice;
Grip dobla los demás dedos. El seguimiento de manos sin mandos conserva la
configuración original de SampleScene y depende del dispositivo.

Al entrar en Play, el jugador busca un punto libre sobre el collider principal
de FloorWithLake. Se rechazan pendientes mayores de 45 grados y posiciones
ocupadas por obstáculos. Si sale del terreno o queda debajo, vuelve al último
punto seguro. La locomoción XRI usa CharacterController para las colisiones.
Esto limita el movimiento virtual; el movimiento físico de la cabeza en una
habitación no puede detenerse mediante un collider virtual.

Se activó Generate Colliders en los diez modelos de entorno usados por la escena,
incluyendo árboles, rocas, puente y casa. Unity debe terminar de reimportarlos
antes de probar. No se generan colliders de mallas en cada fotograma.

Comprobado: scripts compilan contra Unity instalado, referencias de piso y manos
conectadas, diff sin errores de espacios. Pendiente: prueba visual en Unity y visor.

Prueba: salir de Play, esperar la importación, abrir ForestScene y entrar en Play.
Comprobar aparición sobre tierra, manos y dedos, caminar contra troncos/rocas/casa,
y aproximarse a los bordes del terreno. El simulador XR del proyecto está habilitado
para instanciarse automáticamente en el editor.

## Linterna de cabeza

ForestScene tiene luces diurnas desactivadas, cielo oscuro y niebla tenue.
VRHeadFlashlight crea una luz Spot hija de la cámara XR: gira con la cabeza,
alcanza 28 m y tiene un cono de 55 grados. Intensidad, alcance, color y apertura
se ajustan en VRHeadFlashlight del rig activo. Volver a entrar en Play aplica
los cambios. Los perfiles URP Quality y Performance permiten luces adicionales
y sombras hasta 30 m; estas opciones también afectan otras escenas que los usen.
La compilación y conexión del componente están comprobadas. Pendiente evaluar
el aspecto y rendimiento de las sombras en el visor.
