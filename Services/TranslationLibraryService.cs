using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace English_Listen_WinUI.Services
{
    public class TranslationLibraryService
    {
        private const int MAX_TRANSLATIONS = 50000;

        private readonly string _libraryPath;
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
            
            var dataDir = Path.Combine(appDataPath, "data");
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }
            _libraryPath = Path.Combine(dataDir, "translation_library.txt");
            _translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _isDirty = false;
            LoadLibrary();
        }

        private void LoadLibrary()
        {
            if (!File.Exists(_libraryPath))
            {
                return;
            }

            try
            {
                var lines = File.ReadAllLines(_libraryPath);
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine)) continue;

                    var separatorIndex = trimmedLine.IndexOf('|');
                    if (separatorIndex > 0 && separatorIndex < trimmedLine.Length - 1)
                    {
                        var word = trimmedLine.Substring(0, separatorIndex).Trim();
                        var translation = trimmedLine.Substring(separatorIndex + 1).Trim();
                        if (!string.IsNullOrEmpty(word) && !string.IsNullOrEmpty(translation))
                        {
                            _translations[word] = translation;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载翻译库失败: {ex.Message}");
            }
        }

        public string? GetTranslation(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return null;
            
            var trimmedWord = word.Trim();
            return _translations.TryGetValue(trimmedWord, out var translation) ? translation : null;
        }

        public void SaveTranslation(string word, string translation)
        {
            if (string.IsNullOrWhiteSpace(word) || string.IsNullOrWhiteSpace(translation)) return;

            var trimmedWord = word.Trim();
            var trimmedTranslation = translation.Trim();

            if (_translations.TryGetValue(trimmedWord, out var existing) && existing == trimmedTranslation)
            {
                return;
            }

            // Evict oldest entry if at capacity
            if (_translations.Count >= MAX_TRANSLATIONS && !_translations.ContainsKey(trimmedWord))
            {
                var oldestKey = _translations.Keys.First();
                _translations.Remove(oldestKey);
            }

            _translations[trimmedWord] = trimmedTranslation;
            _isDirty = true;
        }

        public void SaveTranslations(IEnumerable<(string Word, string Translation)> translations)
        {
            foreach (var (word, translation) in translations)
            {
                SaveTranslation(word, translation);
            }
            SaveToFile();
        }

        public void SaveToFile()
        {
            if (!_isDirty) return;

            try
            {
                var lines = _translations
                    .OrderBy(kvp => kvp.Key)
                    .Select(kvp => $"{kvp.Key}|{kvp.Value}")
                    .ToList();

                File.WriteAllLines(_libraryPath, lines);
                _isDirty = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存翻译库失败: {ex.Message}");
            }
        }

        public int GetTranslationCount()
        {
            return _translations.Count;
        }

        public Dictionary<string, string> GetAllTranslations()
        {
            return new Dictionary<string, string>(_translations, StringComparer.OrdinalIgnoreCase);
        }
    }
}
