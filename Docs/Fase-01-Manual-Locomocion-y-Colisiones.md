# Fase 1 — Locomoción de escritorio y colisiones

## 1. Objetivo

Este documento describe el estado real del proyecto Unity VR en relación con la locomoción sin visor y la prevención de colisiones en la habitación. Su objetivo es servir de base para la tesis y para explicar cómo funciona hoy la demo de escritorio sin depender de un casco VR físico.

La necesidad de esta solución surgió porque, en la fase inicial, la habitación ya tenía colliders y un XR Origin con infraestructura XRI, pero el movimiento visible no lograba respetar las paredes ni la geometría de la escena. El problema era doble:

- la demo debía poder explorarse desde el editor sin visor;
- la locomoción XR futura debía conservarse como una ruta distinta, sin perder la arquitectura del proyecto.

Por eso se eligió WASD como entrada de escritorio para que el jugador pudiera moverse de manera simple, directa y verificable. La intención no era reemplazar de forma permanente la locomoción XR, sino habilitar una prueba funcional en editor mientras no se dispone de visor físico.

El estado actual resuelto es el siguiente: el movimiento de escritorio funciona mediante el script DesktopKeyboardLocomotion y el CharacterController del XR Origin, y las paredes, puertas, ventanas y muebles sólidos bloquean el avance del jugador. Lo que queda reservado para el futuro es la prueba real con visor, donde la locomoción XR podrá reactivarse mediante DynamicMoveProvider y la infraestructura XRI.

## 2. Estado inicial del problema

Antes de implementar la solución final, el escenario presentaba varios síntomas que conviene explicar para entender el cambio:

- la habitación tenía colliders en la geometría base;
- el XR Origin ya estaba presente con una estructura de movimiento propia de XR Interaction Toolkit;
- las paredes de la habitación tenían BoxCollider o estaban configuradas como sólidos;
- sin embargo, el jugador podía atravesar las paredes en la práctica;
- el movimiento visible no estaba pasando por DynamicMoveProvider como locomoción activa;
- las acciones de movimiento de manos permanecían en cero, por lo que no se estaba generando un desplazamiento equivalente desde los inputs XR;
- el estado de locomotionState permanecía en Idle o no se convertía en un movimiento visible de desplazamiento del jugador.

En otras palabras, tener colliders no era suficiente. Un collider solo define una superficie sólida para la física; el movimiento del jugador solo se detiene si el código de desplazamiento usa un mecanismo que consulte esas superficies. Aquí el punto clave es que el desplazamiento anterior no estaba siendo realizado con CharacterController.Move, por lo que la cápsula del jugador no interactuaba de forma efectiva con los colliders del entorno.

## 3. Solución final adoptada

La solución adoptada para Fase 1 es un flujo de escritorio que usa teclado WASD y un script propio para mover al jugador mediante CharacterController.Move.

El flujo actual es este:

```text
Teclado WASD
→ DesktopKeyboardLocomotion
→ CharacterController.Move
→ detección de colliders
→ jugador bloqueado
```

Este enfoque está pensado para pruebas de escritorio dentro del editor, porque actualmente no se dispone de un visor VR físico. No reemplaza de forma permanente la locomoción XR del proyecto; simplemente permite demostrar la habitación, la navegación básica y la colisión sin depender del hardware.

## 4. Arquitectura actual

La estructura real observada en la escena es la siguiente:

```text
SampleScene
├── XR Origin (XR Rig)
│   ├── Main Camera
│   ├── CharacterController
│   ├── DesktopKeyboardLocomotion
│   ├── InputActionManager
│   ├── XRInputModalityManager
│   └── Locomotion
│       ├── DynamicMoveProvider
│       ├── LocomotionMediator
│       └── XRBodyTransformer
├── XR Interaction Simulator
├── Room
│   ├── Structure
│   ├── Furniture
│   └── Architecture
└── GrabCube
```

