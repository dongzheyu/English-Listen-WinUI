using System;
using System.Security.Cryptography;

namespace English_Listen_WinUI.Services
{
    public static class PasswordService
    {
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 100_000;
        private const char Separator = ':';

        public static string HashPassword(string password)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));

            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

            return $"{Convert.ToBase64String(salt)}{Separator}{Convert.ToBase64String(hash)}";
        }

        public static bool VerifyPassword(string password, string passwordHash)
        {
            if (string.IsNullOrEmpty(passwordHash))
                return false;

            if (password == null) return false;

            var parts = passwordHash.Split(Separator);
            if (parts.Length != 2)
                return false;

            try
            {
                byte[] salt = Convert.FromBase64String(parts[0]);
                byte[] storedHash = Convert.FromBase64String(parts[1]);

                byte[] computedHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256,
                    storedHash.Length);
                return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
            }
            catch
            {
                return false;
            }
        }

        public static bool NeedsRehash(string passwordHash)
        {
            if (string.IsNullOrEmpty(passwordHash))
                return true;

            var parts = passwordHash.Split(Separator);
            if (parts.Length != 2)
                return true;

            try
            {
                byte[] salt = Convert.FromBase64String(parts[0]);
                return salt.Length != SaltSize;
            }
            catch
            {
                return true;
            }
        }
    }
}