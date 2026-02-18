using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SnipTool.Models;
using SnipTool.Services;

namespace SnipTool.UI;

public partial class LibraryWindow : Window
{
    private readonly AppSettings _settings;
    private readonly LibraryService _libraryService = new();
    private readonly ObservableCollection<CaptureItem> _filteredCaptures = new();
    private readonly ObservableCollection<FolderNode> _folders = new();
    private List<CaptureItem> _allCaptures = new();
    private string? _selectedFolderPath;

    public LibraryWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        CaptureList.ItemsSource = _filteredCaptures;
        FolderTree.ItemsSource = _folders;

        SourceInitialized += (_, _) =>
        {
            if (System.Windows.Application.Current is App app)
            {
                WindowThemeHelper.Apply(this, app.IsDarkMode);
            }
        };

        Loaded += (_, _) => RefreshLibrary();
        SearchBox.TextChanged += (_, _) => ApplyFilter();
        FolderTree.SelectedItemChanged += (_, args) =>
        {
            if (args.NewValue is FolderNode node)
            {
                _selectedFolderPath = node.FullPath;
                ApplyFilter();
            }
        };

        OpenButton.Click += (_, _) => OpenSelected();
        ShowInFolderButton.Click += (_, _) => ShowSelectedInFolder();
        EditButton.Click += (_, _) => OpenEditorForSelected();
        RefreshButton.Click += (_, _) => RefreshLibrary();
        OpenRootButton.Click += (_, _) => OpenFolder(_settings.SaveRootPath);
        CaptureList.MouseDoubleClick += (_, _) => OpenEditorForSelected();
        CaptureList.SelectionChanged += (_, _) => UpdateButtonStates();
        DeleteButton.Click += (_, _) => DeleteSelected();
        PreviewKeyDown += (s, e) =>
        {
            if (e.Key == Key.Delete) DeleteSelected();
        };

        UpdateButtonStates();
    }

    public void RefreshLibrary()
    {
        _allCaptures = _libraryService.LoadCaptures(_settings.SaveRootPath);
        _selectedFolderPath = _settings.SaveRootPath;

        _folders.Clear();
        var root = _libraryService.BuildFolderTree(_settings.SaveRootPath);
        _folders.Add(root);

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = (SearchBox.Text ?? string.Empty).Trim();
        var selectedFolder = _selectedFolderPath;

        IEnumerable<CaptureItem> items = _allCaptures;
        if (!string.IsNullOrWhiteSpace(selectedFolder))
        {
            items = items.Where(item => item.FilePath.StartsWith(selectedFolder, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            items = items.Where(item => item.FileName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.FolderPath.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var list = items.ToList();
        _filteredCaptures.Clear();
        foreach (var item in list)
        {
            _filteredCaptures.Add(item);
        }

        CountText.Text = $"{_filteredCaptures.Count} captures";
        EmptyStateText.Visibility = _filteredCaptures.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        var hasSelection = CaptureList.SelectedItems.Count > 0;
        var singleSelection = CaptureList.SelectedItems.Count == 1;
        
        OpenButton.IsEnabled = singleSelection;
        EditButton.IsEnabled = singleSelection;
        ShowInFolderButton.IsEnabled = singleSelection;
        DeleteButton.IsEnabled = hasSelection;
    }

    private CaptureItem? GetSelectedCapture() => CaptureList.SelectedItem as CaptureItem;

    private void DeleteSelected()
    {
        var selected = CaptureList.SelectedItems.Cast<CaptureItem>().ToList();
        if (selected.Count == 0) return;

        var message = selected.Count == 1 
            ? $"Are you sure you want to delete '{selected[0].FileName}'?" 
            : $"Are you sure you want to delete {selected.Count} items?";

        var result = System.Windows.MessageBox.Show(this, message, "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        foreach (var item in selected)
        {
            try
            {
                if (File.Exists(item.FilePath))
                {
                    File.Delete(item.FilePath);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to delete {item.FileName}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        RefreshLibrary();
    }

    private void OpenSelected()
    {
        var item = GetSelectedCapture();
        if (item == null || !File.Exists(item.FilePath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(item.FilePath) { UseShellExecute = true });
    }

    private void ShowSelectedInFolder()
    {
        var item = GetSelectedCapture();
        if (item == null || !File.Exists(item.FilePath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{item.FilePath}\"") { UseShellExecute = true });
    }

    private void OpenEditorForSelected()
    {
        var item = GetSelectedCapture();
        if (item == null || !File.Exists(item.FilePath))
        {
            return;
        }

        var editor = new EditorWindow(item.FilePath) { Owner = this };
        if (System.Windows.Application.Current is App app)
        {
            WindowThemeHelper.Apply(editor, app.IsDarkMode);
        }
        editor.ShowDialog();
    }

    private void OpenFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
    }
}
