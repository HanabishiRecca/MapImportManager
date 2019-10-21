using System.Windows;

namespace MapImportManager {
    public partial class App : Application {
        void Init(object sender, StartupEventArgs e) {
            var mainWindow = new MainWindow();
            mainWindow.Show();

            if(e.Args.Length > 0)
                mainWindow.LoadList(e.Args[0]);
        }
    }
}
