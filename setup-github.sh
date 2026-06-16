#!/usr/bin/env bash
# Настройка GitHub и запуск сборки FragariaSetup.exe
set -euo pipefail

export PATH="$HOME/.local/bin:$PATH"
cd "$(dirname "$0")"

REPO_NAME="${1:-Fragaria}"
GITHUB_USER="${2:-K0DDO}"

echo ""
echo "=== Fragaria → GitHub Actions ==="
echo ""

# 1. GitHub CLI
if ! command -v gh &>/dev/null; then
    echo "Устанавливаю GitHub CLI..."
    curl -sSL "https://github.com/cli/cli/releases/download/v2.65.0/gh_2.65.0_linux_amd64.tar.gz" \
        | tar -xz -C /tmp
    mkdir -p "$HOME/.local/bin"
    cp /tmp/gh_2.65.0_linux_amd64/bin/gh "$HOME/.local/bin/"
fi

# 2. Авторизация
if ! gh auth status &>/dev/null; then
    echo "Нужен вход в GitHub. Откроется браузер или введи код."
    echo ""
    gh auth login -h github.com -p https -s repo,workflow
fi

echo "✓ Авторизован: $(gh api user -q .login)"

# 3. Репозиторий
if gh repo view "$GITHUB_USER/$REPO_NAME" &>/dev/null; then
    echo "✓ Репозиторий $GITHUB_USER/$REPO_NAME уже существует"
    git remote remove origin 2>/dev/null || true
    git remote add origin "https://github.com/$GITHUB_USER/$REPO_NAME.git"
else
    echo "Создаю репозиторий $GITHUB_USER/$REPO_NAME ..."
    gh repo create "$REPO_NAME" --public --source=. --remote=origin \
        --description "Fragaria — виртуальный аудио-микшер для Windows"
fi

# 4. Push
echo "Отправляю код на GitHub..."
git push -u origin main

# 5. Запуск сборки
echo ""
echo "Запускаю сборку установщика..."
gh workflow run "Build Fragaria Installer" --ref main

echo ""
echo "Ожидание завершения (3–8 минут)..."
sleep 10
RUN_ID=$(gh run list --workflow="Build Fragaria Installer" --limit 1 --json databaseId -q '.[0].databaseId')

if [[ -n "$RUN_ID" ]]; then
    gh run watch "$RUN_ID" || true
    echo ""
    STATUS=$(gh run view "$RUN_ID" --json conclusion -q .conclusion)
    if [[ "$STATUS" == "success" ]]; then
        echo ""
        echo "=========================================="
        echo "  СБОРКА УСПЕШНА!"
        echo "=========================================="
        echo ""
        echo "Скачать установщик:"
        echo "  gh run download $RUN_ID -n FragariaSetup"
        echo ""
        echo "Или в браузере:"
        echo "  https://github.com/$GITHUB_USER/$REPO_NAME/actions/runs/$RUN_ID"
        echo ""
        mkdir -p ./download
        gh run download "$RUN_ID" -n FragariaSetup -D ./download 2>/dev/null && \
            echo "✓ Скачано: ./download/FragariaSetup.exe" || \
            echo "Скачай артефакт вручную со страницы Actions"
    else
        echo "Сборка завершилась со статусом: $STATUS"
        echo "Логи: https://github.com/$GITHUB_USER/$REPO_NAME/actions/runs/$RUN_ID"
    fi
else
    echo "Открой Actions вручную:"
    echo "  https://github.com/$GITHUB_USER/$REPO_NAME/actions"
fi
