# Glosario

Vocabulario del proyecto. Se completa durante la entrevista inicial.

- **LoS** (List of Signals): Excel con todas las señales I4.0 que intercambian los PLC con el IPC. La hoja `LoS` lista cada señal con su elemento, tipo de dato, plantilla y dirección.
- **Proyecto**: conjunto de trabajo de la app; contiene PLCs y se alimenta de una LoS.
- **PLC**: autómata de la línea (en la LoS, `PLC06`, `PLC07`…). Cada uno tiene una hoja `PLCxx - OUT` y `PLCxx - IN` con sus devices.
- **Device**: bloque de intercambio de un PLC con el IPC (`DEVICE 1`, `DEVICE 2`…). Cada device da lugar a un UDT (`GSDML_DeviceNN`), igual para IN y OUT.
- **Asset**: cada elemento de la LoS (columna `Element`, p. ej. `LX9_01_1000FIX01_01_MOVEHR`) con su tipo y sus variables.
- **Tipo** (de asset): la columna `Template` de la LoS (p. ej. `OP_UNXLOAD`, `CE_CELL`); se corresponde con un FB y un UDT de TIA Portal.
- **Variable** (de asset): cada `SignalName` del asset, con su tipo de dato y su dirección.
- **Dirección**: nombre de la posición en el device, p. ej. `Bool_061_o(000)_00` (tipo, PLC+device, sentido, palabra, bit).
- **Operación**: segmento de configuración del FB (p. ej. `Operation 110`) con un número de locations y las referencias que usa.
- **Location**: puesto dentro del PLC, con nombre y número, que se asigna a operaciones.
