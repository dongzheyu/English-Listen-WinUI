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
            
            var answerText = "";
            
            // Use CurrentWords if available, otherwise fall back to WordsText
            var words = _viewModel.CurrentWords != null && _viewModel.CurrentWords.Count > 0 
                ? _viewModel.CurrentWords 
                : _viewModel.WordsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            
            for (int i = 0; i < words.Count; i++)
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
