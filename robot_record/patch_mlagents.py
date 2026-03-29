"""
Patches mlagents 0.28.0 for Python 3.11+ compatibility.

Run after `pip install -r requirements.txt`:
  python patch_mlagents.py

Fixes:
1. settings.py: Dict[...] generic aliases passed to cattr.register_structure_hook
   fail with functools.singledispatch on Python 3.11+. Replaced with
   register_structure_hook_func (predicate-based).
2. buffer.py: np.float removed in NumPy 1.24+. Replaced with float.
"""
import site
import sys
from pathlib import Path


def find_mlagents():
    for sp in site.getsitepackages():
        p = Path(sp) / "mlagents"
        if p.exists():
            return p
    return None


def patch_settings(mlagents_dir: Path):
    settings = mlagents_dir / "trainers" / "settings.py"
    text = settings.read_text(encoding="utf-8")
    changed = False

    # Patch 1: Dict[RewardSignalType, RewardSignalSettings]
    old1 = (
        "cattr.register_structure_hook(\n"
        "        Dict[RewardSignalType, RewardSignalSettings], RewardSignalSettings.structure\n"
        "    )"
    )
    new1 = (
        "cattr.register_structure_hook_func(\n"
        "        lambda t: t is Dict[RewardSignalType, RewardSignalSettings],\n"
        "        lambda val, _: RewardSignalSettings.structure(val, Dict[RewardSignalType, RewardSignalSettings]),\n"
        "    )"
    )
    if old1 in text:
        text = text.replace(old1, new1)
        changed = True
        print("  Patched settings.py: Dict[RewardSignalType, RewardSignalSettings]")

    # Patch 2: Dict[str, EnvironmentParameterSettings]
    old2 = (
        "cattr.register_structure_hook(\n"
        "        Dict[str, EnvironmentParameterSettings], EnvironmentParameterSettings.structure\n"
        "    )"
    )
    new2 = (
        "cattr.register_structure_hook_func(\n"
        "        lambda t: t is Dict[str, EnvironmentParameterSettings],\n"
        "        lambda val, _: EnvironmentParameterSettings.structure(val, Dict[str, EnvironmentParameterSettings]),\n"
        "    )"
    )
    if old2 in text:
        text = text.replace(old2, new2)
        changed = True
        print("  Patched settings.py: Dict[str, EnvironmentParameterSettings]")

    if changed:
        settings.write_text(text, encoding="utf-8")
    else:
        print("  settings.py: already patched or different version")


def patch_buffer(mlagents_dir: Path):
    buffer = mlagents_dir / "trainers" / "buffer.py"
    text = buffer.read_text(encoding="utf-8")

    old = "pad_value: np.float = 0"
    new = "pad_value: float = 0"
    if old in text:
        text = text.replace(old, new)
        buffer.write_text(text, encoding="utf-8")
        print("  Patched buffer.py: np.float -> float")
    else:
        print("  buffer.py: already patched or different version")


def main():
    mlagents_dir = find_mlagents()
    if mlagents_dir is None:
        print("ERROR: mlagents package not found in site-packages")
        sys.exit(1)

    print(f"Patching mlagents at: {mlagents_dir}")
    patch_settings(mlagents_dir)
    patch_buffer(mlagents_dir)
    print("Done.")


if __name__ == "__main__":
    main()
