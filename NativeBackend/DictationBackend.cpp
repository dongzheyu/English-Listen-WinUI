#include "DictationBackend.h"
#include <iostream>
#include <chrono>
#include <thread>
#include <random>

#ifdef _WIN32
#include <windows.h>
#endif

namespace EnglishListenNative {

    DictationBackend::DictationBackend()
        : m_currentIndex(0)
        , m_testCountdown(0)
        , m_isTesting(false)
        , m_isPaused(false)
        , m_isOnlineMode(false)
        , m_readInterval(5)
        , m_randomOrder(false)
        , m_correctAnswers(0)
        , m_wrongAnswers(0)
        , m_fliteVoiceModel("")
        , m_wordChangedCallback(nullptr)
        , m_countdownCallback(nullptr)
        , m_testStateCallback(nullptr)
        , m_speechStatusCallback(nullptr)
        , m_timer(nullptr)
        , m_randomGenerator(std::random_device{}())
    {
    }

    DictationBackend::~DictationBackend()
    {
        StopTest();
        StopTimer();
    }

    bool DictationBackend::Initialize()
    {
        // Initialize any platform-specific components
        return true;
    }

    void DictationBackend::Shutdown()
    {
        StopTest();
        StopTimer();
    }

    void DictationBackend::SetWords(const std::vector<std::string>& words)
    {
        m_words = words;
        m_originalWords = words;
        
        // Pre-allocate user inputs for online mode
        m_userInputs.resize(words.size());
        m_answerResults.resize(words.size());
        
        if (m_randomOrder) {
            RandomizeWords();
        }
    }

    void DictationBackend::SetRandomOrder(bool random)
    {
        m_randomOrder = random;
        if (random && !m_words.empty()) {
            RandomizeWords();
        } else if (!random && !m_originalWords.empty()) {
            m_words = m_originalWords;
        }
    }

    std::vector<std::string> DictationBackend::GetWords() const
    {
        return m_words;
    }

    bool DictationBackend::StartTest(int dictationMode)
    {
        if (m_words.empty()) {
            return false;
        }

        // Reset state
        m_currentIndex = 0;
        m_testCountdown = 0;
        m_isTesting = true;
        m_isPaused = false;
        m_isOnlineMode = (dictationMode == 1);
        m_correctAnswers = 0;
        m_wrongAnswers = 0;

        // Clear user inputs for new test
        std::fill(m_userInputs.begin(), m_userInputs.end(), "");
        std::fill(m_answerResults.begin(), m_answerResults.end(), false);

        // Randomize words if needed
        if (m_randomOrder) {
            RandomizeWords();
        }

        // Notify state change
        if (m_testStateCallback) {
            m_testStateCallback(m_isTesting, m_isPaused);
        }

        // Start timer for paper mode
        if (!m_isOnlineMode) {
            StartTimer();
        }

        // Speak first word immediately for online mode
        if (m_isOnlineMode && !m_words.empty()) {
            SpeakCurrentWord();
        }

        return true;
    }

    void DictationBackend::StopTest()
    {
        m_isTesting = false;
        m_isPaused = false;
        StopTimer();
        StopSpeech();

        if (m_testStateCallback) {
            m_testStateCallback(m_isTesting, m_isPaused);
        }
    }

    void DictationBackend::PauseResume()
    {
        if (!m_isTesting) return;

        m_isPaused = !m_isPaused;

        if (m_isPaused) {
            StopTimer();
            PauseSpeech();
        } else {
            if (!m_isOnlineMode) {
                StartTimer();
            }
            ResumeSpeech();
        }

        if (m_testStateCallback) {
            m_testStateCallback(m_isTesting, m_isPaused);
        }
    }

    bool DictationBackend::IsTesting() const
    {
        return m_isTesting;
    }

    bool DictationBackend::IsPaused() const
    {
        return m_isPaused;
    }

