using System.Text.RegularExpressions;
using Telegram.Bot.Polling;
using TgCaseBot.Classes;

namespace TgCaseBot;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using System.Text.Json;
using System.IO;

class Program
{
    private static readonly TelegramBotClient Bot = new TelegramBotClient(File.ReadAllText("../../../tkn.txt"));
    
    private static readonly string[] Lines = File.ReadAllLines("skins.jsonl");

    private static readonly JsonSerializerOptions options = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static List<Skin?> _skins = Lines
        .Select(line => JsonSerializer.Deserialize<Skin>(line,options))
        .ToList();
    
    static async Task Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        
        Bot.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandleErrorAsync,
            cancellationToken: cts.Token
        );        
        Console.ReadLine();
        await cts.CancelAsync(); 

        Console.ReadLine();
    }

    private static Task HandleErrorAsync(ITelegramBotClient arg1, Exception arg2, HandleErrorSource arg3, CancellationToken arg4)
    {
        throw new NotImplementedException();
    }

    static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Message is not { } msg)
            return;
        if (msg.Date < DateTime.UtcNow.AddSeconds(-5))
            return;
        
        if (msg.Text is null || !msg.Text.StartsWith("/case")) {
            await Bot.SendMessage(msg.Chat.Id, "ehhhh...");
            return;
        }
        var rng = new Random();
        Skin? randomSkin = _skins[rng.Next(_skins.Count)];
        Console.WriteLine($"User received:\n " +
                          $"{randomSkin.Rarity}\n " +
                          $"{randomSkin.Name}\n" +
                          $"Quality:{randomSkin.Exterior}\n" +
                          $"Price is ${Utilities.ExtractFirstPriceNumber(PriceFetcher.GetPrice($"{randomSkin.Name}").Result) / 100}\n\n");

        var path = $"D:\\skinsSet\\images\\{randomSkin.ImageId}.png";
        await using var stream = File.OpenRead(path);
        
        await Bot.SendPhoto(msg.Chat.Id, 
            photo: InputFile.FromStream(stream, $"{randomSkin.ImageId}.png"), 
            caption:$"🎉 You have received:\n" +
                    $"{Utilities.GetRarityColor(randomSkin.Rarity)} {randomSkin.Rarity}\n" +
                    $"👉 {randomSkin.Name}\n"+
                    $"🛠 Quality:{randomSkin.Exterior}\n" +
                    $"💵 ${Utilities.ExtractFirstPriceNumber(PriceFetcher.GetPrice($"{randomSkin.Name}").Result) / 100}\n\n");
    }
    
}
