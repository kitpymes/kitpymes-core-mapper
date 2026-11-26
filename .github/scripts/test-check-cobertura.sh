#!/usr/bin/env bash
# Prueba check-cobertura.py contra informes falsos: tiene que fallar por debajo del umbral, sin líneas de la
# librería y sin informes; pasar en el umbral y por encima; y unir informes (una línea cubierta en cualquiera cuenta).
set -u
dir=$(mktemp -d)
trap 'rm -rf "$dir"' EXIT
script="$(dirname "$0")/check-cobertura.py"

informe() { # fichero paquete aciertos... (una línea por valor: 0 sin cubrir, 1 cubierta)
  local f="$dir/$1" p="$2"; shift 2
  { echo "<coverage><sources><source>/src</source></sources><packages><package name=\"$p\"><classes><class filename=\"A.cs\"><lines>"
    local n=1; for h in "$@"; do echo "<line number=\"$n\" hits=\"$h\"/>"; n=$((n + 1)); done
    echo "</lines></class></classes></package></packages></coverage>"; } > "$f"
}

informe bajo.xml Lib 1 1 1 1 1 1 1 1 0 0       # 80 %
informe justo.xml Lib 1 1 1 1 1 1 1 1 1 0      # 90 %
informe otra.xml Otra 1 1 1 1 1 1 1 1 1 1      # 100 %, de otro ensamblado
informe mitad1.xml Lib 1 1 1 1 1 0 0 0 0 0     # 50 %
informe mitad2.xml Lib 0 0 0 0 0 1 1 1 1 1     # 50 %, las otras líneas

fallos=0
espera() { # codigo esperado (0 pasa, 1 falla), informes...
  local esperado=$1; shift
  GITHUB_STEP_SUMMARY= python3 "$script" 90 Lib "$@" > /dev/null 2>&1; local real=$?
  [ "$real" -ne 0 ] && real=1
  if [ "$real" -ne "$esperado" ]; then echo "MAL: $* dio $real y se esperaba $esperado"; fallos=1; else echo "bien: $(basename -a "$@" | tr '\n' ' ')-> $real"; fi
}

espera 1 "$dir/bajo.xml"
espera 0 "$dir/justo.xml"
espera 1 "$dir/otra.xml"
espera 0 "$dir/mitad1.xml" "$dir/mitad2.xml"
espera 1 "$dir/mitad1.xml"
espera 1 "$dir/no-existe.xml"
GITHUB_STEP_SUMMARY= python3 "$script" 90 Lib > /dev/null 2>&1 && { echo "MAL: sin informes pasa"; fallos=1; } || echo "bien: sin informes -> 1"

exit $fallos
