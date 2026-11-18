"""Packt builds/linux-x64 als tar.gz und setzt dabei das Ausfuehrungsrecht (unter Windows gebaut geht es sonst verloren).
Aufruf: python tools/pack_linux.py"""
import os
import tarfile

here = os.path.dirname(os.path.abspath(__file__))
src = os.path.join(here, "..", "builds", "linux-x64")
out = os.path.join(here, "..", "builds", "DreamyHellcore-linux-x64.tar.gz")


def fix(info):
    info.mode = 0o755 if info.name.endswith("/DreamyHellcore") or info.name.endswith(".so") else 0o644
    return info


with tarfile.open(out, "w:gz") as tar:
    for name in sorted(os.listdir(src)):
        if name.endswith(".sav"):
            continue
        tar.add(os.path.join(src, name), arcname="DreamyHellcore-linux-x64/" + name, filter=fix)
print("gepackt:", out, os.path.getsize(out), "Bytes")
