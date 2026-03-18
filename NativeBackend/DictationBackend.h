#pragma once
#include <string>
#include <vector>
#include <functional>
#include <memory>
#include <random>
#include <algorithm>

namespace EnglishListenNative {

    // Test result structure
    struct TestResult {
        std::string timestamp;
        int totalWords;
        int correctCount;
        double accuracy;
        std::string wordListName;
    };

    // User data structure
    struct UserData {
        std::string username;
        std::string nickname;
        std::string createdTime;
        std::string lastLoginTime;
        bool isActive;
        
        std::vector<std::string> wordLists;
        std::vector<TestResult> testHistory;
        int totalStudyTime;
        int completedTests;
        
        bool isDarkTheme;
        int readInterval;
        int speechEngine;
        bool isRandomOrder;
        
        bool allowDataCollection;
        bool allowCloudSync;
        bool allowAnalytics;
        bool shareLearningStats;
        
        UserData() : isActive(true), totalStudyTime(0), completedTests(0), 
                     isDarkTheme(false), readInterval(5), speechEngine(0), isRandomOrder(false),
                     allowDataCollection(false), allowCloudSync(false), 
                     allowAnalytics(false), shareLearningStats(false) {}
    };

    // Callback types for C# interop
    using WordChangedCallback = void(*)(const char* word, int currentIndex, int totalWords);
    using CountdownCallback = void(*)(int countdown);
    using TestStateCallback = void(*)(bool isTesting, bool isPaused);
    using SpeechStatusCallback = void(*)(bool isSpeaking);

    class DictationBackend {
    public:
        DictationBackend();
        ~DictationBackend();

        // Initialization
        bool Initialize();
        void Shutdown();

        // Word list management
        void SetWords(const std::vector<std::string>& words);
        void SetRandomOrder(bool random);
        std::vector<std::string> GetWords() const;

        // Test management
        bool StartTest(int dictationMode); // 0=paper, 1=online
        void StopTest();
        void PauseResume();
        bool IsTesting() const;
        bool IsPaused() const;

        // Word navigation
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
        void StartTimer();
        void StopTimer();
        void OnTimerTick();
        void ProcessWord();
        void MoveToNextWord();
        void SpeakCurrentWord();
        void RandomizeWords();

        // Core state
        std::vector<std::string> m_words;
        std::vector<std::string> m_originalWords;
        std::vector<std::string> m_userInputs;
        std::vector<bool> m_answerResults;
        int m_currentIndex;
        int m_testCountdown;
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

        // Random generator
        std::mt19937 m_randomGenerator;

        // Timer implementation (platform-specific)
        void* m_timer;
        static void TimerCallback(void* context);
    };

}