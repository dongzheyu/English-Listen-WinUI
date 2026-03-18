#pragma once

#ifdef _WIN32
    #define NATIVEDICTATION_API __declspec(dllexport)
#else
    #define NATIVEDICTATION_API
#endif

#ifdef __cplusplus
extern "C" {
#endif

// Forward declaration for the backend handle
typedef void* NativeDictationHandle;

// Callback types for C interop
typedef void(*WordChangedCallback)(const char* word, int currentIndex, int totalWords);
typedef void(*CountdownCallback)(int countdown);
typedef void(*TestStateCallback)(bool isTesting, bool isPaused);
typedef void(*SpeechStatusCallback)(bool isSpeaking);

// Core API functions
NATIVEDICTATION_API NativeDictationHandle CreateDictationBackend();
NATIVEDICTATION_API void DestroyDictationBackend(NativeDictationHandle handle);

// Initialization
NATIVEDICTATION_API bool InitializeBackend(NativeDictationHandle handle);
NATIVEDICTATION_API void ShutdownBackend(NativeDictationHandle handle);

// Word list management
NATIVEDICTATION_API void SetWords(NativeDictationHandle handle, const char** words, int wordCount);
NATIVEDICTATION_API void SetRandomOrder(NativeDictationHandle handle, bool random);
NATIVEDICTATION_API int GetWordsCount(NativeDictationHandle handle);
NATIVEDICTATION_API void GetWords(NativeDictationHandle handle, char** wordsBuffer, int bufferSize);

// Test management
NATIVEDICTATION_API bool StartTest(NativeDictationHandle handle, int dictationMode);
NATIVEDICTATION_API void StopTest(NativeDictationHandle handle);
NATIVEDICTATION_API void PauseResume(NativeDictationHandle handle);
NATIVEDICTATION_API bool IsTesting(NativeDictationHandle handle);
NATIVEDICTATION_API bool IsPaused(NativeDictationHandle handle);

// Word navigation
NATIVEDICTATION_API void NextWord(NativeDictationHandle handle);
NATIVEDICTATION_API void PreviousWord(NativeDictationHandle handle);
NATIVEDICTATION_API void RepeatWord(NativeDictationHandle handle);
NATIVEDICTATION_API int GetCurrentIndex(NativeDictationHandle handle);
NATIVEDICTATION_API const char* GetCurrentWord(NativeDictationHandle handle);

// Settings
NATIVEDICTATION_API void SetReadInterval(NativeDictationHandle handle, int interval);
NATIVEDICTATION_API int GetReadInterval(NativeDictationHandle handle);
NATIVEDICTATION_API void SetFliteVoiceModel(NativeDictationHandle handle, const char* voiceModel);
NATIVEDICTATION_API const char* GetFliteVoiceModel(NativeDictationHandle handle);

// Online dictation
NATIVEDICTATION_API void SubmitOnlineAnswer(NativeDictationHandle handle, const char* userInput);
NATIVEDICTATION_API int GetCorrectAnswers(NativeDictationHandle handle);
NATIVEDICTATION_API int GetWrongAnswers(NativeDictationHandle handle);

// Callbacks
NATIVEDICTATION_API void SetWordChangedCallback(NativeDictationHandle handle, WordChangedCallback callback);
NATIVEDICTATION_API void SetCountdownCallback(NativeDictationHandle handle, CountdownCallback callback);
NATIVEDICTATION_API void SetTestStateCallback(NativeDictationHandle handle, TestStateCallback callback);
NATIVEDICTATION_API void SetSpeechStatusCallback(NativeDictationHandle handle, SpeechStatusCallback callback);

#ifdef __cplusplus
}
#endif