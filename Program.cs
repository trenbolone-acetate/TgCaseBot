using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using Telegram.Bot.Polling;
using TgCaseBot.Classes;

using NRedisStack;
using NRedisStack.RedisStackCommands;
using Renci.SshNet.Sftp;
using StackExchange.Redis;

namespace TgCaseBot;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using System.Text.Json;
using System.IO;
using Renci.SshNet;
using static GlobalVars;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine($"{DateTime.Now}:  Bot is alive...\n");
        
        await InitializeSftpConnection();
        
        await GetJson();
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

        var userId = msg.From?.Id;
        if (userId == null) return;

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
                if (!IsOnCooldown(userId))
                {
                    if (text.StartsWith("/case", StringComparison.OrdinalIgnoreCase) ||
                        text.StartsWith("Open",StringComparison.OrdinalIgnoreCase))
                        await GetSkin(msg.Chat.Id, userId);
                    else
                        await SendDefault(msg.Chat.Id, ct);
                }
                else
                {
                        var elapsed = DateTime.UtcNow - userCooldowns.Last(u => u.Key == userId).Value;
                        await Bot.SendMessage(msg.Chat.Id, $"⏱️ Wait {Math.Ceiling((cooldown - elapsed).TotalSeconds)} seconds before opening a new case.", cancellationToken: ct);
                }
                break;
        }
    }
    private static Task SendStart(long chatId, CancellationToken ct) =>
        Bot.SendMessage(chatId, "дарова", replyMarkup: GetKeyboard(), cancellationToken: ct);
    private static async Task GetSkin(long msgChatId, string userId)
    {
        var randomSkin = RollSkin();
        
        double? price = Utilities.ExtractFirstPriceNumberSteam(
                            await PriceFetcher.GetSteamPrice($"{randomSkin.Name}"));
        Console.WriteLine(await PriceFetcher.GetSteamPrice($"{randomSkin.Name}"));
        
        Console.WriteLine($"{userId}:\n " +
                          $"{randomSkin.Rarity}  -  {randomSkin.Name}\n" +
                          $"{randomSkin.Exterior}\n" +
                          $"${price}\n");

        await SendSkin(msgChatId, userId, randomSkin, price);
        
        await DbAddEntry(userId, price);
    }
    private static Skin? RollSkin()
    {
        var rarity = Utilities.GetChances();
        var skins = _skinsByRarity[rarity];
        return skins[Random.Shared.Next(skins.Count)];
    }
    private static async Task SendSkin(long msgChatId, string userId, Skin randomSkin, double? price)
    {
        await SendImage(msgChatId, randomSkin.ImageId);
        
        await Bot.SendMessage(msgChatId,
            text:$"🎉 @{userId} has received:\n" + 
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

        Console.WriteLine($"add {price} to {userId} balance\n");
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
        Console.WriteLine($"{userId} reset balance\n");
    }
    private static async Task DbGetUserDetails(string userId, long msgChatId, CancellationToken ct)
    {
        if (await db.KeyExistsAsync(userId))
        {
            var balance = (double)db.HashGet(userId, "balance");
            var casesOpened = (double)db.HashGet(userId, "cases_opened");
            await Bot.SendMessage(msgChatId, $"💰@{userId} balance is: ${balance:F2}\n📦You have opened {casesOpened} cases!",
                replyMarkup: GetKeyboard(), cancellationToken: ct);
        }
        else
        {
            await Bot.SendMessage(msgChatId, $"💰Your balance is: $0",
                replyMarkup: GetKeyboard(), cancellationToken: ct);
        }
        Console.WriteLine($"{userId} check balance\n");
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
    private static bool IsOnCooldown(string username)
    {
        if (userCooldowns.TryGetValue(username, out var lastTime))
        {
            var elapsed = DateTime.UtcNow - lastTime;
            if (elapsed < cooldown)
                return true;
        }

        userCooldowns[username] = DateTime.UtcNow;
        return false;
    }
    private static ReplyKeyboardMarkup GetKeyboard() => new(new[]
        {
            new KeyboardButton[] { "Open🗝️", "Check balance" },
            new KeyboardButton[] { "Reset score", "Leaderboard📊" }
        })
        {
            ResizeKeyboard = true
        };

    private static async Task InitializeSftpConnection()
    {
        sftp = new SftpClient(host, username, password);
        sftp.Connect();
    }
    private static async Task GetJson()
    {
        using (var stream = new MemoryStream())
        {
            sftp.DownloadFile("/home/armanus/TgBotFiles/skins.jsonl", stream);
            stream.Position = 0;
            using (var reader = new StreamReader(stream))
            {
                (await reader.ReadToEndAsync()).Split('\n');
            }
        }
    
        sftp.Disconnect();
    }
    private static async Task SendImage(long msgChatId, string pictureNumber)
    {
        if (!sftp.IsConnected)
            sftp.Connect();

        try
        {
            using var stream = new MemoryStream();
            await sftp.DownloadFileAsync($"/home/armanus/TgBotFiles/imagesWebP/{pictureNumber}.webp", stream);

            stream.Position = 0;

            await Bot.SendDocument(msgChatId,
                document: InputFile.FromStream(stream, $"{pictureNumber}.webp"));
        }
        catch (Exception e)
        {
            Console.WriteLine(e);

            using var fallbackStream = new MemoryStream();
            await sftp.DownloadFileAsync("/home/armanus/TgBotFiles/imagesWebP/eyes.webp", fallbackStream);

            fallbackStream.Position = 0;

            await Bot.SendDocument(msgChatId,
                document: InputFile.FromStream(fallbackStream, "eyes.webp"));
        }
    }
    
}
