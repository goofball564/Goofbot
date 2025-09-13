namespace Goofbot.Utils;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

internal class ColorDictionary : IDisposable
{
    public const string ColorNamesRequestUrl = "https://api.color.pizza/v1/";

    private readonly Random random = new ();
    private readonly string colorNamesFile;
    private readonly HttpClient httpClient = new ();
    private readonly SemaphoreSlim semaphore = new (1, 1);

    private List<string> colorNameList;
    private Dictionary<string, string> colorDictionary;

    private List<string> saturatedColorNameList;
    private Dictionary<string, string> saturatedColorDictionary;

    public ColorDictionary(string colorNamesFile)
    {
        this.colorNamesFile = colorNamesFile;
    }

    public void Dispose()
    {
        this.httpClient.Dispose();
        this.semaphore.Dispose();
    }

    public async Task InitializeAsync(bool forceRedownload = false)
    {
        await this.RefreshAsync(forceRedownload);
    }

    public async Task RefreshAsync(bool forceRedownload = false)
    {
        await this.semaphore.WaitAsync();
        try
        {
            this.colorNameList = [];
            this.colorDictionary = [];
            this.saturatedColorNameList = [];
            this.saturatedColorDictionary = [];

            if (forceRedownload || !File.Exists(this.colorNamesFile))
            {
                await this.RefreshColorNamesFileAsync();
            }

            dynamic colorNamesJson = await Program.ParseJsonFileAsync(this.colorNamesFile);
            await Task.Run(() =>
            {
                foreach (dynamic color in colorNamesJson.colors)
                {
                    string colorName = Convert.ToString(color.name);
                    string colorNameLower = colorName.ToLowerInvariant();
                    string hexColorCode = Convert.ToString(color.hex).ToLowerInvariant();

                    if (this.colorDictionary.TryAdd(colorNameLower, hexColorCode))
                    {
                        this.colorNameList.Add(colorName);
                    }

                    GetHSV(hexColorCode, out double h, out double s, out double v);

                    if (v >= 0.2 && GoodSaturation(s, v) && this.saturatedColorDictionary.TryAdd(colorNameLower, hexColorCode))
                    {
                        this.saturatedColorNameList.Add(colorName);
                    }
                }
            });
        }
        finally
        {
            this.semaphore.Release();
        }
    }

    public async Task<(bool, string)> TryGetHexColorCodeAsync(string colorName)
    {
        await this.semaphore.WaitAsync();
        try
        {
            bool success = this.colorDictionary.TryGetValue(colorName.ToLowerInvariant(), out string hexColorCode);
            return (success, hexColorCode);
        }
        finally
        {
            this.semaphore.Release();
        }
    }

    public async Task<ColorNameAndHexColorCode> GetRandomSaturatedColorAsync()
    {
        await this.semaphore.WaitAsync();
        try
        {
            int randomIndex = this.random.Next(0, this.saturatedColorNameList.Count);
            string colorName = this.saturatedColorNameList[randomIndex];
            string hexColorCode = this.colorDictionary[colorName.ToLowerInvariant()];
            return new ColorNameAndHexColorCode(colorName, hexColorCode);
        }
        finally
        {
            this.semaphore.Release();
        }
    }

    public struct ColorNameAndHexColorCode
    {
        public readonly string ColorName;
        public readonly string HexColorCode;

        public ColorNameAndHexColorCode(string colorName, string hexColorCode)
        {
            this.ColorName = colorName;
            this.HexColorCode = hexColorCode;
        }
    }

    private static void GetHSV(string hexColorCode, out double hue, out double saturation, out double value)
    {
        Color color = ColorTranslator.FromHtml(hexColorCode);

        int max = Math.Max(color.R, Math.Max(color.G, color.B));
        int min = Math.Min(color.R, Math.Min(color.G, color.B));

        hue = color.GetHue();
        saturation = max == 0 ? 0 : 1.0 - (1.0 * min / max);
        value = max / 255.0;
    }

    private static bool GoodSaturation(double saturation, double value)
    {
        value *= 100;
        saturation *= 100;

        return saturation >= ((100 - value) / 2.0) + 30.0;
    }

    private async Task<string> RequestColorNamesAsync()
    {
        HttpResponseMessage response = await this.httpClient.GetAsync(ColorNamesRequestUrl);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadAsStringAsync();
        }
        else
        {
            return string.Empty;
        }
    }

    private async Task RefreshColorNamesFileAsync()
    {
        string colorNamesString = await this.RequestColorNamesAsync();
        if (!colorNamesString.Equals(string.Empty))
        {
            await File.WriteAllTextAsync(this.colorNamesFile, colorNamesString);
        }
    }
}
