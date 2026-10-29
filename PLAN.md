# PLAN.md — Estudio de viabilidad: Victus Charge Control

**Fecha:** 2026-10-29
**Equipo inspeccionado:** HP Victus by HP Gaming Laptop 15-fb2xxx (`A14MNUA#ABA`, placa `8C2F`; número de serie omitido)
**CPU / GPU:** AMD Ryzen 5 8645HS / NVIDIA RTX 4050 Laptop
**BIOS:** F.09 (2025-12-15)
**SO:** Windows 11 Home 26H2, build 26300.9711
**Alcance de este documento:** viabilidad técnica previa al diseño. **No se escribió código de la aplicación.**

---

## 1. Veredicto

| Capacidad | Resultado | Evidencia |
|---|---|---|
| Consultar porcentaje y estado de carga | **Sí, viable y verificado** | 4 APIs probadas en este equipo |
| Mostrar alertas / notificaciones | **Sí, viable** | APIs estándar de Windows, sin admin |
| Detener físicamente la carga por software | **No disponible en este equipo** | Sin API, sin ajuste de BIOS, sin utilidad HP compatible |
| Fijar un límite configurable (ej. 80 %) | **No disponible** | El BIOS solo expone "Adaptive Battery Extender" on/off |
| Alternativa real para no cargar al 100 % | **Manual o hardware externo** | Desconectar el cargador o cortar la AC con una regleta inteligente |

**Conclusión corta:** la aplicación puede ser un **monitor y alertador de carga confiable**, pero **no puede prometer que detendrá la carga** en este modelo con BIOS F.09. Cualquier diseño que incluya un botón "detener carga" sin hardware externo sería engañoso.

---

## 2. Entorno verificado

| Dato | Valor | Cómo se obtuvo |
|---|---|---|
| Fabricante / modelo | HP / Victus 15-fb2xxx | `Win32_ComputerSystem` |
| Número de producto | `A14MNUA#ABA` | `Win32_ComputerSystem.SystemSKUNumber` |
| BIOS | `F.09`, 2025-12-15 | `Win32_BIOS`, `powercfg /batteryreport` |
| Placa base | `8C2F` | `Win32_BaseBoard` |
| SO | Windows 11 Home 26H2, build `26300.9711` | `Win32_OperatingSystem`, registro |
| Batería | Li-ion, `Primary`, HP | `powercfg /batteryreport` |
| Capacidad de diseño | **64 988 mWh** | `powercfg /batteryreport` |
| Capacidad de carga completa | **64 988 mWh** (salud ≈ 100 %) | `BatteryFullChargedCapacity`, WinRT |
| Ciclos | **101** | `BatteryCycleCount`, reporte |
| Sesión de inspección | **Sin privilegios de administrador** | `WindowsPrincipal.IsInRole(Administrator)` → `False` |

Todas las lecturas de batería y del BIOS HP funcionaron **sin elevación**. Esto es relevante para la arquitectura: la app no necesita un servicio privilegiado para leer.

Estado medido durante la inspección (varias muestras):

```
GetSystemPowerStatus : AC=0  BatteryFlag=1  Percent=81  BatteryLifeTime=11687 s
WinRT Battery        : 52 945 / 64 988 mWh  Status=Discharging  Rate=-16 308 mW
WMI BatteryStatus    : PowerOnline=False Charging=False Discharging=True
                       RemainingCapacity=53 006 mWh  Voltage=16 182 mV  DischargeRate=14 753 mW
Win32_Battery        : EstimatedChargeRemaining=81  BatteryStatus=1
```

---

## 3. APIs de consulta verificadas

### 3.1 `Windows.Devices.Power.Battery` (WinRT) — **recomendada como fuente principal**

- **Qué entrega:** `RemainingCapacityInMilliwattHours`, `FullChargeCapacityInMilliwattHours`,
  `Status` (`Charging` / `Discharging` / `Idle` / `NotPresent`), `ChargeRateInMilliwatts`,
  y el evento `ReportUpdated`.
