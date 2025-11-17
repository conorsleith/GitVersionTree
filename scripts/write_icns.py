#!/usr/bin/env python3
import struct
import sys
from pathlib import Path


def read_manifest(manifest_path: Path) -> dict[int, bytes]:
    sizes: dict[int, bytes] = {}
    for line in manifest_path.read_text().splitlines():
        line = line.strip()
        if not line or ":" not in line:
            continue
        size_text, path_text = line.split(":", 1)
        try:
            size = int(size_text)
        except ValueError:
            continue
        png_path = Path(path_text.strip())
        if png_path.exists():
            sizes[size] = png_path.read_bytes()
    return sizes


def build_icns(manifest: dict[int, bytes]) -> bytes:
    entries = [
        ("ic10", 1024),
        ("ic09", 512),
        ("ic08", 256),
        ("ic07", 128),
        ("ic06", 64),
        ("ic05", 32),
        ("ic04", 16),
        ("ic14", 512),  # 256@2x
        ("ic13", 256),  # 128@2x
        ("ic12", 64),   # 32@2x
        ("ic11", 32),   # 16@2x
    ]

    chunks = []
    for code, size in entries:
        png = manifest.get(size)
        if not png:
            continue
        header = code.encode("ascii") + struct.pack(">I", len(png) + 8)
        chunks.append(header + png)

    if not chunks:
        raise RuntimeError("No icon data available to build AppIcon.icns")

    total_size = 8 + sum(len(chunk) for chunk in chunks)
    output = bytearray()
    output.extend(b"icns")
    output.extend(struct.pack(">I", total_size))
    for chunk in chunks:
        output.extend(chunk)
    return bytes(output)


def main() -> int:
    if len(sys.argv) != 3:
        print("Usage: write_icns.py <manifest> <output>", file=sys.stderr)
        return 1

    manifest_path = Path(sys.argv[1])
    output_path = Path(sys.argv[2])

    manifest = read_manifest(manifest_path)
    data = build_icns(manifest)
    output_path.write_bytes(data)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
