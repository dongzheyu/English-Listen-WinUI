using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace English_Listen_WinUI.Services
{
    public class TranslationLibraryService
    {
        private const int MaxTranslations = 50000;
        private const int MaxWordLength = 256;
        private const int MaxTranslationLength = 2048;
        private const long MaxLibraryFileBytes = 10 * 1024 * 1024;

        private readonly object _lock = new();
        private readonly string _libraryPath;
        private readonly string _libraryDirectory;
        private Dictionary<string, string> _translations;
        private bool _isDirty;

        public TranslationLibraryService()
        {
            string appDataPath;
            try
            {
                appDataPath = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            }
            catch
            {
                appDataPath = AppDomain.CurrentDomain.BaseDirectory;
            }

            _libraryDirectory = Path.Combine(appDataPath, "data");
            Directory.CreateDirectory(_libraryDirectory);
            _libraryPath = Path.Combine(_libraryDirectory, "translation_library.txt");
            _translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            LoadLibrary();
        }

        private void LoadLibrary()
        {
            try
            {
                if (!File.Exists(_libraryPath))
                    return;

                var info = new FileInfo(_libraryPath);
                if (info.Length > MaxLibraryFileBytes)
                    return;

                using var reader = new StreamReader(_libraryPath);
                while (!reader.EndOfStream && _translations.Count < MaxTranslations)
                {
                    var line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line) || line.Length > MaxTranslationLength + MaxWordLength + 1)
                        continue;

                    var separatorIndex = line.IndexOf('|');
                    if (separatorIndex <= 0 || separatorIndex >= line.Length - 1)
                        continue;

                    var word = line[..separatorIndex].Trim();
                    var translation = line[(separatorIndex + 1)..].Trim();
                    if (!IsValidWord(word) || !IsValidTranslation(translation))
                        continue;

                    _translations[word] = translation;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载翻译库失败: {ex.Message}");
                lock (_lock)
                {
                    _translations.Clear();
                    _isDirty = false;
                }
            }
        }

        private static bool IsValidWord(string value) =>
            !string.IsNullOrWhiteSpace(value) && value.Length <= MaxWordLength && !value.Any(char.IsControl);

        private static bool IsValidTranslation(string value) =>
            !string.IsNullOrWhiteSpace(value) && value.Length <= MaxTranslationLength && !value.Any(char.IsControl);

        public string? GetTranslation(string word)
        {
            if (!IsValidWord(word?.Trim() ?? string.Empty))
                return null;

            lock (_lock)
            {
                return _translations.TryGetValue(word.Trim(), out var translation) ? translation : null;
            }
        }

        public void SaveTranslation(string word, string translation)
        {
            var trimmedWord = word?.Trim() ?? string.Empty;
            var trimmedTranslation = translation?.Trim() ?? string.Empty;
            if (!IsValidWord(trimmedWord) || !IsValidTranslation(trimmedTranslation))
                return;

            lock (_lock)
            {
                if (_translations.TryGetValue(trimmedWord, out var existing) && existing == trimmedTranslation)
                    return;

                if (_translations.Count >= MaxTranslations && !_translations.ContainsKey(trimmedWord))
                {
                    var oldestKey = _translations.Keys.First();
                    _translations.Remove(oldestKey);
                }

                _translations[trimmedWord] = trimmedTranslation;
                _isDirty = true;
            }
        }

        public void SaveTranslations(IEnumerable<(string Word, string Translation)> translations)
        {
            if (translations == null)
                throw new ArgumentNullException(nameof(translations));

            foreach (var (word, translation) in translations.Take(MaxTranslations))
                SaveTranslation(word, translation);

            SaveToFile();
        }

        public void SaveToFile()
        {
            Dictionary<string, string> snapshot;
            lock (_lock)
            {
                if (!_isDirty)
                    return;

                snapshot = new Dictionary<string, string>(_translations, StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                Directory.CreateDirectory(_libraryDirectory);
                var tempPath = Path.Combine(_libraryDirectory, $"translation_library.{Guid.NewGuid():N}.tmp");
                var lines = snapshot
                    .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(kvp => $"{kvp.Key}|{kvp.Value}")
                    .ToList();

                File.WriteAllLines(tempPath, lines);
                File.Move(tempPath, _libraryPath, true);

                lock (_lock)
                {
                    _isDirty = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存翻译库失败: {ex.Message}");
            }
        }

        public int GetTranslationCount()
        {
            lock (_lock)
            {
                return _translations.Count;
            }
        }

        public Dictionary<string, string> GetAllTranslations()
        {
            lock (_lock)
            {
                return new Dictionary<string, string>(_translations, StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}