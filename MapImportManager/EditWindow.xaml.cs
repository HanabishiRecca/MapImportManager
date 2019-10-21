using System.Windows;
using System.Windows.Input;

namespace MapImportManager {
    public partial class EditWindow : Window {
        EditWindow(ImportFile file, Window parentWindow) {
            Owner = parentWindow;
            InitializeComponent();
            thisFile = file;
            PathBox.Text = thisFile.FilePath;
            PathBox.TextChanged += (o, e) => changed = true;
        }

        ImportFile thisFile;
        bool changed;

        public static bool ShowEditor(ImportFile file, Window parentWindow) {
            return (new EditWindow(file, parentWindow).ShowDialog()) == true;
        }

        void SaveAndExit() {
            if(changed) {
                thisFile.FilePath = PathBox.Text;
                DialogResult = thisFile.Changed = true;
            }
            Close();
        }

        void OkButton_Click(object sender, RoutedEventArgs e) {
            SaveAndExit();
        }

        void CancelButton_Click(object sender, RoutedEventArgs e) {
            Close();
        }

        void Window_KeyDown(object sender, KeyEventArgs e) {
            if(e.Key == Key.Enter)
                SaveAndExit();
            else if(e.Key == Key.Escape)
                Close();
        }
    }
}
