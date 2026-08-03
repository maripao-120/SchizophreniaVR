# Fase 1 — Habitación Base

> **Alcance de este manual.** Este documento describe el estado inspeccionado del proyecto y la herramienta `TesisRoomBuilder`. No ejecuta la herramienta, no corrige transformaciones y no modifica escenas, scripts, materiales, paquetes ni configuración.
>
> **Convenciones.** `Comprobado` significa que el dato aparece en un archivo inspeccionado. `Inferencia` significa una explicación técnica razonable que no está serializada directamente. `Pendiente` significa que requiere una prueba en Unity o un log que no está disponible en el repositorio.

## 1. Objetivo de la fase

La Fase 1 construye una habitación placeholder jugable alrededor del sistema XR que ya existía en `SampleScene`. La habitación representa el espacio físico inicial de la experiencia: piso, techo, cuatro paredes, mobiliario, elementos arquitectónicos, iluminación y un objeto de prueba agarrable.

Se utilizaron primitivas de Unity porque permiten comprobar rápidamente escala, posición, colisiones, navegación e interacción. Un `Cube` o un `Cylinder` puede reemplazarse posteriormente por un modelo de Blender sin cambiar la organización de la escena ni el flujo XR. Esta decisión es coherente con el objetivo de la tesis: primero validar la demo funcional y después mejorar el aspecto visual.

En esta fase todavía no se implementan:

- la interacción narrativa con el frasco de pastillas o la nota;
- voces o alucinaciones auditivas;
- alucinaciones visuales, brazos o silueta;
- cambios de narrativa;
- puertas o ventanas funcionales;
- modelos 3D realistas;
- sistemas de gameplay propios.

La Fase 1 se considera estructuralmente construida cuando existen los objetos placeholder, sus materiales, los colliders definidos, la luz auxiliar, el `GrabCube` de prueba y el sistema XR original continúa disponible. La validación completa de locomoción, teletransporte, colisión y visor físico sigue siendo una actividad de prueba, no una propiedad que pueda deducirse solo del YAML.

## 2. Estado inicial del proyecto

### 2.1 Escenas

El proyecto usa Unity `6000.3.20f1` y tiene como escena incluida en Build Settings únicamente `Assets/Scenes/SampleScene.unity` (`ProjectSettings/EditorBuildSettings.asset`, líneas 7-10). `Assets/Scenes/Habitacion.unity` existe como asset, pero no está incluida en la lista de escenas de compilación.

| Escena                            | Estado inspeccionado                                                                                                | Papel en la Fase 1                                                                    |
| --------------------------------- | ------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------- |
| `Assets/Scenes/SampleScene.unity` | Escena principal. Contiene la base XR y, en el estado actual, `Room` y sus hijos.                                   | Escena de trabajo y de ejecución.                                                     |
| `Assets/Scenes/Habitacion.unity`  | Asset separado. El archivo contiene configuración de escena y no forma parte de Build Settings.                     | No se utiliza para esta fase.                                                         |
| `Assets/XR_Simulator_Test.unity`  | Escena de prueba separada con `GrabCube`, `Directional Light`, `Global Volume`, `Floor` y `XR Interaction Manager`. | No fue modificada por `TesisRoomBuilder`; sirve como referencia/prueba independiente. |

Se eligió `SampleScene` porque es la única escena habilitada en Build Settings y porque ya contenía la infraestructura de interacción que debía conservarse. Trabajar sobre `Habitacion` habría producido una escena que no era la principal; trabajar sobre `XR_Simulator_Test` habría mezclado una escena de prueba con la demo principal.

### 2.2 Base XR y objetos existentes

Antes de la habitación, `SampleScene` ya contenía:

- una instancia del prefab `XR Origin (XR Rig)`;
- una instancia del prefab `XR Interaction Simulator`;
- un `XR Interaction Manager` de XR Interaction Toolkit;
- un `Floor` heredado de la escena;
- un `GrabCube` con componentes físicos y `XRGrabInteractable`;
- `Directional Light` y `Global Volume`.

El stack declarado en `AGENTS.md` y `Packages/manifest.json` es:

- Unity `6000.3.20f1`;
- Universal Render Pipeline `17.3.0`;
- XR Interaction Toolkit `3.3.2`;
- Input System `1.19.0`;
- Visual Scripting `1.9.11` disponible, pero no utilizado por esta herramienta.

La carpeta `Assets/Editor` contiene dos herramientas de Editor: `BuildRoomWizard.cs` y `TesisRoomBuilder.cs`. La primera es un script anterior y distinto; la herramienta documentada en este manual es `TesisRoomBuilder.cs`.

## 3. Arquitectura actual de la escena

### 3.1 Jerarquía real serializada

La lista `SceneRoots` de `SampleScene.unity` contiene siete raíces. La jerarquía comprobada es:

```text
SampleScene
├── Directional Light
├── Global Volume
├── XR Interaction Manager
├── XR Origin (XR Rig)
├── XR Interaction Simulator
├── Floor                         (existente, fuera de Room)
└── Room
    ├── Structure
    │   ├── Floor
    │   ├── Ceiling
    │   ├── Wall_North
    │   ├── Wall_South
    │   ├── Wall_East
    │   └── Wall_West
    ├── Furniture
    │   ├── Bed
    │   ├── Nightstand
    │   ├── Wardrobe
    │   ├── Desk
    │   ├── Television
    │   ├── Mirror
    │   └── CoatRack
    ├── Architecture
    │   ├── Door
    │   ├── Window_01
    │   └── Window_02
    ├── Props
    │   └── GrabCube
    └── Lighting
        └── RoomLight
```

