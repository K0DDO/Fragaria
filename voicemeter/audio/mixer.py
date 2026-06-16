"""Per-application dual-bus routing (headphones + stream)."""

from __future__ import annotations

from dataclasses import dataclass, field

from voicemeter import config
from voicemeter.audio import pactl


@dataclass
class StripState:
    sink_input_id: int
    app_sink_name: str
    app_sink_module: int
    hp_loop_module: int
    stream_loop_module: int
    hp_loop_input: int | None = None
    stream_loop_input: int | None = None
    hp_volume: float = 100.0
    stream_volume: float = 100.0
    hp_limit: float = 100.0
    stream_limit: float = 100.0
    muted: bool = False


@dataclass
class MixerEngine:
    strips: dict[int, StripState] = field(default_factory=dict)
    master_hp: float = 100.0
    master_stream: float = 100.0
    master_hp_limit: float = 100.0
    master_stream_limit: float = 100.0

    def _apply_limited(self, value: float, limit: float) -> float:
        return max(0.0, min(value, limit))

    def _effective(self, value: float, limit: float, master: float, master_limit: float) -> float:
        v = self._apply_limited(value, limit)
        m = self._apply_limited(master, master_limit)
        return v * m / 100.0

    def ensure_strip(self, sink_input: pactl.SinkInputInfo) -> StripState:
        if sink_input.index in self.strips:
            return self.strips[sink_input.index]

        app_sink = f"{config.PREFIX}-app-{sink_input.index}"
        module_out = pactl.run(
            [
                "pactl",
                "load-module",
                "module-null-sink",
                f"sink_name={app_sink}",
                "sink_properties="
                f'device.description="VM internal" device.hide=true',
            ]
        )
        app_sink_module = int(module_out.strip())

        pactl.move_sink_input(sink_input.index, app_sink)

        hp_before = set(pactl.sink_inputs_on_sink(config.SINK_HEADPHONES))
        hp_loop = pactl.load_loopback(f"{app_sink}.monitor", config.SINK_HEADPHONES)
        hp_loop_input = pactl.new_sink_input_after(config.SINK_HEADPHONES, hp_before)

        stream_before = set(pactl.sink_inputs_on_sink(config.SINK_STREAM))
        stream_loop = pactl.load_loopback(f"{app_sink}.monitor", config.SINK_STREAM)
        stream_loop_input = pactl.new_sink_input_after(config.SINK_STREAM, stream_before)

        strip = StripState(
            sink_input_id=sink_input.index,
            app_sink_name=app_sink,
            app_sink_module=app_sink_module,
            hp_loop_module=hp_loop,
            stream_loop_module=stream_loop,
            hp_loop_input=hp_loop_input,
            stream_loop_input=stream_loop_input,
        )
        self.strips[sink_input.index] = strip
        self.apply_strip_volumes(strip)
        return strip

    def apply_strip_volumes(self, strip: StripState) -> None:
        if strip.muted:
            if strip.hp_loop_input is not None:
                pactl.set_sink_input_mute(strip.hp_loop_input, True)
            if strip.stream_loop_input is not None:
                pactl.set_sink_input_mute(strip.stream_loop_input, True)
            return

        hp_vol = self._effective(
            strip.hp_volume, strip.hp_limit, self.master_hp, self.master_hp_limit
        )
        stream_vol = self._effective(
            strip.stream_volume,
            strip.stream_limit,
            self.master_stream,
            self.master_stream_limit,
        )

        if strip.hp_loop_input is not None:
            pactl.set_sink_input_mute(strip.hp_loop_input, False)
            pactl.set_sink_input_volume(strip.hp_loop_input, hp_vol)
        if strip.stream_loop_input is not None:
            pactl.set_sink_input_mute(strip.stream_loop_input, False)
            pactl.set_sink_input_volume(strip.stream_loop_input, stream_vol)

    def apply_all(self) -> None:
        for strip in self.strips.values():
            self.apply_strip_volumes(strip)

    def remove_stale(self, active_ids: set[int]) -> None:
        for sid in list(self.strips):
            if sid not in active_ids:
                strip = self.strips.pop(sid)
                pactl.unload_module(strip.hp_loop_module)
                pactl.unload_module(strip.stream_loop_module)
                pactl.unload_module(strip.app_sink_module)

    def snapshot(self) -> dict:
        return {
            "master_hp": self.master_hp,
            "master_stream": self.master_stream,
            "master_hp_limit": self.master_hp_limit,
            "master_stream_limit": self.master_stream_limit,
            "strips": {
                str(k): {
                    "hp_volume": v.hp_volume,
                    "stream_volume": v.stream_volume,
                    "hp_limit": v.hp_limit,
                    "stream_limit": v.stream_limit,
                    "muted": v.muted,
                    "label": v.app_sink_name,
                }
                for k, v in self.strips.items()
            },
        }

    def load_snapshot(self, data: dict) -> None:
        self.master_hp = float(data.get("master_hp", 100))
        self.master_stream = float(data.get("master_stream", 100))
        self.master_hp_limit = float(data.get("master_hp_limit", 100))
        self.master_stream_limit = float(data.get("master_stream_limit", 100))
        strips_data = data.get("strips", {})
        for sid_str, sdata in strips_data.items():
            sid = int(sid_str)
            if sid in self.strips:
                strip = self.strips[sid]
                strip.hp_volume = float(sdata.get("hp_volume", 100))
                strip.stream_volume = float(sdata.get("stream_volume", 100))
                strip.hp_limit = float(sdata.get("hp_limit", 100))
                strip.stream_limit = float(sdata.get("stream_limit", 100))
                strip.muted = bool(sdata.get("muted", False))
        self.apply_all()
