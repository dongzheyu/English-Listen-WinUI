using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using English_Listen_WinUI.ViewModels;

namespace English_Listen_WinUI.Views
{
    public sealed partial class AnswersPage : Page
    {
        private readonly MainViewModel _viewModel;

        public AnswersPage()
        {
            this.InitializeComponent();
            _viewModel = App.SharedViewModel!;
            this.DataContext = _viewModel;
            Loaded += AnswersPage_Loaded;
        }

        private void AnswersPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAnswers();
        }

        private void LoadAnswers()
        {
            if (_viewModel == null) return;
            
            var words = _viewModel.WordsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var answerText = "";
            
            for (int i = 0; i < words.Length; i++)
            {
                answerText += $"{i + 1}. {words[i].Trim()}\n";
            }
            
            AnswersTextBlock.Text = answerText;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame?.Navigate(typeof(HomePage));
        }
    }
}
