using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TgCaseBot;

public static class Utilities
{
    public static double? ExtractFirstPriceNumberSteam(string json)
    {
        
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("median_price", out var median) && median.GetString() != null)
        { 
            var medianPrice= median.GetString();
            
            medianPrice = medianPrice.Replace("$", "");
            return double.Parse(medianPrice, CultureInfo.InvariantCulture);
        }

        if (root.TryGetProperty("lowest_price", out var lowest))
        {
            var lowestPrice= lowest.GetString();
            lowestPrice = lowestPrice.Replace("$", "");
            return double.Parse(lowestPrice, CultureInfo.InvariantCulture);
        }
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