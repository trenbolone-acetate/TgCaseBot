using System.Net.Http.Headers;

namespace TgCaseBot;
using System.Net.Http;
using System.Threading.Tasks;
using static GlobalVars;


public static class PriceFetcher
{
 private static readonly HttpClient steamWebClient = new();
 public static async Task<string> GetSteamPrice(string skinName) {

  if (skinName.StartsWith("Souvenir "))
  {
   skinName = skinName.Remove(0, 9);
  }
  var vanillaIndex = skinName.IndexOf(" | Vanilla", StringComparison.Ordinal);
  if (vanillaIndex >= 0)
   skinName = skinName.Substring(0, vanillaIndex);
  
  string encodedName = Uri.EscapeDataString($"{skinName}");
  string url = $"https://steamcommunity.com/market/priceoverview/?country=US&appid=730&market_hash_name={encodedName}&currency=USD";
  
  var response = await steamWebClient.GetAsync(url);
  response.EnsureSuccessStatusCode();
  
  var content = await response.Content.ReadAsStringAsync();
  return content;
 }
}