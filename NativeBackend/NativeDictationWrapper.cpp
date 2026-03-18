#include "NativeDictationWrapper.h"

#ifdef __cplusplus_cli

namespace EnglishListenWinUI {

    NativeDictationWrapper::NativeDictationWrapper()
    {
        m_backend = new EnglishListenNative::DictationBackend();
        
        // Set up callbacks
        m_backend->SetWordChangedCallback(&OnWordChanged, this);
        m_backend->SetCountdownCallback(&OnCountdownChanged, this);
        m_backend->SetTestStateCallback(&OnTestStateChanged, this);
        m_backend->SetSpeechStatusCallback(&OnSpeechStatusChanged, this);
        
        m_backend->Initialize();
    }

    NativeDictationWrapper::~NativeDictationWrapper()
    {
        this->!NativeDictationWrapper();
    }

    NativeDictationWrapper::!NativeDictationWrapper()
    {
        if (m_backend) {
            m_backend->Shutdown();
            delete m_backend;
            m_backend = nullptr;
        }
    }

    void NativeDictationWrapper::SetWords(List<String^>^ words)
    {
        if (!m_backend) return;

        std::vector<std::string> nativeWords;
        for each (String^ word in words) {
            nativeWords.push_back(msclr::interop::marshal_as<std::string>(word));
        }
        
        m_backend->SetWords(nativeWords);
    }

    void NativeDictationWrapper::SetRandomOrder(bool random)
    {
        if (m_backend) {
            m_backend->SetRandomOrder(random);
        }
    }

    List<String^>^ NativeDictationWrapper::GetWords()
    {
        List<String^>^ words = gcnew List<String^>();
        
        if (m_backend) {
            auto nativeWords = m_backend->GetWords();
            for (const auto& word : nativeWords) {
                words->Add(gcnew String(word.c_str()));
            }
        }
        
        return words;
    }

    bool NativeDictationWrapper::StartTest(int dictationMode)
    {
        return m_backend ? m_backend->StartTest(dictationMode) : false;
    }

    void NativeDictationWrapper::StopTest()
    {
        if (m_backend) {
            m_backend->StopTest();
        }
    }

    void NativeDictationWrapper::PauseResume()
    {
        if (m_backend) {
            m_backend->PauseResume();
        }
    }

    bool NativeDictationWrapper::IsTesting::get()
    {
        return m_backend ? m_backend->IsTesting() : false;
    }

    bool NativeDictationWrapper::IsPaused::get()
    {
        return m_backend ? m_backend->IsPaused() : false;
    }

    void NativeDictationWrapper::NextWord()
    {
        if (m_backend) {
            m_backend->NextWord();
        }
    }

    void NativeDictationWrapper::PreviousWord()
    {
        if (m_backend) {
            m_backend->PreviousWord();
        }
    }

    void NativeDictationWrapper::RepeatWord()
    {
        if (m_backend) {
            m_backend->RepeatWord();
        }
    }

    int NativeDictationWrapper::CurrentIndex::get()
    {
        return m_backend ? m_backend->GetCurrentIndex() : 0;
    }

    int NativeDictationWrapper::WordsCount::get()
    {
        return m_backend ? m_backend->GetWordsCount() : 0;
    }

    String^ NativeDictationWrapper::CurrentWord::get()
    {
        if (m_backend) {
            std::string word = m_backend->GetCurrentWord();
            return gcnew String(word.c_str());
        }
        return String::Empty;
    }

    void NativeDictationWrapper::SetReadInterval(int interval)
    {
        if (m_backend) {
            m_backend->SetReadInterval(interval);
        }
    }

    int NativeDictationWrapper::ReadInterval::get()
    {
        return m_backend ? m_backend->GetReadInterval() : 5;
    }

    void NativeDictationWrapper::SetFliteVoiceModel(String^ voiceModel)
    {
        if (m_backend) {
            std::string nativeVoiceModel = msclr::interop::marshal_as<std::string>(voiceModel);
            m_backend->SetFliteVoiceModel(nativeVoiceModel);
        }
    }

    String^ NativeDictationWrapper::FliteVoiceModel::get()
    {
        if (m_backend) {
            std::string voiceModel = m_backend->GetFliteVoiceModel();
            return gcnew String(voiceModel.c_str());
        }
        return String::Empty;
    }

