# GST-LoS-Generator

Aplicación de escritorio (.NET 10 + Avalonia) que lee una LoS (Excel de señales I4.0) y genera el código de PLC para TIA Portal: el FB con la declaración de variables, los segmentos de operaciones y de assets, el paso de struct a device y los swap, además de un UDT por cada device de cada PLC.

> Estado: especificación cerrada (`docs/especificacion.md`); en desarrollo.

## Documentación

- `GLOSSARY.md`: vocabulario del proyecto. Léelo antes de nombrar cosas.
- `docs/especificacion.md`: qué hace la app, acordado en la entrevista inicial.
- `TODO.md`: pendientes.

## Datos de ejemplo

La carpeta `Datos Ejemplo/` (LoS, UDT y FB exportados de TIA Portal) está en `.gitignore`: son datos reales y solo sirven para conocer el formato. Los tests usan datos inventados.

## Comprobaciones

`./checks.sh` (desde Git Bash) compila con los avisos como errores y pasa todos los tests. Sin `./checks.sh` en verde no hay commit ni PR.
