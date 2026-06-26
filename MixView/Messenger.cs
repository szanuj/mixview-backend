using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;



/// <summary>
/// A generic wrapper class to REST API calls
/// </summary>
/// <typeparam name="T"></typeparam>
public static class HTTPClientWrapper<T> where T : class
{
    /// <summary>
    /// For getting the resources from a web api
    /// </summary>
    /// <param name="url">API Url</param>
    /// <returns>A Task with result object of type T</returns>
    public static async Task<T?> Get(string url)
    {
        T? result = null;
        using (var httpClient = new HttpClient())
        {
            var response = httpClient.GetAsync(new Uri(url)).Result;

            response.EnsureSuccessStatusCode();
            await response.Content.ReadAsStringAsync().ContinueWith((Task<string> x) =>
            {
                if (x.IsFaulted)
                    throw x.Exception;
                result = JsonConvert.DeserializeObject<T>(x.Result);
            });
        }

        return result;
    }
}