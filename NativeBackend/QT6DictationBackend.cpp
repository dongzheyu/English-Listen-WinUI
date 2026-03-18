#include "QT6DictationBackend.h"
#include <iostream>
#include <chrono>
#include <thread>
#include <random>
#include <algorithm>

namespace EnglishListenNative {

    QT6DictationBackend::QT6DictationBackend()
        : m_currentIndex(0)
        , m_countdown(0)
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
        , m_timerRunning(false)
        , m_randomGenerator(std::random_device{}())
    {
    }

    QT6DictationBackend::~QT6DictationBackend()
    {
        StopTest();
        StopTimer();
    }

    bool QT6DictationBackend::Initialize()
    {
        return true;
    }

    void QT6DictationBackend::Shutdown()
    {
        StopTest();
        StopTimer();
    }

    void QT6DictationBackend::SetWords(const std::vector<std::string>& words)
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

    void QT6DictationBackend::SetRandomOrder(bool random)
    {
        m_randomOrder = random;
        if (random && !m_words.empty()) {
            RandomizeWords();
        } else if (!random && !m_originalWords.empty()) {
            m_words = m_originalWords;
        }
    }

    std::vector<std::string> QT6DictationBackend::GetWords() const
    {
        return m_words;
    }

    bool QT6DictationBackend::StartTest(int dictationMode)
    {
        if (m_words.empty()) {
            return false;
        }

        // Reset state - matching QT6 exactly
        m_currentIndex = 0;
        m_countdown = m_readInterval;
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

        // Start timer for paper mode - matching QT6 exactly
        if (!m_isOnlineMode) {
            StartTimer();
            
            // Show initial countdown
            if (m_countdownCallback) {
                m_countdownCallback(m_countdown);
            }
            
            // Show first word
            if (m_wordChangedCallback) {
                m_wordChangedCallback(m_words[m_currentIndex].c_str(), m_currentIndex + 1, m_words.size());
            }
        } else {
            // Online mode: speak first word immediately
            SpeakCurrentWord();
            
            // Show first word
            if (m_wordChangedCallback) {
                m_wordChangedCallback(m_words[m_currentIndex].c_str(), m_currentIndex + 1, m_words.size());
            }
        }

        return true;
    }

    void QT6DictationBackend::StopTest()
    {
        m_isTesting = false;
        m_isPaused = false;
        StopTimer();
        StopSpeech();

        if (m_testStateCallback) {
            m_testStateCallback(m_isTesting, m_isPaused);
        }
    }

    void QT6DictationBackend::PauseResume()
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

    bool QT6DictationBackend::IsTesting() const
    {
        return m_isTesting;
    }

    bool QT6DictationBackend::IsPaused() const
    {
        return m_isPaused;
    }

    // Matching QT6 onNextWord() exactly
    void QT6DictationBackend::NextWord()
    {
        // If paused, do nothing - matching QT6 line 1834-1836
        if (m_isPaused) {
            return;
        }

        // Decrement countdown - matching QT6 line 1838
        m_countdown--;
        
        // Update countdown display - matching QT6 line 1839
        if (m_countdownCallback) {
            m_countdownCallback(m_countdown);
        }

        // If countdown reaches 0 - matching QT6 line 1841
        if (m_countdown <= 0) {
            ProcessWord();
        }
    }

    void QT6DictationBackend::PreviousWord()
    {
        // Ensure not first word
        if (m_currentIndex > 0) {
            m_currentIndex--;
            
            // Show "正在朗读" - matching QT6 line 1902
            if (m_countdownCallback) {
                m_countdownCallback(-1); // -1 means "正在朗读"
            }

            // Speak current word
            SpeakCurrentWord();

            // Reset countdown - matching QT6 line 1911
            m_countdown = m_readInterval;
            if (m_countdownCallback) {
                m_countdownCallback(m_countdown);
            }
            
            // Update word display
            if (m_wordChangedCallback) {
                m_wordChangedCallback(m_words[m_currentIndex].c_str(), m_currentIndex + 1, m_words.size());
            }
        }
    }

    void QT6DictationBackend::RepeatWord()
    {
        // Ensure current index is valid - matching QT6 line 1900
        if (m_currentIndex < m_words.size()) {
            // Show "正在朗读" - matching QT6 line 1902
            if (m_countdownCallback) {
                m_countdownCallback(-1); // -1 means "正在朗读"
            }

            // Speak current word
            SpeakCurrentWord();

            // Reset countdown - matching QT6 line 1911
            m_countdown = m_readInterval;
            if (m_countdownCallback) {
                m_countdownCallback(m_countdown);
            }
        }
    }

    int QT6DictationBackend::GetCurrentIndex() const
    {
        return m_currentIndex;
    }

