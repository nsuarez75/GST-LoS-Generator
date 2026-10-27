# Especificación de GST-LoS-Generator

Acordada en la entrevista del 2026-10-26. El vocabulario está en `GLOSSARY.md`.

## Objetivo

App de escritorio en .NET 10 con Avalonia que, a partir de una LoS (Excel de señales I4.0), genera para cada PLC:

- El FB `User_LoS_Device_In_FB`, como XML de Openness.
- Un UDT `GSDML_DeviceNN` por device, en formato de fuente externa `.udt`.

La importación a TIA Portal queda fuera: la hará otra app con Openness, que exige .NET Framework 4.8.

## Interfaz

- Aspecto profesional y sobrio (tema Fluent de Avalonia), sin adornos innecesarios.
- Tema claro y oscuro: por defecto sigue a Windows, con selector «Claro / Oscuro / Sistema» que se recuerda.
- Idioma castellano/inglés: por defecto el de Windows, con selector. Solo cambia la interfaz; el código generado no cambia.
- Ventana principal:
  - Barra superior: archivo (nuevo, abrir, guardar y más), deshacer y rehacer, las acciones del proyecto en orden (Importar LoS, Catálogo, Leer FB y Generar) y, a la derecha, idioma, tema y Acerca de. La interfaz se adapta al ancho de la ventana.
  - Panel izquierdo: árbol del proyecto (proyecto → PLCs → Devices, Assets, Operaciones, Locations).
  - Zona central: el editor de lo seleccionado.
  - Barra inferior de avisos: al pulsar uno, lleva a lo que lo provoca.
- Assets en árbol (celda → asset de operación → sub-assets), con buscador. Cada asset se despliega y muestra sus variables con tipo de dato y dirección.
- Catálogo de tipos en una ventana propia (botón *Catálogo* de la barra superior): tipos a la izquierda y, a la derecha, sus variables y su equivalencia. Desde ahí se exporta e importa.
- Atajos de teclado y además botones: guardar a mano (Ctrl+S), deshacer y rehacer (Ctrl+Z / Ctrl+Y) en todas las ediciones, y aviso al cerrar con cambios sin guardar. Al abrir, se reabre el último proyecto.

## Archivos

- Proyecto: un archivo `.losproj` que se guarda donde se quiera y se vuelve a abrir en la app.
- Catálogo de tipos: común a todos los proyectos del equipo. Se exporta e importa como `.loscat`.
- Salida: carpeta elegida por proyecto y recordada. Contiene `User_LoS_Device_In_FB.xml` y un `GSDML_DeviceNN.udt` por device. Se avisa antes de sobrescribir.

## Proyectos, PLCs y assets

- Al importar una LoS, se crea un PLC por cada valor de `Source` y sus devices salen de la hoja `PLCxx - OUT` (en el PLC06, `Device_61`, `Device_62`…).
- Todo se puede crear también a mano, sin LoS: PLCs, devices, assets y direcciones.
- Cambiar el tipo de un asset mantiene sus variables y direcciones; si alguna no existe en el tipo nuevo, se avisa.
- Nueva versión de la LoS:
  - Los assets se reconocen por `Element`.
  - Se conservan los cambios hechos a mano y se añaden los assets nuevos.
  - Los que desaparecen se marcan, no se borran.
  - Antes de aplicar se muestra un resumen de cambios.
  - Los assets manuales no se marcan como desaparecidos. Si la LoS trae uno con el mismo nombre, manda la LoS.
- Tipos:
  - Un `Template` que no está en el catálogo se crea con sus variables, pendiente de equivalencia.
  - Si un asset trae una variable que su tipo no tiene, se avisa y se ofrece añadirla al tipo.
  - Los assets sin equivalencia salen marcados y no se generan.
  - Un tipo se puede marcar «Sin FB» (p. ej. `CE_PLC`): sus assets no tienen llamada ni variable en la estática. Cada variable del tipo lleva un origen (`#LifeBit`, `"DB".a.b`) que se escribe en la región `General` de Struct TO DEVICE; las variables sin origen no se escriben.

## Operaciones y locations

