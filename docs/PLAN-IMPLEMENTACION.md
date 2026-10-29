# Victus Charge Control â€” Plan de implementaciÃ³n

> **Para quien ejecute:** sub-skill requerida: `superpowers:executing-plans`. Los pasos usan
> checkbox (`- [x]`) para seguimiento.

**Goal:** App WPF de bandeja que monitorea la baterÃ­a del Victus y alerta al salir del intervalo
40â€“80 %, sin prometer ni intentar detener la carga.

**Architecture:** `Core` (net8.0, sin UI) concentra modelo, zonas, motor de alertas, configuraciÃ³n,
historial y parseo de `batteryreport`; `App` (WPF) aporta fuentes WinRT/WMI/powercfg, bandeja,
notificaciones y vistas. Toda la lÃ³gica decide con datos inyectados, por lo que es testeable sin
hardware.

**Tech Stack:** C# 12, .NET 8, WPF, WinForms `NotifyIcon`, `System.Management`, xUnit.

**Spec:** `docs/ESPECIFICACION.md`

## Global Constraints

- EspaÃ±ol en toda la UI y en los mensajes de notificaciÃ³n.
- Sin administrador, sin drivers, sin escrituras a BIOS/EC/ACPI/registro de hardware.
- `null` significa "no disponible"; nunca se muestra como `0`.
- La app **no** detiene la carga y debe decirlo en UI, alertas y documentaciÃ³n.
- Sin suspensiÃ³n/hibernaciÃ³n automÃ¡tica en esta versiÃ³n.
- Target: `net8.0-windows10.0.19041.0`, `win-x64`, publicaciÃ³n autocontenida.

## Review Focus

1. **BaterÃ­a ausente o `Percent` nulo** (equipo de escritorio, driver roto): la UI y el motor deben
   seguir vivos mostrando "â€”", sin excepciones ni alertas falsas.
2. **OscilaciÃ³n en el umbral** (79 â†” 80 â†” 81): no debe producir una notificaciÃ³n por muestra.
3. **ReanudaciÃ³n tras suspensiÃ³n**: el evento puede llegar antes que el hardware; hay que releer y
   no confiar en el Ãºltimo valor cacheado.
4. **`powercfg` lento o bloqueado** (equipo sin baterÃ­a, polÃ­tica de empresa): debe tener timeout y
   degradar a "no disponible" sin bloquear la UI.
5. **Cierre con historial pendiente**: los eventos en bÃºfer deben volcarse al salir sin bloquear la
   salida si el disco falla.

---

### Task 1: Modelo y zonas

**Files:** `src/VictusChargeControl.Core/Model/*.cs`, `tests/.../ChargeZoneTests.cs`

**Interfaces:**
- Produce: `BatteryReading`, `BatteryChargeState`, `BatterySource`, `ChargeZone`, `ChargeZoneCalculator.GetZone(int? percent, int lower, int upper)`.

- [x] Test: 80/81 â†’ `AboveUpper`, 40/39 â†’ `BelowLower`, 79/41 â†’ `Within`, `null` â†’ `Unknown`.
- [x] Ejecutar: falla por tipo inexistente.
- [x] Implementar modelo + calculadora (zona superior tiene prioridad si los umbrales se cruzan).
- [x] Ejecutar: verde.

### Task 2: Motor de alertas

**Files:** `src/.../Alerts/AlertEngine.cs`, `AlertThresholds.cs`, `BatteryAlert.cs`, `AlertText.cs`

**Interfaces:**
- Consume: `BatteryReading`, `ChargeZoneCalculator`.
- Produce: `AlertThresholds(int Upper, int Lower, bool UpperEnabled, bool LowerEnabled)`,
  `AlertEngine.Evaluate(BatteryReading) -> IReadOnlyList<BatteryAlert>`, `AlertEngine.Reset()`,
  `AlertEngine.CurrentZone`.

- [x] Tests: alerta Ãºnica al entrar en `AboveUpper` con AC; no repetir en la misma zona; rearmar
      tras bajar a 78 y volver a 80; alerta inferior solo descargando; alertas desactivadas no
      disparan; lectura sin % no dispara ni rearma; `NotPresent` no dispara.