No se agregó un segundo `Global Volume`, ni se recrearon los objetos XR. `GrabCube` ya no aparece como raíz: su `Transform.m_Father` corresponde a `Room/Props`.

### 3.2 Responsabilidad de cada grupo

| Grupo          | Responsabilidad                                                                                    |
| -------------- | -------------------------------------------------------------------------------------------------- |
| `Structure`    | Geometría principal y colliders de la habitación: piso, techo y paredes.                           |
| `Furniture`    | Mobiliario visual placeholder. Los muebles no reciben `Rigidbody` ni `XRGrabInteractable`.         |
| `Architecture` | Representaciones visuales de puerta y ventanas. No se abren huecos ni se implementa funcionalidad. |
| `Props`        | Objetos de prueba o elementos sueltos. Actualmente contiene `GrabCube`.                            |
| `Lighting`     | Organización de la iluminación propia de la habitación. Contiene `RoomLight`.                      |

Todos los grupos aparecen con posición local `(0, 0, 0)`, rotación local identidad y escala local `(1, 1, 1)`. `Room` también es una raíz con esos valores.

## 4. Flujo completo de generación

### 4.1 Por qué no se editó manualmente el YAML

Una escena `.unity` es un asset serializado por Unity. Aunque su representación es YAML legible, editarla a mano puede romper referencias `fileID`, `guid`, prefabs, componentes o datos específicos de la versión del Editor. El procedimiento elegido crea objetos mediante API oficial y permite que Unity serialice correctamente la escena.

### 4.2 Qué hizo OpenCode y qué hizo Unity

| Acción                                                   | Responsable                                                                                                                        |
| -------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------- |
| Inspeccionar el repositorio y comprobar el estado de Git | OpenCode, mediante herramientas de inspección.                                                                                     |
| Crear `Assets/Editor/TesisRoomBuilder.cs`                | OpenCode, escribiendo el archivo autorizado.                                                                                       |
| Importar el script y generar su `.meta`                  | Unity Editor.                                                                                                                      |
| Compilar el script y registrar errores de compilación    | Unity Editor; el resultado debe revisarse en Console. No existe un log de compilación persistente en el repositorio inspeccionado. |
| Ejecutar el menú o el método batch                       | Unity Editor, cuando el usuario lo ejecuta.                                                                                        |
| Crear `GameObject`, primitivas, componentes y luz        | La herramienta, ejecutada dentro del dominio de Unity Editor.                                                                      |
| Crear los `.mat` y sus `.meta`                           | `AssetDatabase` y Unity.                                                                                                           |
| Serializar `SampleScene.unity`                           | `EditorSceneManager.SaveScene`.                                                                                                    |
| Ver la habitación en Play Mode y probar XR               | Usuario en Unity. No se deduce de los archivos.                                                                                    |

Durante la primera tentativa batch registrada en la sesión de implementación, Unity devolvió un error porque el proyecto estaba abierto en otra instancia. Posteriormente el estado actual contiene los materiales y la escena generada; los archivos prueban el resultado serializado, pero no sustituyen una prueba de Play Mode.

### 4.3 Secuencia de `TesisRoomBuilder`

1. Abre `Assets/Scenes/SampleScene.unity` con `EditorSceneManager.OpenScene`.
2. Comprueba que la escena sea válida, esté cargada y tenga la ruta esperada.
3. Crea o reutiliza los seis materiales bajo `Assets/Materials/TesisRoom`.
4. Busca o crea `Room` como objeto raíz.
5. Crea o reutiliza los grupos `Structure`, `Furniture`, `Architecture` y `Props`.
6. Crea o actualiza las primitivas de estructura, mobiliario y arquitectura.
7. Reubica `GrabCube` dentro de `Room/Props` si todavía es una raíz.
8. Crea o actualiza `Lighting/RoomLight`.
9. Marca la escena como modificada y la guarda.
10. Guarda y refresca los assets.
11. Escribe un resumen en Console.

## 5. Explicación detallada de `TesisRoomBuilder.cs`

### 5.1 `using` y estructura general

El archivo se encuentra en `Assets/Editor/TesisRoomBuilder.cs`. Su clase es `public static`, por lo que no necesita un componente en un GameObject ni una instancia en la escena.

```csharp
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
```

`System` aporta `Exception` e `InvalidOperationException`. `UnityEditor` aporta `MenuItem`, `AssetDatabase`, `EditorUtility` y otras APIs que solo deben compilarse en una carpeta `Editor`. `UnityEditor.SceneManagement` aporta `EditorSceneManager`. `UnityEngine` aporta `GameObject`, `Transform`, `PrimitiveType`, `Material`, `Light`, `Collider` y `Vector3`. `UnityEngine.SceneManagement` aporta `Scene` y `SceneManager`.

Si se eliminara `UnityEditor`, el script no podría declarar el menú ni crear assets. Si se eliminara `UnityEngine.SceneManagement`, no podría validar ni mover objetos a la escena objetivo.

### 5.2 Clase, constantes y menú

```csharp
public static class TesisRoomBuilder
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string MaterialsPath = "Assets/Materials/TesisRoom";
    private const string RoomName = "Room";
    private const float RoomCenterZ = -16.22f;
```

Las constantes concentran las rutas, el nombre administrado y el centro Z de la habitación. Evitan repetir cadenas de ruta, aunque las dimensiones y muchas posiciones siguen siendo literales dentro de `BuildRoom`.

```csharp
[MenuItem("Tools/Tesis VR/Build Placeholder Room")]
private static void BuildRoomFromMenu()
```

`MenuItem` registra una entrada en el menú del Editor. El método estático privado puede invocarse desde el menú, pero no es el método usado por `-executeMethod`.

### 5.3 Ejecución desde el menú y por línea de comandos

