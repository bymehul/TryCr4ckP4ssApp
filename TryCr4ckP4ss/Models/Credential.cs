using System;

namespace TryCr4ckP4ss.Models;

public class Credential
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Site { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Category { get; set; } = "general";
    public string TotpSecret { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}

public static class Categories
{
    public static string[] All { get; } = 
    {
        "general",
        "social",
        "work",
        "finance",
        "shopping",
        "entertainment",
        "development"
    };

    public static string GetIcon(string category) => category switch
    {
        "social" => "👥",
        "work" => "💼",
        "finance" => "💰",
        "shopping" => "🛒",
        "entertainment" => "🎮",
        "development" => "⚡",
        _ => "🔐"
    };

    public static string GetColor(string category) => category switch
    {
        "social" => "#7aa2f7",
        "work" => "#bb9af7",
        "finance" => "#c3e88d",
        "shopping" => "#ff9e64",
        "entertainment" => "#ff757f",
        "development" => "#7dcfff",
        _ => "#a9b1d6"
    };
}
