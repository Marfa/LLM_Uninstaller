# LLM Uninstaller

[English version](README.en.md)

Приложение для Windows, которое автоматически обнаруживает локально установленные AI-модели (LLM, Embedding, Diffusion), показывает занимаемое ими место на диске и позволяет безопасно удалить выбранные модели.

![Скриншот LLM Uninstaller](docs/screenshot.png)

> **Создано в [Cursor](https://cursor.com)** — код этого проекта написан с помощью AI-редактора Cursor.

## Возможности

- **Автоматический поиск** моделей в стандартных каталогах: Ollama, LM Studio, Hugging Face, GPT4All, Jan, ComfyUI, Text Generation WebUI, KoboldCpp, llama.cpp, Open WebUI
- **Дополнительное сканирование** дисков C:, D:, E:
- **Классификация** по типу: LLM / Diffusion / Embedding
- **Безопасное удаление** — в Корзину Windows (по умолчанию) или безвозвратно
- **Защита системных каталогов** — Windows, Program Files, ProgramData требуют явного подтверждения
- **Логирование** в SQLite (GUI) или JSON/SQLite (CLI)
- **Экспорт отчётов** в CSV
- **Локализация** русский / английский
- **Автообновление** из [GitHub Releases](https://github.com/Marfa/LLM_Uninstaller/releases)

## Требования

- Windows 10/11 (64-bit)
- Для **framework-dependent** сборки: [.NET 8 Desktop Runtime (x64)](https://aka.ms/dotnet/8.0/windowsdesktopruntime-x64)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (для сборки из исходников)

## Скачать

Два варианта в [Releases](https://github.com/Marfa/LLM_Uninstaller/releases):

| Вариант | Размер | .NET на ПК | Файлы |
|---------|--------|------------|-------|
| **Portable** (self-contained) | ~70 МБ | не нужен | `LLMUninstaller.exe`, `llmuninstaller-cli.exe`, `LLMUninstaller-portable-win-x64.zip` |
| **Framework-dependent** | ~4 МБ | [.NET 8 Desktop Runtime](https://aka.ms/dotnet/8.0/windowsdesktopruntime-x64) | `LLMUninstaller-framework.exe`, `llmuninstaller-cli-framework.exe`, `LLMUninstaller-framework-win-x64.zip` |

## Сборка из исходников

```powershell
git clone https://github.com/Marfa/LLM_Uninstaller.git
cd LLM_Uninstaller
dotnet build LLMUninstaller.sln -c Release
```

### Публикация

**Portable** (не требует .NET на ПК):

```powershell
dotnet publish src\LLMUninstaller.Gui\LLMUninstaller.Gui.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
dotnet publish src\LLMUninstaller.Cli\LLMUninstaller.Cli.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

**Framework-dependent** (требует .NET 8 Desktop Runtime):

```powershell
dotnet publish src\LLMUninstaller.Gui\LLMUninstaller.Gui.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
dotnet publish src\LLMUninstaller.Cli\LLMUninstaller.Cli.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

## Использование CLI

```powershell
llmuninstaller-cli [опции]
```

| Параметр | Описание |
|----------|----------|
| `--export-csv <путь>` | Экспорт отчёта в CSV |
| `--no-disk-scan` | Не сканировать диски C:/D:/E: |
| `--drives C:,D:` | Указать диски для сканирования |
| `--json-log` | Логирование в JSON (по умолчанию SQLite) |
| `--log <путь>` | Путь к файлу лога |
| `--help` | Справка |

## Правила определения модели

Каталог считается моделью, если:

- содержит файл **> 500 МБ** с расширением из поддерживаемого списка, **или**
- суммарный размер каталога **> 1 ГБ**

### Поддерживаемые расширения

| Тип | Расширения |
|-----|------------|
| LLM | `.gguf`, `.bin`, `.safetensors`, `.pth`, `.pt` |
| Diffusion | `.ckpt`, `.safetensors`, `.onnx` |
| Embedding | `.gguf`, `.bin`, `.safetensors` |

## Стандартные пути поиска

| Приложение | Путь |
|------------|------|
| Ollama | `%USERPROFILE%\.ollama\models` |
| LM Studio | `%USERPROFILE%\.lmstudio\models` |
| Hugging Face | `%USERPROFILE%\.cache\huggingface\hub` |
| GPT4All | `%LOCALAPPDATA%\nomic.ai\GPT4All` |
| Jan | `%APPDATA%\Jan\data\models` |
| ComfyUI | `*\ComfyUI\models` |
| Text Generation WebUI | `*\text-generation-webui\models` |
| KoboldCpp | `*\KoboldCpp\models` |
| llama.cpp | `*\llama.cpp\models` |
| Open WebUI | `%USERPROFILE%\open-webui` |

## Структура проекта

```
LLMUninstaller/
├── .github/workflows/   # автоматическая сборка релизов
├── docs/                  # скриншоты и документация
├── src/
│   ├── LLMUninstaller.Core/
│   ├── LLMUninstaller.Cli/
│   └── LLMUninstaller.Gui/
└── LLMUninstaller.sln
```

## Поддержка

- Исходный код: [github.com/Marfa/LLM_Uninstaller](https://github.com/Marfa/LLM_Uninstaller)
- Донат: [donationalerts.com/r/themarfa](https://www.donationalerts.com/r/themarfa)
- Донат криптой: [nowpayments.io/donation/themarfa](https://nowpayments.io/donation/themarfa)

## Лицензия

Проект распространяется по лицензии [Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International (CC BY-NC-SA 4.0)](https://creativecommons.org/licenses/by-nc-sa/4.0/).

Вы можете свободно использовать, изменять и распространять проект при условии указания авторства, некоммерческого использования и распространения производных работ на тех же условиях.
