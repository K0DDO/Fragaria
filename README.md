# Voicemeter / Fragaria

Этот репозиторий содержит два проекта:

| Папка | Платформа | Описание |
|-------|-----------|----------|
| **`Fragaria/`** | **Windows** | Основной проект — микшер с маршрутизацией по окнам, WinUI 3 |
| `voicemeter/` | Linux (PipeWire) | Ранний прототип для Fedora |

## Fragaria (Windows)

См. [Fragaria/README.md](Fragaria/README.md) — сборка, настройка стрима, roadmap.

```powershell
cd Fragaria
dotnet build -c Release
```

## Voicemeter (Linux)

```bash
./run.sh
```

См. оригинальный README ниже для Linux-версии.

---

## Voicemeter (Linux) — оригинальный README

Виртуальный аудио-микшер для Linux (PipeWire), вдохновлённый Voicemeeter Potato/Banana.

## Возможности (MVP)

- **Две шины вывода**: наушники (A) и стрим (B) с независимой громкостью
- **Виртуальный микрофон** `Voicemeter Mic` — выберите его в OBS/Discord для захвата стрим-микса
- **Дорожка на каждое приложение** — отдельные фейдеры для наушников и стрима
- **Лимитер** — ползунок «Лимит» ограничивает максимальную громкость дорожки
- **Пресеты** — сохранение/загрузка JSON в `presets/`

## В системе видны

| Устройство | Назначение |
|---|---|
| Voicemeter Headphones | Шина наушников (внутри связана с вашими наушниками) |
| Voicemeter Stream | Шина стрима |
| Voicemeter Mic | Виртуальный микрофон для OBS/Discord |

Внутренние устройства скрыты (`device.hide=true`).

## Запуск

```bash
chmod +x run.sh
./run.sh
```

Требуется: Python 3, GTK 4 (`python3-gobject`), PipeWire, `pactl`, `pw-link`.

## Использование со стримом

1. Запустите Voicemeter
2. В OBS выберите источник звука **Voicemeter Mic**
3. Приложения автоматически играют через Voicemeter (default sink)
4. Регулируйте громкость каждого приложения отдельно для наушников и стрима

## Ограничения

- На Linux звук идёт **по приложению**, а не по окну (как в Windows Voicemeeter)
- Лимитер — программное ограничение громкости (не LADSPA-компрессор)
- При закрытии приложения виртуальные устройства удаляются

## Стек

Python 3 + GTK 4 (Libadwaita) + PipeWire/PulseAudio API через `pactl`/`pw-link`.
