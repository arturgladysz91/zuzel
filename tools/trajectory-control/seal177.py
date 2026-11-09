"""Seal immutable raw trials, proposals and budgets after validation."""
import argparse
from pathlib import Path
from zipfile import ZipFile, ZipInfo, ZIP_STORED, ZIP_DEFLATED

from verify177 import DOC, ROOT, sha


def pack(path, files):
    with ZipFile(path, "x") as archive:
        for file in sorted(files, key=lambda f: f.name):
            meta = ZipInfo(file.name)
            codec = ZIP_STORED if file.suffix == ".zip" else ZIP_DEFLATED
            archive.writestr(meta, file.read_bytes(), compress_type=codec)


def seal(directory):
    pack(DOC/"177-natywne.zip", (p for p in directory.glob("*.zip") if not p.name.startswith("held-")))
    pack(DOC/"177-walidacja.zip", directory.glob("held-*.zip"))
    pack(DOC/"177-protokol.zip", (p for p in directory.iterdir()
        if p.suffix in (".json", ".cs") and not p.name.endswith(".result.json")))
    manifest = DOC/"SHA256SUMS"
    files = [p for p in DOC.rglob("*") if p.is_file() and p != manifest]
    files += [p for p in (ROOT/"tools/trajectory-control").iterdir() if p.is_file()]
    files.append(ROOT/".github/workflows/study-177.yml")
    manifest.write_text("".join(f"{sha(p)}  {p.relative_to(ROOT).as_posix()}\n" for p in sorted(files)),
                        encoding="utf-8", newline="\n")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("evidence", type=Path)
    seal(parser.parse_args().evidence)
