using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using English_Listen_WinUI.ViewModels;

namespace English_Listen_WinUI.Views
{
    public sealed partial class AnswersPage : Page
    {
        private readonly MainViewModel _viewModel;
        private List<DictationTestPage.WordTranslationPair>? _wordList;

        public AnswersPage()
        {
            this.InitializeComponent();
            _viewModel = App.SharedViewModel!;
            this.DataContext = _viewModel;
            Loaded += AnswersPage_Loaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is List<DictationTestPage.WordTranslationPair> wordList)
            {
                _wordList = wordList;
            }
        }

        private void AnswersPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAnswers();
        }

        private void LoadAnswers()
        {
            var answerText = "";
            
            if (_wordList != null && _wordList.Count > 0)
            {
                // 使用从DictationTestPage传递过来的单词列表
                for (int i = 0; i < _wordList.Count; i++)
                {
                    string translation = !string.IsNullOrEmpty(_wordList[i].Translation) ? $"  ({_wordList[i].Translation})" : "";
                    answerText += $"{i + 1}. {_wordList[i].Word}{translation}\n";
                }
            }
            else if (_viewModel != null)
            {
                // 回退到使用ViewModel中的单词列表
                if (_viewModel.CurrentWords != null && _viewModel.CurrentWords.Count > 0)
                {
                    foreach (var word in _viewModel.CurrentWords)
                    {
                        answerText += $"{_viewModel.CurrentWords.IndexOf(word) + 1}. {word.Trim()}\n";
                    }
                }
                else
                {
                    var words = _viewModel.WordsText.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < words.Length; i++)
                    {
                        answerText += $"{i + 1}. {words[i].Trim()}\n";
                    }
                }
            }
            
            AnswersTextBlock.Text = answerText;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // 返回单词管理界面
            Frame?.Navigate(typeof(WordsPage));
        }
    }
}
