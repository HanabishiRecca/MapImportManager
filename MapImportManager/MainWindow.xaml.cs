using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using IWin32Window = System.Windows.Forms.IWin32Window;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;
using WinFormsDialogResult = System.Windows.Forms.DialogResult;

namespace MapImportManager {
    public partial class MainWindow : Window, IWin32Window {
        const string ProgName = "Map Import Manager";

        public IntPtr Handle { get; }

        public MainWindow() {
            try {
                Map.Load();
            } catch {
                ShowError(Properties.Resources.StormLibError);
                Environment.Exit(0x1);
            }

            InitializeComponent();
            Title = ProgName;
            Handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;

            OpenBut.ToolTip = $"{Properties.Resources.OpenMap} [Ctrl+O]";
            SaveBut.ToolTip = $"{Properties.Resources.SaveMap} [Ctrl+S]";
            ImportFileBut.ToolTip = $"{Properties.Resources.ImportFiles} [Ctrl+I]";
            ImportDirBut.ToolTip = $"{Properties.Resources.ImportDir} [Ctrl+D]";
            EditBut.ToolTip = $"{Properties.Resources.EditPath} [Enter]";
            DeleteBut.ToolTip = $"{Properties.Resources.Del} [Del]";
            ExportBut.ToolTip = $"{Properties.Resources.Export} [Ctrl+E]";
        }
        
        List<ImportFile> currentFileList;
        string currentMapPath;
        bool listChanged;

        void MakeChanged() {
            if(listChanged)
                return;

            listChanged = true;
            Title = $"{Title} *";
            SaveBut.IsEnabled = true;
        }

        OpenFileDialog mapSelectDialog = new OpenFileDialog {
            Multiselect = false,
            CheckFileExists = true,
            CheckPathExists = true,
            Title = Properties.Resources.OpenMap,
            Filter = $"{Properties.Resources.Maps}|*.w3m; *.w3x; *.w3n",
        };

        void OpenMap_Click(object sender, RoutedEventArgs e) {
            OpenMap();
        }

        void OpenMap() {
            if(listChanged && !ShowQuestion(Properties.Resources.ConfirmLoad))
                return;

            if(mapSelectDialog.ShowDialog(this) == WinFormsDialogResult.OK)
                LoadList(mapSelectDialog.FileName);
        }

        public void LoadList(string mapPath) {
            if(!File.Exists(mapPath))
                return;

            var list = Map.GetImportList(mapPath);
            if(list == null) {
                ShowError(Properties.Resources.OpenMapError);
                return;
            }

            listChanged = false;
            Title = $"{ProgName} - [{mapPath}]";
            currentMapPath = mapPath;
            ImportList.ItemsSource = currentFileList = list;
            ImportList.IsEnabled = true;
            SaveBut.IsEnabled = false;
            ImportFileBut.IsEnabled = true;
            ImportDirBut.IsEnabled = true;
            ExportBut.IsEnabled = true;
            AllowDrop = true;
        }

        void SaveMap_Click(object sender, RoutedEventArgs e) {
            SaveMap();
        }

        void SaveMap() {
            if(currentFileList == null || !listChanged)
                return;

            var saveResult = Map.SaveImportList(currentMapPath, currentFileList);
            if(saveResult.Saved) {
                if(saveResult.FailedFiles?.Count > 0)
                    ShowError($"{Properties.Resources.FailedFiles}{Environment.NewLine}{string.Join(Environment.NewLine, saveResult.FailedFiles)}");

                LoadList(currentMapPath);
            } else
                ShowError(Properties.Resources.SaveMapError);
        }

        OpenFileDialog fileSelectDialog = new OpenFileDialog {
            Multiselect = true,
            CheckFileExists = true,
            CheckPathExists = true,
            Title = Properties.Resources.ImportFiles,
        };

        void ImportFile_Click(object sender, RoutedEventArgs e) {
            ImportFileStart();
        }

        void ImportFileStart() {
            if(currentFileList == null)
                return;

            if(fileSelectDialog.ShowDialog(this) == WinFormsDialogResult.OK)
                AppendList(ImportFiles(fileSelectDialog.FileNames));
        }

        FolderBrowserDialog directorySelectDialog = new FolderBrowserDialog {
            Description = Properties.Resources.ImportDir,
            ShowNewFolderButton = false,
        };

        void ImportDirBut_Click(object sender, RoutedEventArgs e) {
            ImportDirStart();
        }

        void ImportDirStart() {
            if(currentFileList == null)
                return;

            if(directorySelectDialog.ShowDialog(this) == WinFormsDialogResult.OK)
                AppendList(ImportDirectory(directorySelectDialog.SelectedPath, ShowQuestion(Properties.Resources.ImportDirVariant)));
        }

        List<ImportFile> ImportFiles(string[] files) {
            var result = new List<ImportFile>(files.Length);

            for(int i = 0; i < files.Length; i++) {
                var path = files[i];
                if(File.Exists(path))
                    result.Add(new ImportFile(Path.GetFileName(path), path));
            }

            return result;
        }

