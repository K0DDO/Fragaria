"""Main GTK window."""

from __future__ import annotations

import gi

gi.require_version("Gtk", "4.0")
from gi.repository import GLib, Gtk

from voicemeter import config
from voicemeter.audio import pactl
from voicemeter.audio.devices import DeviceManager
from voicemeter.audio.mixer import MixerEngine
from voicemeter.presets import list_presets, load_preset, save_preset
from voicemeter.ui.strip import StripWidget


class MainWindow(Gtk.ApplicationWindow):
    def __init__(self, app: Gtk.Application, devices: DeviceManager, mixer: MixerEngine):
        super().__init__(application=app, title="Voicemeter")
        self.devices = devices
        self.mixer = mixer
        self.strip_widgets: dict[int, StripWidget] = {}

        self.set_default_size(920, 640)

        root = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=8)
        root.set_margin_top(12)
        root.set_margin_bottom(12)
        root.set_margin_start(12)
        root.set_margin_end(12)
        self.set_child(root)

        root.append(self._build_header())
        root.append(self._build_master())
        root.append(Gtk.Separator())
        root.append(self._build_strips_area())

        GLib.timeout_add(config.POLL_INTERVAL_MS, self._poll_streams)

    def _build_header(self) -> Gtk.Widget:
        box = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=8)

        title = Gtk.Label(label="Voicemeter")
        title.add_css_class("title-1")
        title.set_halign(Gtk.Align.START)
        title.set_hexpand(True)
        box.append(title)

        preset_box = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=4)
        self.preset_combo = Gtk.ComboBoxText.new_with_entry()
        for name in list_presets():
            self.preset_combo.append_text(name)
        if list_presets():
            self.preset_combo.set_active(0)
        preset_box.append(self.preset_combo)

        load_btn = Gtk.Button(label="Загрузить")
        load_btn.connect("clicked", self._load_preset)
        preset_box.append(load_btn)

        save_btn = Gtk.Button(label="Сохранить")
        save_btn.connect("clicked", self._save_preset)
        preset_box.append(save_btn)

        box.append(preset_box)
        return box

    def _master_row(self, title: str, attr_vol: str, attr_lim: str) -> Gtk.Box:
        box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=4)
        box.set_hexpand(True)

        lbl = Gtk.Label(label=title, xalign=0)
        lbl.add_css_class("heading")
        box.append(lbl)

        vol = Gtk.Scale.new_with_range(Gtk.Orientation.HORIZONTAL, 0, 100, 1)
        vol.set_value(getattr(self.mixer, attr_vol))
        vol.connect("value-changed", lambda s: self._master_changed(attr_vol, attr_lim, s))
        box.append(vol)

        lim = Gtk.Scale.new_with_range(Gtk.Orientation.HORIZONTAL, 0, 100, 1)
        lim.set_value(getattr(self.mixer, attr_lim))
        lim.connect("value-changed", lambda s: self._master_limit_changed(attr_lim, s))
        box.append(lim)

        setattr(self, f"{attr_vol}_scale", vol)
        setattr(self, f"{attr_lim}_scale", lim)
        return box

    def _build_master(self) -> Gtk.Widget:
        frame = Gtk.Frame()
        frame.set_label("Мастер-шины")

        grid = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=16)
        grid.set_margin_top(8)
        grid.set_margin_bottom(8)
        grid.set_margin_start(8)
        grid.set_margin_end(8)

        grid.append(self._master_row("Наушники (A)", "master_hp", "master_hp_limit"))
        grid.append(self._master_row("Стрим (B)", "master_stream", "master_stream_limit"))

        info = Gtk.Label(
            label=(
                f"В системе: «{config.DESC_HEADPHONES}», «{config.DESC_STREAM}», "
                f"микрофон «{config.DESC_MIC}»"
            ),
            xalign=0,
            wrap=True,
        )
        info.add_css_class("dim-label")

        outer = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=6)
        outer.append(grid)
        outer.append(info)
        frame.set_child(outer)
        return frame

    def _build_strips_area(self) -> Gtk.Widget:
        scrolled = Gtk.ScrolledWindow()
        scrolled.set_vexpand(True)
        scrolled.set_policy(Gtk.PolicyType.NEVER, Gtk.PolicyType.AUTOMATIC)

        self.strips_box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=4)
        self.empty_label = Gtk.Label(label="Ожидание звука от приложений…")
        self.empty_label.add_css_class("dim-label")
        self.strips_box.append(self.empty_label)

        scrolled.set_child(self.strips_box)
        return scrolled

    def _master_changed(self, attr_vol: str, attr_lim: str, scale):
        val = scale.get_value()
        lim = getattr(self.mixer, attr_lim)
        if val > lim:
            val = lim
            scale.set_value(val)
        setattr(self.mixer, attr_vol, val)
        self.mixer.apply_all()

    def _master_limit_changed(self, attr_lim: str, scale):
        setattr(self.mixer, attr_lim, scale.get_value())
        self.mixer.apply_all()

    def _poll_streams(self) -> bool:
        active: dict[int, pactl.SinkInputInfo] = {}

        for si in pactl.list_sink_inputs():
            if not si.is_routable_app:
                continue
            if si.index in self.mixer.strips:
                active[si.index] = si
                continue
            # Grab any real application stream (new or already playing elsewhere).
            active[si.index] = si

        self.mixer.remove_stale(set(active.keys()))

        for sid, si in active.items():
            if sid not in self.strip_widgets:
                strip = self.mixer.ensure_strip(si)
                widget = StripWidget(strip, si.label, self.mixer, lambda: None)
                self.strip_widgets[sid] = widget
                self.strips_box.append(widget)
                if self.empty_label.get_parent() is not None:
                    self.strips_box.remove(self.empty_label)

        stale_widgets = [sid for sid in self.strip_widgets if sid not in active]
        for sid in stale_widgets:
            widget = self.strip_widgets.pop(sid)
            self.strips_box.remove(widget)

        if not self.strip_widgets and self.empty_label.get_parent() is None:
            self.strips_box.append(self.empty_label)

        return True

    def _load_preset(self, _btn):
        name = self.preset_combo.get_active_text()
        if not name:
            return
        try:
            data = load_preset(name)
            self.mixer.load_snapshot(data)
            self.master_hp_scale.set_value(self.mixer.master_hp)
            self.master_stream_scale.set_value(self.mixer.master_stream)
            self.master_hp_limit_scale.set_value(self.mixer.master_hp_limit)
            self.master_stream_limit_scale.set_value(self.mixer.master_stream_limit)
        except FileNotFoundError:
            self._toast(f"Пресет «{name}» не найден")

    def _save_preset(self, _btn):
        name = self.preset_combo.get_active_text()
        if not name:
            return
        save_preset(name, self.mixer.snapshot())
        self._toast(f"Пресет «{name}» сохранён")

    def _toast(self, text: str):
        toast = Gtk.Toast.new(text)
        toast.set_timeout(2)
        root = self.get_root()
        if isinstance(root, Gtk.ApplicationWindow):
            # Adwaita ToastOverlay not required for basic toast on window
            pass
        overlay = getattr(self, "_toast", None)
        if overlay is None:
            self._toast = toast
        # Simple fallback: update title briefly
        old = self.get_title()
        self.set_title(text)
        GLib.timeout_add(2000, lambda: self.set_title(old) or False)