- **Verificado en este equipo:** `52 945 / 64 988 mWh`, `Discharging`, `-16 308 mW`.
  La suscripción a `ReportUpdated` se registró correctamente sin admin.
- **Requisito:** TFM `net8.0-windows10.0.19041.0` (o superior) para las proyecciones WinRT.
- **Limitaciones:**
  - No entrega el porcentaje directamente: hay que calcularlo (`remaining / full`).
  - `FullChargeCapacity` es la capacidad **actual** a plena carga, no la de diseño; para "salud"
    hace falta la capacidad de diseño del reporte `powercfg`.
  - En PowerShell el *token* de desuscripción se maneja distinto; en C# es directo.

### 3.2 `GetSystemPowerStatus` (`kernel32.dll`) — **fallback liviano**

- **Qué entrega:** `ACLineStatus`, `BatteryFlag`, `BatteryLifePercent` (0–100),
  `BatteryLifeTime`, `BatteryFullLifeTime`.
- **Verificado:** `AC=0`, `81 %`, `11 687 s` restantes, `Full=-1`.
- **Ventajas:** sin dependencias, resolución de 1 %, ideal para *polling* barato o si WinRT falla.
- **Limitaciones:** `BatteryLifeTime` puede ser `-1`; no hay mWh ni tasa de carga.

### 3.3 WMI `root\wmi:BatteryStatus` — **detalle eléctrico**

- **Qué entrega:** `PowerOnline`, `Charging`, `Discharging`, `Critical`, `ChargeRate`,
  `DischargeRate`, `RemainingCapacity`, `Voltage`, `Tag`, `InstanceName`.
- **Verificado:** `PowerOnline=False`, `Discharging=True`, `RemainingCapacity=53 006`,
  `Voltage=16 182`, `DischargeRate=14 753`.
- **Evento:** existe la clase `root\wmi:BatteryStatusChange`
  (`Charging`, `Discharging`, `PowerOnline`, `Critical`, `Tag`), útil si se prefiere WMI puro.
- **Limitaciones:** más costoso que WinRT; los valores son instantáneos y pueden variar
  entre muestras; no siempre hay instancia para todas las clases
  (`BatteryStaticData` no devolvió instancia en este equipo).

### 3.4 `Win32_Battery` — **compatibilidad legacy**

- **Verificado:** `EstimatedChargeRemaining=81`, `BatteryStatus=1` (descargando),
  `Chemistry=6` (Li-ion), `DesignVoltage=15 954`.
- **Limitaciones:** sin capacidades ni tasa; `DesignCapacity` y `FullChargeCapacity` vacíos.
  Solo como *fallback* o para equipos muy antiguos.

### 3.5 `powercfg /batteryreport` — **salud, ciclos e historial**

- **Verificado:** diseño `64 988 mWh`, carga completa `64 988 mWh`, `101` ciclos.
- **Uso recomendado:** calcular salud (`full / design`) e historial de sesiones.
- **Limitaciones:** no es tiempo real; genera HTML/XML en disco; el parseo debe tolerar
  cambios de formato entre builds.

### 3.6 HP BIOS WMI (`root\HP\InstrumentedBIOS`) — **lectura opcional**

- **Proveedor:** `wbiosrvc.dll` (`Provider_BIOSInterface`, CLSID `D2D588B5-D081-11d0-99E0-00C04FC2F8EC`).
- **Qué entrega (verificado):** 124 instancias, **51 ajustes con nombre**, incluidos
  `BIOS Revision`, `Product Name`, `Product Number`, `Serial Number`, `System Board ID`
  y ajustes de batería (ver §5).
- **Ventaja:** identifica el equipo y el estado de la protección de batería de HP sin admin.
- **Limitación:** es una interfaz de gestión de HP; su disponibilidad varía por modelo y BIOS.
  La app debe **detectar y degradar** si el namespace no existe.

---

## 4. Cómo distinguir los estados de carga

