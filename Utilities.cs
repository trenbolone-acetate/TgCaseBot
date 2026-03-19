using System.Text.RegularExpressions;

namespace TgCaseBot;

public static class Utilities
{
    public static double? ExtractFirstPriceNumber(string text)
    {
        var match = Regex.Match(text, @"""price"":(\d+)", RegexOptions.IgnoreCase);
        if (match.Success && double.TryParse(match.Groups[1].Value, out double price))
            return price;
        return null; 
    }
    
    public static string GetRarityColor(string rarityName)
    {
        return rarityName switch
        {
            "Consumer Grade" => "🚮",
            "Industrial Grade" => "👨‍❤️‍👨",
            "Mil-Spec" => "😐",
            "Restricted" => "🤘",
            "Classified" => "👩‍❤️‍💋‍👩",
            "Covert" => "🤤",
            _ => "white"
        };
    }
    public static string GetChances()
    {
        var random = new Random();
        switch (random.Next(1, 273))
        {
            case <= 79:
                return "Consumer Grade";
            case <= 139:
                return "Industrial Grade";
            case <= 240:
                return "Mil-Spec";
            case <= 258:
                return "Restricted";
            case <= 263:
                return "Classified";
            case <= 270:
                return "Covert";
            case <= 272:
                return "Contraband";
            default:
                return "Unknown";
        }
    }
}