- Las locations (nombre y número) se definen por PLC y se reutilizan en varias operaciones.
- Las operaciones se crean y numeran a mano. Cada una tiene sus locations y en cada location se fijan:
  - `Loc`: el número de la location.
  - `Mask`: las referencias asignadas, una por bit, hasta 8 (las 8 juntas dan 255).
  - `VirtualLoc`.
  - Por cada referencia, la señal de pieza presente y la de clampas abiertas. Si no se escriben, salen como `alwaysFalse`.
- Una operación puede ir a varios assets. Se asigna desde la ficha de la operación y también desde el árbol de assets (columna «Operación»); las dos vistas están sincronizadas.
- Los assets sin operación se llaman sin ella.

## Estructura del FB generado

1. «Config Signals» (vacío).
2. Cabecera «Setup Operations».
3. Un segmento STL por operación, ordenadas por número, con el título «Operation <n> :<locations>».
4. Cabecera «Call all blocks».
5. Un segmento LAD por asset, en el orden de la LoS, con el título «<Element> [<UDT>]». Llama al FB de su equivalencia, con la instancia sin el prefijo de línea, el pin `Operation` y el pin del UDT conectado a la variable del asset. Los demás pines de la interfaz conocida quedan abiertos (`OpenCon`).
6. «Life bit» (`clock1Hz` → `LifeBit`).
7. Separador.
8. «Struct TO DEVICE» en SCL, como el segmento 118 del ejemplo. Tiene una REGION `General` y una REGION por celda; dentro de cada celda, `CELLxx_Estado` y una sub-región por asset de operación.
9. Separador.
10. «SWAP Float»: `Swap_Real` sobre todas las direcciones `Float` del device.
11. «SWAP»: todas las direcciones `Int16`, `Int32` e `Int64` del device, se usen o no. Strings y Bytes sin swap.

Estática:
- Los devices.
- Una variable por asset, con su UDT y el nombre completo.
- La instancia del FB de cada asset, sin el prefijo de línea.
- Una variable `Operation_<n>` de tipo `I40_FixtureCfg_UDT` por operación.

Las señales IN no generan nada en el FB.

## UDT de los devices

- Llevan todas las direcciones de la hoja del PLC, se usen o no, con los nombres de la hoja OUT. Sirven para IN y OUT.
- Tipos de dato: `Bool`→`Bool`, `Byte`→`Byte`, `Int16`→`Int`, `Int32`→`DInt`, `Int64`→`ULInt`, `Float`→`Real`, `String`→`Array[1..20] of Char`, `StringNN`→`Array[1..NN] of Char`.

## Generar

- Se elige el PLC, se marcan los assets y las partes (estática, operaciones, llamadas, Struct TO DEVICE, swaps y UDT) y la carpeta de salida.
- El FB resultante lleva siempre todos los segmentos:
  - Si un segmento no está en el FB actual, se añade en su sitio.
  - Si ya está y no se marca, se copia tal cual.
  - Si ya está y se marca, se regenera.
  - Los segmentos hechos a mano se conservan.
  - Si un asset ya no existe en la app, su segmento no se borra: se avisa.
- Al regenerar la llamada de un asset solo se reescribe lo que pone la app (título, FB llamado, instancia, pin `Operation` y pin del UDT). La lógica y los pines conectados a mano se conservan. Si el FB nuevo ya no tiene un pin que estaba conectado, se avisa y no se regenera ese segmento sin confirmación.
- En Struct TO DEVICE solo se reescriben las regiones que la app reconoce por nombre; las sub-regiones manuales se conservan.
- Validaciones antes de escribir (operación sin locations o sin assets, asset sin equivalencia, dirección repetida en un device): aviso con la lista, y se deja generar.

## Leer FB

- Lee un FB exportado de TIA y adapta la configuración de la app a lo que hay en él:
  - operaciones, con sus locations, `Loc`, `Mask`, `VirtualLoc` y señales;
  - la operación de cada asset;
  - el FB y el UDT que llama cada asset.
- Muestra un resumen de diferencias y, al aplicar, lo del FB gana.
- El FB queda guardado en el proyecto como «FB actual» del PLC y es la base de la siguiente generación, conservando la lógica cableada a mano.

## Datos de ejemplo

`Datos Ejemplo/` contiene datos reales de fábrica, está en `.gitignore` y solo sirve como referencia de formato. Del FB de ejemplo se ignoran los segmentos 113-115, 117 y 119-121. Los tests usan datos inventados.
