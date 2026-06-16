#!/usr/bin/env bash
# Восстановить обычный звук после Voicemeter/Fragaria (PipeWire)
set -euo pipefail

REAL=$(pactl get-default-sink 2>/dev/null || true)
# Если default уже voicemeter — найти первый реальный sink
if [[ "$REAL" == voicemeter* ]] || [[ -z "$REAL" ]]; then
  REAL=$(pactl list short sinks | awk '!/voicemeter/ {print $2; exit}')
fi

if [[ -z "$REAL" ]]; then
  echo "Не найден физический выход звука."
  exit 1
fi

echo "Восстанавливаю вывод на: $REAL"

for id in $(pactl list short sink-inputs 2>/dev/null | awk '{print $1}'); do
  pactl move-sink-input "$id" "$REAL" 2>/dev/null || true
done

pactl set-default-sink "$REAL"

for mid in $(pactl list short modules 2>/dev/null | awk '/voicemeter/ {print $1}' | sort -rn); do
  pactl unload-module "$mid" 2>/dev/null || true
done

echo "Готово. Default sink: $(pactl get-default-sink)"
