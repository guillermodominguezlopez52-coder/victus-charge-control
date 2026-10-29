# Limitaciones conocidas — Victus Charge Control 1.0

## 1. No detiene la carga (limitación principal)

En el HP Victus 15-fb2xxx con BIOS F.09 **no existe una interfaz soportada** para fijar un tope
de carga. Verificado y documentado en [`PLAN.md`](../PLAN.md):

- La WMI de BIOS de HP (`root\HP\InstrumentedBIOS`) expone 51 ajustes; los únicos relacionados
  con batería son `Adaptive Battery Extender` (on/off), su estado y el tiempo restante.
- `Adaptive Battery Extender` es una protección dinámica de HP, **no** un umbral de porcentaje:
  el historial muestra cargas al 100 % con el ajuste activo.
- `_BTP` de ACPI es un *Battery Trip Point* (notificación), no un límite de carga; no hay
  `_BMC`/`_BMD` ni registro de umbral en la EC.
- El camino propietario de HP (`SetMBHStatus` → `\\.\QCOMBATTMGR`) no existe en este equipo AMD;
  HP Power Manager / Battery Health Manager son de la línea comercial.
- Windows no ofrece API pública de límite de carga.

Por eso esta versión **no** intenta cortar la carga ni la corriente. Tampoco modifica BIOS, EC,
ACPI, NVRAM ni registros de hardware, y no se ejecuta como administrador.

## 2. Las alertas no son un control

Una notificación llega **después** de que el porcentaje cruzó el umbral y no cambia el estado
eléctrico. Con el cargador conectado, la batería seguirá cargando hasta donde decida el firmware.

## 3. Dependencia del shell de notificaciones

1. La vía principal es el globo del icono de bandeja (`NotifyIcon.ShowBalloonTip`), que Windows 11
   presenta como notificación del sistema.
2. **Asistente de concentración / No molestar / modo juego** puede silenciarla o enviarla
   directamente al centro de notificaciones.
3. Por eso la app **siempre** muestra además un banner dentro de la ventana, y una ventana
   flotante propia si el icono no está disponible.
4. Si el proceso no está en ejecución (app cerrada, sesión sin iniciar), no hay alertas.

## 4. Cobertura del historial

- Es un registro de **eventos**, no un muestreo continuo.
- No hay datos del tiempo en que la app estuvo cerrada, el equipo suspendido o el usuario cambió.
- Se registra un evento cuando cambia el estado, la corriente o la zona, o cuando el porcentaje
  varía 5 puntos o más respecto del último evento.
- Las escrituras se agrupan (mínimo 60 s entre volcados, 10 eventos pendientes o cierre) para no
  castigar el disco; ante un corte de energía se puede perder el último lote.
- Tope de 5000 entradas; al superarlo se recorta el archivo.

## 5. Disponibilidad de datos

- **Porcentaje, estado, AC y potencia**: `Windows.Devices.Power.Battery` con respaldo en
  `GetSystemPowerStatus` y, para potencia, `root\wmi:BatteryStatus`.
- **Diseño, capacidad completa y ciclos**: solo desde `powercfg /batteryreport /xml`. Puede faltar
  en equipos sin batería, con driver incompleto o si `powercfg` está bloqueado por política.
  En ese caso la UI muestra `—`, nunca `0`.
- La potencia puede no informarse en algunos momentos (por ejemplo, en reposo con AC); también
  se muestra `—`.
- La salud (%) es una relación entre capacidad completa y de diseño; no es un diagnóstico.

## 6. Precisión temporal

- Evento de WinRT cuando el driver lo emite, más consulta periódica (30 s por defecto,
  configurable 15/30/60/120 s).
- Un cruce de umbral puede detectarse con hasta un intervalo de retraso.
- Tras suspensión/reanudación se fuerza una relectura inmediata; aun así, el primer dato puede
  tardar uno o dos segundos en reflejar el estado real.

## 7. Alcance de la versión 1.0

- No corta corriente, no suspende, no hiberna, no apaga.
- No hay integración con `Adaptive Battery Extender` (ni lectura de su estado en la UI).
- No hay instalador MSI; es un ejecutable portable.
- Una sola instancia por sesión de usuario.
- Interfaz en español, pensada para un único usuario.
- Probado en HP Victus 15-fb2xxx / Windows 11 26H2 (build 26300). En otros equipos la lectura
  debería funcionar igual, pero no está validado.

## 8. Qué haría falta para un límite real

Solo si el fabricante lo habilita: una interfaz soportada en BIOS/EC (por ejemplo, un ajuste de
"Battery Care" o "Charge limit" en el setup) o un servicio del fabricante para esta línea. En ese
caso habría que revisar de nuevo `PLAN.md` antes de tocar cualquier cosa.
