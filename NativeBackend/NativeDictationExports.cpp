#include "NativeDictationExports.h"
#include "QT6DictationBackend.h"
#include <vector>
#include <string>
#include <cstring>

using namespace EnglishListenNative;

// Helper function to convert C string array to std::vector<std::string>
std::vector<std::string> ConvertCStringArray(const char** words, int wordCount) {
    std::vector<std::string> result;
    for (int i = 0; i < wordCount; i++) {
        if (words[i] != nullptr) {
            result.push_back(std::string(words[i]));
        }
    }
    return result;
}

// Helper function to convert std::vector<std::string> to C string array
void ConvertStringVectorToCArray(const std::vector<std::string>& words, char** wordsBuffer, int bufferSize) {
    int count = std::min(static_cast<int>(words.size()), bufferSize);
    for (int i = 0; i < count; i++) {
        // Allocate memory for each string
        size_t len = words[i].length() + 1;
        wordsBuffer[i] = new char[len];
        strcpy_s(wordsBuffer[i], len, words[i].c_str());
    }
}

extern "C" {

NATIVEDICTATION_API NativeDictationHandle CreateDictationBackend() {
    return new QT6DictationBackend();
}

NATIVEDICTATION_API void DestroyDictationBackend(NativeDictationHandle handle) {
    if (handle != nullptr) {
        QT6DictationBackend* backend = static_cast<QT6DictationBackend*>(handle);
        delete backend;
    }
}

NATIVEDICTATION_API bool InitializeBackend(NativeDictationHandle handle) {
    if (handle == nullptr) return false;
    QT6DictationBackend* backend = static_cast<QT6DictationBackend*>(handle);
    return backend->Initialize();
}

NATIVEDICTATION_API void ShutdownBackend(NativeDictationHandle handle) {
    if (handle != nullptr) {
        QT6DictationBackend* backend = static_cast<QT6DictationBackend*>(handle);
        backend->Shutdown();
    }
}

NATIVEDICTATION_API void SetWords(NativeDictationHandle handle, const char** words, int wordCount) {
    if (handle == nullptr || words == nullptr || wordCount <= 0) return;
    QT6DictationBackend* backend = static_cast<QT6DictationBackend*>(handle);
    std::vector<std::string> wordList = ConvertCStringArray(words, wordCount);
    backend->SetWords(wordList);
}

NATIVEDICTATION_API void SetRandomOrder(NativeDictationHandle handle, bool random) {
    if (handle == nullptr) return;
    QT6DictationBackend* backend = static_cast<QT6DictationBackend*>(handle);
    backend->SetRandomOrder(random);
}

NATIVEDICTATION_API int GetWordsCount(NativeDictationHandle handle) {
    if (handle == nullptr) return 0;
    QT6DictationBackend* backend = static_cast<QT6DictationBackend*>(handle);
    return backend->GetWordsCount();
}

NATIVEDICTATION_API void GetWords(NativeDictationHandle handle, char** wordsBuffer, int bufferSize) {
    if (handle == nullptr || wordsBuffer == nullptr || bufferSize <= 0) return;
    QT6DictationBackend* backend = static_cast<QT6DictationBackend*>(handle);
    std::vector<std::string> words = backend->GetWords();
    ConvertStringVectorToCArray(words, wordsBuffer, bufferSize);
}

NATIVEDICTATION_API bool StartTest(NativeDictationHandle handle, int dictationMode) {
    if (handle == nullptr) return false;
    QT6DictationBackend* backend = static_cast<QT6DictationBackend*>(handle);
    return backend->StartTest(dictationMode);
}

NATIVEDICTATION_API void StopTest(NativeDictationHandle handle) {
    if (handle != nullptr) {
        QT6DictationBackend* backend = static_cast<QT6DictationBackend*>(handle);
        backend->StopTest();
    }
}

NATIVEDICTATION_API void PauseResume(NativeDictationHandle handle) {
    if (handle != nullptr) {
        QT6DictationBackend* backend = static_cast<QT6DictationBackend*>(handle);
        backend->PauseResume();
    }
}

NATIVEDICTATION_API bool IsTesting(NativeDictationHandle handle) {
    if (handle == nullptr) return false;
    QT6DictationBackend* backend = static_cast<QT6DictationBackend*>(handle);
    return backend->IsTesting();
}

NATIVEDICTATION_API bool IsPaused(NativeDictationHandle handle) {
    if (handle == nullptr) return false;
    QT6DictationBackend* backend = static_cast<QT6DictationBackend*>(handle);
    return backend->IsPaused();
}

NATIVEDICTATION_API void NextWord(NativeDictationHandle handle) {
    if (handle != nullptr) {
        QT6DictationBackend* backend = static_cast<QT6DictationBackend*>(handle);
        backend->NextWord();
    }
}

NATIVEDICTATION_API void PreviousWord(NativeDictationHandle handle) {
    if (handle != nullptr) {
        QT6DictationBackend* backend = static_cast<QT6DictationBackend*>(handle);
        backend->PreviousWord();
    }
}

NATIVEDICTATION_API void RepeatWord(NativeDictationHandle handle) {
    if (handle != nullptr) {
        QT6DictationBackend* backend = static_cast<QT6DictationBackend*>(handle);
        backend->RepeatWord();
    }
}

NATIVEDICTATION_API int GetCurrentIndex(NativeDictationHandle handle) {
    if (handle == nullptr) return -1;
    QT6DictationBackend* backend = static_cast<QT6DictationBackend*>(handle);
    return backend->GetCurrentIndex();
}

NATIVEDICTATION_API const char* GetCurrentWord(NativeDictationHandle handle) {
    if (handle == nullptr) return "";
    QT6DictationBackend* backend = static_cast<QT6DictationBackend*>(handle);
    static std::string currentWord = backend->GetCurrentWord();
    return currentWord.c_str();
}

NATIVEDICTATION_API void SetReadInterval(NativeDictationHandle handle, int interval) {
    if (handle != nullptr) {
        QT6DictationBackend* backend = static_cast<QT6DictationBackend*>(handle);
        backend->SetReadInterval(interval);
    }
}

NATIVEDICTATION_API int GetReadInterval(NativeDictationHandle handle) {
    if (handle == nullptr) return 0;
    QT6DictationBackend* backend = static_cast<QT6DictationBackend*>(handle);
    return backend->GetReadInterval();
}

NATIVEDICTATION_API void SetFliteVoiceModel(NativeDictationHandle handle, const char* voiceModel) {
    if (handle != nullptr && voiceModel != nullptr) {
        QT6DictationBackend* backend = static_cast<QT6DictationBackend*>(handle);
        backend->SetFliteVoiceModel(std::string(voiceModel));
    }
}

NATIVEDICTATION_API const char* GetFliteVoiceModel(NativeDictationHandle handle) {
    if (handle == nullptr) return "";
    QT6DictationBackend* backend = static_cast<QT6DictationBackend*>(handle);
    static std::string voiceModel = backend->GetFliteVoiceModel();
    return voiceModel.c_str();
}

NATIVEDICTATION_API void SubmitOnlineAnswer(NativeDictationHandle handle, const char* userInput) {
    if (handle != nullptr && userInput != nullptr) {
        QT6DictationBackend* backend = static_cast<QT6DictationBackend*>(handle);
        backend->SubmitOnlineAnswer(std::string(userInput));
    }
}

NATIVEDICTATION_API int GetCorrectAnswers(NativeDictationHandle handle) {
    if (handle == nullptr) return 0;
    QT6DictationBackend* backend = static_cast<QT6DictationBackend*>(handle);
    return backend->GetCorrectAnswers();
}

NATIVEDICTATION_API int GetWrongAnswers(NativeDictationHandle handle) {
    if (handle == nullptr) return 0;
    QT6DictationBackend* backend = static_cast<QT6DictationBackend*>(handle);
    return backend->GetWrongAnswers();
}

// Callback wrappers
NATIVEDICTATION_API void SetWordChangedCallback(NativeDictationHandle handle, WordChangedCallback callback) {
    if (handle != nullptr) {
        QT6DictationBackend* backend = static_cast<QT6DictationBackend*>(handle);
        backend->SetWordChangedCallback(callback);
    }
}

NATIVEDICTATION_API void SetCountdownCallback(NativeDictationHandle handle, CountdownCallback callback) {
    if (handle != nullptr) {
        QT6DictationBackend* backend = static_cast<QT6DictationBackend*>(handle);
        backend->SetCountdownCallback(callback);
    }
}

NATIVEDICTATION_API void SetTestStateCallback(NativeDictationHandle handle, TestStateCallback callback) {
    if (handle != nullptr) {
        QT6DictationBackend* backend = static_cast<QT6DictationBackend*>(handle);
        backend->SetTestStateCallback(callback);
    }
}

NATIVEDICTATION_API void SetSpeechStatusCallback(NativeDictationHandle handle, SpeechStatusCallback callback) {
    if (handle != nullptr) {
        QT6DictationBackend* backend = static_cast<QT6DictationBackend*>(handle);
        backend->SetSpeechStatusCallback(callback);
    }
}

}