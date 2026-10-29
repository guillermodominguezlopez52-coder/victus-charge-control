# Especificación — Victus Charge Control (v1.0)

**Fecha:** 2026-10-29
**Referencia de hardware:** `PLAN.md` (Victus 15-fb2xxx, BIOS F.09)
**Alcance:** arquitectura **A** — monitor + alertas, sin hardware externo.
**Stack:** C# · .NET 8 · WPF · Windows 11 · aplicación de bandeja.

---

## 1. Propósito

Aplicación residente en la bandeja que informa el estado real de la batería y avisa cuando la
carga sale del intervalo configurado (por defecto **40 % – 80 %**).

**La aplicación no detiene la carga.** No existe interfaz soportada en este equipo para hacerlo
(ver `PLAN.md` §5). La app mide, avisa y deja la decisión al usuario.

## 2. No objetivos

- Cortar la carga, controlar la AC, ni hablar con la EC/BIOS/ACPI.
- Suspender, hibernar o apagar automáticamente.
- Escribir ajustes de BIOS (`Adaptive Battery Extender` incluido).
- Requerir administrador, drivers o servicios.
- Inventar métricas: si una API no entrega un dato, se muestra como **no disponible**.

## 3. Componentes

| Componente | Responsabilidad | Proyecto |
|---|---|---|
| `BatteryReading` | Instantánea inmutable y normalizada (%, estado, potencia, AC, fuente, hora) | Core |
| `IBatterySource` | Contrato de lectura (`ReadAsync`) | Core |
| `WinRtBatterySource` | Fuente principal: `Windows.Devices.Power.Battery` | App |
| `SystemPowerStatusSource` | Respaldo: `GetSystemPowerStatus` | App |
| `WmiBatteryDetailSource` | Detalle eléctrico opcional: `root\wmi:BatteryStatus` | App |
| `PowerCfgReportProvider` | Diseño / carga completa / ciclos vía `powercfg /batteryreport /xml` | App + Core (parser) |
| `BatteryMonitor` | Orquesta fuentes, evento + polling, relectura tras reanudación | App |
| `ChargeZoneCalculator` | Traduce % a zona (`AboveUpper`, `Within`, `BelowLower`, `Unknown`) | Core |
| `AlertEngine` | Decide alertas con armado/desarmado e histéresis | Core |
| `AlertText` | Mensajes en español, honestos sobre el alcance | Core |
| `SettingsStore` | `settings.json` con validación y escritura atómica | Core |
| `HistoryRecorder` | Decide qué evento es relevante y agrupa escrituras | Core |
| `HistoryStore` | Persistencia JSONL con tope de entradas | Core |
| `INotifier` / `ShellNotifier` | Notificación del shell + banner interno de respaldo | Core / App |
| `IAutoStart` / `RunKeyAutoStart` | Alta/baja en `HKCU\...\Run` | Core / App |
| `MainViewModel`, `MainWindow`, `SettingsWindow` | UI oscura en español | App |
| `TrayIconHost` | Icono, menú, minimizar a bandeja | App |

## 4. Dependencias

- **.NET 8** (`net8.0` para Core/tests, `net8.0-windows10.0.19041.0` para la app).
- **WPF** + **WinForms** (solo `NotifyIcon`), incluidos en el SDK.
- **WinRT** `Windows.Devices.Power` (proyección del SDK de Windows).
- **System.Management** (NuGet) para WMI.
- **xUnit** + `Microsoft.NET.Test.Sdk` (solo tests).
- Sin paquetes de terceros en producción más allá de `System.Management`.

## 5. Modelo de datos

```csharp
record BatteryReading(
    int? Percent,                      // null = desconocido
    BatteryChargeState State,          // Unknown | Charging | Discharging | AcConnectedIdle | NotPresent
    bool? AcConnected,                 // null = desconocido
    double? PowerWatts,                // + carga, - descarga, null = no disponible
    BatterySource Source,              // WinRt | SystemPowerStatus | Wmi | Unknown
    DateTimeOffset Timestamp);

record BatteryHealthInfo(int? DesignCapacityMWh, int? FullChargeCapacityMWh, int? CycleCount);
```

Regla: **`null` ≠ 0**. Un valor ausente nunca se muestra como `0`.

## 6. Reglas de alerta

- Umbral superior por defecto **80 %**; inferior **40 %**; ambos configurables (20–100).
- Zona: `>= superior` → `AboveUpper`; `<= inferior` → `BelowLower`; resto → `Within`.
- Alerta superior solo si hay AC conectado; alerta inferior solo si la batería se está descargando.
- Una alerta por **entrada** a la zona. Para volver a dispararse hay que salir de la zona con
  **histéresis de 1 punto**.
- Lecturas sin porcentaje no disparan nada y **no** rearman las alertas.
- Cada alerta es desactivable por separado.
- Texto explícito: *"Esta alerta no detiene la carga."*

## 7. Persistencia

- Configuración: `%APPDATA%\VictusChargeControl\settings.json` (escritura atómica, validada).
- Historial: `%LOCALAPPDATA%\VictusChargeControl\history.jsonl`, tope 5000 entradas.
- Se registra un evento cuando cambia estado, AC o zona, o cuando el % varía ≥ 5 puntos.
- Escrituras agrupadas: mínimo 60 s entre volcados, o 10 eventos pendientes, o al cerrar.
- El historial se etiqueta como **registro de eventos**, no como muestreo continuo.

## 8. Integración con Windows

- Arranque opcional en `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- Sin elevación.
- `ReportUpdated` de WinRT como fuente de eventos; polling de 30 s como respaldo.
- `PowerModeChanged(Resume)` → relectura inmediata y reinicio del temporizador.
- Notificación: `NotifyIcon.ShowBalloonTip` (shell de Windows) + banner y globo alternativos si
  el shell la suprime.

## 9. Criterios de aceptación

1. El porcentaje mostrado coincide con el de Windows (±1 punto).
2. Se distingue cargando / descargando / AC sin carga / desconocido / sin batería.
3. Potencia, diseño, capacidad completa y ciclos muestran `—` cuando la fuente no los entrega.
4. Al cruzar el 80 % en AC se emite **una** notificación; no se repite mientras siga arriba.
5. Al bajar de 79 % y volver a 80 % se vuelve a notificar (histéresis).
6. Al cruzar el 40 % descargando se notifica una vez.
7. Datos desconocidos no generan alertas ni rompen la app.
8. Con AC y % > umbral se muestra la advertencia de que la app no detiene la carga.
9. Cerrar la ventana minimiza a bandeja; "Salir" termina el proceso.
10. Arranque con Windows activable/desactivable desde la UI.
11. Tras suspensión/reanudación la app vuelve a leer y sigue alertando.
12. Historial sobrevive reinicios y no registra duplicados en cada polling.
13. Compila sin advertencias y todas las pruebas pasan.
14. Publicación `win-x64` autocontenida que corre sin instalar .NET.
