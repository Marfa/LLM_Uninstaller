namespace LLMUninstaller.Gui.Localization;

public static class Strings
{
    private static readonly Dictionary<string, (string Ru, string En)> Map = new()
    {
        ["AppTitle"] = ("LLM Uninstaller", "LLM Uninstaller"),
        ["AppSubtitle"] = ("Поиск и удаление локально установленных AI-моделей",
            "Find and remove locally installed AI models"),
        ["Scan"] = ("Сканировать", "Scan"),
        ["ExportCsv"] = ("Экспорт CSV", "Export CSV"),
        ["DeletePermanently"] = ("Удалить безвозвратно", "Delete permanently"),
        ["DeleteSelected"] = ("Удалить выбранные", "Delete selected"),
        ["About"] = ("О программе", "About"),
        ["LanguageToggle"] = ("EN", "RU"),
        ["StatusIdle"] = ("Нажмите «Сканировать» для поиска моделей",
            "Click «Scan» to search for models"),
        ["StatusScanning"] = ("Сканирование: {0}  |  Найдено: {1}", "Scanning: {0}  |  Found: {1}"),
        ["StatusComplete"] = ("Сканирование завершено. Найдено моделей: {0}",
            "Scan complete. Models found: {0}"),
        ["StatusCancelled"] = ("Сканирование отменено.", "Scan cancelled."),
        ["StatusError"] = ("Ошибка: {0}", "Error: {0}"),
        ["StatusReportSaved"] = ("Отчёт сохранён: {0}", "Report saved: {0}"),
        ["StatusDeleteResult"] = ("Удалено: {0}. Освобождено: {1}.", "Deleted: {0}. Freed: {1}."),
        ["StatusDeleteErrors"] = ("Ошибок: {0}. Подробности в логе.", "Errors: {0}. See log for details."),
        ["Summary"] = ("Всего: {0} моделей  |  Занято: {1}", "Total: {0} models  |  Used: {1}"),
        ["ColSelect"] = ("✓", "✓"),
        ["ColName"] = ("Название", "Name"),
        ["ColSize"] = ("Размер", "Size"),
        ["ColType"] = ("Тип", "Type"),
        ["ColApp"] = ("Приложение", "Application"),
        ["ColModified"] = ("Изменён", "Modified"),
        ["ColPath"] = ("Путь", "Path"),
        ["ErrorTitle"] = ("Ошибка", "Error"),
        ["ScanError"] = ("Ошибка сканирования:\n{0}", "Scan error:\n{0}"),
        ["NoSelectionTitle"] = ("Нет выбора", "No selection"),
        ["NoSelection"] = ("Выберите модели для удаления.", "Select models to delete."),
        ["ProtectedTitle"] = ("Защищённые пути", "Protected paths"),
        ["ProtectedMessage"] = (
            "Выбрано {0} модель(ей) в защищённых системных каталогах (Windows, Program Files и т.д.).\n\nУдалить их всё равно?",
            "Selected {0} model(s) in protected system directories (Windows, Program Files, etc.).\n\nDelete them anyway?"),
        ["ConfirmDeleteTitle"] = ("Подтверждение удаления", "Confirm deletion"),
        ["ConfirmDeleteRecycle"] = (
            "Удалить {0} модель(ей) в Корзину?\n\nОсвободится: {1}",
            "Delete {0} model(s) to Recycle Bin?\n\nWill free: {1}"),
        ["ConfirmDeletePermanent"] = (
            "Удалить {0} модель(ей) безвозвратно?\n\nОсвободится: {1}",
            "Permanently delete {0} model(s)?\n\nWill free: {1}"),
        ["DeleteResultTitle"] = ("Результат удаления", "Deletion result"),
        ["CsvFilter"] = ("CSV файлы (*.csv)|*.csv", "CSV files (*.csv)|*.csv"),
        ["AboutTitle"] = ("О программе", "About"),
        ["AboutVersion"] = ("Версия {0}", "Version {0}"),
        ["AboutSourceCode"] = ("Исходный код", "Source code"),
        ["AboutDonate"] = ("Донат", "Donate"),
        ["AboutDonateCrypto"] = ("Донат криптой", "Crypto donation"),
        ["AboutClose"] = ("Закрыть", "Close"),
        ["UpdateAvailableTitle"] = ("Доступно обновление", "Update available"),
        ["UpdateAvailableMessage"] = (
            "Доступна новая версия {0} (текущая {1}).\n\nУстановить обновление?",
            "New version {0} is available (current {1}).\n\nInstall update?"),
        ["UpdateDownloading"] = ("Загрузка обновления… {0:F0}%", "Downloading update… {0:F0}%"),
        ["UpdateReady"] = ("Обновление загружено. Приложение будет перезапущено.",
            "Update downloaded. The app will restart."),
        ["UpdateErrorTitle"] = ("Ошибка обновления", "Update error"),
        ["UpdateError"] = ("Не удалось обновить приложение:\n{0}", "Failed to update the application:\n{0}"),
        ["Dash"] = ("—", "—"),
    };

    public static string Get(string key, AppLanguage language)
    {
        if (!Map.TryGetValue(key, out var pair))
            return key;
        return language == AppLanguage.Russian ? pair.Ru : pair.En;
    }

    public static string Format(AppLanguage language, string key, params object[] args)
    {
        var template = Get(key, language);
        return args.Length > 0 ? string.Format(template, args) : template;
    }
}
