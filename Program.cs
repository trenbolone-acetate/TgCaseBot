namespace TgCaseBot;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

class Program
{ 
    static TelegramBotClient bot = new TelegramBotClient(File.ReadAllText("../../../tkn.txt"));
    static async Task Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        var me = await bot.GetMe();
        bot.OnMessage += OnMessage;
        
        Console.WriteLine($"bot @{me.Username} currently running..");
        Console.ReadLine();
        await cts.CancelAsync(); 
    }
    static async Task OnMessage(Message msg, UpdateType type)
    {
        if (msg.Text is null) return;
        Console.WriteLine($"Received {type} '{msg.Text}' in {msg.Chat}");
        
        await bot.SendMessage(msg.Chat, $"{msg.From} said: {msg.Text}");
    }
}
