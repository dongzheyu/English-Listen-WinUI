#pragma once

#ifdef ENGLISHLISTENNATIVE_EXPORTS
#define ENGLISHLISTENNATIVE_API __declspec(dllexport)
#else
#define ENGLISHLISTENNATIVE_API __declspec(dllimport)
#endif

#ifdef __cplusplus
extern "C" {
#endif

// Opaque handle to the backend
typedef void* DictationBackendHandle;

// Callback function types
typedef void(*WordChangedCallback)(const char* word, int currentIndex, int totalWords);
typedef void(*CountdownCallback)(int countdown);
typedef void(*TestStateCallback)(bool isTesting, bool isPaused);
typedef void(*SpeechStatusCallback)(bool isSpeaking);

// Backend creation and destruction
ENGLISHLISTENNATIVE_API DictationBackendHandle dictation_backend_create();
ENGLISHLISTENNATIVE_API void dictation_backend_destroy(DictationBackendHandle backend);

// Initialization
ENGLISHLISTENNATIVE_API bool dictation_backend_initialize(DictationBackendHandle backend);
ENGLISHLISTENNATIVE_API void dictation_backend_shutdown(DictationBackendHandle backend);

// Word list management
ENGLISHLISTENNATIVE_API void dictation_backend_set_words(DictationBackendHandle backend, const char** words, int count);
ENGLISHLISTENNATIVE_API void dictation_backend_set_random_order(DictationBackendHandle backend, bool random);

// Test management
ENGLISHLISTENNATIVE_API bool dictation_backend_start_test(DictationBackendHandle backend, int dictationMode);
ENGLISHLISTENNATIVE_API void dictation_backend_stop_test(DictationBackendHandle backend);
ENGLISHLISTENNATIVE_API void dictation_backend_pause_resume(DictationBackendHandle backend);
ENGLISHLISTENNATIVE_API bool dictation_backend_is_testing(DictationBackendHandle backend);
ENGLISHLISTENNATIVE_API bool dictation_backend_is_paused(DictationBackendHandle backend);

// Word navigation
ENGLISHLISTENNATIVE_API void dictation_backend_next_word(DictationBackendHandle backend);
ENGLISHLISTENNATIVE_API void dictation_backend_previous_word(DictationBackendHandle backend);
ENGLISHLISTENNATIVE_API void dictation_backend_repeat_word(DictationBackendHandle backend);
ENGLISHLISTENNATIVE_API int dictation_backend_get_current_index(DictationBackendHandle backend);
ENGLISHLISTENNATIVE_API int dictation_backend_get_words_count(DictationBackendHandle backend);
ENGLISHLISTENNATIVE_API const char* dictation_backend_get_current_word(DictationBackendHandle backend);

// Settings
ENGLISHLISTENNATIVE_API void dictation_backend_set_read_interval(DictationBackendHandle backend, int interval);
ENGLISHLISTENNATIVE_API int dictation_backend_get_read_interval(DictationBackendHandle backend);
ENGLISHLISTENNATIVE_API void dictation_backend_set_flite_voice_model(DictationBackendHandle backend, const char* voiceModel);
ENGLISHLISTENNATIVE_API const char* dictation_backend_get_flite_voice_model(DictationBackendHandle backend);

// Online dictation
ENGLISHLISTENNATIVE_API void dictation_backend_submit_online_answer(DictationBackendHandle backend, const char* userInput);
ENGLISHLISTENNATIVE_API int dictation_backend_get_correct_answers(DictationBackendHandle backend);
ENGLISHLISTENNATIVE_API int dictation_backend_get_wrong_answers(DictationBackendHandle backend);

// Callback setters
ENGLISHLISTENNATIVE_API void dictation_backend_set_word_changed_callback(DictationBackendHandle backend, WordChangedCallback callback);
ENGLISHLISTENNATIVE_API void dictation_backend_set_countdown_callback(DictationBackendHandle backend, CountdownCallback callback);
ENGLISHLISTENNATIVE_API void dictation_backend_set_test_state_callback(DictationBackendHandle backend, TestStateCallback callback);
ENGLISHLISTENNATIVE_API void dictation_backend_set_speech_status_callback(DictationBackendHandle backend, SpeechStatusCallback callback);

#ifdef __cplusplus
}
#endif