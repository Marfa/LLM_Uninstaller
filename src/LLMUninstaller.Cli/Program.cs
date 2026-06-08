using LLMUninstaller.Core;
using LLMUninstaller.Core.Export;
using LLMUninstaller.Core.Logging;
using LLMUninstaller.Core.Models;
using LLMUninstaller.Core.Scanning;
using LLMUninstaller.Core.Utilities;

namespace LLMUninstaller.Cli;

internal static class Program
{
    static async Task<int> Main(string[] args)
    {
        var options = ParseArgs(args);

        if (options.ShowHelp)
        {
            PrintHelp();
            return 0;
        }

        IAppLogger logger = options.UseJsonLog
            ? new JsonFileLogger(options.LogPath)
            : new SqliteLogger(options.LogPath);

        Console.WriteLine($"LLM Uninstaller CLI v{AppInfo.Version}");
        Console.WriteLine(new string('─', 50));

        var scanner = new ModelScanner(logger);
        var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var progress = new Progress<ScanProgress>(p =>
        {
            Console.Write($"\rСканирование: {p.CurrentPath,-60} Найдено: {p.ModelsFound}");
        });

        IReadOnlyList<ModelInfo> models;
        try
        {
            models = await scanner.ScanAsync(new ScanOptions
            {
                ScanStandardPaths = true,
                ScanAdditionalDisks = !options.NoDiskScan,
                AdditionalDrives = options.Drives,
                Progress = progress,
                CancellationToken = cts.Token
            });
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\n\nСканирование прервано.");
            return 1;
        }

        Console.WriteLine($"\n\nНайдено моделей: {models.Count}");
        Console.WriteLine($"Общий объём: {SizeFormatter.Format(models.Sum(m => m.SizeBytes))}");
        Console.WriteLine(new string('─', 50));

        PrintTable(models);

        if (!string.IsNullOrEmpty(options.ExportCsv))
        {
            await ReportExporter.ExportCsvAsync(models, options.ExportCsv);
            Console.WriteLine($"\nCSV-отчёт сохранён: {options.ExportCsv}");
        }
        else
        {
            var defaultDir = Path.Combine(Environment.CurrentDirectory, "reports");
            Directory.CreateDirectory(defaultDir);
            var csvPath = Path.Combine(defaultDir, $"scan_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            await ReportExporter.ExportCsvAsync(models, csvPath);
            Console.WriteLine($"\nОтчёт сохранён: {csvPath}");
        }

        return 0;
    }

    private static void PrintTable(IReadOnlyList<ModelInfo> models)
    {
        Console.WriteLine($"{"Название",-30} {"Размер",10} {"Тип",12} {"Приложение",-20}");
        Console.WriteLine(new string('─', 80));

        foreach (var m in models)
        {
            var name = m.Name.Length > 28 ? m.Name[..25] + "..." : m.Name;
            var owner = m.OwnerApplication ?? "—";
            if (owner.Length > 18) owner = owner[..15] + "...";

            Console.WriteLine($"{name,-30} {m.FormattedSize,10} {m.Type,-12} {owner,-20}");
            Console.WriteLine($"  {m.FullPath}");
            if (m.IsProtectedPath)
                Console.WriteLine("  ⚠ Защищённый системный путь");
        }
    }

    private static CliOptions ParseArgs(string[] args)
    {
        var options = new CliOptions();

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h":
                    options.ShowHelp = true;
                    break;
                case "--no-disk-scan":
                    options.NoDiskScan = true;
                    break;
                case "--json-log":
                    options.UseJsonLog = true;
                    break;
                case "--log" when i + 1 < args.Length:
                    options.LogPath = args[++i];
                    break;
                case "--export-csv" when i + 1 < args.Length:
                    options.ExportCsv = args[++i];
                    break;
                case "--drives" when i + 1 < args.Length:
                    options.Drives = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    break;
            }
        }

        return options;
    }

    private static void PrintHelp()
    {
        Console.WriteLine($"""
            LLM Uninstaller CLI v{AppInfo.Version}

            Использование:
              llmuninstaller-cli [опции]

            Опции:
              --export-csv <путь>    Экспорт отчёта в CSV
              --no-disk-scan         Не сканировать диски C:/D:/E:
              --drives C:,D:         Указать диски для сканирования
              --json-log             Логирование в JSON (по умолчанию SQLite)
              --log <путь>           Путь к файлу лога
              --help, -h             Справка

            Примеры:
              llmuninstaller-cli
              llmuninstaller-cli --export-csv report.csv
              llmuninstaller-cli --no-disk-scan --drives C:,D:
            """);
    }

    private sealed class CliOptions
    {
        public bool ShowHelp { get; set; }
        public bool NoDiskScan { get; set; }
        public bool UseJsonLog { get; set; }
        public string? LogPath { get; set; }
        public string? ExportCsv { get; set; }
        public IReadOnlyList<string> Drives { get; set; } = ["C:", "D:", "E:"];
    }
}
