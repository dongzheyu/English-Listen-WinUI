using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using English_Listen_WinUI.Services;

namespace English_Listen_WinUI.Views
{
    public sealed partial class MemorizePage : Page
    {
        private List<DictationTestPage.WordTranslationPair> wordList = new();
        private int currentIndex;
        private int totalWords;
        private readonly TranslationLibraryService _translationLibrary = new();
        private SpeechSynthesizer? _synthesizer;

        public MemorizePage()
        {
            this.InitializeComponent();
            InitializeSynthesizer();
        }

        private void InitializeSynthesizer()
        {
            try
            {
                _synthesizer = new SpeechSynthesizer();
                _synthesizer.SetOutputToDefaultAudioDevice();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"语音初始化失败: {ex.Message}");
                _synthesizer = null;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            try
            {
                if (e.Parameter is List<DictationTestPage.WordTranslationPair> wordListParam)
                {
                    wordList = wordListParam;
                    var mainWindow = App.MainWindow as MainWindow;
                    mainWindow?.SetSidebarVisibility(false);
                }
                else
                {
                    var words = App.SharedViewModel?.CurrentWords;
                    if (words != null && words.Count > 0)
                    {
                        wordList = words.Select(w => new DictationTestPage.WordTranslationPair
                        {
                            Word = w,
                            Translation = _translationLibrary.GetTranslation(w) ?? ""
                        }).ToList();
                    }
                }

                if (wordList.Count == 0)
                {
                    ShowErrorDialog("没有可背诵的单词，请先在词库中选择单词。");
                    return;
                }

                totalWords = wordList.Count;
                currentIndex = 0;
                ShowCurrentWord();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnNavigatedTo error: {ex.Message}");
                ShowErrorDialog($"页面加载失败: {ex.Message}");
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            if (_synthesizer != null)
            {
                try { _synthesizer.SpeakAsyncCancelAll(); } catch { }
                try { _synthesizer.Dispose(); } catch { }
                _synthesizer = null;
            }

            var mainWindow = App.MainWindow as MainWindow;
            mainWindow?.SetSidebarVisibility(true);
        }

        private void ShowCurrentWord()
        {
            if (wordList.Count == 0) return;

            var pair = wordList[currentIndex];
            WordText.Text = pair.Word;
            TranslationText.Text = pair.Translation;
            ProgressText.Text = $"第 {currentIndex + 1} / {totalWords} 个";

            PreviousButton.IsEnabled = currentIndex > 0;
            NextButton.IsEnabled = currentIndex < totalWords - 1;
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentIndex > 0)
            {
                currentIndex--;
                ShowCurrentWord();
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentIndex < totalWords - 1)
            {
                currentIndex++;
                ShowCurrentWord();
            }
        }

        private void EndButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
            else
            {
                Frame.Navigate(typeof(HomePage));
            }
        }

        private void ReplayButton_Click(object sender, RoutedEventArgs e)
        {
            SpeakCurrentWord();
        }

        private async void SpeakCurrentWord()
        {
            if (_synthesizer == null || wordList.Count == 0) return;

            try
            {
                _synthesizer.SpeakAsyncCancelAll();
                var word = wordList[currentIndex].Word;

                var englishVoiceName = App.SharedViewModel?.Settings?.Settings?.WindowsTtsEnglishVoiceName;
                if (!string.IsNullOrEmpty(englishVoiceName))
                {
                    try { _synthesizer.SelectVoice(englishVoiceName); } catch { }
                }

                _synthesizer.SpeakAsync(word);

                var translation = wordList[currentIndex].Translation;
                if (!string.IsNullOrEmpty(translation))
                {
                    await Task.Delay(500);

                    var chineseVoiceName = App.SharedViewModel?.Settings?.Settings?.WindowsTtsChineseVoiceName;
                    if (!string.IsNullOrEmpty(chineseVoiceName))
                    {
                        try { _synthesizer.SelectVoice(chineseVoiceName); } catch { }
                    }

                    _synthesizer.SpeakAsync(translation);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"朗读失败: {ex.Message}");
            }
        }

        private void ShowErrorDialog(string message)
        {
            try
            {
                MainWindow.ShowNotification(message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowErrorDialog error: {ex.Message}");
            }
        }
    }
}