```csharp
try
{
    BuildRoom();
    EditorUtility.DisplayDialog("Tesis VR", "SampleScene: habitación placeholder construida correctamente.", "OK");
}
catch (Exception exception)
{
    Debug.LogException(exception);
    EditorUtility.DisplayDialog("Tesis VR - Error", exception.Message, "OK");
}
```

La ejecución del menú muestra un diálogo de éxito o error y registra la excepción completa. El diálogo ayuda al usuario del Editor; `Debug.LogException` conserva el stack trace.

```csharp
public static void BuildRoomFromCommandLine()
{
    BuildRoom();
}
```

El método es público y estático porque Unity busca métodos de ese formato al usar `-executeMethod`. No captura la excepción: si falla, el proceso batch puede terminar con error claro.

### 5.4 Apertura y validación de la escena

```csharp
Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
    throw new InvalidOperationException("No se pudo abrir correctamente Assets/Scenes/SampleScene.unity.");
```

La herramienta no depende de que el usuario haya abierto previamente la escena correcta. `OpenSceneMode.Single` sustituye la escena abierta por `SampleScene`. La comprobación evita continuar con una escena incorrecta o no cargada. El riesgo evitado es crear una habitación en otra escena.

### 5.5 Creación de jerarquía

```csharp
Transform room = GetOrCreateRoot(scene, RoomName);
SetIdentity(room);

Transform structure = GetOrCreateChild(room, "Structure");
Transform furniture = GetOrCreateChild(room, "Furniture");
Transform architecture = GetOrCreateChild(room, "Architecture");
Transform props = GetOrCreateChild(room, "Props");
```

`Room` se busca solo entre las raíces de la escena. Los grupos se buscan como hijos directos mediante `Transform.Find`. Esto limita la administración a la jerarquía de la habitación y no afecta objetos XR de otras ramas.

````csharp
### 5.6 Búsqueda y reutilización de grupos

```csharp
private static Transform GetOrCreateRoot(Scene scene, string objectName)
{
    foreach (GameObject rootObject in scene.GetRootGameObjects())
        if (rootObject.name == objectName)
            return rootObject.transform;

    GameObject created = new GameObject(objectName);
    SceneManager.MoveGameObjectToScene(created, scene);
    return created.transform;
}
````

`GetRootGameObjects` evita una búsqueda global repetida. Si no existe `Room`, `new GameObject` crea un contenedor vacío y `MoveGameObjectToScene` garantiza que pertenezca a la escena abierta.

```csharp
private static Transform GetOrCreateChild(Transform parent, string objectName)
{
    Transform existing = parent.Find(objectName);
    if (existing != null)
        return existing;

    GameObject created = new GameObject(objectName);
    created.transform.SetParent(parent, false);
    return created.transform;
}
```

El segundo parámetro `false` conserva una configuración local controlada al establecer la relación padre-hijo. Si el grupo ya existe, se reutiliza y no se duplica.

```csharp
private static void SetIdentity(Transform transform)
{
    transform.localPosition = Vector3.zero;
    transform.localRotation = Quaternion.identity;
    transform.localScale = Vector3.one;
}
```

Esta función hace que los contenedores no introduzcan desplazamientos, rotaciones o escalas adicionales. Las posiciones de los hijos quedan interpretables directamente en coordenadas de la escena.

### 5.7 Creación o actualización de primitivas

Una llamada representativa es:

```csharp
CreatePlaceholder(
    structure,
    "Floor",
    PrimitiveType.Cube,
    new Vector3(0f, -0.05f, RoomCenterZ),
    new Vector3(3.94f, 0.1f, 4f),
    materials.Floor,
    true);
```

`PrimitiveType.Cube` indica a Unity que use la malla cúbica integrada. La escala se aplica sobre una primitiva cuyo tamaño base es aproximadamente un metro por eje. El último argumento indica si el collider debe estar habilitado.

La función auxiliar es:

```csharp
Transform existing = parent.Find(objectName);
GameObject placeholder = existing != null
    ? existing.gameObject
    : GameObject.CreatePrimitive(primitiveType);
placeholder.name = objectName;
placeholder.transform.SetParent(parent, false);
placeholder.transform.SetPositionAndRotation(worldPosition, Quaternion.identity);
placeholder.transform.localScale = scale;
```

En la primera ejecución se crea la primitiva. En ejecuciones posteriores se reutiliza el hijo directo con ese nombre y se actualizan sus propiedades. `GameObject.CreatePrimitive` añade automáticamente la malla, `MeshFilter`, `MeshRenderer` y el collider predeterminado de la primitiva.

El nombre `worldPosition` es descriptivo, pero el script no hace una conversión matemática explícita. Como `Room` y sus grupos se fuerzan a identidad y `Room` es raíz en el estado actual, esas coordenadas coinciden con las coordenadas mundiales. Si un futuro cambio coloca `Room` en otra posición, esta implementación debería revisarse antes de reutilizarla.

````csharp
### 5.8 Materiales y colliders

```csharp
MeshRenderer renderer = placeholder.GetComponent<MeshRenderer>();
if (renderer != null)
    renderer.sharedMaterial = material;

Collider collider = placeholder.GetComponent<Collider>();
if (colliderEnabled && collider == null)
    placeholder.AddComponent<BoxCollider>();
else if (collider != null)
    collider.enabled = colliderEnabled;
````

`MeshRenderer.sharedMaterial` asigna el asset compartido sin crear una copia de material por objeto. Para los objetos visuales, el collider predeterminado se conserva pero se desactiva; no se elimina. Para estructura y muebles con colisión se conserva o se añade un `BoxCollider` si falta.

