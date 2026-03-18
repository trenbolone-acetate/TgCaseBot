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
}