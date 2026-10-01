# Resonance — Documentación técnica del estado actual

Sistema de música adaptativa para un laberinto 3D en Unity. Una única canción completa representa el progreso del jugador por la ruta correcta: avanza cuando el jugador avanza, se distorsiona cuando se desvía y puede reproducirse hacia atrás cuando el jugador retrocede por la ruta correcta.

> **Estado:** prototipo funcional. Todo el código fue escrito sin poder compilarlo ni ejecutarlo en Unity en el entorno donde se generó; los valores por defecto hay que afinarlos a oído. Ver la sección [Limitaciones y problemas conocidos](#14-limitaciones-y-problemas-conocidos).

---

## Índice

1. [Qué es Resonance](#1-qué-es-resonance)
2. [Qué hemos construido](#2-qué-hemos-construido)
3. [Arquitectura general](#3-arquitectura-general)
4. [Waypoints y la ruta correcta](#4-waypoints-y-la-ruta-correcta)
5. [Detección de ruta correcta / incorrecta](#5-detección-de-ruta-correcta--incorrecta)
6. [Sistema de progreso](#6-sistema-de-progreso)
7. [La canción: reproducción hacia delante y hacia atrás](#7-la-canción-reproducción-hacia-delante-y-hacia-atrás)
8. [Audio Mixer y distorsión](#8-audio-mixer-y-distorsión)
9. [Comportamiento del sistema](#9-comportamiento-del-sistema)
10. [Relación entre movimiento, waypoints y música](#10-relación-entre-movimiento-waypoints-y-música)
11. [Scripts](#11-scripts)
12. [Cómo se conecta todo en Unity](#12-cómo-se-conecta-todo-en-unity)
13. [Configuración y cómo modificar el sistema](#13-configuración-y-cómo-modificar-el-sistema)
14. [Limitaciones y problemas conocidos](#14-limitaciones-y-problemas-conocidos)
15. [Alternativas discutidas (NO implementadas)](#15-alternativas-discutidas-no-implementadas)

---

## 1. Qué es Resonance

Resonance es una experiencia 3D en la que el jugador recorre un laberinto. La música comunica el progreso:

- Hay **una sola canción completa**, sin dividir en fragmentos.
- La posición de reproducción de esa canción representa el progreso del jugador en la **ruta correcta**.
- Si el jugador se equivoca de camino, la canción **sigue sonando**, pero se degrada (distorsión, filtro, reverb, volumen).
- Si el jugador retrocede físicamente por la ruta correcta, la canción **se reproduce hacia atrás** (60 s → 59 s → 58 s…), no salta ni se reinicia.
- Si vuelve de un camino incorrecto, la canción **continúa hacia delante** y la distorsión desaparece progresivamente.

El trabajo se ha centrado exclusivamente en el sistema de reproducción de la canción y el backtracking; el laberinto, el jugador y la cámara son del proyecto y no forman parte de este sistema.

---

## 2. Qué hemos construido

| Pieza | Función |
|---|---|
| `ReversibleSongPlayer` | Reproduce la canción completa hacia delante o hacia atrás con un único `AudioSource`. |
| `MazeRoute` | Representa la ruta correcta como una línea de waypoints y calcula el progreso del jugador sobre ella. |
| `ResonanceMusicDirector` | Cerebro: decide en cada frame la velocidad de reproducción y el nivel de distorsión. |
| `MusicDegradation` | Traduce un valor 0..1 de "distorsión" a varios parámetros del AudioMixer. |
| `SongPlayerTester` | Herramienta de prueba para validar el rewind sin laberinto. |
| `AudioMixer` (`MainMixer`) | Contiene los efectos que degradan la música. |

Los scripts de movimiento (`PlayerMovement`) y cámara (`MouseLook`) son del proyecto y usan el **Input System nuevo**. No se modificaron.

---

## 3. Arquitectura general

```
Posición del jugador
   └─► MazeRoute (proyecta al jugador sobre la línea de waypoints)
            │   progreso (m a lo largo de la ruta) + distancia lateral (m)
            ▼
     ResonanceMusicDirector
            ├─► velocidad de reproducción deseada (+ / − / 0) ─► ReversibleSongPlayer ─► AudioSource.pitch
            └─► "wrongness" 0..1 (distancia a la ruta) ───────► MusicDegradation ─────► AudioMixer
```

Idea clave: **el director nunca salta a una posición de la canción: calcula una velocidad.** La música se desplaza por su línea de tiempo con `pitch` positivo (adelante) o negativo (atrás), de modo que el movimiento siempre es continuo.

Orden de ejecución por frame (fijado con `[DefaultExecutionOrder]` para evitar un frame de desfase):

1. Movimiento del jugador (scripts del proyecto)
2. `ResonanceMusicDirector` (orden 100)
3. `MusicDegradation` (orden 200)
4. `ReversibleSongPlayer` (orden 300)

---

## 4. Waypoints y la ruta correcta

La ruta correcta se define con **waypoints**: objetos vacíos colocados en orden sobre el camino correcto del laberinto, del punto de salida a la meta.

- Se crean como hijos de un objeto `CorrectRoute` que lleva el componente `MazeRoute`.
- Se nombran por orden (`WP_00`, `WP_01`, …). El orden de la jerarquía es el orden de la ruta.
- Se coloca un waypoint en cada esquina. Entre waypoints, la ruta es una línea recta.
- Si el campo `Waypoints` de `MazeRoute` está vacío, usa automáticamente los hijos del objeto.
- Se trabaja solo en el **plano XZ** (la altura Y se ignora).
- En la vista Scene, la ruta se dibuja como líneas y esferas verdes (gizmos).

`MazeRoute` convierte esos puntos en una **polilínea con distancias acumuladas**. El *progreso* de un punto de la ruta es la distancia en metros desde el primer waypoint.

**Primer waypoint:** el director tiene un campo `First Waypoint` donde se arrastra el waypoint de inicio. La canción solo empieza cuando el jugador llega a él (ver [sección 9](#9-comportamiento-del-sistema)). Si se deja vacío, se usa el primer punto de `MazeRoute`.

---

## 5. Detección de ruta correcta / incorrecta

`MazeRoute.Project(posición, progresoMin, progresoMax)` proyecta la posición del jugador sobre cada segmento de la polilínea y devuelve:

- `progress`: metros a lo largo de la ruta del punto más cercano.
- `lateral`: distancia entre el jugador y la ruta.

**Ventana de búsqueda.** Solo se consideran tramos dentro de `[último progreso − searchWindow, último progreso + searchWindow]` (15 m por defecto). Así un pasillo contiguo, al otro lado de una pared, no se confunde con otra parte de la ruta.

**Estados** del director (`RouteState`):

| Estado | Significado |
|---|---|
| `Arming` | El jugador aún no ha llegado al primer waypoint. No suena nada. |
| `OnRoute` | El jugador está en la ruta correcta. |
| `OffRoute` | El jugador se ha desviado. |

**Histéresis.** Para evitar parpadeos entre `OnRoute` y `OffRoute` se usan dos radios distintos:

- Se **sale** de la ruta cuando `lateral > routeLeaveRadius` (2.5 m por defecto).
- Se **vuelve** a la ruta cuando `lateral ≤ routeEnterRadius` (1.5 m por defecto).

**Importante:** el estado `OnRoute/OffRoute` decide la lógica de la *canción*. La *distorsión* **no** depende del estado: se calcula directamente de la distancia lateral en cada frame (sección 8). Por eso la distorsión responde de inmediato y sin histéresis.

---

## 6. Sistema de progreso

El progreso del jugador es la distancia en metros a lo largo de la ruta (`progress`). Para convertirlo en un segundo de canción:

```csharp
float BaseMap(float progress)
{
    float k = route.progressToSong.Evaluate(route.Normalized(progress)); // 0..1
    return Mathf.Lerp(songStartTime, songEndTime, k);
}
```

- `songStartTime`: segundo de la canción en el inicio de la ruta.
- `songEndTime`: segundo de la canción en la meta.
- `progressToSong` (en `MazeRoute`): curva opcional para que el mapeo no sea lineal (por ejemplo, para anclar hitos musicales a puntos concretos).
- `referenceSpeed`: si es mayor que 0, el director calcula `songEndTime = songStartTime + longitudRuta / referenceSpeed`, de modo que a esa velocidad la canción suene a 1x. Si la canción es más corta que eso, lo limita y muestra un aviso en la Console.

**Posición esperada y desfase (`songOffset`).** La posición esperada de la canción en un punto es `BaseMap(progress) + songOffset`. El `songOffset` se fija en dos momentos:

1. Al llegar al primer waypoint: `songOffset = songStartTime − BaseMap(progreso)`.
2. Al volver a la ruta desde un desvío: `songOffset = song.CurrentTime − BaseMap(progreso)`.

En ambos casos se hace lo mismo: **anclar** la canción a donde esté sonando en ese instante. Así, la diferencia entre "donde debería estar" y "donde está" empieza siendo cero, y no se interpreta nada como retroceso.

---

## 7. La canción: reproducción hacia delante y hacia atrás

`ReversibleSongPlayer` usa **un solo `AudioSource`** con la canción completa.

### Cómo funciona
- La dirección y velocidad se controlan con `AudioSource.pitch`: positivo = adelante, negativo = atrás. `-1` equivale a `60 s → 59 s → 58 s…`.
- La posición se lee y se escribe con `timeSamples` (precisión de muestra). Se usa para el arranque y para leer la posición; **no se salta de posición mientras suena**, porque produce clics.
- **Nunca se llama a `Stop()`.** La canción siempre está en reproducción; solo cambia su velocidad.
- La velocidad deseada se suaviza (`SmoothDamp`, 0.05 s). Al cruzar por 0 se obtiene un efecto de "tape stop" en lugar de un cambio brusco.
- Cuando `|velocidad|` es menor que `fadeBelowRate`, el volumen se atenúa proporcionalmente. Evita zumbidos y clics al pausar o invertir. Una canción pausada queda en silencio.

### Decisiones técnicas importantes
- **`loop = true`** aunque la canción no se repite. Con `pitch` negativo y loop desactivado, Unity puede no reproducir el audio. El salto del loop se evita con un margen (`edgeMarginSeconds`, 0.25 s): al acercarse al inicio o al final se frena la velocidad.
- **Pitch mínimo de 0.01** conservando el signo (nunca exactamente 0).
- Rango de `pitch` en Unity: de −3 a 3.
- Al llegar al final se dispara el evento `SongEnded`.
- El `AudioSource` debe ser **2D** (`spatialBlend = 0`).

### Configuración recomendada del AudioClip
Esto fue una recomendación práctica, no algo probado en el proyecto:

- **Load Type:** Decompress On Load
- **Preload Audio Data:** activado
- **Force To Mono:** desactivado
- **Compression Format:** Vorbis (o PCM si hay memoria)
- No usar Streaming.

### Plataformas
En WebGL solo se admiten valores positivos de `pitch`, por lo que el reverse no funciona ahí.

---

## 8. Audio Mixer y distorsión

### Para qué se usa el AudioMixer
Para aplicar efectos a la música **sin modificar el audio original**. El `AudioSource` envía su salida al grupo `Music` de `MainMixer`, que contiene los efectos que degradan el sonido. Desde código se cambian sus valores mediante **parámetros expuestos**.

### Configuración
Grupo `Music` (hijo de `Master`) con estos efectos y parámetros expuestos (los nombres deben coincidir exactamente):

| Efecto | Parámetro expuesto | Limpio | Degradado |
|---|---|---|---|
| Attenuation (Volume) | `MusicVolume` | 0 dB | −9 dB |
| Lowpass Simple (Cutoff freq) | `MusicLowpass` | 22000 Hz | 700 Hz |
| Distortion (Distortion) | `MusicDistortion` | 0 | 0.5 |
| SFX Reverb (Wet Mix) | `MusicReverbWet` | −10000 | −1500 |

Los valores de ejemplo están definidos en `MusicDegradation.Reset()`. Las unidades del *Wet Mix* del reverb (mB) conviene verificarlas en el mixer y ajustarlas.

### Cómo se aplica y se quita la distorsión
Hay un único número, la **"wrongness"** (0 = música limpia, 1 = totalmente degradada), calculado en cada frame por el director a partir de la distancia lateral a la ruta:

```csharp
degradation.Target = Mathf.InverseLerp(safeRadius, fullWrongRadius, lateral);
```

- Hasta `safeRadius` (1 m): música limpia.
- Entre `safeRadius` y `fullWrongRadius` (4 m): distorsión creciente.
- A partir de `fullWrongRadius`: distorsión máxima.

`MusicDegradation` suaviza ese valor con una exponencial (sin inercia inicial) y lo reparte entre los parámetros del mixer, cada uno con su curva:

```csharp
float tau = Target > current ? attackSeconds : releaseSeconds;   // 0.08 s al empeorar, 0.25 s al recuperar
float k = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.001f, tau));
current = Mathf.Lerp(current, Mathf.Clamp01(Target), k);
```

Como el valor depende de la distancia, **la distorsión baja de forma progresiva a medida que el jugador se acerca a la ruta** y desaparece al volver a ella. No hay un "interruptor": todo es continuo.

---

## 9. Comportamiento del sistema

### Antes de empezar (`Arming`)
- No suena nada y la distorsión es 0.
- Cuando la distancia en el plano entre el jugador y el primer waypoint es menor o igual que `startTriggerRadius` (1.5 m), arranca la canción en `songStartTime` y se ancla el progreso (`songOffset`).
- Llegar al primer waypoint **no** se interpreta como retroceso: es la inicialización.

### Avanzar por la ruta correcta (`OnRoute`)
La velocidad de la canción depende del camino:

```csharp
float rate = localSlope * progressVelocity + Correction(err);
```

- `progressVelocity`: velocidad (m/s) con la que el jugador avanza **a lo largo de la ruta**, suavizada.
- `localSlope`: segundos de canción por metro en ese punto de la ruta.
- `Correction(err)`: corrección suave si la canción se desvía más de `syncDeadzone` (0.25 s) de la posición esperada.
- Jugador quieto = música en pausa. Jugador lento = música lenta. Jugador rápido = música rápida (hasta `maxForwardRate`).

### Retroceder por la ruta correcta (backtracking)
- Se considera retroceso real cuando `progressVelocity < −retreatSpeed` (0.1 m/s por defecto).
- Entonces la velocidad es negativa y la canción se reproduce **hacia atrás**, proporcional a la velocidad de retroceso (hasta `maxReverseRate`, 2.5).
- Si no hay retroceso real, la velocidad nunca es negativa: `rate = Mathf.Max(0f, rate)`.
- Al volver a avanzar, la canción continúa hacia delante desde donde quedó.

### Desviarse (`OffRoute`)
- La canción **sigue sonando hacia delante**, con velocidad proporcional al movimiento del jugador en el plano (`playerSpeed * songPerMeter`, limitada entre 0 y `maxForwardRate`). Si el jugador se queda quieto, la música se detiene.
- **Nunca hay reverse fuera de ruta**, aunque el jugador deshaga el camino.
- La distorsión aumenta según la distancia a la ruta.

### Regresar desde una ruta incorrecta
- Mientras el jugador se acerca a la ruta, la canción sigue hacia delante y la distorsión disminuye progresivamente.
- Al volver a `OnRoute` se **reancla**: `songOffset = song.CurrentTime − BaseMap(progreso)`. La canción continúa por donde estaba sonando, sin rebobinar.
- Consecuencia: tras un desvío, la canción queda **adelantada** respecto a la relación original metros ↔ segundos.

Resumen:

| Situación | Música | Distorsión |
|---|---|---|
| Ruta correcta, avanzando | Avanza | 0 |
| Ruta correcta, retrocediendo | Reverse | 0 |
| Desvío | Avanza (nunca reverse) | Sube con la distancia |
| Regreso de un desvío | Avanza, sin reverse | Baja progresivamente |
| Ruta correcta otra vez | Avanza desde donde estaba | 0 |

---

## 10. Relación entre movimiento, waypoints y música

1. **Waypoints** → definen la ruta correcta (polilínea) y dónde empieza.
2. **Posición del jugador** (plano XZ) → `MazeRoute.Project` la convierte en `progress` y `lateral`.
3. **`lateral`** → decide el estado (`OnRoute`/`OffRoute`) y la distorsión.
4. **`progress`** → se convierte en segundos de canción esperados y en velocidad a lo largo de la ruta (`progressVelocity`).
5. **Velocidad deseada** → `ReversibleSongPlayer` la aplica al `AudioSource.pitch`.
6. **Distorsión deseada** → `MusicDegradation` la aplica a los parámetros del AudioMixer.

---

## 11. Scripts

Todos viven en el espacio de nombres `Resonance` y en `Assets/Scripts/Resonance/`.

### `ReversibleSongPlayer.cs`
Reproductor de la canción completa. Va en `MusicSystem` (requiere `AudioSource`, que Unity añade solo).
- API: `Begin(float segundo)`, `SetDesiredRate(float)`, `Seek(float)`, `CurrentTime`, `CurrentRate`, `Length`, `IsRunning`, evento `SongEnded`.
- Detalles clave: ver [sección 7](#7-la-canción-reproducción-hacia-delante-y-hacia-atrás).

### `MazeRoute.cs`
Ruta correcta como polilínea. Va en `CorrectRoute`.
- `Rebuild()`: construye la polilínea y las distancias acumuladas a partir de los waypoints.
- `Project(posición, progresoMin, progresoMax, out Sample)`: devuelve `progress`, `lateral` y el punto proyectado, limitado a una ventana de la ruta.
- `Normalized(progress)`: progreso de 0 a 1. `Length`: longitud total. `StartPoint`: primer punto.
- `progressToSong`: curva de mapeo progreso → canción.

### `ResonanceMusicDirector.cs`
El cerebro. Va en `MusicDirector`. Contiene la máquina de estados (`Arming`, `OnRoute`, `OffRoute`), el mapeo ruta → canción, el cálculo de la velocidad y el cálculo de la distorsión.
- Incluye una línea de **debug** en pantalla (`showDebug`): estado, segundo de canción, velocidad, error, velocidad por la ruta, distancia lateral, segundos por metro y distorsión.
- Al seleccionarlo, dibuja una esfera cian con el radio de arranque alrededor del primer waypoint.

### `MusicDegradation.cs`
Convierte la distorsión 0..1 en parámetros del mixer. Va en `MusicSystem`.
- Cada parámetro (`MixerParam`) tiene: nombre expuesto, valor limpio, valor degradado, interpolación logarítmica opcional (útil para frecuencias) y una curva.

### `SongPlayerTester.cs`
Prueba del rewind **sin laberinto**. Usa el **Input System nuevo** (`Keyboard.current`): `Enter` inicia, `X` avanza, `Z` retrocede, `Shift` va al doble, sin tecla = pausa suave. **Debe estar desactivado o eliminado cuando se usa el director**, porque ambos controlan la velocidad.

### Partes importantes del código y por qué

| Decisión | Motivo |
|---|---|
| Controlar velocidad (`pitch`), no saltar posición | Un salto mientras suena produce clics y rompe la continuidad. |
| `loop = true` + margen en los bordes | `pitch` negativo con loop desactivado puede no sonar. |
| Un solo `AudioSource`, nunca `Stop()` | Evita reinicios y mantiene la posición continua. |
| Fade por velocidad baja | Evita zumbidos al pausar o cruzar por 0. |
| Ventana de búsqueda en `Project` | Evita confundir pasillos paralelos con la ruta. |
| Histéresis en el estado de ruta | Evita parpadeo en el borde del radio. |
| Distorsión calculada de la distancia, sin estados | Respuesta inmediata y bajada progresiva al volver. |
| Suavizado exponencial en la distorsión | Responde desde el primer frame; `SmoothDamp` tiene inercia inicial. |
| Reanclar (`songOffset`) al llegar y al volver | Evita que un desfase se interprete como retroceso. |
| `Mathf.Max(0f, rate)` salvo retroceso real | Garantiza que solo el retroceso real produce reverse. |
| `DefaultExecutionOrder` | Evita un frame de retraso entre movimiento y audio. |

---

## 12. Cómo se conecta todo en Unity

### Jerarquía

```
Escena
├── Player (tus scripts: PlayerMovement, CharacterController)
│     └── Cámara (MouseLook)
├── CorrectRoute          ← MazeRoute
│     ├── WP_00           (primer waypoint)
│     ├── WP_01
│     └── …
├── MusicSystem           ← AudioSource + ReversibleSongPlayer + MusicDegradation
└── MusicDirector         ← ResonanceMusicDirector
```

### Conexiones en el Inspector

| Componente | Campo | Se asigna |
|---|---|---|
| `ReversibleSongPlayer` | Clip | La canción |
| `AudioSource` | Output | Grupo `Music` de `MainMixer` |
| `MusicDegradation` | Mixer | `MainMixer` |
| `ResonanceMusicDirector` | Player | El objeto del jugador |
| `ResonanceMusicDirector` | Route | `CorrectRoute` |
| `ResonanceMusicDirector` | Song | `MusicSystem` |
| `ResonanceMusicDirector` | Degradation | `MusicSystem` |
| `ResonanceMusicDirector` | First Waypoint | `WP_00` |

### Pasos de montaje
1. Copia los 5 scripts a `Assets/Scripts/Resonance/` y espera a que compile.
2. Configura el AudioClip (sección 7).
3. Crea `MusicSystem` con `ReversibleSongPlayer` (asigna el clip) y `MusicDegradation`.
4. **Prueba primero con `SongPlayerTester`** (enlaza su campo `Song` a `MusicSystem`): confirma que avanza y retrocede. Después desactívalo.
5. Crea `MainMixer` con el grupo `Music` y los cuatro efectos; expón y renombra los parámetros (sección 8); asigna el `Output` del `AudioSource` y el `Mixer` de `MusicDegradation`.
6. Crea `CorrectRoute` con `MazeRoute` y los waypoints hijos en orden.
7. Crea `MusicDirector` con `ResonanceMusicDirector` y rellena los campos.
8. Ajusta `Song Start Time` y `Song End Time` (o `Reference Speed`).
9. Pulsa Play y observa la línea de debug.

---

## 13. Configuración y cómo modificar el sistema

### Parámetros de `ResonanceMusicDirector` (valores por defecto)

| Parámetro | Defecto | Qué hace |
|---|---|---|
| `startTriggerRadius` | 1.5 | Distancia al primer waypoint que arranca la canción. |
| `songStartTime` / `songEndTime` | 0 / 150 | Segundos de canción en el inicio y en la meta. |
| `referenceSpeed` | 0 | Si > 0, calcula `songEndTime` para que a esa velocidad (m/s) la canción suene a 1x. |
| `routeEnterRadius` / `routeLeaveRadius` | 1.5 / 2.5 | Radios (m) para entrar y salir de la ruta. |
| `searchWindow` | 15 | Ventana (m) de búsqueda de la ruta alrededor del último progreso. |
| `safeRadius` / `fullWrongRadius` | 1 / 4 | Distancia (m) donde empieza y termina de crecer la distorsión. |
| `speedSmoothing` | 0.08 | Suavizado (s) de las velocidades medidas. Menor = reacciona antes, más sensible a temblores. |
| `retreatSpeed` | 0.1 | Velocidad (m/s) hacia atrás que cuenta como retroceso real. |
| `maxForwardRate` / `maxReverseRate` | 2 / 2.5 | Límites de velocidad de la canción. |
| `syncDeadzone` / `syncGain` / `maxCorrectionRate` | 0.25 / 2 / 1 | Corrección de posición en ruta. |

### Otros componentes

| Componente | Parámetro | Defecto |
|---|---|---|
| `ReversibleSongPlayer` | `rateSmoothSeconds` | 0.05 |
| | `fadeBelowRate` | 0.15 (se recomendó bajarlo a 0.05 para velocidades lentas) |
| | `edgeMarginSeconds` | 0.25 |
| | `maxAbsRate` | 3 |
| `MusicDegradation` | `attackSeconds` / `releaseSeconds` | 0.08 / 0.25 |

### Ajustes habituales
- **Distorsión demasiado pronto o tarde:** `safeRadius` (aprox. la mitad del ancho del pasillo) y `fullWrongRadius`.
- **Marca `OffRoute` en el camino correcto:** sube `routeLeaveRadius` y `routeEnterRadius`; revisa que los waypoints estén en el centro del pasillo.
- **Música muy lenta/rápida en ruta:** revisa `songEndTime` o `referenceSpeed` (ver [sección 14](#14-limitaciones-y-problemas-conocidos)).
- **Música se apaga a velocidades bajas:** baja `fadeBelowRate`.
- **Más o menos efecto:** edita los valores limpio/degradado y las curvas en `MusicDegradation`.
- **Añadir otro efecto:** expón su parámetro en el mixer y añádelo a la lista `parameters` con su nombre exacto.
- **Cambios de nombre de campos serializados:** Unity descarta los valores guardados y aplica los del código; revisa el Inspector.

### Notas de configuración del proyecto
- El proyecto usa el **Input System nuevo**. Los scripts de Resonance no leen `UnityEngine.Input` salvo que se use una versión antigua del tester, que daría `InvalidOperationException`.
- Para menos latencia de audio: **Project Settings → Audio → DSP Buffer Size → Best latency** (si hay chasquidos, volver a Good latency).

---

## 14. Limitaciones y problemas conocidos

Estos puntos son observaciones reales del estado actual:

1. **Falsos retrocesos.** El retroceso se detecta con la velocidad del progreso proyectado sobre la línea de waypoints. Si el jugador no camina exactamente sobre la línea (zigzag, esquinas, línea fuera del centro del pasillo), la proyección puede deslizarse o saltar hacia atrás y activar un reverse sin que el jugador retroceda. La detección de estar fuera de ruta (distancia lateral) es más estable y por eso la distorsión funciona bien.
2. **Velocidad de la canción atada a la longitud del recorrido.** La música suena a 1x solo si la duración de la canción coincide con el tiempo que tarda el jugador en recorrer la ruta a su velocidad. Con otra combinación suena más lenta, más rápida o en pausa. Por ejemplo, 1 m/s a 1x requeriría una ruta de tantos metros como segundos tenga el tramo de canción.
3. **El tono cambia con la velocidad.** La velocidad se controla con `pitch`, que también cambia el tono; un jugador lento oye música lenta y grave. El `AudioSource` nativo no ofrece time-stretch.
4. **Tras un desvío, la canción queda adelantada.** Al reanclar, la canción puede llegar a su final antes que el jugador.
5. **La distancia lateral es en línea recta.** Si dos pasillos van en paralelo a menos de `safeRadius`, un jugador en el pasillo equivocado no se distorsionará.
6. **Un atajo no se detecta como regreso.** Si un camino incorrecto vuelve a cruzar la ruta más allá de `searchWindow`, el sistema no lo reconoce.
7. **WebGL no soporta `pitch` negativo.**
8. **No probado en Unity.** El código fue escrito y revisado leyéndolo, no compilándolo ni ejecutándolo.
9. **Plan B no implementado.** Se planteó como alternativa dividir la canción en fragmentos automáticamente; no se ha escrito ningún código para eso.

---

## 15. Alternativas discutidas (NO implementadas)

La última conversación terminó con una propuesta aún sin implementar, anotada aquí solo para contexto. **Nada de esto existe en el código actual.**

- **La canción como reloj:** reproducirla siempre a 1x hacia delante desde el primer waypoint, y que su duración sea el tiempo límite para llegar a la meta. Eliminaría la dependencia entre metros y segundos.
- **Retroceso con registro:** anotar el segundo de la canción en cada punto de la ruta y, al retroceder de verdad, rebobinar hacia el segundo anotado de ese punto.
- **Detección de progreso por zonas:** sustituir la proyección sobre la línea por volúmenes trigger por tramo (o celdas del laberinto), de modo que el retroceso sea una decisión discreta y no un cálculo continuo.

La decisión pendiente es si el retroceso por la ruta correcta debe seguir rebobinando la música o eliminarse.