El método no añade `Rigidbody` ni `XRGrabInteractable` a los muebles. Por tanto, son geometría estática con colisión, no objetos físicos agarrables.

```csharp
foreach (GameObject rootObject in scene.GetRootGameObjects())
{
    if (rootObject.name != "GrabCube")
        continue;

    Transform grabCube = rootObject.transform;
    grabCube.SetParent(props, true);
    grabCube.position = new Vector3(0.55f, 0.30f, -15.00f);
    return;
}
```

### 5.9 Reubicación de `GrabCube`

La herramienta busca `GrabCube` solamente entre las raíces. Si lo encuentra, cambia solo su padre y su posición. El `true` de `SetParent` conserva temporalmente la posición mundial durante el cambio de padre; inmediatamente después se aplica la posición objetivo. No se obtiene ni se modifica ningún componente XR.

Una consecuencia importante es que, si `GrabCube` ya está dentro de `Room/Props`, esta función no lo vuelve a buscar recursivamente. En el estado actual eso no produce duplicados ni modifica sus componentes, pero significa que la reubicación automática está diseñada principalmente para el estado inicial en que era raíz.

### 5.10 Iluminación

```csharp
Transform lighting = GetOrCreateChild(room, "Lighting");
Transform lightTransform = GetOrCreateChild(lighting, "RoomLight");
Light light = lightTransform.GetComponent<Light>();
if (light == null)
    light = lightTransform.gameObject.AddComponent<Light>();
light.type = LightType.Point;
light.range = 8f;
light.intensity = 2f;
light.color = new Color(1f, 0.92f, 0.82f);
```

Se crea una `Point Light` cálida para hacer visible el placeholder. La `Directional Light` existente no es tocada. `RoomLight` está en `(0, 2.4, -16.22)`, tiene rango `8` e intensidad `2` en la serialización actual.

### 5.11 Creación y reutilización de materiales

```csharp
string path = MaterialsPath + "/" + materialName + ".mat";
Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
if (material == null)
{
    Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
    material = new Material(shader) { name = materialName };
    AssetDatabase.CreateAsset(material, path);
}
```

Primero se intenta cargar el `.mat`. Si no existe, se busca el shader URP Lit y se crea un asset. El fallback a `Standard` existe en el código, aunque el proyecto usa URP y los materiales actuales serializan el shader URP Lit con GUID `933532a4fcc9baf4fa0491de14d08ed7`.

```csharp
material.color = color;
if (material.HasProperty("_BaseColor"))
    material.SetColor("_BaseColor", color);
EditorUtility.SetDirty(material);
```

Se actualiza el color genérico y, cuando está disponible, la propiedad URP `_BaseColor`. `SetDirty` indica a Unity que el asset debe guardarse.

### 5.12 Guardado, refresco y errores

```csharp
EditorSceneManager.MarkSceneDirty(scene);
if (!EditorSceneManager.SaveScene(scene))
    throw new InvalidOperationException("Unity no pudo guardar SampleScene.unity.");

AssetDatabase.SaveAssets();
AssetDatabase.Refresh();
Debug.Log("TesisRoomBuilder: SampleScene construida. Room: Structure (6), Furniture (7), Architecture (3), Props y Lighting/RoomLight. GrabCube reubicado si existía.");
```

La escena se marca como modificada antes de guardarla. `SaveAssets` persiste los materiales y `Refresh` actualiza la ventana Project. El mensaje resume el resultado. Las excepciones de apertura, shader o guardado detienen la ejecución; el menú además las muestra en un diálogo.

## 6. Idempotencia

### 6.1 Definición

Idempotencia significa que repetir el generador deja el mismo estado final, en lugar de crear copias nuevas.

### 6.2 Administración y límites

- Cada grupo se busca como hijo directo por nombre.
- Cada objeto administrado se busca dentro de su grupo por nombre.
- Si el objeto existe, se actualizan posición, rotación, escala, material y estado del collider.
- Si no existe, se crea.
- La herramienta no borra hijos desconocidos dentro de `Room`.
- La herramienta no busca ni reconfigura globalmente el XR Origin, el Simulator, el Manager ni el `Floor` raíz.

```text
Primera ejecución:
SampleScene -> se crea Room -> se crean grupos -> se crean primitivas y materiales.

Segunda ejecución:
SampleScene -> se reutiliza Room -> se reutilizan grupos y nombres -> se actualizan transforms.
```

El objetivo es poder regenerar el placeholder después de ajustar el script sin producir `Room (1)`, `Bed (1)` o materiales duplicados.

La idempotencia no implica que el script sea un reconciliador universal: si un usuario cambia el tipo de una primitiva existente, el método no la reemplaza; si añade objetos ajenos dentro de `Room`, no los elimina; y `MoveGrabCube` solo revisa raíces.

## 7. Generación de primitivas

### 7.1 Tipos y conceptos

- `Cube`: piso, techo, paredes, muebles, televisor, espejo, puerta y ventanas.
- `Cylinder`: `CoatRack`.
- `GameObject` vacío: `Room`, grupos y `Lighting`.
- `Point Light`: `RoomLight`, que es un `GameObject` con componente `Light` de tipo `Point`.

### 7.2 Tabla de objetos y colliders

