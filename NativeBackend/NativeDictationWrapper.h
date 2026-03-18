#pragma once

// C++/CLI wrapper header for C# interop
#ifdef __cplusplus_cli

#include "DictationBackend.h"
#include <msclr/marshal_cppstd.h>

using namespace System;
using namespace System::Collections::Generic;

namespace EnglishListenWinUI {

    public ref class NativeDictationWrapper
    {
    public:
        NativeDictationWrapper();
        ~NativeDictationWrapper();
        !NativeDictationWrapper();

        // Word list management
        void SetWords(List<String^>^ words);
        void SetRandomOrder(bool random);
        List<String^>^ GetWords();

        // Test management
        bool StartTest(int dictationMode);
        void StopTest();
        void PauseResume();
        property bool IsTesting { bool get(); }
        property bool IsPaused { bool get(); }

        // Word navigation
        void NextWord();
        void PreviousWord();
        void RepeatWord();
        property int CurrentIndex { int get(); }
        property int WordsCount { int get(); }
        property String^ CurrentWord { String^ get(); }

        // Settings
        void SetReadInterval(int interval);
        property int ReadInterval { int get(); }
        void SetFliteVoiceModel(String^ voiceModel);
        property String^ FliteVoiceModel { String^ get(); }

        // Online dictation
        void SubmitOnlineAnswer(String^ userInput);
        List<String^>^ GetUserInputs();
        List<bool>^ GetAnswerResults();
        property int CorrectAnswers { int get(); }
        property int WrongAnswers { int get(); }

        // Speech control
        void SpeakWord(String^ word);
        void StopSpeech();
        void PauseSpeech();
        void ResumeSpeech();
        property bool IsSpeaking { bool get(); }

        // Event delegates
        delegate void WordChangedDelegate(String^ word, int currentIndex, int totalWords);
        delegate void CountdownDelegate(int countdown);
        delegate void TestStateDelegate(bool isTesting, bool isPaused);
        delegate void SpeechStatusDelegate(bool isSpeaking);

        // Events
        event WordChangedDelegate^ WordChanged;
        event CountdownDelegate^ CountdownChanged;
        event TestStateDelegate^ TestStateChanged;
        event SpeechStatusDelegate^ SpeechStatusChanged;

    private:
        EnglishListenNative::DictationBackend* m_backend;

        // Callback handlers
        static void OnWordChanged(const char* word, int currentIndex, int totalWords, void* context);
        static void OnCountdownChanged(int countdown, void* context);
        static void OnTestStateChanged(bool isTesting, bool isPaused, void* context);
        static void OnSpeechStatusChanged(bool isSpeaking, void* context);

        // Instance callbacks
        void HandleWordChanged(const char* word, int currentIndex, int totalWords);
        void HandleCountdownChanged(int countdown);
        void HandleTestStateChanged(bool isTesting, bool isPaused);
        void HandleSpeechStatusChanged(bool isSpeaking);
    };

}

#endif