La jerarquía anterior se basa en la escena, en los scripts del proyecto y en los componentes de Starter Assets XRI. La descripción de cada bloque es la siguiente:

- XR Origin: representa el punto de referencia del jugador en el mundo, con la cámara y la cápsula de movimiento.
- Main Camera: es la cámara del usuario y se usa como referencia visual para la dirección de movimiento.
- CharacterController: es la cápsula física que mueve al jugador y que detecta colisiones con los colliders del entorno.
- DesktopKeyboardLocomotion: es el componente de escritorio que convierte WASD en movimiento del CharacterController.
- InputActionManager y XRInputModalityManager: aportan la infraestructura de entrada y modalidad de XR, aunque el movimiento de escritorio no depende de ellas directamente.
- Locomotion: contiene la infraestructura futura para locomoción XR, con DynamicMoveProvider, LocomotionMediator y XRBodyTransformer.
- XR Interaction Simulator: permite simular manos y controladores para interacción, no para locomoción física del jugador.
- Room/Structure/Furniture/Architecture: contienen la geometría sólida y los colliders que bloquean el movimiento.
- GrabCube: sigue siendo un objeto interactivo que valida la infraestructura XR de agarre, no la locomoción del jugador.

## 5. Componentes del XR Origin

### XROrigin

XROrigin representa el origen de coordenadas del jugador dentro del mundo virtual. En esta arquitectura, no se mueve la cámara directamente; lo que se mueve es el XR Origin como contenedor del jugador y, dentro de él, la cámara y los componentes asociados. La relación con Camera Offset es la tradicional en XR: el XR Origin define el punto raíz del jugador y la cámara se ubica en relación con ese origen.

En la práctica, el código de configuración del proyecto espera que el XR Origin tenga:

- un CharacterController en el objeto Origin;
- una cámara configurada;
- un Camera Floor Offset Object;
- un componente XRBodyTransformer que pueda trabajar con el CharacterController si existe.

### CharacterController

El CharacterController es la cápsula física que representa al jugador en el espacio del mundo. En el proyecto se configuró con valores concretos mediante TesisCollisionSetup:

- center: $(0, 0.9, 0)$
- height: $1.8$
- radius: $0.25$
- skinWidth: $0.03$
- stepOffset: $0.20$
- slopeLimit: $45$
- minMoveDistance: $0$
- enabled: true

Este componente no necesita Rigidbody. El CharacterController es un controlador de movimiento de propósito especial que ya incorpora la lógica de colisión. No actúa automáticamente simplemente por existir: alguien debe llamar a CharacterController.Move para que la cápsula avance y se contacte con los colliders.

El componente también expone propiedades útiles como collisionFlags e isGrounded, aunque en este escenario se usa principalmente para que la cápsula responda a los colliders del entorno.

### InputActionManager

Este componente forma parte de la infraestructura de entrada de XR Interaction Toolkit. Habilita los activos de entrada que el proyecto necesita para la interacción y la locomoción XR. Aunque el movimiento de escritorio lee directamente el teclado, InputActionManager sigue siendo necesario porque forma parte del sistema de entrada base del proyecto y conserva la compatibilidad con la infraestructura XRI.

### XRInputModalityManager

Este componente está relacionado con la modalidad de entrada, especialmente con la gestión de manos o controladores. En el flujo actual de escritorio, no participa directamente en el desplazamiento por WASD, pero sí forma parte de la infraestructura general del proyecto y de la futura ruta XR.

### Main Camera

La Main Camera es la referencia visual del jugador. En el movimiento de escritorio se usa para calcular el forward y el right de la dirección del movimiento sobre el plano horizontal. Eso permite que el jugador avance según la orientación de la vista y no solo en el eje global del mundo.

### DynamicMoveProvider

DynamicMoveProvider es el componente de locomoción XR original provisto por la muestra Starter Assets. Su función es tomar input de movimiento de manos o controladores y desplazar el XR Origin mediante la infraestructura XRI. En el proyecto actual se conserva porque es la solución futura para probar con visor físico o controladores reales.

