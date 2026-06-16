#!/usr/bin/env python3
"""Voicemeter — virtual audio mixer for PipeWire."""

from __future__ import annotations

import sys
from pathlib import Path

# Allow running without install
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import gi

gi.require_version("Gtk", "4.0")
gi.require_version("Adw", "1")
from gi.repository import Adw, Gtk

from voicemeter.audio.devices import DeviceManager
from voicemeter.audio.mixer import MixerEngine
from voicemeter.presets import load_preset
from voicemeter.ui.window import MainWindow


class VoicemeterApp(Adw.Application):
    def __init__(self):
        super().__init__(application_id="io.voicemeter.mixer")
        self.devices = DeviceManager()
        self.mixer = MixerEngine()

    def do_activate(self):
        self.devices.setup()
        try:
            self.mixer.load_snapshot(load_preset("default"))
        except FileNotFoundError:
            pass

        win = MainWindow(self, self.devices, self.mixer)
        win.present()

    def do_shutdown(self):
        self.mixer.remove_stale(set())
        if self.devices.hardware_sink:
            from voicemeter.audio import pactl

            pactl.run(
                ["pactl", "set-default-sink", self.devices.hardware_sink],
                check=False,
            )
        self.devices.teardown()
        super().do_shutdown()


def main():
    app = VoicemeterApp()
    return app.run(sys.argv)


if __name__ == "__main__":
    raise SystemExit(main())