| Estado real | Señales observables | Qué significa |
|---|---|---|
| **En batería** | `ACLineStatus=0`, `Status=Discharging`, `PowerOnline=False` | La laptop se alimenta de la batería |
| **En AC cargando** | `ACLineStatus=1`, `Status=Charging`, `PowerOnline=True`, `Charging=True` | El cargador está conectado y la carga avanza |
| **En AC sin cargar** | `ACLineStatus=1`, `Status=Idle`, `PowerOnline=True`, `Charging=False`, `Discharging=False` | El firmware decidió no cargar (límite, histéresis, temperatura o protección HP) |
| **Sin batería / no presente** | `Status=NotPresent`, sin instancia `BatteryStatus` | Equipo de escritorio o batería no detectada |

**Importante:** "AC conectado y sin cargar" es el **único** indicio observable de que el firmware
está limitando la carga. La app puede **leerlo**, pero **no puede provocarlo**.

---

## 5. ¿Se puede fijar un límite de carga? — cadena de evidencia

### 5.1 Lo que el BIOS F.09 sí expone

Consultando `root\HP\InstrumentedBIOS` (lectura, sin admin), los únicos ajustes relacionados
con batería son:

| Ajuste | Valor actual | Valores posibles | ¿Es un umbral? |
|---|---|---|---|
| `Adaptive Battery Extender` | `Enable` | `Disable`, `Enable` | **No** — solo on/off |
| `Adaptive Battery Extender Status` | `Activated` | `Not Activated`, `Activated` | **No** — es un estado |
| `Battery Remaining Time` | `Enable` | `Disable`, `Enable` | **No** — solo reporte de tiempo restante |

Datos adicionales del propio ajuste `Adaptive Battery Extender`:

```
Path                    : \System Configuration
IsReadOnly              : 0        (la interfaz dice que es escribible)
RequiresPhysicalPresence: 0
SecurityLevel           : 0
CurrentValue            : Enable
```

- **No existe** ningún ajuste tipo `Battery Care`, `Battery Health Manager`, `Charge Limit`,
  `Maximize My Battery Health` ni un porcentaje 50/60/80/95/100.
- HP documenta `Adaptive Battery Extender` como **"Dynamic battery protection to extend pack
  longevity"** y, en su documentación de *Adaptive Battery Optimizer*, como medidas de protección
  que se activan según temperatura, estado de carga y patrones de uso; el tiempo de ejecución
  "puede reducirse ligeramente". **No es un tope fijo configurable.**
- En el historial de este equipo la batería **llegó al 100 % en AC** en repetidas ocasiones
  (`Active AC 100 %`, `Active AC 95 %`, etc.), por lo que el estado `Activated` **no** está
  funcionando como un límite del 80 %.

> **Caveat honesto:** la lista de `HP_BIOSSetting` es la que HP publica a WMI; la pantalla F10
> podría, en teoría, mostrar algún ítem no expuesto. La comprobación manual en F10 se incluye
> en §11 como cierre.

### 5.2 Interfaces oficiales de HP

| Interfaz | ¿Existe aquí? | ¿Permite límite de carga? |
|---|---|---|
| **HP BIOS WMI** (`HP_BIOSSettingInterface.SetBIOSSetting`) | **Sí** — `wbiosrvc.dll` presente; documentado por HP en su portal de Client Management | **No** — solo permite escribir los ajustes enumerados; ninguno es un umbral de carga |
| **HP Power Manager** (utilidad comercial) | **No** — no está instalado; la clave `HKLM\SOFTWARE\HP\HP Power Manager` está vacía | No aplica: HP lo documenta para **notebooks comerciales** |
| **HP Battery Health Manager** (BHM) | **No** — no aparece en los ajustes del BIOS | No aplica: HP lo documenta para **notebooks comerciales** |
| **HP Support Assistant / OMEN Gaming Hub** | Instalados (HPSA 9.55.10.0, OMEN Command Center 1101.2609.11.0) | **No se encontró** control de límite: sin cadenas de UI de *charge limit* en los paquetes instalados; no se verificó la UI en ejecución |

