using System;
using System.Security.Cryptography;
using System.Text;

namespace English_Listen_WinUI.Services
{
    public class PasswordService
    {
        public static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        public static bool VerifyPassword(string password, string passwordHash)
        {
            if (string.IsNullOrEmpty(passwordHash))
            {
                // 如果没有密码哈希，允许无密码登录（向后兼容）
                return true;
            }
            
            var hash = HashPassword(password);
            return hash == passwordHash;
        }

        public static string GenerateRandomSalt()
        {
            var bytes = new byte[16];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }
    }
}