"""Falla si la cobertura de líneas de una librería está por debajo del umbral.

Uso: python check-cobertura.py <umbral> <ensamblado> <coverage.cobertura.xml> [...]

Une los informes de varios proyectos de tests: una línea cuenta como cubierta si algún informe la ejecuta.
Sólo cuentan las líneas del ensamblado indicado. Sin informes o sin líneas de ese ensamblado, falla.
Si existe GITHUB_STEP_SUMMARY, escribe allí el resultado.
"""
import os
import sys
import xml.etree.ElementTree as ET


def lines_of(assembly, paths):
    lines = {}
    for path in paths:
        root = ET.parse(path).getroot()
        sources = [x.text or "" for x in root.iter("source")]
        base = sources[0] if sources else ""
        for package in root.iter("package"):
            if package.get("name") != assembly:
                continue
            for cls in package.iter("class"):
                filename = os.path.normpath(os.path.join(base, cls.get("filename")))
                for line in cls.iter("line"):
                    key = (filename, line.get("number"))
                    lines[key] = lines.get(key, False) or int(line.get("hits", "0")) > 0
    return lines


def main(args):
    if len(args) < 3:
        print("::error::Uso: check-cobertura.py <umbral> <ensamblado> <informe> [...]")
        return 1
    threshold, assembly, paths = float(args[0]), args[1], args[2:]
    lines = lines_of(assembly, paths)
    if not lines:
        print(f"::error::Los informes de cobertura no tienen líneas de {assembly}.")
        return 1
    covered = sum(1 for hit in lines.values() if hit)
    pct = 100 * covered / len(lines)
    missing = {}
    for (filename, number), hit in lines.items():
        if not hit:
            missing.setdefault(os.path.basename(filename), []).append(int(number))
    out = [f"### Cobertura de líneas de {assembly}: {pct:.1f} % ({covered}/{len(lines)})", "", "Líneas sin cubrir:"]
    out += [f"- {f}: {sorted(set(v))}" for f, v in sorted(missing.items())]
    print("\n".join(out))
    summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary:
        with open(summary, "a", encoding="utf-8") as f:
            f.write("\n".join(out) + "\n")
    if pct < threshold:
        print(f"::error::Cobertura de líneas {pct:.1f} %, por debajo del {threshold:g} %.")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
