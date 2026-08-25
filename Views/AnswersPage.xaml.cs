using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using English_Listen_WinUI.ViewModels;

namespace English_Listen_WinUI.Views
{
    public sealed partial class AnswersPage : Page
    {
        private const int MaxWordCount = 10000;
        private const int MaxWordLength = 256;
        private const int MaxTranslationLength = 2048;
        private readonly MainViewModel _viewModel;
        private List<DictationTestPage.WordTranslationPair>? _wordList;

        public AnswersPage()
        {
            InitializeComponent();
            _viewModel = App.SharedViewModel!;
            DataContext = _viewModel;
            Loaded += AnswersPage_Loaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is List<DictationTestPage.WordTranslationPair> wordList)
            {
                _wordList = wordList
                    .Take(MaxWordCount)
                    .Where(pair => pair != null && !string.IsNullOrWhiteSpace(pair.Word) && pair.Word.Trim().Length <= MaxWordLength && (pair.Translation?.Length ?? 0) <= MaxTranslationLength)
                    .Select(pair => new DictationTestPage.WordTranslationPair
                    {
                        Word = pair.Word.Trim(),
                        Translation = pair.Translation?.Trim() ?? string.Empty
                    })
                    .ToList();
            }
        }

        private void AnswersPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAnswers();
        }

        private void LoadAnswers()
        {
            var builder = new StringBuilder();

            if (_wordList is { Count: > 0 })
            {
                for (var i = 0; i < _wordList.Count; i++)
                {
                    var pair = _wordList[i];
                    builder.Append(i + 1).Append(". ").Append(pair.Word);
                    if (!string.IsNullOrEmpty(pair.Translation))
                        builder.Append("  (").Append(pair.Translation).Append(')');
                    builder.AppendLine();
                }
            }
            else if (_viewModel != null)
            {
                foreach (var word in (_viewModel.CurrentWords ?? new List<string>()).Take(MaxWordCount))
                {
                    var normalized = word?.Trim() ?? string.Empty;
                    if (normalized.Length == 0 || normalized.Length > MaxWordLength)
                        continue;
                    builder.Append(builder.Length + 1).Append(". ").AppendLine(normalized);
                }
            }

            AnswersTextBlock.Text = builder.ToString();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame?.Navigate(typeof(WordsPage));
        }
    }
}