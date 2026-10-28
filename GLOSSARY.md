# Glosario

Vocabulario del proyecto. La especificación completa está en `docs/especificacion.md`.

- **LoS** (List of Signals): Excel con todas las señales I4.0 que intercambian los PLC con el IPC. La hoja `LoS` lista cada señal con su elemento, tipo de dato, plantilla y dirección.
- **Proyecto**: conjunto de trabajo de la app; contiene PLCs y se alimenta de una LoS. Se guarda en un archivo `.losproj`.
- **PLC**: autómata de la línea (en la LoS, `PLC06`, `PLC07`…). Cada uno tiene una hoja `PLCxx - OUT` y `PLCxx - IN` con sus devices.
- **Device**: bloque de intercambio de un PLC con el IPC (`DEVICE 1`, `DEVICE 2`…), sacado de la hoja del PLC en la LoS. Suele haber 2 o 3 por PLC. En el PLC06 se llaman `Device_61`, `Device_62`… Cada device da lugar a un UDT (`GSDML_Device61`…) en formato de fuente externa `.udt`, igual para IN y OUT.
- **FB del PLC**: bloque generado para un PLC, siempre llamado `User_LoS_Device_In_FB`. Hay uno por PLC y se elige de qué PLC se genera en cada momento. Se entrega como XML de Openness; la importación a TIA la hace otra app aparte (Openness exige .NET Framework 4.8).
- **Asset**: cada elemento de la LoS (columna `Element`, p. ej. `LX9_01_1000FIX01_01_MOVEHR`) con su tipo y sus variables.
- **Tipo** (de asset): la columna `Template` de la LoS (p. ej. `OP_UNXLOAD`, `CE_CELL`). Cada tipo tiene sus variables y se corresponde con un FB y un UDT de TIA Portal mediante una equivalencia.
- **Catálogo de tipos**: lista de tipos con sus variables y equivalencias. Es común a todos los proyectos.
- **Equivalencia** (de tipo): relación entre el tipo de la LoS y los nombres que usa el PLC, es decir, el FB al que se llama y el UDT del asset (p. ej. `OP_MOVE_CNV` → FB `LoS_OP_MOVE_CNV`, UDT `LoS_OPERATION_CNV_UDT`). No siempre coinciden, por eso se asigna a mano. El nombre entre corchetes del título de cada segmento de asset es el UDT.
- **Variable** (de asset): cada `SignalName` del asset, con su tipo de dato y su dirección.
- **Dirección**: nombre de la posición en el device, p. ej. `Bool_061_o(000)_00` (tipo, PLC+device, sentido, palabra, bit).
- **Operación**: segmento de configuración del FB (p. ej. `Operation 110`) con sus locations. En cada location se fijan el número (`Loc`), las referencias que trabajan en ella (`Mask`), si es virtual (`VirtualLoc`) y, por referencia, la señal de pieza presente (`PartPresent_ref`) y la de clampas abiertas (`ClampsOpen_Ref`). La señal que no se escribe en la app sale como `alwaysFalse`.
- **Referencia** (de pieza): cada una de las hasta 8 referencias que pueden pasar por una operación. Se asignan a mano en cada operación. En `Mask` cada referencia es un bit: la 1 vale 1, la 2 vale 2, la 3 vale 4… y la 8 vale 128; las 8 juntas dan 255.
- **Location**: puesto dentro de un PLC, con nombre (p. ej. `1000+FIX01`) y número (el que va en `Loc`). Se define una vez por PLC y se reutiliza en varias operaciones. El título del segmento de una operación es «Operation <número> :<nombres de sus locations>» o, si la operación tiene **comentario**, «Operation <número>: <comentario>».
- **Asignación de operación**: una operación, creada y numerada a mano, se puede asignar a uno o varios assets y entra por el pin `Operation` de su llamada. Los assets sin operación se llaman sin ella.
- **Asset manual**: asset creado a mano en la app, sin LoS. La LoS nueva no lo marca como desaparecido; si trae uno con el mismo nombre, pasa a ser de la LoS.
- **Prefijo de línea**: lo que va antes del primer `_` del nombre de un asset (`LX9_`). La instancia del FB del asset se llama sin él.
- **FB actual**: el FB de un PLC tal como está en TIA, exportado y leído con «Leer FB». Es la base sobre la que se genera para no perder lo programado a mano.
- **Leer FB**: acción que lee un FB exportado y adapta la configuración de la app a lo que hay en él (operaciones, asignaciones, equivalencias).
