using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LLMUninstaller.Core;
using LLMUninstaller.Core.Deletion;
using LLMUninstaller.Core.Export;
using LLMUninstaller.Core.Logging;
using LLMUninstaller.Core.Models;
using LLMUninstaller.Core.Scanning;
using LLMUninstaller.Core.Updates;
using LLMUninstaller.Core.Utilities;
using LLMUninstaller.Gui.Localization;
using Microsoft.Win32;

namespace LLMUninstaller.Gui;

public partial class MainWindow : Window
{
    private readonly LocalizationService _loc = App.Localization;
    private readonly IAppLogger _logger = new SqliteLogger();
    private readonly ModelScanner _scanner;
    private readonly ModelDeleter _deleter;
    private readonly UpdateChecker _updateChecker = new();
    private readonly CursorCacheCleaner _cursorCacheCleaner;
    private readonly ObservableCollection<ModelViewModel> _models = [];
    private CancellationTokenSource? _scanCts;
    private string? _statusKey;
    private object[] _statusArgs = [];

    public MainWindow()
    {
        InitializeComponent();
        _scanner = new ModelScanner(_logger);
        _deleter = new ModelDeleter(_logger);
        _cursorCacheCleaner = new CursorCacheCleaner(_logger);
        ModelsGrid.ItemsSource = _models;
        _loc.LanguageChanged += ApplyLocalization;
        ApplyLocalization();
        LoadColumnWidths();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e) =>
        await CheckForUpdatesAsync();

    private void ApplyLocalization()
    {
        Title = _loc.Get("AppTitle");
        HeaderTitle.Text = _loc.Get("AppTitle");
        HeaderSubtitle.Text = _loc.Get("AppSubtitle");
        ScanButton.Content = _loc.Get("Scan");
        ExportCsvButton.Content = _loc.Get("ExportCsv");
        PermanentDeleteCheckBox.Content = _loc.Get("DeletePermanently");
        CursorCacheCleanupCheckBox.Content = _loc.Get("CursorCacheCleanup");
        ClaudeCacheCleanupCheckBox.Content = _loc.Get("ClaudeCacheCleanup");
        DeleteButton.Content = _loc.Get("DeleteSelected");
        AboutButton.Content = _loc.Get("About");
        LanguageButton.Content = _loc.Get("LanguageToggle");

        ColName.Header = _loc.Get("ColName");
        ColSize.Header = _loc.Get("ColSize");
        ColType.Header = _loc.Get("ColType");
        ColApp.Header = _loc.Get("ColApp");
        ColModified.Header = _loc.Get("ColModified");
        ColPath.Header = _loc.Get("ColPath");
        Resources["OpenFolderToolTip"] = _loc.Get("OpenFolder");

        RefreshStatusText();
        UpdateSummary();
        RefreshModelDisplays();
    }

    private void RefreshStatusText()
    {
        StatusText.Text = _statusKey != null
            ? Strings.Format(_loc.Current, _statusKey, _statusArgs)
            : _loc.Get("StatusIdle");
    }

    private void SetStatus(string key, params object[] args)
    {
        _statusKey = key;
        _statusArgs = args;
        RefreshStatusText();
    }

    private void RefreshModelDisplays()
    {
        foreach (var model in _models)
            model.RefreshDisplay(_loc.Current);
    }

