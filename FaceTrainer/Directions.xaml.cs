using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

namespace FaceTrainer
{
    /// <summary>
    /// Interaction logic for Directions.xaml
    /// </summary>
    public partial class Directions : Page
    {
        public Directions()
        {
            InitializeComponent();
        }

        private void txtUserName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtUserName.Text.Length > 0)
                btnStart.IsEnabled = true;

            Regex rgx = new Regex("[^a-zA-Z0-9 -]");
            txtUserName.Text = rgx.Replace(txtUserName.Text, "");
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