En la configuración actual, TesisCollisionSetup lo desactiva temporalmente para modo escritorio. La razón es evitar que el movimiento XR compita con DesktopKeyboardLocomotion y genere doble desplazamiento o estados inconsistentes.

### LocomotionMediator

LocomotionMediator actúa como un elemento intermedio entre los providers de locomoción y el sistema de transformación del XR Origin. En este proyecto se usa para conectar el provider de movimiento con XRBodyTransformer.

### XRBodyTransformer

XRBodyTransformer transforma el XR Origin para que el movimiento se aplique de forma coherente a la cápsula física. Tiene un campo importante, Use Character Controller If Exists, que se activa en la configuración del proyecto. Eso permite que la locomoción XR futura pueda usar el CharacterController ya presente en el XR Origin.

Aunque DesktopKeyboardLocomotion llama directamente a CharacterController.Move, XRBodyTransformer se conserva porque es parte de la infraestructura XR que el proyecto puede volver a usar cuando se reaccione la locomoción XR con un visor físico.

## 6. Explicación completa de DesktopKeyboardLocomotion.cs

Este es el componente central de la Fase 1. El archivo real se encuentra en Assets/Scripts/Core/XR/DesktopKeyboardLocomotion.cs.

### Estructura general

El script declara una clase sellada, MonoBehaviour, con atributos que le imponen condiciones muy claras:

- [DisallowMultipleComponent]: evita que el objeto tenga más de una copia del script;
- [RequireComponent(typeof(CharacterController))]: exige que exista un CharacterController en el mismo GameObject;
- [AddComponentMenu("Tesis VR/Desktop Keyboard Locomotion (Editor Only)")]: lo deja visible en el menú de componentes del editor.

### Campos SerializeField

El script tiene campos serializados para configurar referencias y comportamiento:

- characterController: referencia al CharacterController del XR Origin;
- mainCamera: referencia a la cámara principal;
- moveSpeed: velocidad de desplazamiento, con valor inicial $1.5$;
- gravity: gravedad, con valor inicial $-9.81$;
- enableKeyboardLocomotion: habilita o deshabilita el movimiento por teclado;
- debugLogs: activa registros de depuración.

Estos campos son importantes porque el script no busca objetos por nombre ni usa búsquedas globales; recibe sus referencias de forma explícita desde la herramienta de configuración.

### Awake

En Awake, el script valida que las referencias necesarias existan. Si faltan, registra un error y deshabilita el componente. Esto evita que el script se ejecute con referencias nulas y produzca un movimiento erróneo.

### OnValidate

OnValidate corrige valores de configuración para que no entren en estados inconsistentes. Por ejemplo, fuerza que moveSpeed no sea negativo y que gravity no sea positiva.

### Update

La lógica de movimiento está en Update. Este método se ejecuta en cada frame y está protegido con la directiva #if UNITY_EDITOR. Eso significa que el comportamiento se limita al editor, que es justamente el contexto de prueba de escritorio previsto para la Fase 1.

El flujo interno es el siguiente:

1. Verifica que el movimiento por teclado esté habilitado y que el CharacterController siga activo.
2. Obtiene Keyboard.current para leer el estado del teclado.
3. Construye un vector de entrada basado en las teclas A, D, S, W.
4. Obtiene el forward de la cámara y lo proyecta sobre el plano horizontal.
5. Calcula el right relativo a esa dirección.
6. Genera el vector de movimiento deseado.
7. Llama a CharacterController.Move con ese vector multiplicado por la velocidad y por el deltaTime.
8. Aplica la gravedad con una segunda llamada a CharacterController.Move.

### Lectura del teclado

El script usa Input System y, en concreto, Keyboard.current. Las teclas A/D y S/W se interpretan como ejes de entrada. La función ReadAxis devuelve un valor de $-1$, $0$ o $1$ según qué tecla esté presionada. Esto permite que el movimiento sea unidireccional o combinado.