    private void LanguageButton_Click(object sender, RoutedEventArgs e) =>
        _loc.ToggleLanguage();

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow(_loc) { Owner = this };
        about.ShowDialog();
    }

    private void SelectAllCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb)
            return;

        var selectAll = cb.IsChecked == true;
        foreach (var model in _models)
            model.IsSelected = selectAll;

        UpdateDeleteButtonState();
        e.Handled = true;
    }

    private void RowCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdateDeleteButtonState();
        UpdateSelectAllCheckBox();
        e.Handled = true;
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string path })
            return;

        try
        {
            var folder = File.Exists(path) ? Path.GetDirectoryName(path) : path;
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                Strings.Format(_loc.Current, "OpenFolderError", ex.Message),
                _loc.Get("ErrorTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        e.Handled = true;
    }

    private void LoadColumnWidths()
    {
        var settings = GridColumnSettings.Load();
        if (settings != null)
        {
            ApplyColumnWidth(ColSelect, settings.Select);
            ApplyColumnWidth(ColName, settings.Name);
            ApplyColumnWidth(ColSize, settings.Size);
            ApplyColumnWidth(ColType, settings.Type);
            ApplyColumnWidth(ColApp, settings.App);
            ApplyColumnWidth(ColModified, settings.Modified);

            if (settings.PathColumn is > 20)
                ColPath.MinWidth = settings.PathColumn.Value;
        }
    }

    private void SaveColumnWidths()
    {
        GridColumnSettings.Save(new GridColumnSettings
        {
            Select = GetColumnWidth(ColSelect),
            Name = GetColumnWidth(ColName),
            Size = GetColumnWidth(ColSize),
            Type = GetColumnWidth(ColType),
            App = GetColumnWidth(ColApp),
            Modified = GetColumnWidth(ColModified),
            PathColumn = Math.Max(ColPath.ActualWidth, ColPath.MinWidth)
        });
    }

    private static void ApplyColumnWidth(DataGridColumn column, double? width)
    {
        if (width is > 20)
            column.Width = new DataGridLength(width.Value);
    }

    private static double GetColumnWidth(DataGridColumn column) =>
        column.Width.IsAbsolute ? column.Width.Value : column.ActualWidth;

    private void ModelsGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
            return;

        if (FindParent<Button>(source) != null)
            return;

        var checkbox = FindParent<CheckBox>(source);
        if (checkbox == null || checkbox == SelectAllCheckBox)
            return;

        if (checkbox.DataContext is ModelViewModel vm)
        {
            vm.IsSelected = !vm.IsSelected;
            checkbox.IsChecked = vm.IsSelected;
            UpdateDeleteButtonState();
            UpdateSelectAllCheckBox();
            e.Handled = true;
        }
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        while (true)
        {
            if (child is T match)
                return match;

            var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (parent == null)
                return null;

            child = parent;
        }
    }

    private void UpdateSelectAllCheckBox()
    {
        if (_models.Count == 0)
        {
            SelectAllCheckBox.IsChecked = false;
            return;
        }

        var allSelected = _models.All(m => m.IsSelected);
        var anySelected = _models.Any(m => m.IsSelected);
        SelectAllCheckBox.IsChecked = allSelected ? true : anySelected ? null : false;
    }

    private void UpdateDeleteButtonState() =>
        DeleteButton.IsEnabled = _models.Any(m => m.IsSelected);

    private static ModelInfo CreateCacheModelInfo(CacheItem item)
    {
        var (lastAccess, lastModified) = PathHelper.GetTimestamps(item.FullPath);

        return new ModelInfo
        {
            Name = item.Name,
            FullPath = item.FullPath,
            SizeBytes = PathHelper.GetSize(item.FullPath),
            Type = ModelType.Unknown,
            OwnerApplication = item.OwnerApplication,
            LastAccessTime = lastAccess,
            LastModifiedTime = lastModified,
            IsProtectedPath = false,
            IsDirectory = item.IsDirectory
        };
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();

        ScanButton.IsEnabled = false;
        DeleteButton.IsEnabled = false;
        ExportCsvButton.IsEnabled = false;
        SelectAllCheckBox.IsChecked = false;
        ScanProgress.Visibility = Visibility.Visible;
        ScanProgress.IsIndeterminate = true;
        _models.Clear();
        _statusKey = null;

        var progress = new Progress<ScanProgress>(p =>
            SetStatus("StatusScanning", p.CurrentPath, p.ModelsFound));

        try
        {
            var results = await _scanner.ScanAsync(new ScanOptions
            {
                ScanStandardPaths = true,
                ScanAdditionalDisks = true,
                Progress = progress,
                CancellationToken = _scanCts.Token
            });

            var allResults = results.ToList();

            // When requested, show app cache entries as additional “deletable models”
            // so they appear in the UI results list.
            if (CursorCacheCleanupCheckBox.IsChecked == true)
                allResults.AddRange(_cursorCacheCleaner.GetCursorCacheItems()
                    .Select(CreateCacheModelInfo));

            if (ClaudeCacheCleanupCheckBox.IsChecked == true)
                allResults.AddRange(_cursorCacheCleaner.GetClaudeCacheItems()
                    .Select(CreateCacheModelInfo));

            foreach (var model in allResults)
                _models.Add(new ModelViewModel(model, _loc.Current));

            UpdateSummary();
            ExportCsvButton.IsEnabled = _models.Count > 0;
            UpdateDeleteButtonState();
            SetStatus("StatusComplete", _models.Count);
        }
        catch (OperationCanceledException)
        {
            SetStatus("StatusCancelled");
        }
        catch (Exception ex)
        {
            SetStatus("StatusError", ex.Message);
            MessageBox.Show(
                Strings.Format(_loc.Current, "ScanError", ex.Message),
                _loc.Get("ErrorTitle"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ScanButton.IsEnabled = true;
            ScanProgress.Visibility = Visibility.Collapsed;
            ScanProgress.IsIndeterminate = false;
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = _models.Where(m => m.IsSelected).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show(_loc.Get("NoSelection"), _loc.Get("NoSelectionTitle"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var protectedModels = selected.Where(m => m.IsProtectedPath).ToList();
        var allowProtected = false;

        if (protectedModels.Count > 0)
        {
            var result = MessageBox.Show(
                Strings.Format(_loc.Current, "ProtectedMessage", protectedModels.Count),
                _loc.Get("ProtectedTitle"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                selected = selected.Where(m => !m.IsProtectedPath).ToList();

            allowProtected = result == MessageBoxResult.Yes;
        }

        if (selected.Count == 0)
            return;

        var totalSize = selected.Sum(m => m.SizeBytes);
        var permanentDelete = PermanentDeleteCheckBox.IsChecked == true;
        var confirmMessage = permanentDelete
            ? Strings.Format(_loc.Current, "ConfirmDeletePermanent", selected.Count, SizeFormatter.Format(totalSize))
            : Strings.Format(_loc.Current, "ConfirmDeleteRecycle", selected.Count, SizeFormatter.Format(totalSize));
        if (CursorCacheCleanupCheckBox.IsChecked == true)
            confirmMessage += "\n\n" + _loc.Get("CursorCacheCleanupConfirm");
        if (ClaudeCacheCleanupCheckBox.IsChecked == true)
            confirmMessage += "\n\n" + _loc.Get("ClaudeCacheCleanupConfirm");

        if (MessageBox.Show(confirmMessage, _loc.Get("ConfirmDeleteTitle"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        DeleteButton.IsEnabled = false;
        var freed = 0L;
        var errors = 0;

        foreach (var vm in selected)
        {
            var deleteResult = await _deleter.DeleteAsync(vm.Model, new DeleteOptions
            {
                UseRecycleBin = !permanentDelete,
                AllowProtectedPaths = allowProtected
            });

            if (deleteResult.Success)
            {
                freed += deleteResult.FreedBytes;
                _models.Remove(vm);
            }
            else
            {
                errors++;
            }
        }

        long cursorFreed = 0;
        int cursorErrors = 0;
        long claudeFreed = 0;
        int claudeErrors = 0;
        if (CursorCacheCleanupCheckBox.IsChecked == true)
        {
            var cursorResult = await _cursorCacheCleaner.ClearGlobalStorageAsync();
            cursorFreed = cursorResult.FreedBytes;
            cursorErrors = cursorResult.Errors;
        }
        if (ClaudeCacheCleanupCheckBox.IsChecked == true)
        {
            var claudeResult = await _cursorCacheCleaner.ClearClaudeCacheAsync();
            claudeFreed = claudeResult.FreedBytes;
            claudeErrors = claudeResult.Errors;
        }

        UpdateSummary();
        UpdateDeleteButtonState();
        UpdateSelectAllCheckBox();

        var deletedCount = selected.Count - errors;
        var totalFreed = freed + cursorFreed + claudeFreed;
        var message = Strings.Format(_loc.Current, "StatusDeleteResult",
            deletedCount, SizeFormatter.Format(totalFreed));
        if (errors > 0)
            message += "\n" + Strings.Format(_loc.Current, "StatusDeleteErrors", errors);
        if (cursorFreed > 0)
            message += "\n" + Strings.Format(_loc.Current, "CursorCacheCleared", SizeFormatter.Format(cursorFreed));
        if (claudeFreed > 0)
            message += "\n" + Strings.Format(_loc.Current, "ClaudeCacheCleared", SizeFormatter.Format(claudeFreed));
        if (cursorErrors > 0)
            message += "\n" + Strings.Format(_loc.Current, "CursorCacheErrors", cursorErrors);
        if (claudeErrors > 0)
            message += "\n" + Strings.Format(_loc.Current, "ClaudeCacheErrors", claudeErrors);

        SetStatus("StatusDeleteResult", deletedCount, SizeFormatter.Format(totalFreed));
        if (errors > 0)
            StatusText.Text += "\n" + Strings.Format(_loc.Current, "StatusDeleteErrors", errors);
        if (cursorFreed > 0)
            StatusText.Text += "\n" + Strings.Format(_loc.Current, "CursorCacheCleared", SizeFormatter.Format(cursorFreed));
        if (claudeFreed > 0)
            StatusText.Text += "\n" + Strings.Format(_loc.Current, "ClaudeCacheCleared", SizeFormatter.Format(claudeFreed));
        if (cursorErrors > 0)
            StatusText.Text += "\n" + Strings.Format(_loc.Current, "CursorCacheErrors", cursorErrors);
        if (claudeErrors > 0)
            StatusText.Text += "\n" + Strings.Format(_loc.Current, "ClaudeCacheErrors", claudeErrors);

        MessageBox.Show(message, _loc.Get("DeleteResultTitle"), MessageBoxButton.OK,
            errors + cursorErrors + claudeErrors > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
    }

    private async void ExportCsvButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = _loc.Get("CsvFilter"),
            FileName = $"llm_scan_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dialog.ShowDialog() != true)
            return;

        var models = _models.Select(m => m.Model).ToList();
        await ReportExporter.ExportCsvAsync(models, dialog.FileName);
        SetStatus("StatusReportSaved", dialog.FileName);
    }

    private void UpdateSummary()
    {
        var total = _models.Sum(m => m.SizeBytes);
        SummaryText.Text = Strings.Format(_loc.Current, "Summary", _models.Count, SizeFormatter.Format(total));
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var result = await _updateChecker.CheckForUpdateAsync();
            if (!result.UpdateAvailable || result.Update == null)
                return;

            var answer = MessageBox.Show(
                Strings.Format(_loc.Current, "UpdateAvailableMessage",
                    result.Update.Version, AppInfo.Version),
                _loc.Get("UpdateAvailableTitle"),
                MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (answer != MessageBoxResult.Yes)
                return;

            // Kaspersky может ругаться на процесс автозагрузки/установки.
            // Вместо скачивания бинарника просто открываем страницу последнего релиза.
            Process.Start(new ProcessStartInfo
            {
                FileName = AppInfo.ReleasesLatestUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                Strings.Format(_loc.Current, "UpdateError", ex.Message),
                _loc.Get("UpdateErrorTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            // no-op: progress bar used only for model scanning
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        SaveColumnWidths();
        _loc.LanguageChanged -= ApplyLocalization;
        base.OnClosed(e);
    }
}

public sealed class ModelViewModel : INotifyPropertyChanged
{
    private bool _isSelected;
    private AppLanguage _language;

    public ModelViewModel(ModelInfo model, AppLanguage language)
    {
        Model = model;
        _language = language;
    }

    public ModelInfo Model { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public string Name => Model.Name;
    public string FullPath => Model.FullPath;
    public string FormattedSize => Model.FormattedSize;
    public long SizeBytes => Model.SizeBytes;
    public ModelType Type => Model.Type;
    public string TypeDisplay => Model.Type.ToString();
    public DateTime LastModifiedTime => Model.LastModifiedTime;
    public string OwnerApplication => Model.OwnerApplication ?? Strings.Get("Dash", _language);
    public bool IsProtectedPath => Model.IsProtectedPath;
    public string LastModifiedDisplay =>
        Model.LastModifiedTime == DateTime.MinValue
            ? Strings.Get("Dash", _language)
            : Model.LastModifiedTime.ToString("dd.MM.yyyy HH:mm");

    public void RefreshDisplay(AppLanguage language)
    {
        _language = language;
        OnPropertyChanged(nameof(OwnerApplication));
        OnPropertyChanged(nameof(LastModifiedDisplay));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
