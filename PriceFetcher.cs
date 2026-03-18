namespace TgCaseBot;
using System.Net.Http;
using System.Threading.Tasks;

public static class PriceFetcher
{
 public static async Task<string> GetPrice(string skinName) {
  using var client = new HttpClient();

  client.DefaultRequestHeaders.Add("Authorization", File.ReadAllText("../../../csfloatToken.txt"));

  string url = $"https://csfloat.com/api/v1/listings?limit=1&market_hash_name={Uri.EscapeDataString(skinName)}&sort_by=lowest_price";

  var response = await client.GetAsync(url);
  response.EnsureSuccessStatusCode();

  var content = await response.Content.ReadAsStringAsync();
  return content;
 }
}