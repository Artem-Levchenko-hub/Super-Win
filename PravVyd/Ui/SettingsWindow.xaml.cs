using System.Windows;
using Microsoft.Win32;
using PravVyd.Settings;

namespace PravVyd.Ui;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly SettingsStore _store;
    private readonly Action? _onChanged;
    private bool _loading;

    public SettingsWindow(AppSettings settings, SettingsStore store, Action? onChanged = null)
    {
        InitializeComponent();

        _settings = settings;
        _store = store;
        _onChanged = onChanged;

        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        _loading = true;

        MdCheck.IsChecked = _settings.MarkdownEnabled;
        PdfCheck.IsChecked = _settings.PdfEnabled;
        DocxCheck.IsChecked = _settings.DocxEnabled;

        TempRadio.IsChecked = _settings.Output == OutputLocation.Temp;
        DocsRadio.IsChecked = _settings.Output == OutputLocation.Documents;
        CustomRadio.IsChecked = _settings.Output == OutputLocation.Custom;
        FolderBox.Text = _settings.CustomOutputFolder ?? string.Empty;
        BrowseButton.IsEnabled = _settings.Output == OutputLocation.Custom;

        AutoStartCheck.IsChecked = AutoStart.IsEnabled();

        _loading = false;
    }

    private void Format_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading)
            return;

        _settings.MarkdownEnabled = MdCheck.IsChecked == true;
        _settings.PdfEnabled = PdfCheck.IsChecked == true;
        _settings.DocxEnabled = DocxCheck.IsChecked == true;
        Persist();
    }

    private void Location_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading)
            return;

        if (TempRadio.IsChecked == true)
            _settings.Output = OutputLocation.Temp;
        else if (DocsRadio.IsChecked == true)
            _settings.Output = OutputLocation.Documents;
        else if (CustomRadio.IsChecked == true)
            _settings.Output = OutputLocation.Custom;

        BrowseButton.IsEnabled = _settings.Output == OutputLocation.Custom;
        Persist();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog();
        if (!string.IsNullOrWhiteSpace(_settings.CustomOutputFolder))
            dialog.InitialDirectory = _settings.CustomOutputFolder;

        if (dialog.ShowDialog() != true)
            return;

        _settings.CustomOutputFolder = dialog.FolderName;
        FolderBox.Text = dialog.FolderName;
        Persist();
    }

    private void AutoStart_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading)
            return;

        var enabled = AutoStartCheck.IsChecked == true;
        AutoStart.Set(enabled);
        _settings.AutoStart = enabled;
        Persist();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Persist()
    {
        _store.Save(_settings);
        _onChanged?.Invoke();
    }
}