| Objeto            | Primitiva |      Posición `(x,y,z)` |     Escala `(x,y,z)` | Grupo        | Collider                    |
| ----------------- | --------- | ----------------------: | -------------------: | ------------ | --------------------------- |
| `Floor` de `Room` | Cube      |    `(0, -0.05, -16.22)` | `(3.94, 0.10, 4.00)` | Structure    | BoxCollider activo          |
| `Ceiling`         | Cube      |     `(0, 2.85, -16.22)` | `(3.94, 0.10, 4.00)` | Structure    | BoxCollider activo          |
| `Wall_North`      | Cube      |     `(0, 1.40, -18.17)` | `(3.94, 2.80, 0.10)` | Structure    | BoxCollider activo          |
| `Wall_South`      | Cube      |     `(0, 1.40, -14.27)` | `(3.94, 2.80, 0.10)` | Structure    | BoxCollider activo          |
| `Wall_East`       | Cube      |  `(2.02, 1.40, -16.22)` | `(0.10, 2.80, 4.00)` | Structure    | BoxCollider activo          |
| `Wall_West`       | Cube      | `(-2.02, 1.40, -16.22)` | `(0.10, 2.80, 4.00)` | Structure    | BoxCollider activo          |
| `Bed`             | Cube      |     `(0, 0.25, -17.20)` | `(1.40, 0.50, 1.70)` | Furniture    | BoxCollider activo          |
| `Nightstand`      | Cube      |  `(1.10, 0.30, -17.20)` | `(0.40, 0.60, 0.40)` | Furniture    | BoxCollider activo          |
| `Wardrobe`        | Cube      | `(-1.65, 0.90, -15.45)` | `(0.50, 1.80, 0.80)` | Furniture    | BoxCollider activo          |
| `Desk`            | Cube      | `(-0.75, 0.40, -14.70)` | `(1.00, 0.80, 0.55)` | Furniture    | BoxCollider activo          |
| `Television`      | Cube      |  `(0.95, 1.20, -14.36)` | `(0.80, 0.55, 0.08)` | Furniture    | BoxCollider desactivado     |
| `Mirror`          | Cube      | `(-1.91, 1.30, -17.00)` | `(0.08, 1.00, 0.55)` | Furniture    | BoxCollider desactivado     |
| `CoatRack`        | Cylinder  |  `(1.65, 0.75, -14.65)` | `(0.12, 0.75, 0.12)` | Furniture    | Collider de Cylinder activo |
| `Door`            | Cube      | `(-1.30, 1.00, -18.10)` | `(0.70, 2.00, 0.08)` | Architecture | BoxCollider desactivado     |
| `Window_01`       | Cube      |  `(1.91, 1.35, -16.20)` | `(0.08, 1.00, 1.10)` | Architecture | BoxCollider desactivado     |

La escena inspeccionada confirma, por ejemplo, el `BoxCollider` activo de `Wardrobe`, el `BoxCollider` desactivado de `Television` y el `BoxCollider` desactivado de `Door`. Los muebles no tienen `Rigidbody` en las entradas serializadas que corresponden a las primitivas generadas.

## 8. Materiales generados

### 8.1 Materiales reales y asignación

| Material               | Ruta                                                  | GUID                               | Color `_BaseColor`       | Uso                                                  |
| ---------------------- | ----------------------------------------------------- | ---------------------------------- | ------------------------ | ---------------------------------------------------- |
| `Mat_Floor`            | `Assets/Materials/TesisRoom/Mat_Floor.mat`            | `341d580de2170784abc3961176fe2740` | `(0.38, 0.40, 0.42, 1)`  | `Structure/Floor`                                    |
| `Mat_Wall`             | `Assets/Materials/TesisRoom/Mat_Wall.mat`             | `37747c8714cff4340b12094d56d49bcc` | `(0.88, 0.86, 0.78, 1)`  | `Ceiling` y paredes                                  |
| `Mat_Wood`             | `Assets/Materials/TesisRoom/Mat_Wood.mat`             | `bee818be81ba2c24c95247f079851ad1` | `(0.42, 0.23, 0.12, 1)`  | `Nightstand`, `Wardrobe`, `Desk`, `Door`, `CoatRack` |
| `Mat_Dark`             | `Assets/Materials/TesisRoom/Mat_Dark.mat`             | `56a5a58be67164049a8f6284056ca668` | `(0.035, 0.04, 0.05, 1)` | `Television`                                         |
| `Mat_GlassPlaceholder` | `Assets/Materials/TesisRoom/Mat_GlassPlaceholder.mat` | `5a99b6c7cd9796444bc2e0ec7638ec90` | `(0.45, 0.75, 0.85, 1)`  | `Mirror`, `Window_01`, `Window_02`                   |
| `Mat_Bed`              | `Assets/Materials/TesisRoom/Mat_Bed.mat`              | `1a8e18c0ebfd19a40847fa67cd17e160` | `(0.28, 0.38, 0.48, 1)`  | `Bed`                                                |

En `SampleScene.unity`, los `MeshRenderer` apuntan a esos GUID mediante `m_Materials`. Por ejemplo, el `Floor` de `Room` usa `341d580de2170784abc3961176fe2740`, `Wardrobe` usa `bee818be81ba2c24c95247f079851ad1` y `Television` usa `56a5a58be67164049a8f6284056ca668`.

### 8.2 Material, shader, renderer, `.mat` y `.meta`

- **Material:** asset con propiedades visuales que usa un Renderer.
- **Shader:** programa que determina cómo se transforma la geometría en píxeles; en este caso es el shader Lit de URP.
- **Renderer:** componente del GameObject que dibuja una malla y recibe uno o más materiales.
- **`sharedMaterial`:** referencia al mismo asset de material. Cambiar el asset afecta a todos los renderers que lo comparten.
- **`.mat`:** archivo serializado del material.
- **`.meta`:** archivo auxiliar de Unity que conserva el GUID y datos del importer.
- **GUID:** identificador estable que permite resolver referencias incluso si cambia el nombre o la carpeta del asset.

## 9. Construcción de la habitación

### 9.1 Dimensiones y posiciones

- ancho X: `3.94 m`;
- profundidad Z: `4.00 m`;
- altura Y: `2.80 m`;
- grosor de piso, techo y paredes: `0.10 m`.