    void NativeDictationWrapper::SubmitOnlineAnswer(String^ userInput)
    {
        if (m_backend) {
            std::string nativeInput = msclr::interop::marshal_as<std::string>(userInput);
            m_backend->SubmitOnlineAnswer(nativeInput);
        }
    }

    List<String^>^ NativeDictationWrapper::GetUserInputs()
    {
        List<String^>^ inputs = gcnew List<String^>();
        
        if (m_backend) {
            auto nativeInputs = m_backend->GetUserInputs();
            for (const auto& input : nativeInputs) {
                inputs->Add(gcnew String(input.c_str()));
            }
        }
        
        return inputs;
    }

    List<bool>^ NativeDictationWrapper::GetAnswerResults()
    {
        List<bool>^ results = gcnew List<bool>();
        
        if (m_backend) {
            auto nativeResults = m_backend->GetAnswerResults();
            for (bool result : nativeResults) {
                results->Add(result);
            }
        }
        
        return results;
    }

    int NativeDictationWrapper::CorrectAnswers::get()
    {
        return m_backend ? m_backend->GetCorrectAnswers() : 0;
    }

    int NativeDictationWrapper::WrongAnswers::get()
    {
        return m_backend ? m_backend->GetWrongAnswers() : 0;
    }

    void NativeDictationWrapper::SpeakWord(String^ word)
    {
        if (m_backend) {
            std::string nativeWord = msclr::interop::marshal_as<std::string>(word);
            m_backend->SpeakWord(nativeWord);
        }
    }

    void NativeDictationWrapper::StopSpeech()
    {
        if (m_backend) {
            m_backend->StopSpeech();
        }
    }

    void NativeDictationWrapper::PauseSpeech()
    {
        if (m_backend) {
            m_backend->PauseSpeech();
        }
    }

    void NativeDictationWrapper::ResumeSpeech()
    {
        if (m_backend) {
            m_backend->ResumeSpeech();
        }
    }

    bool NativeDictationWrapper::IsSpeaking::get()
    {
        return m_backend ? m_backend->IsSpeaking() : false;
    }

    // Static callback functions
    void NativeDictationWrapper::OnWordChanged(const char* word, int currentIndex, int totalWords, void* context)
    {
        NativeDictationWrapper^ wrapper = static_cast<NativeDictationWrapper^>(GCHandle::FromIntPtr(IntPtr(context)).Target);
        if (wrapper != nullptr) {
            wrapper->HandleWordChanged(word, currentIndex, totalWords);
        }
    }

    void NativeDictationWrapper::OnCountdownChanged(int countdown, void* context)
    {
        NativeDictationWrapper^ wrapper = static_cast<NativeDictationWrapper^>(GCHandle::FromIntPtr(IntPtr(context)).Target);
        if (wrapper != nullptr) {
            wrapper->HandleCountdownChanged(countdown);
        }
    }

    void NativeDictationWrapper::OnTestStateChanged(bool isTesting, bool isPaused, void* context)
    {
        NativeDictationWrapper^ wrapper = static_cast<NativeDictationWrapper^>(GCHandle::FromIntPtr(IntPtr(context)).Target);
        if (wrapper != nullptr) {
            wrapper->HandleTestStateChanged(isTesting, isPaused);
        }
    }

    void NativeDictationWrapper::OnSpeechStatusChanged(bool isSpeaking, void* context)
    {
        NativeDictationWrapper^ wrapper = static_cast<NativeDictationWrapper^>(GCHandle::FromIntPtr(IntPtr(context)).Target);
        if (wrapper != nullptr) {
            wrapper->HandleSpeechStatusChanged(isSpeaking);
        }
    }

    // Instance callback handlers
    void NativeDictationWrapper::HandleWordChanged(const char* word, int currentIndex, int totalWords)
    {
        if (WordChanged != nullptr) {
            String^ managedWord = gcnew String(word);
            WordChanged(managedWord, currentIndex, totalWords);
        }
    }

    void NativeDictationWrapper::HandleCountdownChanged(int countdown)
    {
        if (CountdownChanged != nullptr) {
            CountdownChanged(countdown);
        }
    }

    void NativeDictationWrapper::HandleTestStateChanged(bool isTesting, bool isPaused)
    {
        if (TestStateChanged != nullptr) {
            TestStateChanged(isTesting, isPaused);
        }
    }

    void NativeDictationWrapper::HandleSpeechStatusChanged(bool isSpeaking)
    {
        if (SpeechStatusChanged != nullptr) {
            SpeechStatusChanged(isSpeaking);
        }
    }

}

#endif