using System.Security.Cryptography;
using System.Text;

namespace RestaurantPos.Api.Helpers
{
    public static class PasswordHelper
    {
        // Existing users in this project are seeded with legacy SHA-256 hashes.
        // Keep backward-compatible verification for those rows, but use BCrypt for any new password storage.
        // Migration path: on a future successful legacy login, re-hash the password with BCrypt and overwrite PasswordHash.
        public static bool Verify(string plainPassword, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(plainPassword) || string.IsNullOrWhiteSpace(storedHash))
            {
                return false;
            }

            if (storedHash.StartsWith("$2", StringComparison.Ordinal))
            {
                return BCrypt.Net.BCrypt.Verify(plainPassword, storedHash);
            }

            return string.Equals(ComputeSha256Hash(plainPassword), storedHash, StringComparison.OrdinalIgnoreCase);
        }

        public static string Hash(string plainPassword)
            => BCrypt.Net.BCrypt.HashPassword(plainPassword, workFactor: 12);

        private static string ComputeSha256Hash(string rawData)
        {
            using var sha256Hash = SHA256.Create();
            var bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var value in bytes)
            {
                builder.Append(value.ToString("x2"));
            }

            return builder.ToString();
        }
    }
}
