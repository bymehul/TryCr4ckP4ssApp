using System;
using System.Linq;

namespace TryCr4ckP4ss.Services;

public static class PasswordHealthService
{
    public static int CalculateScore(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return 0;
        }

        var score = 0;

        if (password.Length >= 8)
        {
            score++;
        }

        if (password.Length >= 12)
        {
            score++;
        }

        if (password.Length >= 16)
        {
            score++;
        }

        var hasLower = password.Any(char.IsLower);
        var hasUpper = password.Any(char.IsUpper);
        var hasDigit = password.Any(char.IsDigit);
        var hasSymbol = password.Any(c => !char.IsLetterOrDigit(c));
        var charClassCount = Convert.ToInt32(hasLower) + Convert.ToInt32(hasUpper) + Convert.ToInt32(hasDigit) + Convert.ToInt32(hasSymbol);

        if (charClassCount >= 2)
        {
            score++;
        }

        if (charClassCount >= 3)
        {
            score++;
        }

        if (charClassCount == 4)
        {
            score++;
        }

        if (HasRepeatedPattern(password))
        {
            score--;
        }

        return Math.Clamp(score - 1, 0, 4);
    }

    public static bool IsWeak(string? password) => CalculateScore(password) <= 1;

    public static string GetLabel(int score) => Math.Clamp(score, 0, 4) switch
    {
        0 => "Very Weak",
        1 => "Weak",
        2 => "Fair",
        3 => "Strong",
        _ => "Very Strong"
    };

    public static string GetColor(int score) => Math.Clamp(score, 0, 4) switch
    {
        0 => "#ff757f",
        1 => "#ff9e64",
        2 => "#e0af68",
        3 => "#9ece6a",
        _ => "#73daca"
    };

    public static int GetPercent(int score) => Math.Clamp((Math.Clamp(score, 0, 4) + 1) * 20, 0, 100);

    private static bool HasRepeatedPattern(string password)
    {
        if (password.Length < 4)
        {
            return false;
        }

        for (var size = 1; size <= password.Length / 2; size++)
        {
            var chunk = password[..size];
            var repeated = string.Concat(Enumerable.Repeat(chunk, password.Length / size + 1));
            if (repeated.StartsWith(password, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