    int QT6DictationBackend::GetWordsCount() const
    {
        return m_words.size();
    }

    std::string QT6DictationBackend::GetCurrentWord() const
    {
        if (m_currentIndex >= 0 && m_currentIndex < m_words.size()) {
            return m_words[m_currentIndex];
        }
        return "";
    }

    void QT6DictationBackend::SetReadInterval(int interval)
    {
        m_readInterval = interval;
    }

    int QT6DictationBackend::GetReadInterval() const
    {
        return m_readInterval;
    }

    void QT6DictationBackend::SetFliteVoiceModel(const std::string& voiceModel)
    {
        m_fliteVoiceModel = voiceModel;
    }

    std::string QT6DictationBackend::GetFliteVoiceModel() const
    {
        return m_fliteVoiceModel;
    }

    void QT6DictationBackend::SubmitOnlineAnswer(const std::string& userInput)
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

    std::vector<std::string> QT6DictationBackend::GetUserInputs() const
    {
        return m_userInputs;
    }

    std::vector<bool> QT6DictationBackend::GetAnswerResults() const
    {
        return m_answerResults;
    }

    int QT6DictationBackend::GetCorrectAnswers() const
    {
        return m_correctAnswers;
    }

    int QT6DictationBackend::GetWrongAnswers() const
    {
        return m_wrongAnswers;
    }

    void QT6DictationBackend::SpeakWord(const std::string& word)
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

    void QT6DictationBackend::StopSpeech()
    {
        if (m_speechStatusCallback) {
            m_speechStatusCallback(false);
        }
    }

    void QT6DictationBackend::PauseSpeech()
    {
        // Implementation would pause current speech
    }

    void QT6DictationBackend::ResumeSpeech()
    {
        // Implementation would resume paused speech
    }

    bool QT6DictationBackend::IsSpeaking() const
    {
        // This would check actual speech state
        return false;
    }

    void QT6DictationBackend::SetWordChangedCallback(WordChangedCallback callback)
    {
        m_wordChangedCallback = callback;
    }

    void QT6DictationBackend::SetCountdownCallback(CountdownCallback callback)
    {
        m_countdownCallback = callback;
    }

    void QT6DictationBackend::SetTestStateCallback(TestStateCallback callback)
    {
        m_testStateCallback = callback;
    }

    void QT6DictationBackend::SetSpeechStatusCallback(SpeechStatusCallback callback)
    {
        m_speechStatusCallback = callback;
    }

    // Private methods
    void QT6DictationBackend::StartTimer()
    {
        StopTimer();
        
        m_timerRunning = true;
        m_timerThread = std::thread([this]() {
            while (m_timerRunning && m_isTesting && !m_isPaused) {
                std::this_thread::sleep_for(std::chrono::seconds(1));
                if (m_timerRunning && m_isTesting && !m_isPaused) {
                    TimerTick();
                }
            }
        });
    }

    void QT6DictationBackend::StopTimer()
    {
        m_timerRunning = false;
        if (m_timerThread.joinable()) {
            m_timerThread.join();
        }
    }

    void QT6DictationBackend::TimerTick()
    {
        // Call NextWord() which handles the countdown logic
        NextWord();
    }

    void QT6DictationBackend::ProcessWord()
    {
        if (!m_isTesting || m_isPaused) return;

        // Show "正在朗读" - matching QT6 line 1844
        if (m_countdownCallback) {
            m_countdownCallback(-1); // -1 means "正在朗读"
        }

        // Speak current word - matching QT6 line 1850
        SpeakCurrentWord();

        // Move to next word or finish test
        if (m_currentIndex < m_words.size() - 1) {
            MoveToNextWord();
        } else {
            StopTest();
        }
    }

    void QT6DictationBackend::MoveToNextWord()
    {
        if (m_currentIndex < m_words.size() - 1) {
            m_currentIndex++;
            
            // Reset countdown - matching QT6 line 1856
            m_countdown = m_readInterval;

            // Update display
            if (m_wordChangedCallback) {
                m_wordChangedCallback(m_words[m_currentIndex].c_str(), m_currentIndex + 1, m_words.size());
            }
            if (m_countdownCallback) {
                m_countdownCallback(m_countdown);
            }

            // Speak immediately for online mode
            if (m_isOnlineMode) {
                SpeakCurrentWord();
            }
        } else {
            StopTest();
        }
    }

    void QT6DictationBackend::SpeakCurrentWord()
    {
        std::string currentWord = GetCurrentWord();
        if (!currentWord.empty()) {
            SpeakWord(currentWord);
        }
    }

    void QT6DictationBackend::RandomizeWords()
    {
        if (m_words.empty()) return;
        
        std::shuffle(m_words.begin(), m_words.end(), m_randomGenerator);
    }

}