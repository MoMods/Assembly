using Assembly.Helpers.Database.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Assembly.Helpers.Database.Views
{
    /// <summary>
    /// Interaction logic for DatabaseTool.xaml
    /// </summary>
    public partial class DatabaseTool
    {
        public DatabaseTool()
        {
            InitializeComponent();
            DataContext = new DatabaseToolViewModel();
        }

        public DatabaseTool(string pathPath)
        {
            InitializeComponent();
            DataContext = new DatabaseToolViewModel();

            tabPanel.SelectedIndex = 1;
        }

        public void Dispose()
        {

        }
    }
}