### Construcción del vector de entrada

El código construye un Vector2 con los ejes horizontal y vertical:

- eje X: A/D;
- eje Y: S/W.

Luego aplica Vector2.ClampMagnitude para evitar que la combinación de teclas supere la magnitud unitaria; así el movimiento diagonal no se vuelve más rápido que el movimiento recto.

### Forward y right de la cámara

El script usa Main Camera para obtener la orientación de la vista. Primero proyecta el forward de la cámara sobre el plano horizontal, con Vector3.ProjectOnPlane(..., Vector3.up). Esto evita que el movimiento se vea afectado por la inclinación vertical de la cámara. Si el forward calculado fuera demasiado pequeño, el script recurre al forward del propio transform del objeto.

Después calcula el vector right como el producto cruzado entre Vector3.up y forward. Esto proporciona una referencia lateral del movimiento.

### Movimiento real

El vector de movimiento se construye como:

```text
requestedMotion = right * input.x + forward * input.y
```

Luego se multiplica por moveSpeed y por Time.deltaTime y se pasa a CharacterController.Move. Este es el punto decisivo: si se eliminara esta llamada, el personaje no se movería físicamente.

### Gravedad y movimiento vertical

El script mantiene una variable verticalVelocity. En la lógica real:

- si el CharacterController está grounded y la velocidad vertical es negativa, se fuerza a $-2$;
- si no está grounded, la velocidad vertical se actualiza con la gravedad multiplicada por el deltaTime.

Después se aplica otra llamada a CharacterController.Move con Vector3.up multiplicado por la velocidad vertical. Así se simula la gravedad y se evita que el jugador quede flotando o salte inesperadamente.

### CollisionFlags y logs

La llamada a CharacterController.Move devuelve CollisionFlags. El script los combina y los usa para registrar el movimiento en depuración si debugLogs está activado. Esto permite saber si el movimiento se vio bloqueado por contacto con un collider.

## 7. Flujo de movimiento WASD

El flujo completo al presionar W es el siguiente:

1. Unity Input System detecta que Keyboard.current.wKey está presionado.
2. DesktopKeyboardLocomotion convierte esa presión en un Vector2 de entrada.
3. El script usa Main Camera para obtener un forward horizontal relativo a la vista.
4. Calcula la dirección de avance según la orientación visual del jugador.
5. Multiplica esa dirección por moveSpeed y por deltaTime.
6. Invoca CharacterController.Move.
7. Unity evalúa los colliders del entorno, incluidos los de Room/Structure/Furniture/Architecture.
8. Si el movimiento choca con una superficie sólida, la cápsula avanza solo hasta el punto de contacto.
9. La pared, puerta, ventana o mueble impiden el desplazamiento restante.
10. El XR Origin queda bloqueado en su posición válida.

Para A y D, el script genera una componente lateral; para S, una componente invertida del movimiento. La combinación de teclas produce diagonales, pero el vector se normaliza para evitar velocidades anómalas.

## 8. Gravedad y contacto con el piso

La gravedad se maneja de forma muy simple y explícita:

- se mantiene verticalVelocity como variable de estado;
- si el CharacterController está grounded y la velocidad vertical es negativa, se reinicia a $-2$;
- si no está grounded, la velocidad vertical se reduce con la gravedad.

La segunda llamada a CharacterController.Move aplica el desplazamiento vertical. Si no se aplicara esta lógica, el jugador podría quedar flotando o perder el contacto con el piso, lo cual no es deseable en una demo de habitación. La relación con el piso depende de la geometría del escenario, especialmente del objeto Floor dentro de Room/Structure.

En el proyecto existen tanto un piso raíz como una estructura de Room/Structure/Floor. El código de TesisCollisionSetup usa la jerarquía Room/Structure/Floor como referencia para configurar los colliders que bloquean el movimiento. La lógica real de movilidad se apoya en esa geometría, no en etiquetas inventadas ni en un Rigidbody del jugador.

## 9. Colliders del escenario