- `Room/Structure/Floor`: posición `(0, -0.05, -16.22)`, escala `(3.94, 0.10, 4.00)`, con `BoxCollider`.

1. el origen y altura del XR Origin;
2. la altura efectiva de `Main Camera` después del tracking;
3. el piso nuevo en Y `-0.05`;
4. el piso heredado, que está en una posición y escala completamente distintas;
5. la posición del simulador, que en la instancia tiene una modificación aproximada `(0, 1.87, -18.45)`;
6. la diferencia entre el origen de seguimiento XR y el origen geométrico de la habitación.

### 9.2 Relación con XR Origin y causa probable de flotación

## 10. Conservación del sistema XR

La herramienta conserva `XR Origin`, controladores, locomoción, `XR Interaction Simulator`, `XR Interaction Manager` e Input Actions. No recrea ni modifica esos objetos; solo integra la geometría alrededor del sistema XR existente.

- `XR Origin (XR Rig)` está aproximadamente en `(0, 1, -16.224968)`.
- `Main Camera` y `Camera Offset` están contenidos en el prefab;
- controladores izquierdo y derecho;
- interactors de proximidad, rayos y teletransporte contenidos en el prefab;
- locomoción y acciones de movimiento del prefab;
- `XR Interaction Simulator`;
- `XR Interaction Manager`;
- Input Actions y referencias del paquete.

## 11. GrabCube

### 11.1 Componentes comprobados

