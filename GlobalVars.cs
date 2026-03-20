using System.Text.Json;
using Renci.SshNet;
using StackExchange.Redis;
using Telegram.Bot;
using TgCaseBot.Classes;

namespace TgCaseBot;

public static class GlobalVars
{
    //db coonection
    public static readonly ConnectionMultiplexer muxer = ConnectionMultiplexer.Connect(
        new ConfigurationOptions{
            EndPoints= { "127.0.0.1:6379"}
        }
    );
    public static readonly IDatabase db = muxer.GetDatabase();
    
    //tg bot
    public static readonly string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public static readonly TelegramBotClient Bot = new TelegramBotClient(File.ReadAllText("/home/armanus/tkn.txt"));
    
    public static readonly string[] Lines = File.ReadAllLines("/home/armanus/TgBotFiles/skins.jsonl");    
    
    //sftp connection
    public static SftpClient sftp;
    public static readonly string host = "172.19.158.11"; 
    public static readonly string username = "armanus";
    public static string password = File.ReadAllText(Path.Combine(home, "/home/armanus/sshPwd.txt"));
    
    //dict for grouping skins by rarity
    public static Dictionary<string, List<Skin?>> _skinsByRarity;
    
    //timer for cooldown
    public static Dictionary<string, DateTime> userCooldowns = new();
    public static readonly TimeSpan cooldown = TimeSpan.FromSeconds(30);
    
    //deserializing skins
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };
    public static List<Skin?> _skins = Lines
        .Select(line => JsonSerializer.Deserialize<Skin>(line,Options))
        .ToList();
}