- [x] Ejecutar: rojo.
- [x] Implementar armado/desarmado con histÃ©resis 1 y puertas por AC/descarga.
- [x] Ejecutar: verde.

### Task 3: ConfiguraciÃ³n

**Files:** `src/.../Settings/AppSettings.cs`, `SettingsStore.cs`

- [x] Tests: valores por defecto 80/40; guardar y releer; JSON corrupto â†’ defaults; valores fuera de
      rango se normalizan (upper 20â€“100, lower 20â€“100, `upper - lower >= 5`, poll 10â€“300); escritura
      atÃ³mica deja archivo vÃ¡lido.
- [x] Implementar con `System.Text.Json` y `File.Replace`.
- [x] Verde.

### Task 4: Historial

**Files:** `src/.../History/HistoryEntry.cs`, `HistoryRecorder.cs`, `HistoryStore.cs`

- [x] Tests: registra primera muestra; no repite con mismo estado/AC/zona; registra cambio de estado;
      registra salto de 5 %; agrupa escrituras (no escribe antes de 60 s ni con < 10 pendientes);
      `LoadRecent` respeta el tope; archivo corrupto se ignora sin romper.
- [x] Implementar recorder + store JSONL.
- [x] Verde.

### Task 5: Parseo de `batteryreport`

**Files:** `src/.../BatteryReport/BatteryReportParser.cs`

- [x] Tests: XML real de este equipo â†’ 64988/64988/101; XML sin ciclos â†’ `null`; XML invÃ¡lido â†’ `null`.
- [x] Implementar con `XDocument` y namespace `http://schemas.microsoft.com/battery/2012`.
- [x] Verde.

### Task 6: Notificaciones y autoarranque (contratos)

**Files:** `src/.../Notifications/INotifier.cs`, `NotificationRequest.cs`, `src/.../Startup/IAutoStart.cs`

- [x] Tests de `AlertText` (mensajes contienen el % y la frase "no detiene la carga").
- [x] Implementar contratos + textos.

### Task 7: App â€” fuentes de datos

**Files:** `src/VictusChargeControl.App/Platform/WinRtBatterySource.cs`, `SystemPowerStatusSource.cs`,
`WmiBatteryDetailSource.cs`, `PowerCfgReportProvider.cs`, `BatteryMonitor.cs`

- [x] Implementar mapeo de `BatteryStatus` WinRT â†’ `BatteryChargeState`, cÃ¡lculo de % con
      `remaining/full`, signo de potencia, y fusiÃ³n con WMI solo cuando WinRT no da potencia.
- [x] `BatteryMonitor`: `ReportUpdated` + `DispatcherTimer` 30 s + relectura en `Resume`, sin consultas
      redundantes (una lectura en vuelo a la vez).

### Task 8: App â€” UI, bandeja, composiciÃ³n

**Files:** `App.xaml`, `MainWindow.xaml(.cs)`, `SettingsWindow.xaml(.cs)`, `ViewModels/*`, `Tray/TrayIconHost.cs`,
`Platform/ShellNotifier.cs`, `Platform/RunKeyAutoStart.cs`, `Platform/PowerModeWatcher.cs`

- [x] Tema oscuro, gauge con banda objetivo, tarjetas de datos, banner de advertencia.
- [x] Bandeja con menÃº, doble clic, cerrarâ†’ocultar, "Salir" real.
- [x] NotificaciÃ³n shell + banner interno; globo alternativo.
- [x] Autoarranque por `HKCU\...\Run`.

### Task 9: PublicaciÃ³n y documentaciÃ³n

- [x] `dotnet publish -c Release -r win-x64 --self-contained true`.
- [x] `README.md` (instalaciÃ³n, desinstalaciÃ³n, diagnÃ³stico), `docs/LIMITACIONES.md`.
- [x] VerificaciÃ³n final: build sin warnings, tests verdes, ejecutable arranca.