        List<ImportFile> ImportDirectory(string directoryPath, bool includeFolderName) {
            var fName = new DirectoryInfo(directoryPath).Name;
            var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
            var result = new List<ImportFile>(files.Length);

            for(int i = 0; i < files.Length; i++) {
                var path = files[i];
                if(File.Exists(path)) {
                    var rel = path.Substring(directoryPath.Length + 1);
                    result.Add(new ImportFile(includeFolderName ? Path.Combine(fName, rel) : rel, path));
                }
            }

            return result;
        }

        void AppendList(List<ImportFile> newFiles) {
            for(int i = 0; i < newFiles.Count; i++) {
                var file = newFiles[i];
                var existing = currentFileList.Find(x => x.FilePath == file.FilePath);
                if(existing == null) {
                    currentFileList.Add(file);
                } else {
                    existing.DiskPath = file.DiskPath;
                    existing.Changed = true;
                }
            }
            ImportList.Items.Refresh();
            MakeChanged();
        }

        void DeleteBut_Click(object sender, RoutedEventArgs e) {
            MarkDeleteSelected();
        }

        void MarkDeleteSelected() {
            if(ImportList.SelectedItems.Count > 0) {
                var del = !(ImportList.SelectedItems[0] as ImportFile).Deleted;
                for(int i = 0; i < ImportList.SelectedItems.Count; i++) {
                    var file = ImportList.SelectedItems[i] as ImportFile;
                    if(string.IsNullOrEmpty(file.DiskPath))
                        file.Deleted = del;
                    else if(del)
                        currentFileList.Remove(file);
                }

                ImportList.Items.Refresh();
                MakeChanged();
            }
        }
        
        void ExportBut_Click(object sender, RoutedEventArgs e) {
            ExportDirStart();
        }

        FolderBrowserDialog exportDirDialog = new FolderBrowserDialog {
            Description = Properties.Resources.Export,
            ShowNewFolderButton = true,
        };

        void ExportDirStart() {
            if(currentFileList == null)
                return;

            if(exportDirDialog.ShowDialog(this) == WinFormsDialogResult.OK)
                Map.Export(currentMapPath, currentFileList, exportDirDialog.SelectedPath);
        }

        void Window_Hotkeys(object sender, KeyEventArgs e) {
            if(e.KeyboardDevice.Modifiers == ModifierKeys.Control) {
                if(e.Key == Key.O) {
                    OpenMap();
                    e.Handled = true;
                } else if(e.Key == Key.S) {
                    SaveMap();
                    e.Handled = true;
                } else if(e.Key == Key.I) {
                    ImportFileStart();
                    e.Handled = true;
                } else if(e.Key == Key.D) {
                    ImportDirStart();
                    e.Handled = true;
                } else if(e.Key == Key.E) {
                    ExportDirStart();
                    e.Handled = true;
                }
            }
        }
        
        void EditBut_Click(object sender, RoutedEventArgs e) {
            if(ImportList.SelectedItems.Count == 1)
                StartEdit(ImportList.SelectedItems[0] as ImportFile);
        }

        void StartEdit(ImportFile file) {
            if(file == null || file.Deleted)
                return;

            if(EditWindow.ShowEditor(file, this)) {
                ImportList.Items.Refresh();
                MakeChanged();
            }
        }

        void ImportList_MouseDown(object sender, MouseButtonEventArgs e) {
            if(e.OriginalSource is ScrollViewer)
                ImportList.SelectedItem = null;
        }

        void ImportList_Hotkeys(object sender, KeyEventArgs e) {
            if((e.Key == Key.Enter) && (ImportList.SelectedItems.Count == 1)) {
                StartEdit(ImportList.SelectedItems[0] as ImportFile);
                e.Handled = true;
                ImportList.Focus();
            } else if(e.Key == Key.Delete) {
                MarkDeleteSelected();
                e.Handled = true;
                ImportList.Focus();
            }
        }

        void Row_DoubleClick(object sender, MouseButtonEventArgs e) {
            StartEdit((sender as DataGridRow)?.DataContext as ImportFile);
        }

        void ImportList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            EditBut.IsEnabled = (ImportList.SelectedItems.Count == 1);
            DeleteBut.IsEnabled = (ImportList.SelectedItems.Count > 0);
        }

        void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            if(listChanged && !ShowQuestion(Properties.Resources.ConfirmExit))
                e.Cancel = true;
        }

        void Window_Drop(object sender, DragEventArgs e) {
            if(e.Data.GetDataPresent(DataFormats.FileDrop)) {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if(files == null)
                    return;
                
                AppendList(ImportFiles(files));
                for(int i = 0; i < files.Length; i++) {
                    var path = files[i];
                    if(Directory.Exists(path))
                        AppendList(ImportDirectory(path, true));
                }
            }
        }

        void ShowError(string msg) {
            MessageBox.Show(this, msg, ProgName, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        bool ShowQuestion(string msg) {
            return (MessageBox.Show(this, msg, ProgName, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes);
        }
    }

    public class BoolConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return (bool)value ? Properties.Resources.Yes : Properties.Resources.No;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return null;
        }
    }
}