Evidencia adicional sobre el servicio de HP:

- `PowerManagerService.dll` (parte de *HP App Enabling Services*) contiene la cadena
  `SetMBHStatus Charge limit applied successfully! enable=%d` y abre el dispositivo
  `\\.\QCOMBATTMGR`.
- En este equipo **ese dispositivo no existe** (`CreateFile` → error 2, `ERROR_FILE_NOT_FOUND`).
- El mismo binario incluye los mensajes `ADSP Battery Driver supported / not supported`.
- Conclusión: la ruta de límite de carga del servicio de HP está pensada para plataformas con
  ese controlador de batería; **en este Victus no está disponible**.

### 5.3 Windows

- **No hay API pública** de "límite de carga". Las APIs documentadas
  (`Windows.Devices.Power`, `GetSystemPowerStatus`, `IOCTL_BATTERY_QUERY_INFORMATION`)
  son de **consulta**, no de control.
- Los ajustes de energía de Windows (`SUB_BATTERY`) solo cubren umbrales de **aviso y acción**
  (batería baja, batería crítica, reserva) y notificaciones. **Ninguno limita la carga.**
- El driver `CAD.sys` (*Charge Arbitration Driver*) contiene cadenas de `ChargeLimiting` y la
  clave `HKLM\SYSTEM\CurrentControlSet\Control\Power\CAD\ChargeLimiting`, pero:
  - esa clave **no existe** en este equipo;
  - es un mecanismo interno de arbitraje, **sin API pública**;
  - no debe escribirse a mano (sería exactamente el tipo de "registro de hardware" a evitar).
- `battc.sys` tiene soporte interno de *charge limiting* para minipuertos que lo implementen,
  pero el minipuerto aquí es el genérico **Microsoft ACPI-Compliant Control Method Battery**
  (`cmbatt.sys`), que no expone un umbral configurable al usuario.

### 5.4 ACPI / EC

- La batería es `ACPI\PNP0C0A` con driver Microsoft `CmBatt`.
- No hay una interfaz ACPI/WMI de HP para fijar un umbral.
- La imagen de firmware staged (`C:\Windows\Firmware\SystemFirmware.bin`, 24,9 MB) contiene
  `EFI_HP_BATTERY_CONTROL_PROTOCOL` con cadenas de **lectura**
  (`GetBatteryInfo`, `GetPowerStatus`, `GetRemainCapacity`, `GetACPowerState`).
  No se encontraron cadenas de escritura de umbral en las secciones legibles.
- **No se va a escribir en la EC, en ACPI ni en NVRAM.** Sería indocumentado, riesgoso y
  potencialmente destructivo.

### 5.5 BIOS F.10 — estado de la verificación

| Pregunta | Resultado |
|---|---|
| ¿Existe F.10 para 15-fb2xxx? | **Sí, indicios consistentes**: reportes de usuarios y un hilo de soporte de HP (abril 2026) que menciona el paquete F.10 para `15-fb2x`. |
| ¿Está publicada en la página de drivers de este SKU? | **No verificado**: la página de HP para `A14MNUA#ABA` no se pudo consultar de forma fiable (contenido dinámico / timeout). |
| ¿Está descargada o staged en este equipo? | **No.** `Win32_BIOS` sigue en **F.09**; `C:\Windows\Firmware` solo contiene `SystemFirmware.bin` (24,9 MB, 2026-01-11) y `C:\SWSetup` no tiene ningún softpaq de BIOS F.10. |
| ¿Las notas de versión de F.10 agregan un límite de carga? | **Sin evidencia.** No se encontró ninguna nota de versión que mencione charge limit / Battery Health Manager para esta familia. |
| ¿HP documenta un umbral de 80 % para Victus? | **No.** HP lo documenta solo para **notebooks comerciales** (BHM / Power Manager). En hilos de la comunidad, la respuesta atribuida a soporte de HP es explícita: *"On HP Victus models, there isn't a supported option to set a hard charging limit of 80%. The Adaptive Battery Optimizer feature works automatically."* (fuente secundaria) |

