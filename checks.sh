#!/usr/bin/env bash
# Comprobaciones antes de cada commit: compila con los avisos como errores y pasa todos los tests.
# Termina en 0 solo si todo está bien. No modifica archivos del repo.
set -euo pipefail
cd "$(dirname "$0")"

echo "== Compilación"
dotnet build GstLosGenerator.slnx -c Release -warnaserror -nologo -v quiet

echo "== Tests"
dotnet test GstLosGenerator.slnx -c Release --no-build -nologo -v quiet

echo "== Todo correcto"
