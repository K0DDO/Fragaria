"""GTK mixer strip widget."""

from __future__ import annotations

import gi

gi.require_version("Gtk", "4.0")
from gi.repository import Gtk

from voicemeter.audio.mixer import MixerEngine, StripState


class StripWidget(Gtk.Box):
    def __init__(self, strip: StripState, label: str, mixer: MixerEngine, on_change):
        super().__init__(orientation=Gtk.Orientation.VERTICAL, spacing=6)
        self.strip = strip
        self.mixer = mixer
        self.on_change = on_change
        self.set_margin_start(8)
        self.set_margin_end(8)
        self.set_margin_top(4)
        self.set_margin_bottom(4)

        title = Gtk.Label(label=label)
        title.set_halign(Gtk.Align.START)
        title.set_ellipsize(3)  # END
        title.add_css_class("heading")
        self.append(title)

        self.append(self._row("Наушники", strip.hp_volume, strip.hp_limit, self._hp_changed))
        self.append(self._row("Стрим", strip.stream_volume, strip.stream_limit, self._stream_changed))

        mute_btn = Gtk.ToggleButton(label="Mute")
        mute_btn.set_active(strip.muted)
        mute_btn.connect("toggled", self._mute_toggled)
        self.append(mute_btn)

    def _row(self, title: str, volume: float, limit: float, on_vol) -> Gtk.Box:
        box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=2)

        header = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL)
        header.append(Gtk.Label(label=title, xalign=0))
        value_lbl = Gtk.Label(label=f"{volume:.0f}%")
        value_lbl.set_halign(Gtk.Align.END)
        value_lbl.set_hexpand(True)
        header.append(value_lbl)
        box.append(header)

        scale = Gtk.Scale.new_with_range(Gtk.Orientation.HORIZONTAL, 0, 100, 1)
        scale.set_value(volume)
        scale.set_hexpand(True)
        scale.connect("value-changed", lambda s: on_vol(s, value_lbl))
        box.append(scale)

        lim_header = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL)
        lim_header.append(Gtk.Label(label="Лимит", xalign=0))
        lim_lbl = Gtk.Label(label=f"{limit:.0f}%")
        lim_lbl.set_halign(Gtk.Align.END)
        lim_lbl.set_hexpand(True)
        lim_header.append(lim_lbl)
        box.append(lim_header)

        lim_scale = Gtk.Scale.new_with_range(Gtk.Orientation.HORIZONTAL, 0, 100, 1)
        lim_scale.set_value(limit)
        lim_scale.add_css_class("limit-scale")
        lim_scale.connect("value-changed", lambda s: self._limit_changed(s, lim_lbl, title))
        box.append(lim_scale)

        if title == "Наушники":
            self.hp_scale = scale
            self.hp_limit_scale = lim_scale
        else:
            self.stream_scale = scale
            self.stream_limit_scale = lim_scale

        return box

    def _hp_changed(self, scale, label):
        self.strip.hp_volume = scale.get_value()
        label.set_text(f"{self.strip.hp_volume:.0f}%")
        self._apply()

    def _stream_changed(self, scale, label):
        self.strip.stream_volume = scale.get_value()
        label.set_text(f"{self.strip.stream_volume:.0f}%")
        self._apply()

    def _limit_changed(self, scale, label, bus: str):
        val = scale.get_value()
        label.set_text(f"{val:.0f}%")
        if bus == "Наушники":
            self.strip.hp_limit = val
            if self.strip.hp_volume > val:
                self.strip.hp_volume = val
                self.hp_scale.set_value(val)
        else:
            self.strip.stream_limit = val
            if self.strip.stream_volume > val:
                self.strip.stream_volume = val
                self.stream_scale.set_value(val)
        self._apply()

    def _mute_toggled(self, btn):
        self.strip.muted = btn.get_active()
        self._apply()

    def _apply(self):
        self.mixer.apply_strip_volumes(self.strip)
        self.on_change()