**Conclusión sobre F.10:** es una posibilidad abierta, **no** una base sobre la que diseñar. Si se
quiere cerrar el tema, el orden correcto es: (1) mirar F10 tal como está hoy, (2) si aparece un
ajuste de umbral, actualizar a F.10 con el cargador conectado y volver a mirar. **No** se debe
actualizar el BIOS solo por esta hipótesis: el riesgo (corte de energía, BitLocker, rollback
bloqueado) supera el beneficio esperado.

### 5.6 Conclusión de §5

En este equipo, con este BIOS:

1. **No hay límite de carga configurable** en BIOS, Windows ni utilidad HP.
2. **No hay API oficial** para fijar un umbral.
3. Lo único que existe es una **protección adaptativa de HP** (on/off) cuyo comportamiento
   no es un tope del 80 % y no se puede controlar desde una app de forma significativa.

---

## 6. Consultar vs. alertar vs. detener

| Nivel | Qué hace | Requiere | Riesgo | ¿Viable aquí? |
|---|---|---|---|---|
| **Consultar** | Lee % , estado, tasa, capacidad, ciclos | Nada (usuario estándar) | Nulo | **Sí** |
| **Alertar** | Notificación/toast/sonido/ventana al llegar a un umbral | Nada | Nulo | **Sí** |
| **Actuar sobre el SO** | Suspender, hibernar, apagar, bloquear | Configuración de energía | Bajo (pierde trabajo si no se avisa) | **Sí**, con confirmación |
| **Detener la carga** | Corta la corriente de carga | Firmware/EC o hardware externo | Alto si se improvisa | **No por software** |

---

## 7. Alternativas reales para "detener" la carga

| Alternativa | ¿Detiene la carga? | Ventajas | Limitaciones |
|---|---|---|---|
| **Desconectar el cargador a mano** | **Sí** | Confiable, gratis, sin riesgo | Manual; la laptop pasa a batería |
| **Regleta inteligente** (Shelly, TP-Link Kasa, Home Assistant) que corte la AC del cargador | **Sí** | Automatizable desde la app; el corte es real | Requiere hardware; la laptop queda a batería; no es "AC sin cargar"; ciclos de conexión/desconexión; evaluar seguridad eléctrica y no cortar durante una actualización de BIOS |
| **Deshabilitar "Microsoft ACPI-Compliant Control Method Battery" en el Administrador de dispositivos** | **No confiable** | Ninguna real | Requiere admin; en muchos equipos la EC sigue cargando; se pierde telemetría y protección térmica; puede romper suspensión; **no recomendado** |
| **Herramientas de terceros que escriben en la EC** (WinRing0, etc.) | Incierto | — | Indocumentado, bloqueado por HVCI en muchos equipos, riesgo de daño; **rechazado** |
| **Alternar `Adaptive Battery Extender` por WMI** | **No** | Es interfaz oficial HP | No es un umbral; modifica configuración de BIOS; no produce un tope del 80 %; **fuera de alcance** |
| **Bypass charging** (usar AC sin cargar) | — | — | Según respuestas de la comunidad de HP (no confirmado por HP para 15-fb2xxx), el Victus no lo soporta; no diseñar sobre esto |
| **Cambiar el plan de energía / Battery Saver** | **No** | Fácil | Solo reduce consumo; no afecta la carga |

**Nota conceptual importante:** un límite de carga real permite **seguir usando la laptop con AC
sin cargar la batería**. Una regleta inteligente **corta la AC**: la laptop sigue encendida, pero
con batería. No son equivalentes. Para el objetivo de "no estar al 100 % mientras juego", la
regleta sirve; para "funcionar con AC al 80 %", no.

---

## 8. Implicaciones para la arquitectura

### 8.1 Qué puede ser la app (fase 1, sin hardware)

Un **asistente de carga** de bandeja del sistema:

