# Victus Charge Control

Aplicación ligera de bandeja para Windows que **mide** la batería de un HP Victus 15 y avisa
cuando la carga sale del intervalo configurado (por defecto **40 % – 80 %**).

> ### La aplicación no detiene la carga
> Este equipo no expone ninguna interfaz soportada para limitar la carga por software. La app
> lee, avisa y deja la decisión al usuario. La evidencia completa está en [`PLAN.md`](PLAN.md).
> `Adaptive Battery Extender` **no** es un umbral configurable.

| Puede | No puede |
|---|---|
| Leer % real, estado, AC y potencia | Cortar la carga ni la corriente alterna |
| Leer diseño, capacidad completa y ciclos (vía `powercfg`) | Cambiar BIOS, EC, ACPI o registros de hardware |
| Avisar al cruzar los umbrales | Suspender/hibernar el equipo |
| Guardar un historial de eventos | Funcionar si la app no está en ejecución |
| Arrancar con Windows, sin administrador | Configurar `Adaptive Battery Extender` |

---

## Equipo de referencia

Desarrollada y probada en un **HP Victus by HP Gaming Laptop 15-fb2xxx**:

| Dato | Valor |
|---|---|
| Modelo | HP Victus 15-fb2xxx (`A14MNUA#ABA`) |
| Placa base | HP 8C2F |
| Procesador | AMD Ryzen 5 8645HS |
| Gráficos | NVIDIA GeForce RTX 4050 Laptop GPU |
| BIOS | F.09 (2025-12-15) |
| Sistema | Windows 11 Home 26H2 (build 26300) |
| Batería | 64 988 mWh de diseño · 101 ciclos |

Es el equipo en el que se verificó que **no existe** un límite de carga configurable por software
(evidencia en [`PLAN.md`](PLAN.md)). En otros portátiles la lectura debería funcionar igual
(usa APIs estándar de Windows), pero no está validada.

---

## 1. Requisitos

- Windows 10 21H2+ o Windows 11, **x64**.
- **No requiere .NET instalado**: el ejecutable publicado es autocontenido.
- **No requiere administrador.**

## 2. Instalación

1. Copiá `dist\win-x64\VictusChargeControl.exe` a una carpeta propia, por ejemplo:
   `%LOCALAPPDATA%\Programs\VictusChargeControl\`.
2. Ejecutá el `.exe`. Se abre la ventana y aparece el icono en la bandeja.
3. Opcional: **Configuración → Iniciar con Windows** (se registra en
   `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, sin privilegios).

No hay instalador MSI: es un único ejecutable. Si movés el `.exe` con el autoarranque activo,
desactivá y volvé a activar la opción para que se actualice la ruta.

## 3. Uso

- **Ventana principal**: porcentaje, estado (cargando / descargando / conectado sin cargar /
  desconocido / sin batería), corriente, potencia, salud, diseño, capacidad completa, ciclos y
  una barra con el intervalo objetivo.
- **Cerrar (X) o minimizar** oculta la ventana: la app sigue en la bandeja.
- **Salir** (menú del icono o botón de la bandeja) termina el proceso.
- **Configuración** (⚙): umbrales superior/inferior, alertas por separado, notificaciones,
  frecuencia de consulta (15/30/60/120 s) e inicio con Windows.
- **Historial**: eventos registrados con fecha, %, estado, corriente y motivo.

Cuando el cargador sigue conectado y el porcentaje supera el umbral, la ventana muestra una
advertencia discreta recordando que la app **no** detiene la carga.

## 4. Cómo avisa

1. **Notificación del shell** (globo del icono de bandeja). En Windows 11 aparece como
   notificación del sistema; Asistente de concentración / No molestar puede silenciarla.
2. **Banner interno** en la ventana principal, siempre.
3. **Ventana flotante** abajo a la derecha si el icono no pudo mostrar la notificación
   (por ejemplo, si el shell se reinició).

Reglas anti-repetición: una alerta por **entrada** a la zona; para volver a avisar hay que salir
de la zona con **histéresis de 1 punto**. La alerta superior solo se emite con corriente
conectada; la inferior solo mientras la batería se descarga.

