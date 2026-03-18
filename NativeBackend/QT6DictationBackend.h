#pragma once
#include <string>
#include <vector>
#include <functional>
#include <memory>
#include <random>
#include <thread>
#include <chrono>
#include <atomic>

namespace EnglishListenNative {

    // Callback types matching QT6 behavior
    using WordChangedCallback = void(*)(const char* word, int currentIndex, int totalWords);
    using CountdownCallback = void(*)(int countdown);
    using TestStateCallback = void(*)(bool isTesting, bool isPaused);
    using SpeechStatusCallback = void(*)(bool isSpeaking);

    class QT6DictationBackend {
    public:
        QT6DictationBackend();
        ~QT6DictationBackend();

        // Initialization
        bool Initialize();
        void Shutdown();

        // Word list management
        void SetWords(const std::vector<std::string>& words);
        void SetRandomOrder(bool random);
        std::vector<std::string> GetWords() const;

        // Test management - matching QT6 exactly
        bool StartTest(int dictationMode); // 0=paper, 1=online
        void StopTest();
        void PauseResume();
        bool IsTesting() const;
        bool IsPaused() const;

        // Word navigation - matching QT6 exactly
        void NextWord();
        void PreviousWord();
        void RepeatWord();
        int GetCurrentIndex() const;
        int GetWordsCount() const;
        std::string GetCurrentWord() const;

        // Settings
        void SetReadInterval(int interval);
        int GetReadInterval() const;
        void SetFliteVoiceModel(const std::string& voiceModel);
        std::string GetFliteVoiceModel() const;

        // Online dictation
        void SubmitOnlineAnswer(const std::string& userInput);
        std::vector<std::string> GetUserInputs() const;
        std::vector<bool> GetAnswerResults() const;
        int GetCorrectAnswers() const;
        int GetWrongAnswers() const;

        // Speech control
        void SpeakWord(const std::string& word);
        void StopSpeech();
        void PauseSpeech();
        void ResumeSpeech();
        bool IsSpeaking() const;

        // Callbacks
        void SetWordChangedCallback(WordChangedCallback callback);
        void SetCountdownCallback(CountdownCallback callback);
        void SetTestStateCallback(TestStateCallback callback);
        void SetSpeechStatusCallback(SpeechStatusCallback callback);

    private:
        // Timer methods matching QT6 behavior
        void StartTimer();
        void StopTimer();
        void TimerTick();
        void ProcessWord();
        void MoveToNextWord();
        void SpeakCurrentWord();
        void RandomizeWords();

        // Core state matching QT6
        std::vector<std::string> m_words;
        std::vector<std::string> m_originalWords;
        std::vector<std::string> m_userInputs;
        std::vector<bool> m_answerResults;
        int m_currentIndex;
        int m_countdown;
        bool m_isTesting;
        bool m_isPaused;
        bool m_isOnlineMode;
        int m_readInterval;
        bool m_randomOrder;
        int m_correctAnswers;
        int m_wrongAnswers;
        std::string m_fliteVoiceModel;

        // Callbacks
        WordChangedCallback m_wordChangedCallback;
        CountdownCallback m_countdownCallback;
        TestStateCallback m_testStateCallback;
        SpeechStatusCallback m_speechStatusCallback;

        // Timer implementation matching QT6 QTimer behavior
        std::atomic<bool> m_timerRunning;
        std::thread m_timerThread;

        // Random generator
        std::mt19937 m_randomGenerator;
    };

}