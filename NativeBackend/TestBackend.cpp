#include <iostream>
#include <vector>
#include <string>
#include <thread>
#include <chrono>
#include "NativeDictationExports.h"

// Test callback functions
void TestWordChanged(const char* word, int currentIndex, int totalWords) {
    std::cout << "Word changed: " << word << " (" << currentIndex + 1 << "/" << totalWords << ")" << std::endl;
}

void TestCountdownChanged(int countdown) {
    if (countdown >= 0) {
        std::cout << "Countdown: " << countdown << std::endl;
    } else {
        std::cout << "Countdown: 正在朗读" << std::endl;
    }
}

void TestTestStateChanged(bool isTesting, bool isPaused) {
    std::cout << "Test state - Testing: " << isTesting << ", Paused: " << isPaused << std::endl;
}

void TestSpeechStatusChanged(bool isSpeaking) {
    std::cout << "Speech status - Speaking: " << isSpeaking << std::endl;
}

int main() {
    std::cout << "Testing C++ Dictation Backend..." << std::endl;

    // Create backend
    NativeDictationHandle backend = CreateDictationBackend();
    if (!backend) {
        std::cerr << "Failed to create backend!" << std::endl;
        return 1;
    }

    // Initialize
    if (!InitializeBackend(backend)) {
        std::cerr << "Failed to initialize backend!" << std::endl;
        DestroyDictationBackend(backend);
        return 1;
    }

    // Set up callbacks
    SetWordChangedCallback(backend, TestWordChanged);
    SetCountdownCallback(backend, TestCountdownChanged);
    SetTestStateCallback(backend, TestTestStateChanged);
    SetSpeechStatusCallback(backend, TestSpeechStatusChanged);

    // Set words
    std::vector<std::string> testWords = {"apple", "banana", "cherry", "date", "elderberry"};
    const char* wordsArray[5];
    for (int i = 0; i < testWords.size(); i++) {
        wordsArray[i] = testWords[i].c_str();
    }
    SetWords(backend, wordsArray, testWords.size());

    // Set read interval
    SetReadInterval(backend, 3);

    std::cout << "Starting paper dictation test..." << std::endl;
    
    // Start test in paper mode (0)
    if (StartTest(backend, 0)) {
        std::cout << "Test started successfully!" << std::endl;
        
        // Simulate some test interactions
        std::this_thread::sleep_for(std::chrono::seconds(2));
        
        std::cout << "\nManual navigation test..." << std::endl;
        PreviousWord(backend);
        std::this_thread::sleep_for(std::chrono::seconds(1));
        
        RepeatWord(backend);
        std::this_thread::sleep_for(std::chrono::seconds(1));
        
        std::cout << "\nPause/resume test..." << std::endl;
        PauseResume(backend);
        std::this_thread::sleep_for(std::chrono::seconds(2));
        PauseResume(backend);
        std::this_thread::sleep_for(std::chrono::seconds(2));
        
        std::cout << "\nStopping test..." << std::endl;
        StopTest(backend);
    } else {
        std::cerr << "Failed to start test!" << std::endl;
    }

    // Clean up
    ShutdownBackend(backend);
    DestroyDictationBackend(backend);

    std::cout << "Test completed successfully!" << std::endl;
    return 0;
}