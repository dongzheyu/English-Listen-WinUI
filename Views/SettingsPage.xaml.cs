using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using English_Listen_WinUI.ViewModels;

namespace English_Listen_WinUI.Views
{
    public sealed partial class SettingsPage : Page
    {
        private readonly MainViewModel _viewModel;
        private readonly Services.SpeechService _speechService = new();

        public SettingsPage()
        {
            this.InitializeComponent();
            _viewModel = App.SharedViewModel!;
            this.DataContext = _viewModel;
            Loaded += SettingsPage_Loaded;
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            ThemeToggle.IsOn = _viewModel.IsDarkTheme;
            IntervalSlider.Value = _viewModel.ReadInterval;
            IntervalText.Text = $"{_viewModel.ReadInterval}秒";
            RandomOrderToggle.IsOn = _viewModel.Settings.Settings.IsRandomOrder;
            LoadVoices();
        }

        private void LoadVoices()
        {
            VoiceComboBox.Items.Clear();
            var voices = _speechService.GetAvailableVoices();
            foreach (var voice in voices)
            {
                VoiceComboBox.Items.Add(new ComboBoxItem { Content = voice, Tag = voice });
            }
            if (VoiceComboBox.Items.Count > 0)
            {
                VoiceComboBox.SelectedIndex = 0;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame?.Navigate(typeof(HomePage));
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.Settings.SaveSettingsAsync();
        }

        private void ThemeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            _viewModel.IsDarkTheme = ThemeToggle.IsOn;
            _viewModel.Settings.Settings.IsDarkTheme = ThemeToggle.IsOn;
        }

        private void IntervalSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            var value = (int)e.NewValue;
            IntervalText.Text = $"{value}秒";
            _viewModel.ReadInterval = value;
        }

        private void RandomOrderToggle_Toggled(object sender, RoutedEventArgs e)
        {
            _viewModel.Settings.Settings.IsRandomOrder = RandomOrderToggle.IsOn;
        }

        private void SpeechEngineComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SpeechEngineComboBox.SelectedItem is ComboBoxItem item)
            {
                var tag = item.Tag?.ToString();
                if (int.TryParse(tag, out var engine))
                {
                    _viewModel.Settings.Settings.SpeechEngine = engine;
                }
            }
        }

        private async void TestVoiceButton_Click(object sender, RoutedEventArgs e)
        {
            if (VoiceComboBox.SelectedItem is ComboBoxItem item)
            {
                var voiceName = item.Tag?.ToString();
                if (!string.IsNullOrEmpty(voiceName))
                {
                    _speechService.SetVoice(voiceName);
                    await _speechService.SpeakAsync("Hello, this is a test.", 0);
                }
            }
        }
    }
}