La siguiente tabla refleja la configuración esperada y validada por TesisCollisionSetup para la habitación:

| Objeto     | Ruta                        | Tipo de collider | Enabled | Is Trigger | Static | Bloquea al jugador | Observaciones                                    |
| ---------- | --------------------------- | ---------------- | ------- | ---------- | ------ | ------------------ | ------------------------------------------------ |
| Floor      | Room/Structure/Floor        | BoxCollider      | true    | false      | true   | sí                 | Piso sólido que da base al movimiento            |
| Ceiling    | Room/Structure/Ceiling      | BoxCollider      | true    | false      | true   | sí                 | Superficie superior del volumen de la habitación |
| Wall_North | Room/Structure/Wall_North   | BoxCollider      | true    | false      | true   | sí                 | Pared norte                                      |
| Wall_South | Room/Structure/Wall_South   | BoxCollider      | true    | false      | true   | sí                 | Pared sur                                        |
| Wall_East  | Room/Structure/Wall_East    | BoxCollider      | true    | false      | true   | sí                 | Pared este                                       |
| Wall_West  | Room/Structure/Wall_West    | BoxCollider      | true    | false      | true   | sí                 | Pared oeste                                      |
| Bed        | Room/Furniture/Bed          | BoxCollider      | true    | false      | true   | sí                 | Mueble sólido                                    |
| Nightstand | Room/Furniture/Nightstand   | BoxCollider      | true    | false      | true   | sí                 | Mueble sólido                                    |
| Wardrobe   | Room/Furniture/Wardrobe     | BoxCollider      | true    | false      | true   | sí                 | Mueble sólido                                    |
| Desk       | Room/Furniture/Desk         | BoxCollider      | true    | false      | true   | sí                 | Mueble sólido                                    |
| Door       | Room/Architecture/Door      | BoxCollider      | true    | false      | true   | sí                 | Puerta sólida                                    |
| Window_01  | Room/Architecture/Window_01 | BoxCollider      | true    | false      | true   | sí                 | Ventana sólida                                   |
| Window_02  | Room/Architecture/Window_02 | BoxCollider      | true    | false      | true   | sí                 | Ventana sólida                                   |

Algunas piezas adicionales, como Television y Mirror, pueden no bloquear según la construcción de la habitación porque el script de configuración no los incluye como elementos de bloqueo. CoatRack, en cambio, se gestiona como un objeto de la habitación que puede existir como un placeholder sólido. La diferencia entre un collider sólido y un trigger es importante: un sólido bloquea el movimiento de la cápsula; un trigger no impide el avance, sino que notifica entradas y salidas.

Los objetos estáticos no necesitan Rigidbody para bloquearse. El CharacterController ya aprovecha los colliders estáticos del escenario para resolver el contacto.

## 10. TesisCollisionSetup.cs

El archivo Assets/Editor/TesisCollisionSetup.cs es una herramienta de Editor, no de runtime. Su responsabilidad es preparar la escena de forma reproducible para la Fase 1.

### Qué hace exactamente

El menú de Unity que se utiliza es:

```text
Tools > Tesis VR > Configure Player Collision
```

Al ejecutarlo, la herramienta:

1. abre SampleScene;
2. encuentra el XR Origin único en la escena;
3. crea o reutiliza un CharacterController en el objeto Origin;
4. configura sus valores de cápsula;
5. encuentra un DynamicMoveProvider compatible;
6. obtiene el LocomotionMediator y el XRBodyTransformer asociados;
7. activa Use Character Controller If Exists en XRBodyTransformer;
8. crea o reutiliza DesktopKeyboardLocomotion en el XR Origin;
9. asigna referencias a CharacterController y Main Camera;
10. activa el modo escritorio;
11. desactiva temporalmente DynamicMoveProvider;
12. busca y configura los colliders de la habitación;
13. valida que la escena quedó en un estado coherente;
14. guarda la escena y reporta mensajes en Console.

