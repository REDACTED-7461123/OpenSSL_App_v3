using System.Text;

namespace OpenSSL_App_v3
{
    internal class Password_helpers
    {
        public static (int score, string label) Evaluate(string password)
        {
            if (string.IsNullOrEmpty(password)) return (0, "");

            int score = 0;

            if (password.Length >= 8) score++;
            if (password.Length >= 12) score++;

            bool hasLower = password.Any(char.IsLower);
            bool hasUpper = password.Any(char.IsUpper);
            bool hasDigit = password.Any(char.IsDigit);
            bool hasSymbol = password.Any(c => !char.IsLetterOrDigit(c));

            int groups = (hasLower ? 1 : 0) + (hasUpper ? 1 : 0) + (hasDigit ? 1 : 0) + (hasSymbol ? 1 : 0);

            if (groups >= 2) score++;
            if (groups >= 3) score++;
            if (score > 4) score = 4;

            string label = score switch
            {
                0 => "Very Weak",
                1 => "Weak",
                2 => "Normal",
                3 => "Good",
                4 => "Strong",
                _ => "—"
            };

            if (password.Distinct().Count() <= 2 && password.Length >= 6)
                return (1, "Weak (Repeats)");

            return (score, label);
        }

        // Source - https://stackoverflow.com/a/54997

        public static string Generate(int length) {
            const string valid = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*()_+=-<>?";
            StringBuilder res = new StringBuilder();
            Random rnd = new Random();
            while (0 < length--) {
                res.Append(valid[rnd.Next(valid.Length)]);
            }
            return res.ToString();
        }

    }
}