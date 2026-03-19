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
    //db coonection
    private static readonly ConnectionMultiplexer muxer = ConnectionMultiplexer.Connect(
        new ConfigurationOptions{
            EndPoints= { "127.0.0.1:6379"}
        }
    );
    private static readonly IDatabase db = muxer.GetDatabase();
    
    //tg bot
    private static readonly TelegramBotClient Bot = new TelegramBotClient(File.ReadAllText("../../../tkn.txt"));
    
    private static readonly string[] Lines = File.ReadAllLines("skins.jsonl");
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };
    //deserializing skins
    private static List<Skin?> _skins = Lines
        .Select(line => JsonSerializer.Deserialize<Skin>(line,Options))
        .ToList();
    
    //dict for grouping skins by rarity
    private static Dictionary<string, List<Skin?>> _skinsByRarity;
    
    //timer for cooldown
    private static Dictionary<string, DateTime> userCooldowns = new();
    private static readonly TimeSpan cooldown = TimeSpan.FromSeconds(3);
    
    
    static async Task Main(string[] args)
    {
        _skinsByRarity = _skins
            .GroupBy(s => s.Rarity)
            .ToDictionary(g => g.Key, g => g.ToList());
        
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
        Console.WriteLine(arg2);
        Console.WriteLine(arg3);
        throw new NotImplementedException();
    }
    private static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Message is not { } msg)
            return;
        if (msg.Date < DateTime.UtcNow.AddSeconds(-5))
            return;
        
        var user = update.Message?.From.Username ?? update.CallbackQuery?.From.Username;
        if (user == null) return;

        if (userCooldowns.TryGetValue(user, out var lastTime))
        {
            var elapsed = DateTime.UtcNow - lastTime;
            if (elapsed < cooldown)
            {
                await Bot.SendMessage(msg.Chat.Id,
                    $"Please wait {Math.Ceiling((cooldown - elapsed).TotalSeconds)} seconds to use the bot again.", cancellationToken: ct);
                return;
            }
        }
        
        userCooldowns[user] = DateTime.UtcNow;
        
        var text = msg.Text;
        if (text == null)
        {
            await SendDefault(msg.Chat.Id, ct);
            return;
        }

        await RouteCommand(msg, text, ct);
    }
    private static Task SendDefault(long chatId, CancellationToken ct) =>
        Bot.SendMessage(chatId, "че", replyMarkup: GetKeyboard(), cancellationToken: ct);
    private static async Task RouteCommand(Message msg, string text, CancellationToken ct)
    {
        var userId = msg.From.Username;

        switch (text)
        {
            case "/start":
                await SendStart(msg.Chat.Id, ct);
                break;

            case "Check balance":
                await DbGetUserDetails(userId, msg.Chat.Id, ct);
                break;

            case "Reset score":
                await DbClearUserEntry(userId, msg.Chat.Id, ct);
                break;

            case "Leaderboard📊":
                await DbGetLeaderboard(msg.Chat.Id, ct);
                break;

            default:
                if (text.StartsWith("/case", StringComparison.OrdinalIgnoreCase) || text.StartsWith("Open",StringComparison.OrdinalIgnoreCase))
                    await GetSkin(msg.Chat.Id, userId);
                else
                    await SendDefault(msg.Chat.Id, ct);
                break;
        }
    }
    private static Task SendStart(long chatId, CancellationToken ct) =>
        Bot.SendMessage(chatId, "дарова", replyMarkup: GetKeyboard(), cancellationToken: ct);
    private static async Task GetSkin(long msgChatId, string userId)
    {
        var randomSkin = RollSkin();
        
        double? price = Utilities.ExtractFirstPriceNumber(
                            await PriceFetcher.GetPrice($"{randomSkin.Name}")) 
                        / 100;
        
        Console.WriteLine($"User {userId} received:\n " +
                          $"{randomSkin.Rarity}\n " +
                          $"{randomSkin.Name}\n" +
                          $"Quality:{randomSkin.Exterior}\n" +
                          $"Price is ${price}\n");

        await SendSkin(msgChatId, randomSkin, price);
        
        await DbAddEntry(userId, price);
    }
    private static Skin? RollSkin()
    {
        var rarity = Utilities.GetChances();
        var skins = _skinsByRarity[rarity];
        return skins[Random.Shared.Next(skins.Count)];
    }
    private static async Task SendSkin(long msgChatId, Skin randomSkin, double? price)
    {
        var path = $"D:\\skinsSet\\imagesWebP\\{randomSkin.ImageId}.webp";
        await using var stream = File.OpenRead(path);

        try
        {
            await Bot.SendDocument(msgChatId,
                document: InputFile.FromStream(stream, $"{randomSkin.ImageId}.webp"));
        }
        //fallback for cases when the image is not found
        catch (Exception e)
        {
            await Bot.SendDocument(msgChatId,
                document: InputFile.FromStream(stream, $"eyes.webp"));
            Console.WriteLine(e);
            throw;
        }
        await Bot.SendMessage(msgChatId,
            text:$"🎉 You have received:\n" + 
                 $"{Utilities.GetRarityColor(randomSkin.Rarity)} {randomSkin.Rarity}\n" + 
                 $"👉 {randomSkin.Name}\n"+ 
                 $"🛠 Quality:{randomSkin.Exterior}\n" + 
                 $"💵 ${price}\n\n",
            replyMarkup: GetKeyboard());
    }
    static async Task DbAddEntry(string userId, double? price)
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
            await db.HashIncrementAsync(userId, "cases_opened");
            await db.HashIncrementAsync(userId, "balance", price ?? 0.00);
        }

        Console.WriteLine($"User {userId} has been updated with {price} to their balance\n");
    }
    private static async Task DbClearUserEntry(string userId, long msgChatId, CancellationToken ct)
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
    private static async Task DbGetUserDetails(string userId, long msgChatId, CancellationToken ct)
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
    private static async Task DbGetLeaderboard(long msgChatId, CancellationToken ct)
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
                $"{user.Username} | Balance: {user.Balance:F2} | Cases Opened: {user.CasesOpened}"
            );
        }

        await Bot.SendMessage(msgChatId, leaderboard.ToString(), cancellationToken: ct);
    }
    private static ReplyKeyboardMarkup GetKeyboard() => new(new[]
        {
            new KeyboardButton[] { "Open🗝️", "Check balance" },
            new KeyboardButton[] { "Reset score", "Leaderboard📊" }
        })
        {
            ResizeKeyboard = true
        };
}
