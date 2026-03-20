using System.Net.Http.Headers;

namespace TgCaseBot;
using System.Net.Http;
using System.Threading.Tasks;
using static GlobalVars;


public static class PriceFetcher
{
 private static readonly HttpClient client = new();
 private static readonly string _floatToken = File.ReadAllText("/home/armanus/csfloatToken.txt").Trim();
 public static async Task<string> GetPrice(string skinName) {
  
  client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(_floatToken);

  string url = $"https://csfloat.com/api/v1/listings?limit=1&market_hash_name={Uri.EscapeDataString(skinName)}&sort_by=lowest_price&type=buy_now";

  var response = await client.GetAsync(url);
  response.EnsureSuccessStatusCode();
  
  var content = await response.Content.ReadAsStringAsync();
  return content;
 }
}