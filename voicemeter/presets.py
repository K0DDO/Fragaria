"""JSON preset save/load."""

from __future__ import annotations

import json
from pathlib import Path

from voicemeter import config


def presets_path() -> Path:
    base = Path(__file__).resolve().parent.parent.parent / config.PRESETS_DIR
    base.mkdir(parents=True, exist_ok=True)
    return base


def list_presets() -> list[str]:
    return sorted(p.stem for p in presets_path().glob("*.json"))


def save_preset(name: str, data: dict) -> None:
    path = presets_path() / f"{name}.json"
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False))


def load_preset(name: str) -> dict:
    path = presets_path() / f"{name}.json"
    return json.loads(path.read_text())
