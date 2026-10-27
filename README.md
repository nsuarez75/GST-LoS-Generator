# GST-LoS-Generator

Aplicación de escritorio (.NET 10 + Avalonia) que lee una LoS (Excel de señales I4.0) y genera el código de PLC para TIA Portal: el FB con la declaración de variables, los segmentos de operaciones y de assets, el paso de struct a device y los swap, además de un UDT por cada device de cada PLC.

> Estado: primera versión completa. Falta probar la importación del FB generado en TIA con la app de Openness (ver `TODO.md`).

## Uso

1. **Importar LoS** en la barra superior (Ctrl+I). Crea un PLC por cada valor de la columna `Source`, con sus devices (hojas `PLCxx - OUT`) y sus assets. Antes de aplicar se enseña un resumen de cambios; con una LoS nueva sobre un proyecto ya trabajado se conserva lo hecho a mano.
2. **Catálogo** en la barra superior (Ctrl+T). Para cada tipo de la LoS, el FB al que se llama, el UDT de la variable del asset y el pin del FB donde se conecta. El catálogo es común a todos los proyectos y se puede exportar e importar (`.loscat`).
3. En cada PLC, **Locations** y **Operaciones**: locations (nombre y número), operaciones con sus locations, referencias (hasta 8) y señales de pieza presente y clampas abiertas, y los assets que reciben cada operación.
4. **Generar** en la barra superior (F5). Escribe `User_LoS_Device_In_FB.xml` (XML de Openness) y un `GSDML_DeviceNN.udt` por device en la carpeta de salida.
5. Cuando el FB ya esté en TIA y se haya programado a mano: exportarlo y **Leer FB** en la barra superior (Ctrl+R). La app se adapta a lo que hay en el FB y, al volver a generar, añade lo que falta y solo reescribe lo que se marque, sin perder lo programado a mano.

El proyecto se guarda en un archivo `.losproj` (Ctrl+S). Deshacer y rehacer con Ctrl+Z / Ctrl+Y. Tema claro, oscuro o el de Windows, e idioma castellano o inglés, con los botones de la derecha de la barra superior. La ventana se adapta al ancho: con poco sitio los botones se quedan solo con el icono y las listas y columnas se estrechan.

## Compilar y ejecutar

```bash
dotnet run --project src/GstLosGenerator.App
```

Ajustes y catálogo de tipos se guardan en `%APPDATA%\GST-LoS-Generator`.

## Estructura

- `src/GstLosGenerator.Core`: modelo, lectura de la LoS, generación y lectura del FB (`Fb/`), UDT, validaciones y textos de la interfaz en inglés.
- `src/GstLosGenerator.App`: interfaz Avalonia (MVVM con CommunityToolkit.Mvvm).
- `tests/`: tests del núcleo y de la interfaz (sin pantalla). Con `GST_SCREENSHOTS=<carpeta>` los tests de la interfaz dejan capturas de cada pantalla.

## Documentación

- `GLOSSARY.md`: vocabulario del proyecto. Léelo antes de nombrar cosas.
- `docs/especificacion.md`: qué hace la app, acordado en la entrevista inicial.
- `TODO.md`: pendientes.

## Datos de ejemplo

La carpeta `Datos Ejemplo/` (LoS, UDT y FB exportados de TIA Portal) está en `.gitignore`: son datos reales y solo sirven para conocer el formato. Los tests usan datos inventados.

## Comprobaciones

`./checks.sh` (desde Git Bash) compila con los avisos como errores y pasa todos los tests. Sin `./checks.sh` en verde no hay commit ni PR.

## Licencia

El código se publica con licencia [MIT](LICENSE) © 2026 Ignacio Suarez. Las librerías de terceros que van dentro del
ejecutable tienen sus propias licencias: ver [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

La licencia no cubre el logotipo de Alomatek ni las marcas de terceros, que pertenecen a sus dueños.
