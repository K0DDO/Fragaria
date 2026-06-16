# Fragaria

**Fragaria** — виртуальный аудио-микшер для Windows с маршрутизацией по окнам и 10 продвинутыми функциями.

## Все 10 функций

| # | Функция | Описание |
|---|---------|----------|
| 1 | **Noise Gate** | Отсечение фона с микрофона (порог в настройках) |
| 2 | **Ducking** | Приглушение игры/музыки, когда говоришь в микрофон |
| 3 | **EQ 3-band** | Low / Mid / High на каждой дорожке (−12…+12 dB) |
| 4 | **Горячие клавиши** | Ctrl+1 Игра, Ctrl+2 Разговор, Ctrl+3 Стрим |
| 5 | **Запись шин** | WAV в `Музыка/Fragaria/` (шина A и/или B) |
| 6 | **Drag & Drop окон** | Панель слева — клик или перетаскивание окна на дорожку |
| 7 | **Виртуальный драйвер** | Setup-скрипт + автоопределение VB-Cable |
| 8 | **Компрессор** | Настоящий dynamics compressor на каждой дорожке |
| 9 | **OBS WebSocket** | Реакция на смену сцен OBS (v5, порт 4455) |
| 10 | **Спектр-анализатор** | 32-полосный FFT-метр на мастер-шинах |

## Сборка установщика (Setup.exe)

Один файл **`FragariaSetup.exe`** — ставит в Program Files, создаёт ярлык, запускает после установки.

### На Windows

1. Установи [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Установи [Inno Setup 6](https://jrsoftware.org/isdl.php) (или `winget install JRSoftware.InnoSetup`)
3. Запусти:

```
Fragaria\build-installer.bat
```

Результат: **`Fragaria\dist\FragariaSetup.exe`**

Скопируй этот файл куда угодно и запусти на Windows — установщик всё сделает сам.

### Через GitHub Actions

1. Залей репозиторий на GitHub
2. **Actions** → **Build Fragaria Installer** → **Run workflow**
3. Скачай артефакт **FragariaSetup** → `FragariaSetup.exe`

### Что делает установщик

- Копирует Fragaria в `C:\Program Files\Fragaria\`
- Ярлык в меню Пуск (+ опционально на рабочий стол)
- Запускает Fragaria после установки
- Опция автозапуска с Windows
- Корректное удаление через «Программы и компоненты»

---

## Сборка portable EXE (без установщика)

### Вариант 1 — двойной клик

```
Fragaria\build.bat
```

### Вариант 2 — PowerShell

```powershell
cd Fragaria
.\build.ps1
```

Готовый файл: **`Fragaria\dist\Fragaria\Fragaria.exe`**

Папка `dist\Fragaria\` содержит все DLL (self-contained, ~150–200 MB). Можно скопировать целиком на флешку или в Program Files.

### Вариант 3 — GitHub Actions (без Visual Studio)

1. Залей репозиторий на GitHub
2. Actions → **Build Fragaria EXE** → Run workflow
3. Скачай артефакт **Fragaria-win-x64** → внутри `Fragaria.exe`

### Требования для сборки

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

> **На Linux exe собрать нельзя** — WinUI 3 требует Windows SDK. Используй Windows-машину или GitHub Actions.

## Быстрый старт (разработка)

1. Запусти Fragaria
2. Слева — список окон, кликни чтобы закрепить дорожку
3. Регулируй A (наушники) и B (стрим) на каждом окне
4. Раскрой дорожку → EQ, компрессор, лимиты
5. **Ctrl+1/2/3** — переключение сцен
6. **⏺ Запись** — пишет WAV
7. Включи **OBS WS** в футере для синхронизации со сценами

## Виртуальный драйвер

```powershell
.\driver\install-fragaria-driver.ps1
```

Пока используется VB-Audio Virtual Cable. Нативный драйвер `Fragaria Input` / `Output A` / `Output B` — в разработке.

## OBS

1. OBS → Tools → WebSocket Server Settings → Enable (port 4455)
2. Fragaria → включи «OBS WS»
3. При смене сцены Fragaria получает событие `CurrentProgramSceneChanged`

## Архитектура DSP

```
Окно → Process Loopback → EQ → Compressor → [Ducking] → Mix A/B
Микрофон → Noise Gate → EQ → Compressor → Mix A/B
Мастер A/B → Bus Compressor → Limiter → Выход + Запись + FFT
```

## Linux

См. `../voicemeter/` — ранний прототип для PipeWire.
