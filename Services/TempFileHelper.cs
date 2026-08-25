using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace English_Listen_WinUI.Services
{
    public static class TempFileHelper
    {
        private const int MaxWords = 10000;
        private const int MaxWordLength = 256;
        private const long MaxFileBytes = 2 * 1024 * 1024;
        private static readonly string TempDirectory = GetTempDirectory();
        private static readonly string TempFilePath = Path.Combine(TempDirectory, "words.txt");
        private static readonly string LegacyTempFilePath = Path.Combine(Path.GetTempPath(), "english_listen_temp.txt");
        private static readonly SemaphoreSlim _lock = new(1, 1);

        private static string GetTempDirectory()
        {
            try
            {
                return Path.Combine(ApplicationData.Current.LocalFolder.Path, "temp");
            }
            catch
            {
                return Path.Combine(AppContext.BaseDirectory, "temp");
            }
        }

        public static async Task<List<string>> ReadWordsAsync()
        {
            await _lock.WaitAsync();
            try
            {
                if (!File.Exists(TempFilePath))
                    return new List<string>();

                var info = new FileInfo(TempFilePath);
                if (info.Length > MaxFileBytes || (File.GetAttributes(TempFilePath) & FileAttributes.ReparsePoint) != 0)
                    return new List<string>();

                var content = await File.ReadAllTextAsync(TempFilePath);
                return content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(w => w.Trim())
                    .Where(w => w.Length > 0 && w.Length <= MaxWordLength)
                    .Take(MaxWords)
                    .ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TempFileHelper.ReadWordsAsync failed: {ex.Message}");
                return new List<string>();
            }
            finally
            {
                _lock.Release();
            }
        }

        public static async Task WriteWordsAsync(List<string> words)
        {
            if (words == null)
                throw new ArgumentNullException(nameof(words));

            var safeWords = words
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .Select(w => w.Trim())
                .Where(w => w.Length <= MaxWordLength)
                .Take(MaxWords)
                .ToList();

            await _lock.WaitAsync();
            try
            {
                Directory.CreateDirectory(TempDirectory);
                if ((File.GetAttributes(TempDirectory) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("拒绝使用重解析点临时目录。");

                var tempPath = Path.Combine(TempDirectory, $"words.{Guid.NewGuid():N}.tmp");
                await File.WriteAllLinesAsync(tempPath, safeWords);
                File.Move(tempPath, TempFilePath, true);

                try
                {
                    if (File.Exists(LegacyTempFilePath))
                        File.Delete(LegacyTempFilePath);
                }
                catch
                {
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TempFileHelper.WriteWordsAsync failed: {ex.Message}");
            }
            finally
            {
                _lock.Release();
            }
        }

        public static async Task ClearAsync()
        {
            await _lock.WaitAsync();
            try
            {
                if (File.Exists(TempFilePath))
                    File.Delete(TempFilePath);
                if (File.Exists(LegacyTempFilePath))
                    File.Delete(LegacyTempFilePath);
            }
            catch
            {
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}