### Importancia de estar en Assets/Editor

Esta herramienta pertenece a Assets/Editor porque su propósito es preparar la escena desde el editor. No tiene sentido dejarla en runtime, porque su función no es ejecutar la demo en tiempo de ejecución, sino garantizar que la escena se encuentre en un estado válido para probarla.

## 11. Idempotencia

La configuración es idempotente. Eso significa que se puede ejecutar varias veces sin romper la escena ni duplicar componentes de forma accidental.

En la práctica, la herramienta:

- evita duplicar CharacterController consultando si ya existe uno en el XR Origin;
- evita duplicar DesktopKeyboardLocomotion consultando si ya existe y reutilizándolo;
- reutiliza colliders existentes en lugar de crear nuevos siempre;
- verifica que los valores de configuración estén correctos.

Por ejemplo, en la primera ejecución se crea el CharacterController y se le asignan sus valores; en la segunda, el mismo componente se encuentra y se reconfigura en lugar de crear otro. Esto es importante para la tesis porque permite reproducir la configuración de forma consistente y evitar artefactos innecesarios en la escena.

## 12. Diferencia entre locomoción de escritorio y locomoción XR

| Aspecto              | Escritorio                       | XR futuro                            |
| -------------------- | -------------------------------- | ------------------------------------ |
| Entrada              | WASD                             | joystick del controlador             |
| Componente principal | DesktopKeyboardLocomotion        | DynamicMoveProvider                  |
| Movimiento           | CharacterController.Move directo | Provider → Mediator → Transformer    |
| Visor necesario      | No                               | Sí                                   |
| Estado actual        | Activo                           | Conservado/desactivado temporalmente |
| Uso                  | Desarrollo y demostración        | Prueba con casco                     |

Es importante destacar que ambos sistemas no deben estar activos simultáneamente si producen un movimiento duplicado o inconsistencias. Por eso TesisCollisionSetup desactiva DynamicMoveProvider cuando se activa DesktopKeyboardLocomotion.

## 13. XR Interaction Simulator

El XR Interaction Simulator sigue aportando valor a la demo. Permite simular manos o controladores dentro del editor, lo cual es útil para probar interacción con objetos y rayos sin un visor físico. En esta fase, su rol principal es la interacción y no la locomoción del jugador.

El componente es útil para:

- simular controladores o manos;
- probar rayos de interacción;
- verificar agarre de objetos;
- trabajar con interactores de XR Interaction Toolkit.

Sin embargo, Full Body Translate no se utilizó como locomoción con colisión. Se mantiene para interacción, pero el desplazamiento real del jugador en esta Fase 1 lo controla DesktopKeyboardLocomotion.

## 14. GrabCube

GrabCube sigue siendo un objeto interactivo. En la escena aparece equipado con XRGrabInteractable y con un Rigidbody. También cuenta con un collider que permite que el sistema XRI lo detecte como elemento agarrable.

Su relevancia en esta fase es la siguiente:

- valida que la infraestructura XR de interacción sigue funcionando;
- permite probar el flujo de agarre con el XR Interaction Simulator;
- demuestra que la locomoción de escritorio y la interacción XR son sistemas distintos.

Agarra un objeto no es lo mismo que chocar con una pared. El cubo valida interacción XR; la pared valida movimiento físico del jugador.

## 15. Elementos temporales eliminados

Se han revisado los nombres que aparecen en la solicitud para evitar documentar algo que no esté verificado.

### XRLocomotionDiagnostics

No se encontró evidencia de que este componente esté activo o que siga siendo parte del flujo actual. No se debe describir como eliminado si sigue presente en la escena o en archivos; en este caso, la verificación del proyecto no lo muestra como parte del flujo actual de locomoción.

### XRPlayerCollisionFollower

No se encontró evidencia de que esté activo o que esté conectado al flujo del jugador en la escena actual. No se presenta como eliminado si no se puede verificar su estado real.

### CharacterControllerDriver