    void DictationBackend::NextWord()
    {
        if (!m_isTesting || m_isPaused || m_isOnlineMode) return;

        if (m_currentIndex < m_words.size() - 1) {
            m_currentIndex++;
            m_testCountdown = 0;
            
            // Update display
            if (m_wordChangedCallback) {
                m_wordChangedCallback(m_words[m_currentIndex].c_str(), m_currentIndex, m_words.size());
            }
            if (m_countdownCallback) {
                m_countdownCallback(m_readInterval);
            }
        } else {
            // Last word
            StopTest();
        }
    }

    void DictationBackend::PreviousWord()
    {
        if (!m_isTesting || m_isPaused || m_currentIndex <= 0) return;

        m_currentIndex--;
        m_testCountdown = 0;
        
        // Update display
        if (m_wordChangedCallback) {
            m_wordChangedCallback(m_words[m_currentIndex].c_str(), m_currentIndex, m_words.size());
        }
        if (m_countdownCallback) {
            m_countdownCallback(m_readInterval);
        }
    }

    void DictationBackend::RepeatWord()
    {
        if (!m_isTesting || m_isPaused) return;

        SpeakCurrentWord();
        
        // Reset countdown for paper mode
        if (!m_isOnlineMode) {
            m_testCountdown = 0;
            if (m_countdownCallback) {
                m_countdownCallback(m_readInterval);
            }
        }
    }

    int DictationBackend::GetCurrentIndex() const
    {
        return m_currentIndex;
    }

    int DictationBackend::GetWordsCount() const
    {
        return m_words.size();
    }

    std::string DictationBackend::GetCurrentWord() const
    {
        if (m_currentIndex >= 0 && m_currentIndex < m_words.size()) {
            return m_words[m_currentIndex];
        }
        return "";
    }

    void DictationBackend::SetReadInterval(int interval)
    {
        m_readInterval = interval;
    }

    int DictationBackend::GetReadInterval() const
    {
        return m_readInterval;
    }

    void DictationBackend::SetFliteVoiceModel(const std::string& voiceModel)
    {
        m_fliteVoiceModel = voiceModel;
    }

    std::string DictationBackend::GetFliteVoiceModel() const
    {
        return m_fliteVoiceModel;
    }

    void DictationBackend::SubmitOnlineAnswer(const std::string& userInput)
    {
        if (!m_isTesting || !m_isOnlineMode || m_currentIndex >= m_userInputs.size()) return;

        m_userInputs[m_currentIndex] = userInput;

        // Check answer
        std::string currentWord = GetCurrentWord();
        bool isCorrect = !userInput.empty() && 
                        userInput.length() == currentWord.length() &&
                        std::equal(userInput.begin(), userInput.end(), currentWord.begin(),
                                  [](char a, char b) { return std::tolower(a) == std::tolower(b); });

        m_answerResults[m_currentIndex] = isCorrect;

        if (isCorrect) {
            m_correctAnswers++;
        } else {
            m_wrongAnswers++;
        }

        // Move to next word after a delay
        std::this_thread::sleep_for(std::chrono::milliseconds(1500));
        MoveToNextWord();
    }

    std::vector<std::string> DictationBackend::GetUserInputs() const
    {
        return m_userInputs;
    }

    std::vector<bool> DictationBackend::GetAnswerResults() const
    {
        return m_answerResults;
    }

    int DictationBackend::GetCorrectAnswers() const
    {
        return m_correctAnswers;
    }

    int DictationBackend::GetWrongAnswers() const
    {
        return m_wrongAnswers;
    }

    void DictationBackend::SpeakWord(const std::string& word)
    {
        // This would integrate with Flite or Windows TTS
        // For now, we'll simulate speech with a delay
        if (m_speechStatusCallback) {
            m_speechStatusCallback(true);
        }

        // Simulate speech duration based on word length
        int duration = std::max(500, static_cast<int>(word.length() * 100));
        std::this_thread::sleep_for(std::chrono::milliseconds(duration));

        if (m_speechStatusCallback) {
            m_speechStatusCallback(false);
        }
    }