- % en vivo, estado (cargando / descargando / AC sin cargar), tasa, tiempo estimado.
- Salud de batería y ciclos (a partir de `powercfg /batteryreport`).
- **Alertas de banda**: "desconectá el cargador" al llegar a 80 % en AC y "conectá el cargador"
  al bajar de 40 % en batería. Esto es honesto y útil sin hardware.
- Historial local de sesiones de carga.
- Acción opcional y explícita: suspender / hibernar / apagar al alcanzar el umbral, con cuenta
  regresiva y cancelación.
- Sin admin. Sin escrituras de BIOS. Sin drivers.

### 8.2 Opciones de alcance

| Opción | Descripción | Veredicto |
|---|---|---|
| **A. Monitor + alertas (recomendada)** | Solo lectura + notificaciones + acciones del SO opt-in | Máximo valor con riesgo mínimo; 100 % viable |
| **B. A + regleta inteligente** | La app corta la AC al llegar al umbral | Detiene la carga de verdad, pero requiere hardware, red local y reglas de seguridad; la laptop queda a batería |
| **C. A + toggle de `Adaptive Battery Extender` por WMI** | Escribe un ajuste de BIOS | **Rechazada**: no es un límite, modifica BIOS y no cumple el objetivo |

### 8.3 Stack sugerido (para decidir en la próxima fase)

| Opción | Pros | Contras |
|---|---|---|
| **WPF + .NET 8/9** (recomendada) | Bandeja madura (`H.NotifyIcon`), publicación simple, WinRT disponible con TFM `net8.0-windows10.0.19041.0`, arranque rápido | UI menos "moderna" que WinUI 3 |
| **WinUI 3 + Windows App SDK** | UI Fluent, notificaciones nativas | Empaquetado MSIX más complejo para bandeja/autoarranque; más peso |
| **Tauri / Rust** | Binario pequeño | Más trabajo para WinRT/WMI; menos ecosistema para bandeja en Windows |

Componentes propuestos (independientes y testeables):

1. `BatteryMonitor` — WinRT como fuente principal; `GetSystemPowerStatus` como fallback;
   WMI solo para detalle eléctrico.
2. `AlertEngine` — umbral + histéresis (ej. avisar a 80 %, re-armar a 75 %), transiciones AC,
   antirrebote.
3. `ActionExecutor` — toast, sonido, ventana; suspender/hibernar/apagar con confirmación.
4. `TrayShell` — icono, menú, ajustes, inicio con Windows.
5. `BatteryHealthReader` — parseo de `powercfg /batteryreport` (XML/HTML).
6. `HpBiosInfo` — lectura opcional de `root\HP\InstrumentedBIOS` con *feature detection*.
7. `IPowerSwitch` — interfaz vacía por defecto; implementaciones futuras (Shelly/Kasa/HA).

### 8.4 Permisos y seguridad

- Lectura de batería y BIOS HP: **usuario estándar**.
- Notificaciones: usuario estándar.
- Suspender/hibernar/apagar: usuario interactivo; hibernación debe estar habilitada.
- **Nada de servicio elevado, driver, EC, ACPI ni NVRAM.**
- Si se agrega regleta: solo red local; credenciales en el almacén de credenciales de Windows.

### 8.5 Manejo de errores

- Si WinRT no está disponible → `GetSystemPowerStatus`.
- Si `powercfg` falla → ocultar salud, no bloquear el resto.
- Si el namespace HP no existe → ocultar la sección HP.
- Si la notificación se suprime (Asistente de concentración) → fallback a sonido/ventana.
- Si hibernación está deshabilitada → ofrecer suspender/apagar.
- Nunca ejecutar acciones destructivas sin confirmación explícita del usuario.

### 8.6 Pruebas

- Unitarias del `AlertEngine`: umbral, histéresis, AC on/off, reinicio de estado.
- Unitarias del parseo de `batteryreport` con fixtures.
- Pruebas manuales en batería y en AC; verificar que "AC sin cargar" se muestre correctamente.
- Prueba de bandeja, autoarranque y notificaciones en Windows 11 26H2.