No se encontró evidencia de que este componente sea parte de la configuración actual del XR Origin. La infraestructura actual se apoya en CharacterController y DesktopKeyboardLocomotion, no en un driver adicional para el movimiento.

## 16. Flujo completo en Play Mode

```text
Play Mode
  ↓
DesktopKeyboardLocomotion activo
  ↓
clic en Game
  ↓
WASD
  ↓
dirección relativa a Main Camera
  ↓
CharacterController.Move
  ↓
colliders de Room
  ↓
movimiento permitido o bloqueado
```

En paralelo, el flujo de interacción es el siguiente:

```text
XR Interaction Simulator
  ↓
controladores simulados
  ↓
interactores
  ↓
XR Interaction Manager
  ↓
GrabCube
```

Ambos flujos conviven, pero tienen responsabilidades distintas: uno mueve al jugador y el otro permite interactuar con objetos del entorno.

## 17. Pruebas realizadas

### Comprobado

- WASD mueve al jugador en el flujo de escritorio;
- la configuración del CharacterController se realiza desde TesisCollisionSetup;
- los colliders de la habitación quedan activos y sólidos;
- el componente DesktopKeyboardLocomotion se asocia al CharacterController y a la cámara;
- el proyecto conserva la infraestructura XR de Starter Assets y XRI;
- se puede ejecutar la herramienta de editor Tools > Tesis VR > Configure Player Collision;
- el estado del proyecto se revisó a través de los scripts y de la escena.

### Pendiente

- probar con visor físico;
- reactivar DynamicMoveProvider en un contexto XR real;
- validar joystick real o entrada de controlador;
- revisar la altura y escala del jugador si la habitación sigue siendo considerada flotante;
- verificar el comportamiento final con hardware real.

## 18. Cómo reproducir la configuración desde cero

1. Requisitos de Unity: usar la misma versión del proyecto o una compatible con los paquetes actuales.
2. Paquetes: conservar XR Interaction Toolkit 3.3.2, Input System 1.19.0 y Starter Assets incluidos.
3. XR Origin: asegurarse de que exista un XR Origin con cámara y CharacterController.
4. XR Interaction Simulator: mantenerlo activo para interacción de escritorio.
5. Room y colliders: contar con Room/Structure/Furniture/Architecture correctamente construidos.
6. Agregar DesktopKeyboardLocomotion: en el XR Origin o en el objeto raíz del jugador.
7. Agregar TesisCollisionSetup: en Assets/Editor, como herramienta de configuración.
8. Ejecutar Tools > Tesis VR > Configure Player Collision.
9. Abrir SampleScene.
10. Entrar en Play.
11. Hacer clic en Game.
12. Usar WASD para moverse.
13. Probar con paredes, puertas, ventanas y muebles.
14. Probar GrabCube.
15. Revisar Console para mensajes de validación.

## 19. Cómo volver al modo XR

Para volver a la ruta XR futura se recomienda:

1. desactivar DesktopKeyboardLocomotion;
2. reactivar DynamicMoveProvider;
3. conservar CharacterController y el XR Origin;
4. conservar LocomotionMediator y XRBodyTransformer;
5. no eliminar las acciones de entrada del proyecto;
6. probar con un dispositivo real;
7. evitar ejecutar el configurador si este restablece el modo escritorio, o tener en cuenta que su comportamiento actual es el de preparar la escena para desktop.

## 20. Archivos involucrados

