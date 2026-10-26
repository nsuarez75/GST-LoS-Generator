# Pendientes

- [ ] Terminar la entrevista de producto y cerrar el glosario.
- [ ] Crear la solución .NET 10 + Avalonia y `checks.sh`.

## Requisitos ya fijados

- Interfaz profesional, con tema claro y oscuro, según los estándares actuales y sin adornos innecesarios.
- El FB se genera como XML de Openness. La importación a TIA queda fuera de esta app.
- Los UDT se generan en formato de fuente externa `.udt`.
- Seguir el tema de Windows por defecto, con selector «Claro / Oscuro / Sistema» que se recuerda.
- Assets en árbol PLC → celda → asset de operación → sub-assets, con buscador. Cada asset se despliega y muestra sus variables con tipo de dato y dirección.
- Reimportar el FB: cada segmento se reconoce por su título. Los existentes no se tocan salvo que se marquen para regenerar, los nuevos se insertan en su sitio, los manuales se conservan y los de assets que ya no existen se avisan, no se borran.
- Selector de idioma castellano / inglés en la app.
- El idioma cambia solo la interfaz; el código generado no cambia. Por defecto, el idioma de Windows.
- Nueva versión de la LoS: los assets se reconocen por `Element`, se conservan los cambios a mano, se añaden los nuevos, los quitados se marcan, y se muestra un resumen de cambios antes de aplicar.
- Tipo de la LoS que no está en el catálogo: se crea con sus variables, pendiente de equivalencia. Variable nueva en un tipo existente: aviso y opción de añadirla. Los assets sin equivalencia salen marcados y no se generan.
- Swap: todas las direcciones `Int16`, `Int32` e `Int64` del device con SWAP en SCL, y las `Float` con el FB `Swap_Real`, se usen o no. Strings y Bytes sin swap.
- Señales IN: no se genera nada para ellas en el FB.
- Generar: se elige el PLC, se marcan assets y partes, y se escribe en una carpeta de salida recordada por proyecto (`User_LoS_Device_In_FB.xml` y un `GSDML_DeviceNN.udt` por device), con aviso antes de sobrescribir. El FB resultante lleva siempre todos los segmentos: los que faltan se añaden, y de los que ya existen solo se sobrescriben los marcados.
- Opción «Leer FB»: lee un FB exportado y adapta la configuración de la app a lo que hay en él.
- Estática: variable de cada asset con su UDT y el nombre completo (`LX9_01_…`); instancia del FB sin el prefijo de línea, es decir, hasta el primer `_` (`01_…`); operaciones `Operation_<n>` de tipo `I40_FixtureCfg_UDT`.
- Orden del FB: Config Signals (vacío) → cabecera Setup Operations → operaciones por número → cabecera Call all blocks → llamadas en el orden de la LoS → Life bit (`clock1Hz` → `LifeBit`) → separador → Struct TO DEVICE → separador → SWAP Float → SWAP. Los pines que no salen de la LoS quedan sin conectar.
- Struct TO DEVICE como el segmento 118 del ejemplo: `General` → celda → `CELLxx_Estado` + una sub-región por asset de operación. Las sub-regiones manuales se conservan; solo se reescriben las que la app reconoce por nombre.
