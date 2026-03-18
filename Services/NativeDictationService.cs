using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace English_Listen_WinUI.Services
{
    public class NativeDictationService : IDisposable
    {
        // Native function declarations using P/Invoke
        [DllImport("EnglishListenNative", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr CreateDictationBackend();

        [DllImport("EnglishListenNative", CallingConvention = CallingConvention.Cdecl)]
        private static extern void DestroyDictationBackend(IntPtr backend);

        [DllImport("EnglishListenNative", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool InitializeBackend(IntPtr backend);

        [DllImport("EnglishListenNative", CallingConvention = CallingConvention.Cdecl)]
        private static extern void ShutdownBackend(IntPtr backend);

        [DllImport("EnglishListenNative", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetWords(IntPtr backend, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[] words, int count);

        [DllImport("EnglishListenNative", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetRandomOrder(IntPtr backend, bool random);

        [DllImport("EnglishListenNative", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool StartTest(IntPtr backend, int dictationMode);

        [DllImport("EnglishListenNative", CallingConvention = CallingConvention.Cdecl)]
        private static extern void StopTest(IntPtr backend);

        [DllImport("EnglishListenNative", CallingConvention = CallingConvention.Cdecl)]
        private static extern void PauseResume(IntPtr backend);

        [DllImport("EnglishListenNative", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool IsTesting(IntPtr backend);

        [DllImport("EnglishListenNative", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool IsPaused(IntPtr backend);

        [DllImport("EnglishListenNative", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NextWord(IntPtr backend);

        [DllImport("EnglishListenNative", CallingConvention = CallingConvention.Cdecl)]
        private static extern void PreviousWord(IntPtr backend);

        [DllImport("EnglishListenNative", CallingConvention = CallingConvention.Cdecl)]
        private static extern void RepeatWord(IntPtr backend);

        [DllImport("EnglishListenNative", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetCurrentIndex(IntPtr backend);

        [DllImport("EnglishListenNative", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetWordsCount(IntPtr backend);

        [DllImport("EnglishListenNative", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GetCurrentWord(IntPtr backend);

        [DllImport("EnglishListenNative", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetReadInterval(IntPtr backend, int interval);

        [DllImport("EnglishListenNative", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetReadInterval(IntPtr backend);

        [DllImport("EnglishListenNative", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetFliteVoiceModel(IntPtr backend, [MarshalAs(UnmanagedType.LPStr)] string voiceModel);

        [DllImport("EnglishListenNative", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GetFliteVoiceModel(IntPtr backend);

        [DllImport("EnglishListenNative", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SubmitOnlineAnswer(IntPtr backend, [MarshalAs(UnmanagedType.LPStr)] string userInput);

        [DllImport("EnglishListenNative", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetCorrectAnswers(IntPtr backend);

        [DllImport("EnglishListenNative", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetWrongAnswers(IntPtr backend);

        // Callback delegate types
        public delegate void WordChangedDelegate(string word, int currentIndex, int totalWords);
        public delegate void CountdownDelegate(int countdown);
        public delegate void TestStateDelegate(bool isTesting, bool isPaused);
        public delegate void SpeechStatusDelegate(bool isSpeaking);

        // Callback setters
        [DllImport("EnglishListenNative", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetWordChangedCallback(IntPtr backend, WordChangedDelegate callback);

        [DllImport("EnglishListenNative", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetCountdownCallback(IntPtr backend, CountdownDelegate callback);

        [DllImport("EnglishListenNative", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetTestStateCallback(IntPtr backend, TestStateDelegate callback);

        [DllImport("EnglishListenNative", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetSpeechStatusCallback(IntPtr backend, SpeechStatusDelegate callback);

        private IntPtr _nativeBackend;
        private bool _disposed = false;

        // Events
        public event WordChangedDelegate? WordChanged;
        public event CountdownDelegate? CountdownChanged;
        public event TestStateDelegate? TestStateChanged;
        public event SpeechStatusDelegate? SpeechStatusChanged;

        public NativeDictationService()
        {
            _nativeBackend = CreateDictationBackend();
            if (_nativeBackend != IntPtr.Zero)
            {
                // Set up callbacks
                SetWordChangedCallback(_nativeBackend, OnWordChanged);
                SetCountdownCallback(_nativeBackend, OnCountdownChanged);
                SetTestStateCallback(_nativeBackend, OnTestStateChanged);
                SetSpeechStatusCallback(_nativeBackend, OnSpeechStatusChanged);

                InitializeBackend(_nativeBackend);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_nativeBackend != IntPtr.Zero)
                {
                    ShutdownBackend(_nativeBackend);
                    DestroyDictationBackend(_nativeBackend);
                    _nativeBackend = IntPtr.Zero;
                }
                _disposed = true;
            }
        }

        // Word list management
        public void SetWords(List<string> words)
        {
            if (_nativeBackend != IntPtr.Zero && words != null)
            {
                string[] wordsArray = words.ToArray();
                SetWords(_nativeBackend, wordsArray, wordsArray.Length);
            }
        }

        public void SetRandomOrder(bool random)
        {
            if (_nativeBackend != IntPtr.Zero)
            {
                SetRandomOrder(_nativeBackend, random);
            }
        }

        // Test management
        public bool StartTest(int dictationMode)
        {
            return _nativeBackend != IntPtr.Zero && StartTest(_nativeBackend, dictationMode);
        }

        public void StopTest()
        {
            if (_nativeBackend != IntPtr.Zero)
            {
                StopTest(_nativeBackend);
            }
        }

        public void PauseResume()
        {
            if (_nativeBackend != IntPtr.Zero)
            {
                PauseResume(_nativeBackend);
            }
        }

        public bool IsTestingActive
        {
            get { return _nativeBackend != IntPtr.Zero && IsTesting(_nativeBackend); }
        }

        public bool IsPausedActive
        {
            get { return _nativeBackend != IntPtr.Zero && IsPaused(_nativeBackend); }
        }

        // Word navigation
        public void NextWord()
        {
            if (_nativeBackend != IntPtr.Zero)
            {
                NextWord(_nativeBackend);
            }
        }

        public void PreviousWord()
        {
            if (_nativeBackend != IntPtr.Zero)
            {
                PreviousWord(_nativeBackend);
            }
        }

        public void RepeatWord()
        {
            if (_nativeBackend != IntPtr.Zero)
            {
                RepeatWord(_nativeBackend);
            }
        }

        public int CurrentIndex
        {
            get { return _nativeBackend != IntPtr.Zero ? GetCurrentIndex(_nativeBackend) : 0; }
        }

        public int WordsCount
        {
            get { return _nativeBackend != IntPtr.Zero ? GetWordsCount(_nativeBackend) : 0; }
        }

        public string CurrentWord
        {
            get
            {
                if (_nativeBackend != IntPtr.Zero)
                {
                    IntPtr wordPtr = GetCurrentWord(_nativeBackend);
                    return Marshal.PtrToStringAnsi(wordPtr) ?? "";
                }
                return "";
            }
        }

        // Settings
        public void SetReadInterval(int interval)
        {
            if (_nativeBackend != IntPtr.Zero)
            {
                SetReadInterval(_nativeBackend, interval);
            }
        }

        public int ReadInterval
        {
            get { return _nativeBackend != IntPtr.Zero ? GetReadInterval(_nativeBackend) : 5; }
        }

        public void SetFliteVoiceModel(string voiceModel)
        {
            if (_nativeBackend != IntPtr.Zero)
            {
                SetFliteVoiceModel(_nativeBackend, voiceModel);
            }
        }

        public string FliteVoiceModel
        {
            get
            {
                if (_nativeBackend != IntPtr.Zero)
                {
                    IntPtr voiceModelPtr = GetFliteVoiceModel(_nativeBackend);
                    return Marshal.PtrToStringAnsi(voiceModelPtr) ?? "";
                }
                return "";
            }
        }

        // Online dictation
        public void SubmitOnlineAnswer(string userInput)
        {
            if (_nativeBackend != IntPtr.Zero)
            {
                SubmitOnlineAnswer(_nativeBackend, userInput);
            }
        }

        public int CorrectAnswers
        {
            get { return _nativeBackend != IntPtr.Zero ? GetCorrectAnswers(_nativeBackend) : 0; }
        }

        public int WrongAnswers
        {
            get { return _nativeBackend != IntPtr.Zero ? GetWrongAnswers(_nativeBackend) : 0; }
        }

        // Callback handlers
        private void OnWordChanged(string word, int currentIndex, int totalWords)
        {
            WordChanged?.Invoke(word, currentIndex, totalWords);
        }

        private void OnCountdownChanged(int countdown)
        {
            CountdownChanged?.Invoke(countdown);
        }

        private void OnTestStateChanged(bool isTesting, bool isPaused)
        {
            TestStateChanged?.Invoke(isTesting, isPaused);
        }

        private void OnSpeechStatusChanged(bool isSpeaking)
        {
            SpeechStatusChanged?.Invoke(isSpeaking);
        }
    }
}