### 8.7 No objetivos explícitos

- No escribir en BIOS/NVRAM/EC/ACPI.
- No deshabilitar la batería en el Administrador de dispositivos.
- No usar drivers de terceros ni WinRing0.
- No prometer "detener la carga" sin hardware externo.
- No requerir administrador.

---

## 9. Criterios de aceptación (fase 1)

1. Muestra el % con una desviación ≤ 1 punto respecto de Windows.
2. Distingue correctamente batería / AC cargando / AC sin cargar.
3. Alerta al alcanzar el umbral configurado estando en AC, una sola vez por sesión.
4. Se re-arma al bajar del umbral menos la histéresis o al desconectar AC.
5. No requiere administrador.
6. No escribe configuración de firmware ni de hardware.
7. Funciona sin conexión a internet.
8. Consumo en reposo despreciable (polling ≥ 5 s + eventos).

---

## 10. Riesgos

| Riesgo | Impacto | Mitigación |
|---|---|---|
| El usuario espera que la app "corte la carga" | Expectativa incumplida | Mensaje claro en UI y documentación; ofrecer alternativa manual/regleta |
| Notificaciones suprimidas por Focus Assist | Alerta no vista | Fallback a sonido/ventana; registrar en historial |
| APIs WinRT no disponibles en algún equipo | Falla de lectura | Fallback a `GetSystemPowerStatus` |
| `batteryreport` cambia de formato | Salud mal calculada | Parseo tolerante + validación |
| Acción de apagado durante trabajo | Pérdida de datos | Confirmación, cuenta regresiva, cancelación |
| Regleta mal configurada (fase 2) | Corte durante actualización de BIOS | Bloqueo explícito si hay actualización de firmware en curso; reglas de seguridad |

---

## 11. Próximos pasos

1. **Confirmación manual del BIOS (5 minutos, opcional pero recomendada):**
   - Guardar trabajo y apagar normalmente (no hace falta desconectar el cargador: es **solo lectura**).
   - Encender y presionar `Esc` repetidamente hasta el menú de arranque → `F10` (Setup Utility).
   - Ir a `System Configuration`. **No cambiar nada.** Si el equipo pide contraseña de BIOS o
     "power-on authentication", detenerse y reportarlo.
   - Buscar explícitamente: `Battery Care`, `Battery Health Manager`, `Charge Limit`,
     `Maximize My Battery Health`, `80%`, `Adaptive Battery Extender`, `Battery Remaining Time`.
   - Esperado en este equipo: **solo** `Adaptive Battery Extender` (Enable/Disable) y
     `Battery Remaining Time`. Registrar lo observado.
   - Salir con `Esc` → `Discard Changes and Exit` (o `F10` sin modificar nada). **Nunca**
     `Load Setup Defaults` ni guardar cambios accidentales.
   - Alternativa sin reiniciar: comparar con la lista de `HP_BIOSSetting` de §12; si un ajuste
     de umbral existiera y estuviera oculto a WMI, es el único caso que F10 revelaría.
2. **Decidir el alcance:**
   - **A)** monitor + alertas (sin hardware), o
   - **B)** A + regleta inteligente para corte real.
3. **Decidir el stack:** WPF (recomendado) vs. WinUI 3.
4. Con esas decisiones, escribir la especificación y luego el plan de implementación.

---

## 12. Comandos de reproducción

