# Tesis 1 — Unity XR Project

## Stack
- **Unity 6000.3.20f1** (Unity 6), URP 17.3.0
- **XR Interaction Toolkit** 3.3.2, Input System 1.19.0
- **Visual Scripting** 1.9.11 available; user's own C# scripts go in `Assets/Scripts/Core/`

## Scenes
- Build settings include only `Assets/Scenes/SampleScene.unity`
- `Assets/Scenes/Habitacion.unity` exists but is NOT in build settings

## OpenCode config
- `Assets/opencode.json` — Ollama local at `http://localhost:11434/v1`, model `ollama/gemma3:4b`

## Contexto del proyecto
Reconstrucción en Unity VR de la tesis: *"Ambientes de la esquizofrenia con Realidad Virtual para fomentar empatía en personas no padecientes"*. Demo funcional mediante cinco vertical slices:

1. Habitación base.
2. Interacción básica con el frasco de pastillas y la nota.
3. Alucinación auditiva mediante voces.
4. Alucinaciones visuales, brazos, silueta y cambio narrativo de la nota.
5. Integración del flujo completo.

No se priorizan modelos 3D realistas — usar primitivas, materiales simples y placeholders reemplazables sin cambiar la arquitectura.

## Objetivo
- Construir primero una demo funcional y verificable.
- Implementar una sola fase a la vez.
- No adelantar funcionalidades de fases futuras.
- Mantener cada fase jugable al finalizar.

## Tecnologías detectadas
- Conservar exactamente las versiones actuales del proyecto.
- No actualizar Unity ni paquetes.
- Usar XR Interaction Toolkit existente.
- Usar el Input System existente.
- No utilizar Visual Scripting salvo autorización expresa.
- Implementar la lógica propia en C#.

## Arquitectura
- Aplicar SOLID, DRY y KISS cuando aporten valor real.
- Separar interacción, audio, alucinaciones, narrativa y estado global.
- Evitar scripts monolíticos.
- Evitar dependencias directas innecesarias entre sistemas.
- Preferir eventos C# o UnityEvent configurado cuando sea apropiado.
- No crear managers, singletons o abstracciones sin necesidad inmediata.
- No implementar arquitectura correspondiente a fases futuras.

## Unity
- No modificar escenas sin explicar previamente los cambios.
- No eliminar GameObjects, prefabs, scripts ni assets existentes sin autorización.
- No modificar ProjectSettings ni Packages/manifest.json sin autorización.
- No instalar paquetes.
- No renombrar escenas o carpetas existentes sin autorización.
- Mantener compatibilidad con XR Device Simulator porque no se dispone actualmente de un visor físico.

## Demo visual
- Usar primitivas de Unity y materiales simples.
- Priorizar escala, posición, navegación, interacción y flujo.
- No dedicar tiempo a texturizado, retopología o realismo.
- Los placeholders deben poder reemplazarse posteriormente por modelos de Blender.

## Código
- Scripts propios en `Assets/Scripts/Core/` o en subcarpetas por responsabilidad.
- Usar `[SerializeField]` para referencias privadas configuradas desde Inspector.
- Evitar `FindObjectOfType`, búsquedas repetidas y números mágicos.
- Validar referencias y mostrar errores claros.
- Métodos y clases con una responsabilidad definida.
- Comentarios solo cuando expliquen una decisión no evidente.

## Pruebas
- Cada fase debe incluir criterios de aceptación verificables.
- Comprobar errores de compilación y consola antes de declarar una tarea terminada.
- Probar siempre con XR Device Simulator.
- No afirmar que algo funciona sin indicar cómo fue comprobado.
- Si OpenCode no puede ejecutar Unity, debe dar instrucciones manuales exactas para validar el resultado en el Editor.

## Flujo obligatorio de trabajo

Para cada solicitud:

1. Inspeccionar el estado actual del proyecto.
2. Resumir lo encontrado.
3. Proponer un plan pequeño.
4. Indicar qué archivos se crearán o modificarán.
5. Esperar aprobación antes de implementar, salvo que el usuario solicite explícitamente ejecución directa.
6. Implementar solamente el alcance aprobado.
7. Resumir los cambios.
8. Proporcionar pasos exactos para probarlos en Unity.
9. Proponer un mensaje de commit.

## Git
- No hacer commit ni push automáticamente.
- No cambiar de rama automáticamente.
- No modificar `.gitignore` salvo necesidad justificada.
- No incluir Library, Temp, Logs, Obj, Build, Builds o UserSettings.
