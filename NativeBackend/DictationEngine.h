#pragma once
#include <string>
#include <vector>
#include <functional>
#include <memory>

namespace EnglishListenNative {

    // Forward declarations
    class SpeechEngine;
    class SettingsManager;

    // Callback types for C# interop
    using WordChangedCallback = void(*)(const char* word, int currentIndex, int totalWords);
    using CountdownCallback = void(*)(int countdown);
    using TestStateCallback = void(*)(bool isTesting, bool isPaused);

    class DictationEngine {
    public:
        DictationEngine();
        ~DictationEngine();

        // Initialization
        bool Initialize();
        void Shutdown();

        // Test management
        bool StartTest(const std::vector<std::string>& words, int dictationMode);
        void StopTest();
        void PauseResume();

        // Word navigation
        void NextWord();
        void PreviousWord();
        void RepeatWord();

        // Settings
        void SetReadInterval(int interval);
        void SetRandomOrder(bool random);
        void SetFliteVoiceModel(const std::string& voiceModel);

        // Callbacks
        void SetWordChangedCallback(WordChangedCallback callback);
        void SetCountdownCallback(CountdownCallback callback);
        void SetTestStateCallback(TestStateCallback callback);

        // State queries
        bool IsTesting() const;
        bool IsPaused() const;
        int GetCurrentIndex() const;
        int GetWordsCount() const;
        std::string GetCurrentWord() const;
        int GetReadInterval() const;

        // Online dictation
        void SubmitOnlineAnswer(const std::string& userInput);
        std::vector<std::string> GetUserInputs() const;
        std::vector<bool> GetAnswerResults() const;

    private:
        void StartTimer();
        void StopTimer();
        void OnTimerTick();
        void ProcessWord();
        void MoveToNextWord();
        void UpdateProgress();

        // Core state
        std::vector<std::string> m_words;
        std::vector<std::string> m_userInputs;
        std::vector<bool> m_answerResults;
        int m_currentIndex;
        int m_testCountdown;
        bool m_isTesting;
        bool m_isPaused;
        bool m_isOnlineMode;
        int m_readInterval;
        bool m_randomOrder;

        // Components
        std::unique_ptr<SpeechEngine> m_speechEngine;
        std::unique_ptr<SettingsManager> m_settingsManager;

        // Callbacks
        WordChangedCallback m_wordChangedCallback;
        CountdownCallback m_countdownCallback;
        TestStateCallback m_testStateCallback;

        // Timer (implementation depends on platform)
        void* m_timer;
        static void TimerCallback(void* context);
    };

}