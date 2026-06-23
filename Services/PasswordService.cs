using System;
using System.Security.Cryptography;
using System.Text;

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

            byte[] hash;
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
            {
                hash = pbkdf2.GetBytes(HashSize);
            }

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

                using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
                {
                    byte[] computedHash = pbkdf2.GetBytes(storedHash.Length);
                    return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
                }
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
