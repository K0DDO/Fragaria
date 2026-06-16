"""Thin wrapper around pactl/pw-link subprocess calls."""

from __future__ import annotations

import re
import subprocess
from dataclasses import dataclass
from typing import Optional


def run(args: list[str], check: bool = True) -> str:
    result = subprocess.run(args, capture_output=True, text=True)
    if check and result.returncode != 0:
        raise RuntimeError(
            f"Command failed ({result.returncode}): {' '.join(args)}\n{result.stderr}"
        )
    return result.stdout


def load_loopback(source: str, sink: str) -> int:
    out = run(
        [
            "pactl",
            "load-module",
            "module-loopback",
            f"source={source}",
            f"sink={sink}",
            "latency_msec=40",
        ]
    )
    return int(out.strip())


def unload_module(module_id: int) -> None:
    run(["pactl", "unload-module", str(module_id)], check=False)


def set_default_sink(name: str) -> None:
    run(["pactl", "set-default-sink", name])


def move_sink_input(sink_input_id: int, sink_name: str) -> None:
    run(["pactl", "move-sink-input", str(sink_input_id), sink_name])


def set_sink_input_mute(sink_input_id: int, mute: bool) -> None:
    flag = "1" if mute else "0"
    run(["pactl", "set-sink-input-mute", str(sink_input_id), flag])


def set_sink_input_volume(sink_input_id: int, volume_percent: float) -> None:
    """Set linear volume 0..100 for a sink-input."""
    vol = max(0, min(100, volume_percent))
    # PulseAudio volume: 65536 = 100%
    pulse_vol = int(65536 * vol / 100)
    run(["pactl", "set-sink-input-volume", str(sink_input_id), str(pulse_vol)])


def link_nodes(source_port: str, sink_port: str) -> None:
    run(["pw-link", source_port, sink_port], check=False)


def get_default_hardware_sink() -> Optional[str]:
    out = run(["pactl", "get-default-sink"])
    name = out.strip()
    if name.startswith("voicemeter"):
        return None
    return name


@dataclass
class SinkInputInfo:
    index: int
    sink_index: int
    app_name: str
    media_name: str
    mute: bool
    is_virtual: bool = False

    @property
    def label(self) -> str:
        if self.media_name and self.media_name != self.app_name:
            return f"{self.app_name} — {self.media_name}"
        return self.app_name or f"Stream #{self.index}"

    @property
    def is_routable_app(self) -> bool:
        if self.is_virtual:
            return False
        if not self.app_name:
            return False
        if "loopback" in self.media_name.lower():
            return False
        blocked = {"Voicemeter", "pipewire", "PulseAudio Volume Control", "pavucontrol"}
        return self.app_name not in blocked


def _parse_bool(value: str) -> bool:
    return value.strip().lower() in ("yes", "true", "1")


def list_sink_inputs() -> list[SinkInputInfo]:
    out = run(["pactl", "list", "sink-inputs"])
    blocks = re.split(r"\n(?=Sink Input #)", out)
    items: list[SinkInputInfo] = []

    for block in blocks:
        if not block.startswith("Sink Input #"):
            continue
        idx_m = re.search(r"Sink Input #(\d+)", block)
        sink_m = re.search(r"Sink:\s*(\d+)", block)
        if not idx_m or not sink_m:
            continue

        app_name = ""
        media_name = ""
        mute = False
        is_virtual = False
        for line in block.splitlines():
            line = line.strip()
            if line.startswith("application.name = "):
                app_name = line.split("=", 1)[1].strip().strip('"')
            elif line.startswith("media.name = "):
                media_name = line.split("=", 1)[1].strip().strip('"')
            elif line.startswith("Mute:"):
                mute = _parse_bool(line.split(":", 1)[1])
            elif line.startswith("node.virtual = "):
                is_virtual = _parse_bool(line.split("=", 1)[1])

        items.append(
            SinkInputInfo(
                index=int(idx_m.group(1)),
                sink_index=int(sink_m.group(1)),
                app_name=app_name,
                media_name=media_name,
                mute=mute,
                is_virtual=is_virtual,
            )
        )
    return items


def sink_name_by_index(index: int) -> Optional[str]:
    out = run(["pactl", "list", "sinks"])
    pattern = rf"Sink #{index}\n.*?Name:\s*(\S+)"
    m = re.search(pattern, out, re.DOTALL)
    return m.group(1) if m else None


def find_sink_index(name: str) -> Optional[int]:
    for line in run(["pactl", "list", "short", "sinks"]).splitlines():
        parts = line.split("\t")
        if len(parts) >= 2 and parts[1] == name:
            return int(parts[0])
    return None


def sink_inputs_on_sink(sink_name: str) -> list[int]:
    sink_idx = find_sink_index(sink_name)
    if sink_idx is None:
        return []
    return [si.index for si in list_sink_inputs() if si.sink_index == sink_idx]


def new_sink_input_after(sink_name: str, before: set[int]) -> int | None:
    after = set(sink_inputs_on_sink(sink_name))
    created = after - before
    if not created:
        return None
    return max(created)