| Archivo                                             | Responsabilidad                                              | Editor o Runtime | Creado o existente | Modificado | Se versiona en Git | Necesario para modo escritorio | Necesario para modo XR |
| --------------------------------------------------- | ------------------------------------------------------------ | ---------------- | ------------------ | ---------- | ------------------ | ------------------------------ | ---------------------- |
| Assets/Scripts/Core/XR/DesktopKeyboardLocomotion.cs | mueve al jugador con WASD usando CharacterController.Move    | Runtime          | existente          | sí         | sí                 | sí                             | no                     |
| Assets/Editor/TesisCollisionSetup.cs                | configura el entorno de colisiones y habilita desktop        | Editor           | existente          | sí         | sí                 | sí                             | no                     |
| Assets/Editor/TesisRoomBuilder.cs                   | construye la habitación placeholder y los colliders base     | Editor           | existente          | sí         | sí                 | sí                             | no                     |
| Assets/Scenes/SampleScene.unity                     | contiene la escena de prueba principal                       | Editor/Runtime   | existente          | sí         | sí                 | sí                             | sí                     |
| Packages/manifest.json                              | define los paquetes instalados, incluidas XRI y Input System | Editor/Runtime   | existente          | no         | sí                 | sí                             | sí                     |
| Assets/Scenes/SampleScene.unity.meta                | metadatos de la escena                                       | Editor           | existente          | no         | sí                 | sí                             | sí                     |
| Assets/Materials                                    | materiales de la habitación                                  | Editor/Runtime   | existentes         | sí         | sí                 | sí                             | no                     |
| scripts y assets XRI referenciados                  | infraestructura de interacción y locomoción                  | Runtime/Editor   | existentes         | no         | sí                 | no                             | sí                     |

## 21. Glosario

- XROrigin: origen del jugador en el mundo virtual.
- Main Camera: cámara principal del jugador.
- Camera Offset: relación de la cámara respecto al XR Origin.
- CharacterController: cápsula física que mueve al jugador y detecta colisión.
- Collider: superficie física que interactúa con el CharacterController.
- BoxCollider: collider rectangular usado para paredes y muebles.
- MeshCollider: collider basado en malla, no usado en esta configuración de forma central.
- Is Trigger: propiedad que convierte un collider en un disparador, no sólido.
- Rigidbody: componente físico que no se usa en el XR Origin para este flujo.
- CharacterController.Move: método que desplaza la cápsula y evalúa colisiones.
- CollisionFlags: información devuelta por la llamada a Move sobre el resultado del contacto.
- isGrounded: estado de contacto con el suelo o una superficie.
- Input System: sistema de entrada moderno de Unity.
- Keyboard.current: acceso al teclado dentro del Input System.
- SerializeField: atributo que expone un campo privado en el inspector.
- RequireComponent: atributo que exige un componente en el mismo GameObject.
- MonoBehaviour: clase base de los scripts de Unity.
- UnityEditor: espacio de nombres para herramientas del editor.
- MenuItem: atributo que expone una opción de menú en Unity.
- DynamicMoveProvider: locomoción XR basada en movimiento continuo de Starter Assets.
- LocomotionMediator: pieza intermedia entre providers y transformers.
- XRBodyTransformer: componente que transforma el XR Origin para la locomoción XR.
- XR Interaction Simulator: herramienta para simular manos y controladores en editor.
- XR Grab Interactable: componente que permite agarrar objetos con XRI.
- idempotencia: capacidad de ejecutar la misma configuración varias veces sin duplicar componentes.
- runtime: tiempo de ejecución de Unity.
- Play Mode: modo de reproducción del editor.

## 22. Conclusión de la Fase 1

La solución actual de Fase 1 es suficiente para una tesis porque permite demostrar de forma clara que el jugador puede moverse por una habitación virtual sin visor y que el movimiento queda restringido por las superficies sólidas del escenario. Evita sobreingeniería porque usa una ruta simple de escritorio basada en WASD y CharacterController.Move, sin introducir una arquitectura nueva para un problema que aún no requiere hardware real.

Además, conserva compatibilidad futura con XR. El proyecto sigue teniendo DynamicMoveProvider, LocomotionMediator y XRBodyTransformer, por lo que puede volver a la ruta XR cuando exista un visor físico o un flujo de controladores real. Lo que queda completado antes de comenzar la Fase 2 es precisamente este punto: demostrar que la locomoción de escritorio y el bloqueo por colisiones son funcionales y comprensibles, con una base que pueda evolucionar hacia el uso real del casco.
