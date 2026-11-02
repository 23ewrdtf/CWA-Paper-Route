"""Fail the build when PaperRoute.Core line coverage drops below a threshold.

Reads every Cobertura report produced by `dotnet test --coverage`, merges hits per
(file, line) across reports, and checks the Core assembly. Stdlib only.
"""

import argparse
import glob
import sys
import xml.etree.ElementTree as ET


def merged_lines(paths: list[str], package: str) -> dict[tuple[str, str], int]:
    lines: dict[tuple[str, str], int] = {}
    for path in paths:
        root = ET.parse(path).getroot()
        for pkg in root.iter("package"):
            if pkg.get("name") != package:
                continue
            for cls in pkg.iter("class"):
                filename = cls.get("filename", "")
                for line in cls.iter("line"):
                    key = (filename, line.get("number", ""))
                    lines[key] = max(lines.get(key, 0), int(line.get("hits", "0")))
    return lines


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--reports", default="TestResults/*.cobertura.xml")
    parser.add_argument("--package", default="PaperRoute.Core")
    parser.add_argument("--min", type=float, default=85.0)
    args = parser.parse_args()

    paths = glob.glob(args.reports)
    if not paths:
        print(f"No coverage reports match {args.reports}")
        return 1
    lines = merged_lines(paths, args.package)
    if not lines:
        print(f"{args.package}: no coverage data in {len(paths)} report(s) - failing (is the package name or collector setup wrong?)")
        return 1
    covered = sum(1 for hits in lines.values() if hits > 0)
    percent = 100.0 * covered / len(lines)
    print(f"{args.package}: {covered}/{len(lines)} lines covered = {percent:.1f}% (minimum {args.min}%)")
    return 0 if percent >= args.min else 1


if __name__ == "__main__":
    sys.exit(main())
