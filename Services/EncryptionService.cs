using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace English_Listen_WinUI.Services
{
    public class EncryptionService
    {
        private const int SaltSize = 16; // 128 bits
        private const int KeySize = 32; // 256 bits
        private const int Iterations = 10000;

        public static async Task<string> EncryptAsync(string plainText, string password)
        {
            if (string.IsNullOrEmpty(plainText) || string.IsNullOrEmpty(password))
                return plainText ?? string.Empty;

            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var key = await DeriveKeyAsync(password, salt);
            
            using var aes = Aes.Create();
            aes.Key = key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var memoryStream = new MemoryStream();
            using var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write);
            using var writer = new StreamWriter(cryptoStream, Encoding.UTF8);

            await writer.WriteAsync(plainText);
            await writer.FlushAsync();

            var encryptedData = memoryStream.ToArray();
            var result = new byte[SaltSize + aes.BlockSize / 8 + encryptedData.Length];
            Buffer.BlockCopy(salt, 0, result, 0, SaltSize);
            Buffer.BlockCopy(aes.IV, 0, result, SaltSize, aes.BlockSize / 8);
            Buffer.BlockCopy(encryptedData, 0, result, SaltSize + aes.BlockSize / 8, encryptedData.Length);

            return Convert.ToBase64String(result);
        }

        public static async Task<string> DecryptAsync(string encryptedText, string password)
        {
            if (string.IsNullOrEmpty(encryptedText) || string.IsNullOrEmpty(password))
                return encryptedText ?? string.Empty;

            try
            {
                var data = Convert.FromBase64String(encryptedText);
                if (data.Length < SaltSize + 16)
                    return encryptedText;

                var salt = new byte[SaltSize];
                var iv = new byte[16];
                var encryptedData = new byte[data.Length - SaltSize - 16];

                Buffer.BlockCopy(data, 0, salt, 0, SaltSize);
                Buffer.BlockCopy(data, SaltSize, iv, 0, 16);
                Buffer.BlockCopy(data, SaltSize + 16, encryptedData, 0, encryptedData.Length);

                var key = await DeriveKeyAsync(password, salt);

                using var aes = Aes.Create();
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using var memoryStream = new MemoryStream(encryptedData);
                using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
                using var reader = new StreamReader(cryptoStream, Encoding.UTF8);

                return await reader.ReadToEndAsync();
            }
            catch
            {
                return encryptedText;
            }
        }

        private static async Task<byte[]> DeriveKeyAsync(string password, byte[] salt)
        {
            return await Task.Run(() =>
            {
                using var rfc2898 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
                return rfc2898.GetBytes(KeySize);
            });
        }
    }
}