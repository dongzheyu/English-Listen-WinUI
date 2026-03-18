using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using English_Listen_WinUI.ViewModels;

namespace English_Listen_WinUI.Views
{
    public sealed partial class ModernDictationPage : Page
    {
        private readonly ModernDictationViewModel _viewModel;

        public ModernDictationPage()
        {
            this.InitializeComponent();
            _viewModel = new ModernDictationViewModel();
            this.DataContext = _viewModel;
            
            // Wire up property changes
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            
            // Load example words
            LoadExampleWords();
        }

        private void LoadExampleWords()
        {
            var exampleWords = @"apple
banana
computer
dictionary
education
friend
government
history
internet
knowledge
language
mathematics
nature
opportunity
program
question
research
science
technology
university
victory
water
example
young
zoo";
            
            WordsTextBox.Text = exampleWords;
            _viewModel.LoadWordsFromText(exampleWords);
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(_viewModel.IsTesting):
                    UpdateButtonStates();
                    UpdateStatusText();
                    break;
                case nameof(_viewModel.IsPaused):
                    UpdatePauseButton();
                    UpdateStatusText();
                    break;
                case nameof(_viewModel.CurrentWord):
                    UpdateWordDisplay();
                    break;
                case nameof(_viewModel.CountdownText):
                    UpdateCountdownDisplay();
                    break;
                case nameof(_viewModel.IsSpeaking):
                    UpdateSpeakingIndicator();
                    break;
                case nameof(_viewModel.CurrentIndex):
                case nameof(_viewModel.TotalWords):
                    UpdateProgressDisplay();
                    break;
            }
        }

        private void UpdateButtonStates()
        {
            StartButton.IsEnabled = !_viewModel.IsTesting;
            StopButton.IsEnabled = _viewModel.IsTesting;
            PauseButton.IsEnabled = _viewModel.IsTesting;
            PreviousButton.IsEnabled = _viewModel.IsTesting;
            RepeatButton.IsEnabled = _viewModel.IsTesting;
            NextButton.IsEnabled = _viewModel.IsTesting;
        }

        private void UpdatePauseButton()
        {
            PauseButton.Content = _viewModel.IsPaused ? "继续" : "暂停";
        }

        private void UpdateStatusText()
        {
            if (!_viewModel.IsTesting)
            {
                StatusText.Text = "准备开始";
            }
            else if (_viewModel.IsPaused)
            {
                StatusText.Text = "已暂停";
            }
            else
            {
                StatusText.Text = "测试进行中";
            }
        }

        private void UpdateWordDisplay()
        {
            CurrentWordText.Text = _viewModel.CurrentWord;
        }

        private void UpdateCountdownDisplay()
        {
            CountdownText.Text = _viewModel.CountdownText;
        }

        private void UpdateSpeakingIndicator()
        {
            SpeakingIndicator.IsActive = _viewModel.IsSpeaking;
            SpeakingIndicator.Visibility = _viewModel.IsSpeaking ? Visibility.Visible : Visibility.Collapsed;
            SpeakingStatusText.Text = _viewModel.IsSpeaking ? "正在朗读..." : "";
        }

        private void UpdateProgressDisplay()
        {
            ProgressText.Text = $"{_viewModel.CurrentIndex + 1}/{_viewModel.TotalWords}";
        }

        #region Event Handlers

        private void WordsTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _viewModel.LoadWordsFromText(WordsTextBox.Text);
        }

        private void LoadExampleButton_Click(object sender, RoutedEventArgs e)
        {
            LoadExampleWords();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            WordsTextBox.Text = "";
            _viewModel.LoadWordsFromText("");
        }

        private void IntervalNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            _viewModel.ReadInterval = (int)args.NewValue;
        }

        private void RandomOrderToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                _viewModel.IsRandomOrder = toggle.IsOn;
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.StartTestCommand.Execute(null);
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.StopTestCommand.Execute(null);
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.PauseResumeCommand.Execute(null);
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.PreviousWordCommand.Execute(null);
        }

        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.RepeatWordCommand.Execute(null);
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.NextWordCommand.Execute(null);
        }

        #endregion

        protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _viewModel.Dispose();
        }
    }
}