| Componente           | Estado                                                                                                                                                                                   |
| -------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Transform`          | Posición `(0.55, 0.30, -15.00)`, escala `(1,1,1)`, padre `Room/Props`.                                                                                                                   |
| `MeshFilter`         | Malla integrada de Cube, `fileID: 10202`.                                                                                                                                                |
| `MeshRenderer`       | Activo. Usa una referencia de material con GUID `31321ba15b8f8eb4c954353edc038b1d`; no se encontró un `.meta` de proyecto con ese GUID, por lo que no se atribuye un nombre de material. |
| `BoxCollider`        | Activo, tamaño `(1,1,1)`, no trigger.                                                                                                                                                    |
| `Rigidbody`          | Masa `1`, gravedad activa, `isKinematic: 0`, sin restricciones.                                                                                                                          |
| `XRGrabInteractable` | Activo. Referencia a `XR Interaction Manager`, interaction layer bits `1`, movimiento dinámico y lanzamiento configurado.                                                                |

### 11.2 Flujo de agarre

1. El simulador genera controladores simulados.
2. Los interactors del XR Origin detectan interactables mediante rayos o interacción cercana.
3. `XR Interaction Manager` coordina hover y selección.
4. `XRGrabInteractable` permite seleccionar y seguir el controlador.
5. `Rigidbody` permite movimiento físico, gravedad y velocidad de lanzamiento.
6. Al soltar, el objeto conserva la respuesta física configurada.

## 12. DontDestroyOnLoad

### 12.1 Qué significa y qué se verificó

`DontDestroyOnLoad` es un contenedor especial de runtime para objetos que sobreviven al cambio de escena. No forma parte de la jerarquía serializada de `SampleScene`. La inspección no encontró llamadas textuales ni logs que identifiquen el objeto real observado, por lo que no se inventa su nombre o función.

### 12.2 Revisión manual durante Play Mode

1. Presionar `Play`.
2. En la ventana `Hierarchy`, localizar y expandir `DontDestroyOnLoad`.
3. Seleccionar cada hijo y observar el nombre, prefab source y componentes en `Inspector`.
4. Revisar si contiene un manager, controlador simulado, sistema de UI o singleton de un paquete.
5. Detener `Play Mode` y confirmar que el objeto desaparece y que la escena editada no recibe hijos nuevos.

## 13. EventSystem

`EventSystem` coordina eventos de UI. No es el responsable directo de colisiones ni agarres, pero puede ser necesario para la interfaz del simulador. No aparece como raíz serializada en `SampleScene`; sí existe en `Starter Assets/DemoScene.unity`. El origen exacto del objeto visto durante Play Mode no puede determinarse con los archivos disponibles.

Debe revisarse durante Play Mode junto con `InputSystemUIInputModule` y los módulos XR UI. No debe eliminarse sin comprobar su origen.

## 14. Mensajes de advertencia háptica

El mensaje reportado `Failed to get haptic capabilities of XRSimulatedController...` indica que el simulador no pudo consultar capacidades de vibración de hardware simulado. Un warning no equivale automáticamente a un error y no impide necesariamente la selección de un interactable. No hay un log persistente para comprobar ocurrencias o stack trace; la conclusión se limita al comportamiento reportado.

## 15. Validación realizada

### Comprobado en archivos

- Jerarquía `Room` y grupos.
- Seis materiales URP y sus `.meta`.
- Componentes físicos y XR de `GrabCube`.
- Instancias serializadas de los objetos XR originales.

### Pendiente

- Visibilidad de controladores simulados.
- Agarre en Play Mode.
- Locomoción, teletransporte y colisiones.
- Errores rojos de Console.
- Validación con visor físico.

- **No comprobado:** el objeto exacto que aparece en `DontDestroyOnLoad` durante Play Mode.
- **Revisión recomendada:** expandir el objeto en runtime y revisar `EventSystem`, `InputSystemUIInputModule`, módulos XR UI y referencias de acciones.

## 16. Cómo reconstruir la Fase 1 desde cero

1. Instalar Unity `6000.3.20f1`.
2. Crear o abrir un proyecto con URP.
3. Usar URP `17.3.0`, Input System `1.19.0` y XR Interaction Toolkit `3.3.2`.
4. Importar los samples `Starter Assets` y `XR Interaction Simulator`.
5. Crear `SampleScene` con `XR Origin`, `XR Interaction Simulator`, `XR Interaction Manager` y `GrabCube`.
6. Colocar `TesisRoomBuilder.cs` en `Assets/Editor/` y esperar la compilación.
7. Ejecutar `Tools > Tesis VR > Build Placeholder Room`.
8. Probar la escena en Play Mode.
9. Como alternativa, cerrar Unity y ejecutar el comando batch incluido en la sección 4.

## 17. Archivos involucrados

| Archivo o carpeta              | Función                   | Estado                                    | Git | Importancia |
| ------------------------------ | ------------------------- | ----------------------------------------- | --- | ----------- |
| `TesisRoomBuilder.cs`          | Generador idempotente.    | Creado.                                   | Sí. | Alta.       |
| `SampleScene.unity`            | Escena principal.         | Existente y contiene Room.                | Sí. | Alta.       |
| `Assets/Materials/TesisRoom/`  | Materiales placeholder.   | Creada con seis `.mat`.                   | Sí. | Alta.       |
| Archivos `.meta`               | GUID e importación.       | Generados por Unity.                      | Sí. | Alta.       |
| Prefabs XR de `Assets/Samples` | XR Origin y Simulator.    | Existentes, no modificados.               | Sí. | Alta.       |
| `AGENTS.md`                    | Reglas del proyecto.      | Existente, no modificado.                 | Sí. | Alta.       |
| `Packages/manifest.json`       | Dependencias y versiones. | Existente, no modificado.                 | Sí. | Alta.       |
| `ProjectSettings` relevantes   | Unity y Build Settings.   | Existentes, no modificados por el manual. | Sí. | Alta.       |

## 18. Glosario

- **GameObject:** objeto base de una escena.
- **Component:** módulo agregado a un GameObject.
- **Transform:** posición, rotación, escala y jerarquía.
- **Scene:** conjunto serializado de objetos y referencias.
- **Prefab:** plantilla reutilizable.
- **Primitive:** geometría integrada como Cube o Cylinder.
- **Collider:** forma de colisión.
- **Rigidbody:** simulación física.
- **Interactor:** sistema XR que busca o selecciona.
- **Interactable:** objeto XR interactuable.
- **XR Origin:** raíz de cámara y tracking.
- **XR Interaction Manager:** coordinador XR.
- **XR Interaction Simulator:** simulación de dispositivos XR.
- **Material:** propiedades visuales de un Renderer.
- **Shader:** programa de dibujo del material.
- **GUID:** identificador estable de asset.
- **`.meta`:** archivo de GUID e importación.
- **UnityEditor:** APIs de edición.
- **`MenuItem`:** registro de menú del Editor.
- **Idempotencia:** repetir sin duplicar el resultado.
- **Batch mode:** ejecución de Unity sin interfaz.
- **`DontDestroyOnLoad`:** persistencia de runtime entre escenas.

## 19. Problemas conocidos y siguiente paso

La habitación aparece reportada como visualmente flotante. También existen advertencias hápticas reportadas, posible diferencia de coordenadas y dos objetos `Floor` con posiciones y escalas distintas. El contenido exacto de `DontDestroyOnLoad` y del `EventSystem` runtime no puede verificarse desde los archivos disponibles.

El siguiente paso recomendado es únicamente alinear correctamente la habitación, el piso y el XR Origin. No se describe ni se implementa la Fase 2.

- un **Error** indica una falla que normalmente requiere atención y puede interrumpir una operación;
- la ausencia de háptica en un simulador no implica ausencia de detección de colliders o selección del interactable.

- Existen los grupos `Structure`, `Furniture`, `Architecture`, `Props` y `Lighting`.
- Existen las seis primitivas estructurales, siete muebles y tres elementos arquitectónicos definidos por la herramienta.
- Existe `RoomLight` como `Point Light` con rango `8` e intensidad `2`.
- Existen los seis materiales en `Assets/Materials/TesisRoom/`.
- La escena contiene `XR Origin (XR Rig)`, `XR Interaction Simulator` y `XR Interaction Manager`.
- `GrabCube` tiene `MeshFilter`, `MeshRenderer`, `BoxCollider`, `Rigidbody` y `XRGrabInteractable`.
- El `Floor` raíz y el nuevo `Room/Structure/Floor` coexisten.

- Controladores simulados visibles: requiere prueba actual en Play Mode.
- `GrabCube` agarrable: requiere prueba actual en Play Mode; la configuración serializada es compatible con esa interacción.
- Ausencia o presencia de errores rojos: no existe una Console serializada ni log persistente inspeccionable.
- Advertencias hápticas: reportadas, pero sin log local para validar detalle.
- Locomoción completa.
- Colisión con todas las paredes y muebles.
- Teletransporte.
- Ajuste de escala y distribución.
- Validación con visor físico.
- Cualquier integración narrativa de la Fase 2.

2. Crear o abrir un proyecto con URP.
3. Usar URP `17.3.0`, Input System `1.19.0` y XR Interaction Toolkit `3.3.2`.
4. Importar desde Package Manager los samples necesarios del XR Interaction Toolkit, incluidos `Starter Assets` y `XR Interaction Simulator`.
5. Crear `SampleScene` y añadir una instancia de `XR Origin (XR Rig)`.
6. Añadir una instancia de `XR Interaction Simulator`.
7. Añadir un `XR Interaction Manager`.
8. Crear un `Cube` de prueba con `BoxCollider`, `Rigidbody` y `XRGrabInteractable`, o usar el `GrabCube` equivalente.
9. Colocar `Assets/Editor/TesisRoomBuilder.cs` en la carpeta `Assets/Editor/`.
10. Esperar a que Unity importe y compile el script.
11. Abrir `SampleScene` y ejecutar `Tools > Tesis VR > Build Placeholder Room`.
12. Comprobar la jerarquía, materiales y Console.
13. Como alternativa, cerrar Unity antes de usar batch y ejecutar:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\isvar\Documents\tesis 1" -executeMethod TesisRoomBuilder.BuildRoomFromCommandLine -logFile "C:\Users\isvar\AppData\Local\Temp\opencode\TesisRoomBuilder.log"
```

