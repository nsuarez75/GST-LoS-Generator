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
