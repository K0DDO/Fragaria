"""Virtual PipeWire device setup and teardown."""

from __future__ import annotations

import atexit
from dataclasses import dataclass, field

from voicemeter import config
from voicemeter.audio import pactl


@dataclass
class DeviceManager:
    module_ids: list[int] = field(default_factory=list)
    hardware_sink: str | None = None
    _initialized: bool = False

    def setup(self) -> None:
        if self._initialized:
            return

        self.hardware_sink = pactl.get_default_hardware_sink()
        if not self.hardware_sink:
            # Fallback: pick first non-voicemeter sink
            for line in pactl.run(["pactl", "list", "short", "sinks"]).splitlines():
                parts = line.split("\t")
                if len(parts) >= 2 and not parts[1].startswith("voicemeter"):
                    self.hardware_sink = parts[1]
                    break

        self._create_sink(
            config.SINK_HEADPHONES,
            config.DESC_HEADPHONES,
            "device.icon-name=audio-headphones",
        )
        self._create_sink(
            config.SINK_STREAM,
            config.DESC_STREAM,
            "device.icon-name=audio-speakers",
        )
        self._create_sink(
            config.SINK_APPS,
            config.DESC_APPS,
            "device.hide=true",
        )

        # Virtual microphone (PipeWire Audio/Source)
        mic_id = pactl.run(
            [
                "pactl",
                "load-module",
                "module-null-sink",
                f"sink_name={config.SOURCE_MIC}",
                "sink_properties="
                f'device.description="{config.DESC_MIC}" '
                "media.class=Audio/Source/Virtual "
                "device.icon-name=audio-input-microphone",
            ]
        )
        self.module_ids.append(int(mic_id.strip()))

        # Stream bus -> virtual mic
        pactl.link_nodes(
            f"{config.SINK_STREAM}:monitor_FL",
            f"{config.SOURCE_MIC}:input_FL",
        )
        pactl.link_nodes(
            f"{config.SINK_STREAM}:monitor_FR",
            f"{config.SOURCE_MIC}:input_FR",
        )

        # Headphone bus -> real headphones
        if self.hardware_sink:
            pactl.link_nodes(
                f"{config.SINK_HEADPHONES}:monitor_FL",
                f"{self.hardware_sink}:playback_FL",
            )
            pactl.link_nodes(
                f"{config.SINK_HEADPHONES}:monitor_FR",
                f"{self.hardware_sink}:playback_FR",
            )

        pactl.set_default_sink(config.SINK_APPS)
        self._initialized = True
        atexit.register(self.teardown)

    def _create_sink(self, name: str, description: str, extra_props: str = "") -> None:
        props = f'device.description="{description}"'
        if extra_props:
            props += f" {extra_props}"
        out = pactl.run(
            [
                "pactl",
                "load-module",
                "module-null-sink",
                f"sink_name={name}",
                f"sink_properties={props}",
            ]
        )
        self.module_ids.append(int(out.strip()))

    def teardown(self) -> None:
        for module_id in reversed(self.module_ids):
            pactl.unload_module(module_id)
        self.module_ids.clear()
        self._initialized = False