## 5. Archivos de datos

| Ruta | Contenido |
|---|---|
| `%APPDATA%\VictusChargeControl\settings.json` | Configuración (escritura atómica) |
| `%LOCALAPPDATA%\VictusChargeControl\history.jsonl` | Historial de eventos (máx. 5000 líneas) |
| `%LOCALAPPDATA%\VictusChargeControl\error.log` | Errores no controlados (diagnóstico) |

El historial es un **registro de eventos**, no un muestreo continuo: no hay datos mientras la
app estuvo cerrada o el equipo suspendido.

## 6. Diagnóstico

```powershell
# ¿Está corriendo?
Get-Process VictusChargeControl -ErrorAction SilentlyContinue

# ¿Está el autoarranque activo?
Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name VictusChargeControl

# Lectura de batería que usa la app (solo lectura)
Get-CimInstance -Namespace root\wmi -ClassName BatteryStatus |
  Select-Object PowerOnline, Charging, Discharging, ChargeRate, DischargeRate, RemainingCapacity

# Informe completo de Windows
powercfg /batteryreport

# Últimos errores de la app
Get-Content "$env:LOCALAPPDATA\VictusChargeControl\error.log" -Tail 40

# Últimos eventos del historial
Get-Content "$env:LOCALAPPDATA\VictusChargeControl\history.jsonl" -Tail 5
```

Restablecer la configuración: cerrar la app y borrar `settings.json`. El historial se puede
borrar del mismo modo.

Si la app no arranca: ejecutarla desde una terminal con `VictusChargeControl.exe` no muestra
consola (es una app de ventana); revisar `error.log` y el Visor de eventos
(`Aplicación` → `.NET Runtime`).

## 7. Desinstalación

1. Menú de la bandeja → **Salir**.
2. Configuración → desactivar **Iniciar con Windows** (o borrar el valor `VictusChargeControl`
   de `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`).
3. Borrar el `.exe` y, si se desea, las carpetas:
   - `%APPDATA%\VictusChargeControl`
   - `%LOCALAPPDATA%\VictusChargeControl`

No se instalan servicios, drivers ni tareas programadas.

## 8. Compilar y probar

Requiere .NET SDK 8.

```powershell
dotnet restore
dotnet build VictusChargeControl.sln -c Release
dotnet test tests\VictusChargeControl.Core.Tests -c Release

dotnet publish src\VictusChargeControl.App\VictusChargeControl.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true -p:DebugType=None `
  -o dist\win-x64
```

## 9. Estructura del código

```
VictusChargeControl.sln
src/
  VictusChargeControl.Core/          # sin UI, testeable
    Model/                           # BatteryReading, estados, zonas
    Alerts/                          # AlertEngine, textos, umbrales
    Settings/                        # AppSettings + SettingsStore (JSON atómico)
    History/                         # HistoryRecorder + HistoryStore (JSONL)
    BatteryReport/                   # parser del XML de powercfg
    Monitoring/                      # IBatterySource
    Notifications/  Startup/         # contratos INotifier / IAutoStart
  VictusChargeControl.App/           # WPF, bandeja, plataforma
    Platform/                        # WinRT, GetSystemPowerStatus, WMI, powercfg
    Services/                        # BatteryApp (composición), CrashLog, ToastWindow
    ViewModels/  Views/  Themes/     # UI oscura en español
    Tray/                            # NotifyIcon + notificaciones del shell
tests/VictusChargeControl.Core.Tests # 69 pruebas xUnit
docs/ESPECIFICACION.md               # componentes y criterios de aceptación
docs/PLAN-IMPLEMENTACION.md          # plan ejecutado
docs/LIMITACIONES.md                 # limitaciones conocidas
PLAN.md                              # investigación de hardware (fuente de verdad)
```

## 10. Limitaciones

Ver [`docs/LIMITACIONES.md`](docs/LIMITACIONES.md). Resumen: no hay control real de carga en
este modelo, las notificaciones dependen del shell de Windows y el historial no cubre el tiempo
en que la app estuvo cerrada.
