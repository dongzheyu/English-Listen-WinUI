using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using English_Listen_WinUI.ViewModels;

namespace English_Listen_WinUI.Views
{
    public sealed partial class WordsPage : Page
    {
        private readonly MainViewModel _viewModel;

        public WordsPage()
        {
            this.InitializeComponent();
            _viewModel = App.SharedViewModel!;
            this.DataContext = _viewModel;
            Loaded += WordsPage_Loaded;
        }

        private void WordsPage_Loaded(object sender, RoutedEventArgs e)
        {
            WordListComboBox.ItemsSource = _viewModel.WordListFiles;
            if (_viewModel.WordListFiles.Count > 0)
            {
                WordListComboBox.SelectedIndex = 0;
            }
            WordsTextBox.Text = _viewModel.WordsText;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame?.Navigate(typeof(HomePage));
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.WordsText = WordsTextBox.Text;
            await _viewModel.SaveWordsAsync();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            WordsTextBox.Text = "";
        }

        private async void WordListComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (WordListComboBox.SelectedItem != null)
            {
                await _viewModel.LoadWordsAsync(WordListComboBox.SelectedItem as string);
                WordsTextBox.Text = _viewModel.WordsText;
            }
        }

        private async void NewWordListButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "新建词库",
                Content = new TextBox { PlaceholderText = "请输入词库名称" },
                PrimaryButtonText = "创建",
                CloseButtonText = "取消"
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var textBox = dialog.Content as TextBox;
                if (textBox != null && !string.IsNullOrWhiteSpace(textBox.Text))
                {
                    var fileName = textBox.Text + ".txt";
                    await _viewModel.ImportWordsFromFileAsync(fileName);
                    WordListComboBox.ItemsSource = _viewModel.WordListFiles;
                }
            }
        }

        private async void DeleteWordListButton_Click(object sender, RoutedEventArgs e)
        {
            if (WordListComboBox.SelectedItem == null) return;

            var dialog = new ContentDialog
            {
                Title = "确认删除",
                Content = $"确定要删除词库 \"{WordListComboBox.SelectedItem}\" 吗？",
                PrimaryButtonText = "删除",
                CloseButtonText = "取消"
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
            }
        }
    }
}