```powershell
# Identidad del equipo y BIOS
Get-CimInstance Win32_ComputerSystem | Select Manufacturer,Model,SystemSKUNumber
Get-CimInstance Win32_BIOS | Select SMBIOSBIOSVersion,ReleaseDate

# Batería (tres fuentes)
Get-CimInstance -Namespace root\wmi -ClassName BatteryStatus |
  Select PowerOnline,Charging,Discharging,ChargeRate,DischargeRate,RemainingCapacity,Voltage
Get-CimInstance Win32_Battery | Select EstimatedChargeRemaining,BatteryStatus,Chemistry
powercfg /batteryreport /output "$env:TEMP\battery-report.html"

# Ajustes de BIOS visibles por HP WMI (solo lectura)
Get-CimInstance -Namespace root\HP\InstrumentedBIOS -ClassName HP_BIOSSetting |
  Where-Object Name | Select Name,Value | Sort-Object Name

# Ajuste de batería y su estado
Get-CimInstance -Namespace root\HP\InstrumentedBIOS -ClassName HP_BIOSSetting |
  Where-Object { $_.Name -match 'Battery|Adaptive' } | Format-List Name,Value,Path,IsReadOnly

# Proveedor HP
Get-CimInstance -Namespace root\HP\InstrumentedBIOS -ClassName __Win32Provider |
  Where-Object Name -match 'BIOS'

# Dispositivo de control de carga de HP (ausente en este equipo)
# \\.\QCOMBATTMGR  -> ERROR_FILE_NOT_FOUND (2)
```

---

## 13. Fuentes

**Documentación oficial de HP**

- HP Battery Health Manager (notebooks comerciales):
  <https://support.hp.com/us-en/document/ish_4449597-3519507-16>
- HP Power Manager (notebooks comerciales):
  <https://support.hp.com/us-en/document/ish_5180936-5180983-16>
- HP Gaming Notebook PCs — Using Adaptive Battery Optimizer:
  <https://support.hp.com/us-en/document/ish_9908083-9908127-16>
- HP Client Management — Understanding HP BIOS Settings (`HP_BIOSSettingInterface.SetBIOSSetting`):
  <https://developers.hp.com/hp-client-management/doc/understanding-hp-bios-settings>

**Documentación de Microsoft**

- Obtener información de la batería (`Windows.Devices.Power.Battery`):
  <https://learn.microsoft.com/en-us/windows/apps/develop/devices-sensors/get-battery-info>
- `GetSystemPowerStatus`:
  <https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getsystempowerstatus>
- ACPI battery and power subsystem firmware implementation:
  <https://learn.microsoft.com/en-us/windows-hardware/design/component-guidelines/acpi-battery-and-power-subsystem-firmware-implementation>

**Fuentes secundarias (comunidad; solo contexto, no base del diseño)**

- HP Support Community — Victus 15-fb2000 sin opción de límite en BIOS:
  <https://h30434.www3.hp.com/t5/Gaming-Notebooks/HP-Victus-Battery-Limit/m-p/9146526>
- HP Support Community — Victus sin Battery Health Manager:
  <https://h30434.www3.hp.com/t5/Gaming-Notebooks/HP-VICTUS-BATTERY-HEALTH-MANAGER-OR-BIOS-SETTING-AVAILABLE/td-p/9184747>
- Reddit r/HPLaptops — "need help with hp victus battery limit" (respuesta: no hay tope duro de 80 % en Victus):
  <https://www.reddit.com/r/HPLaptops/comments/1xare3t/need_help_with_hp_victus_battery_limit/>
- HP BIOS Simulator — descripción de `Adaptive Battery Extender`:
  <https://www.manualowl.com/m/Hewlett-Packard/Pavilion-Gaming-15-ec0000/Manual/578888?page=19>

---

## Anexo: observación del firmware

- `C:\Windows\Firmware\SystemFirmware.bin` (24,9 MB, 2026-01-11) contiene las cadenas
  `EFI_HP_BATTERY_CONTROL_PROTOCOL` y `EFI_HP_BATTERY_CONTROL_PROTOCOL_2010`.
- Las cadenas asociadas son de **lectura**: `GetBatteryInfo`, `GetPowerStatus`,
  `GetRemainCapacity`, `GetACPowerState`, `GetCapacityPercent`, `GetCTLabelString`.
- No se hallaron cadenas de escritura de umbral en las secciones legibles del contenedor.
- Este archivo es una imagen **staged** (posible actualización pendiente), no necesariamente
  el firmware F.09 en ejecución; se usa solo como evidencia complementaria.
