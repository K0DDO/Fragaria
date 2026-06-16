"""Voicemeter configuration constants."""

PREFIX = "voicemeter"

# User-visible virtual devices (shown in system sound settings)
SINK_HEADPHONES = f"{PREFIX}-headphones"
SINK_STREAM = f"{PREFIX}-stream"
SOURCE_MIC = f"{PREFIX}-mic"

# Internal routing sink where applications play
SINK_APPS = f"{PREFIX}-apps"

DESC_HEADPHONES = "Voicemeter Headphones"
DESC_STREAM = "Voicemeter Stream"
DESC_MIC = "Voicemeter Mic"
DESC_APPS = "Voicemeter Apps (internal)"

POLL_INTERVAL_MS = 400
PRESETS_DIR = "presets"
