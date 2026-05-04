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

namespace FaceTrainer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Directions pDirections;
        Trainer pTrainer;
        Finished pFinished;

        public MainWindow()
        {
            InitializeComponent();
            
            pDirections = new Directions();
            pDirections.btnStart.Click += BtnStart_Click;

            pTrainer = new Trainer();
            pTrainer.TrainingCompleted += PTrainer_TrainingCompleted;

            pFinished = new Finished();
            pFinished.btnAnother.Click += BtnAnother_Click;

            frame.Content = pDirections;

            frame.Navigating += Frame_Navigating;
        }

        public void ClearHistory()
        {
            if (!this.frame.CanGoBack && !this.frame.CanGoForward)
            {
                return;
            }

            var entry = this.frame.RemoveBackEntry();
            while (entry != null)
            {
                entry = this.frame.RemoveBackEntry();
            }

            this.frame.Navigate(new PageFunction<string>() { RemoveFromJournal = true });
        }

        private void PTrainer_TrainingCompleted(object sender, EventArgs e)
        {
            frame.Navigate(pFinished);
        }

        private void BtnAnother_Click(object sender, RoutedEventArgs e)
        {
            pDirections.txtUserName.Text = "";
            pTrainer.Stop();
            ClearHistory();

            frame.Content = pDirections;

        }

        private void Frame_Navigating(object sender, NavigatingCancelEventArgs e)
        {
            // Check to see if the loaded page content is the trainer page
            if (Object.ReferenceEquals(e.Content, pTrainer))
            {
                pTrainer.Start(pDirections.txtUserName.Text);
            }
            else
                pTrainer.Stop();
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (pDirections.txtUserName.Text != "")
            {
                frame.Navigate(pTrainer);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
        }
    }
}
