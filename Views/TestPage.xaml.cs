using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using English_Listen_WinUI.ViewModels;

namespace English_Listen_WinUI.Views
{
    public sealed partial class TestPage : Page
    {
        private readonly MainViewModel _viewModel;

        public TestPage()
        {
            this.InitializeComponent();
            _viewModel = App.SharedViewModel!;
            this.DataContext = _viewModel;
            Loaded += TestPage_Loaded;
        }

        private void TestPage_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel.StartTestCommand.Execute(null);
            IntervalSlider.Value = _viewModel.ReadInterval;
            IntervalText.Text = $"{_viewModel.ReadInterval}秒";
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel?.StopTestCommand.Execute(null);
            Frame?.Navigate(typeof(HomePage));
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel?.PreviousWordCommand.Execute(null);
            UpdateDisplay();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel?.NextWordCommand.Execute(null);
            UpdateDisplay();
        }

        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel?.RepeatWordCommand.Execute(null);
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel?.PauseResumeCommand.Execute(null);
            PauseButton.Content = _viewModel?.IsPaused == true ? "▶ 继续" : "⏸ 暂停";
        }

        private void IntervalSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            var value = (int)e.NewValue;
            IntervalText.Text = $"{value}秒";
            _viewModel.ReadInterval = value;
        }

        private void UpdateDisplay()
        {
            CurrentWordText.Text = _viewModel.CurrentWord;
            ProgressText.Text = $"{_viewModel.CurrentIndex + 1}/{_viewModel.TestHistory.Count}";
            CountdownLabel.Text = _viewModel.Countdown.ToString();
        }
    }
}
