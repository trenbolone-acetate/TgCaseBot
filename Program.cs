using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using Telegram.Bot.Polling;
using TgCaseBot.Classes;

using NRedisStack;
using NRedisStack.RedisStackCommands;
using StackExchange.Redis;

namespace TgCaseBot;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
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

    static ConnectionMultiplexer muxer = ConnectionMultiplexer.Connect(
        new ConfigurationOptions{
            EndPoints= { "127.0.0.1:6379"}
        }
    );
    static IDatabase db = muxer.GetDatabase();
        
    
    
    
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
        
        if (msg.Text != null && msg.Text.Equals("/start", StringComparison.OrdinalIgnoreCase))
        {
            await Bot.SendMessage(msg.Chat.Id, "бутылка", cancellationToken: ct, 
                replyMarkup: new ReplyKeyboardMarkup(new[]
                {
                    new KeyboardButton[] { "Open🗝️", "Check balance" },
                    new KeyboardButton[] { "Reset score", "Leaderboard📊" }
                })
                {
                    ResizeKeyboard = true
                });        
            return;
        }
        
        if (msg.Text is null || 
            (!msg.Text.StartsWith("/case", StringComparison.OrdinalIgnoreCase) && 
             !msg.Text.StartsWith("Open", StringComparison.OrdinalIgnoreCase) && 
             !msg.Text.StartsWith("Check balance", StringComparison.OrdinalIgnoreCase) && 
             !msg.Text.StartsWith("Leaderboard", StringComparison.OrdinalIgnoreCase) && 
             !msg.Text.StartsWith("Reset score", StringComparison.OrdinalIgnoreCase)))
        {
            await Bot.SendMessage(msg.Chat.Id, "че",
                replyMarkup: new ReplyKeyboardMarkup(new[]
                {
                    new KeyboardButton[] { "Open🗝️", "Check balance" },
                    new KeyboardButton[] { "Reset score", "Leaderboard📊" }
                })
                {
                    ResizeKeyboard = true
                },
                cancellationToken: ct
            );
            return;
        }
        
        var userId = $"{msg.From.Username}";

        switch (msg.Text)
        {
            case "Check balance":
                await DbGetUserDetails(userId, msg.Chat.Id, ct);
                return;
            case "Reset score":
                await DbClearUserEntry(userId, msg.Chat.Id, ct);
                return;
            case "Leaderboard📊":
                await DbGetLeaderboard(msg.Chat.Id, ct);
                return;
            default:
                await GetSkin(msg.Chat.Id, userId);
                break;
        }
    }
    private static async Task GetSkin(long msgChatId, string userId)
    {
        var rarity = Utilities.GetChances();
        var group = _skins.FirstOrDefault(s => s.Rarity == rarity);
        
        var skinsOfRarity = _skins.Where(s => s.Rarity == rarity).ToList();

        var randomSkin = skinsOfRarity[Random.Shared.Next(skinsOfRarity.Count)];
        double price = (double)(Utilities.ExtractFirstPriceNumber(PriceFetcher.GetPrice($"{randomSkin.Name}").Result) / 100);
        
        Console.WriteLine($"User {userId} received:\n " +
                          $"{randomSkin.Rarity}\n " +
                          $"{randomSkin.Name}\n" +
                          $"Quality:{randomSkin.Exterior}\n" +
                          $"Price is ${price}\n\n");

        var path = $"D:\\skinsSet\\images\\{randomSkin.ImageId}.png";
        await using var stream = File.OpenRead(path);
        
        await Bot.SendPhoto(msgChatId, 
            photo: InputFile.FromStream(stream, $"{randomSkin.ImageId}.png"), 
            caption:$"🎉 You have received:\n" +
                    $"{Utilities.GetRarityColor(randomSkin.Rarity)} {randomSkin.Rarity}\n" +
                    $"👉 {randomSkin.Name}\n"+
                    $"🛠 Quality:{randomSkin.Exterior}\n" +
                    $"💵 ${price}\n\n",
            replyMarkup: new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "Open🗝️", "Check balance" },
                new KeyboardButton[] { "Reset score", "Leaderboard📊" }
            })
            {
                ResizeKeyboard = true
            });
        
        await DbAddEntry(userId, price);
    }

    static async Task DbAddEntry(string userId, double price)
    {
        if (!await db.KeyExistsAsync(userId))
        {
            await db.HashSetAsync(userId, [
                new HashEntry("cases_opened", 1),
                new HashEntry("balance", price)
            ]);
        }
        else
        {
            await db.HashIncrementAsync(userId, "cases_opened", 1);
            await db.HashIncrementAsync(userId, "balance", price);
        }

        Console.WriteLine($"User {userId} has been updated with {price} to their balance\n");
    }
    static async Task DbClearUserEntry(string userId, long msgChatId, CancellationToken ct)
    {
        if (!await db.KeyExistsAsync(userId))
        {
            await db.HashSetAsync(userId, [
                new HashEntry("cases_opened", 0),
                new HashEntry("balance", 0)
            ]);
        }
        else
        {
            await db.HashSetAsync(userId, [
                new HashEntry("cases_opened", 0),
                new HashEntry("balance", 0.0)
            ]);
        }
        await Bot.SendMessage(msgChatId, "Your stats have been reset", cancellationToken: ct);
        Console.WriteLine($"User {userId} has been reset\n");
    }
    static async Task DbGetUserDetails(string userId, long msgChatId, CancellationToken ct)
    {
        if (await db.KeyExistsAsync(userId))
        {
            var balance = (double)db.HashGet(userId, "balance");
            var casesOpened = (double)db.HashGet(userId, "cases_opened");
            await Bot.SendMessage(msgChatId, $"💰Your balance is: ${balance:F2}\n📦You have opened {casesOpened} cases!",
                replyMarkup: new ReplyKeyboardMarkup(new[]
                {
                    new KeyboardButton[] { "Open🗝️", "Check balance" },
                    new KeyboardButton[] { "Reset score", "Leaderboard📊" }
                })
                {
                    ResizeKeyboard = true
                }, cancellationToken: ct);
        }
        else
        {
            await Bot.SendMessage(msgChatId, $"💰Your balance is: $0",
                replyMarkup: new ReplyKeyboardMarkup(new[]
                {
                    new KeyboardButton[] { "Open🗝️", "Check balance" },
                    new KeyboardButton[] { "Reset score", "Leaderboard📊" }
                })
                {
                    ResizeKeyboard = true
                }, cancellationToken: ct);
        }
        Console.WriteLine($"User {userId}'s details have been displayed\n");
    }

    static async Task DbGetLeaderboard(long msgChatId, CancellationToken ct)
    {
        var server = muxer.GetServer("127.0.0.1:6379");
        var keys = server.Keys(pattern: "*");

        var leaderboardData = new List<(string Username, double Balance, long CasesOpened)>();

        foreach (var key in keys)
        {
            var hash = await db.HashGetAllAsync(key);

            var balanceValue = hash.FirstOrDefault(f => f.Name == "balance").Value;
            var casesValue = hash.FirstOrDefault(f => f.Name == "cases_opened").Value;

            double balance = balanceValue.HasValue ? (double)balanceValue : 0;
            long cases = casesValue.HasValue ? (long)casesValue : 0;

            leaderboardData.Add((key.ToString(), balance, cases));
        }

        var topUsers = leaderboardData
            .OrderByDescending(x => x.Balance)
            .Take(10);

        StringBuilder leaderboard = new StringBuilder();
        leaderboard.AppendLine("The leaderboard📊:\n");

        foreach (var user in topUsers)
        {
            leaderboard.AppendLine(
                $"{user.Username} | Balance: {user.Balance} | Cases Opened: {user.CasesOpened}"
            );
        }

        await Bot.SendMessage(msgChatId, leaderboard.ToString(), cancellationToken: ct);
    }
}
