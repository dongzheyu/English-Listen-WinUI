using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace English_Listen_WinUI.Services
{
    public static class TempFileHelper
    {
        private static readonly string TempFilePath = Path.Combine(
            Path.GetTempPath(), "english_listen_temp.txt");

        private static readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public static async Task<List<string>> ReadWordsAsync()
        {
            await _lock.WaitAsync();
            try
            {
                if (!File.Exists(TempFilePath))
                {
                    return new List<string>();
                }

                var content = await File.ReadAllTextAsync(TempFilePath);
                return content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(w => w.Trim())
                    .Where(w => !string.IsNullOrEmpty(w))
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
            await _lock.WaitAsync();
            try
            {
                // Atomic write via temp file
                var tempPath = TempFilePath + ".tmp";
                await File.WriteAllLinesAsync(tempPath, words);
                if (File.Exists(TempFilePath))
                {
                    File.Delete(TempFilePath);
                }

                File.Move(tempPath, TempFilePath);
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
                {
                    File.Delete(TempFilePath);
                }
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