    void DictationBackend::StopSpeech()
    {
        if (m_speechStatusCallback) {
            m_speechStatusCallback(false);
        }
    }

    void DictationBackend::PauseSpeech()
    {
        // Implementation would pause current speech
    }

    void DictationBackend::ResumeSpeech()
    {
        // Implementation would resume paused speech
    }

    bool DictationBackend::IsSpeaking() const
    {
        // This would check actual speech state
        return false;
    }

    void DictationBackend::SetWordChangedCallback(WordChangedCallback callback)
    {
        m_wordChangedCallback = callback;
    }

    void DictationBackend::SetCountdownCallback(CountdownCallback callback)
    {
        m_countdownCallback = callback;
    }

    void DictationBackend::SetTestStateCallback(TestStateCallback callback)
    {
        m_testStateCallback = callback;
    }

    void DictationBackend::SetSpeechStatusCallback(SpeechStatusCallback callback)
    {
        m_speechStatusCallback = callback;
    }

    // Private methods
    void DictationBackend::StartTimer()
    {
        StopTimer();
        
        // Simple timer implementation using thread
        m_timer = new std::thread([this]() {
            while (m_isTesting && !m_isPaused) {
                std::this_thread::sleep_for(std::chrono::seconds(1));
                if (m_isTesting && !m_isPaused) {
                    OnTimerTick();
                }
            }
        });
    }

    void DictationBackend::StopTimer()
    {
        if (m_timer) {
            // Signal thread to stop
            m_isTesting = false;
            
            auto timerThread = static_cast<std::thread*>(m_timer);
            if (timerThread->joinable()) {
                timerThread->join();
            }
            delete timerThread;
            m_timer = nullptr;
        }
    }

    void DictationBackend::OnTimerTick()
    {
        if (!m_isTesting || m_isPaused || m_isOnlineMode) return;

        m_testCountdown++;
        int countdown = m_readInterval - m_testCountdown;

        if (m_countdownCallback) {
            m_countdownCallback(countdown);
        }

        if (countdown <= 0) {
            ProcessWord();
        }
    }

    void DictationBackend::ProcessWord()
    {
        if (!m_isTesting || m_isPaused) return;

        // Show "正在朗读"
        if (m_countdownCallback) {
            m_countdownCallback(-1); // Special value for "正在朗读"
        }

        // Speak current word
        SpeakCurrentWord();

        // Move to next word or finish test
        if (m_currentIndex < m_words.size() - 1) {
            MoveToNextWord();
        } else {
            StopTest();
        }
    }

    void DictationBackend::MoveToNextWord()
    {
        if (m_currentIndex < m_words.size() - 1) {
            m_currentIndex++;
            m_testCountdown = 0;

            // Update display
            if (m_wordChangedCallback) {
                m_wordChangedCallback(m_words[m_currentIndex].c_str(), m_currentIndex, m_words.size());
            }
            if (m_countdownCallback) {
                m_countdownCallback(m_readInterval);
            }

            // Speak immediately for online mode
            if (m_isOnlineMode) {
                SpeakCurrentWord();
            }
        } else {
            StopTest();
        }
    }

    void DictationBackend::SpeakCurrentWord()
    {
        std::string currentWord = GetCurrentWord();
        if (!currentWord.empty()) {
            SpeakWord(currentWord);
        }
    }

    void DictationBackend::RandomizeWords()
    {
        if (m_words.empty()) return;
        
        std::shuffle(m_words.begin(), m_words.end(), m_randomGenerator);
    }

    void DictationBackend::TimerCallback(void* context)
    {
        // Platform-specific timer callback
        DictationBackend* backend = static_cast<DictationBackend*>(context);
        if (backend) {
            backend->OnTimerTick();
        }
    }

}