14. Abrir `SampleScene`, presionar `Play`, confirmar el simulador, probar movimiento, colisiones y agarre.
15. Detener Play Mode y revisar que no haya cambios runtime persistentes en la escena.

|---|---|---|---|---|
| `Assets/Editor/TesisRoomBuilder.cs` | Herramienta idempotente de construcción. | Creado para la fase. | Sí, debe versionarse. | Alta. |
| `Assets/Editor/TesisRoomBuilder.cs.meta` | GUID del script. | Generado por Unity. | Sí. | Alta para referencias del asset. |
| `Assets/Editor/BuildRoomWizard.cs` | Herramienta anterior independiente. | Existente, no es el generador documentado. | Sí, si pertenece al proyecto. | Referencia histórica; evitar confundir menús. |
| `Assets/Scenes/SampleScene.unity` | Escena principal con XR y Room. | Existente, actualmente contiene la habitación. | Sí. | Alta. |
| `Assets/Scenes/SampleScene.unity.meta` | GUID de la escena principal. | Existente. | Sí. | Alta para Build Settings. |
| `Assets/Scenes/Habitacion.unity` | Escena separada no incluida en Build Settings. | Existente, no usada. | Sí. | Contexto, no modificar para Fase 1. |
| `Assets/XR_Simulator_Test.unity` | Escena XR de prueba separada. | Existente, no administrada por el generador. | Sí. | Referencia/prueba. |
| `Assets/Materials/TesisRoom/*.mat` | Seis materiales URP del placeholder. | Creados y existentes. | Sí. | Alta para diferenciación visual. |
| `Assets/Materials/TesisRoom/*.mat.meta` | GUID de cada material. | Generados por Unity. | Sí. | Alta para referencias de escena. |
| `XR Origin (XR Rig).prefab` | Cámara, controladores, interactors, locomoción y acciones XR. | Sample del paquete, no modificado. | Sí, como dependencia/sample. | Alta. |
| `XR Interaction Simulator.prefab` | Controladores y entradas simuladas. | Sample del paquete, no modificado. | Sí, como dependencia/sample. | Alta. |
| `AGENTS.md` | Reglas y contexto de arquitectura del proyecto. | Existente, inspeccionado. | Sí. | Guía de alcance. |
| `Packages/manifest.json` | Versiones y dependencias. | Existente, no modificado por esta fase. | Sí. | Determina APIs disponibles. |
| `ProjectSettings/EditorBuildSettings.asset` | Lista de escenas de compilación. | Existente, solo `SampleScene`. | Sí. | Define la escena principal. |
| `ProjectSettings/ProjectVersion.txt` | Versión exacta de Unity. | Existente. | Sí. | Reproducibilidad. |

- **Component:** módulo que aporta comportamiento o datos a un GameObject.
- **Transform:** componente que define posición, rotación, escala y jerarquía.
- **Scene:** conjunto serializado de GameObjects, componentes y referencias.
- **Prefab:** plantilla reutilizable de GameObjects y componentes.
- **Primitive:** geometría integrada de Unity, como Cube o Cylinder.
- **Collider:** forma física usada para detección de colisiones.
- **Rigidbody:** componente que incorpora simulación física a un objeto.
- **Interactor:** sistema XR que busca, apunta o selecciona interactables.
- **Interactable:** objeto XR que puede recibir hover, selección o interacción.
- **XR Origin:** raíz que relaciona cámara, tracking y controladores con el mundo Unity.
- **XR Interaction Manager:** coordinador de interactors e interactables.
- **XR Interaction Simulator:** sistema que simula dispositivos XR mediante teclado, ratón o Input Actions.
- **Material:** conjunto de propiedades visuales que usa un Renderer.
- **Shader:** programa de GPU que define cómo se dibuja un material.
- **GUID:** identificador único y persistente de un asset.
- **`.meta`:** archivo de Unity que conserva GUID e información de importación.
- **UnityEditor:** namespace de APIs disponibles durante edición, no gameplay final.
- **`MenuItem`:** atributo que registra una función en el menú del Editor.
- **Idempotencia:** propiedad de obtener el mismo estado final al repetir una operación.
- **Batch mode:** ejecución de Unity sin interfaz mediante argumentos de línea de comandos.
- **`DontDestroyOnLoad`:** mecanismo de runtime para conservar objetos al cambiar de escena.

- Coexisten el `Floor` raíz heredado y `Room/Structure/Floor`, con posiciones, mallas y escalas diferentes.
- El XR Origin tiene raíz en Y `1` y la cámara posee un offset local Y `1.36144`; estos valores deben compararse con la cara superior del piso.
- El simulador tiene una posición serializada diferente a la del centro geométrico de la habitación.
- Las advertencias hápticas de `XRSimulatedController` están reportadas, pero no hay log local suficiente para determinar su alcance.
- `DontDestroyOnLoad` y el `EventSystem` observados durante Play Mode no pueden identificarse con certeza a partir de los archivos disponibles.
- `BuildRoomWizard.cs` permanece como herramienta anterior con otro menú y otra jerarquía de nombres; no debe confundirse con `TesisRoomBuilder`.
