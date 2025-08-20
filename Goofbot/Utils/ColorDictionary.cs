namespace Goofbot.Utils;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

internal class ColorDictionary
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

    public async Task Initialize(bool forceRedownload = false)
    {
        await this.Refresh(forceRedownload);
    }

    public async Task Refresh(bool forceRedownload = false)
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
                await this.RefreshColorNamesFile();
            }

            dynamic colorNamesJson = Program.ParseJsonFile(this.colorNamesFile);
            foreach (var color in colorNamesJson.colors)
            {
                string colorName = Convert.ToString(color.name);
                string colorNameLower = colorName.ToLowerInvariant();
                string colorHex = Convert.ToString(color.hex).ToLowerInvariant();

                if (this.colorDictionary.TryAdd(colorNameLower, colorHex))
                {
                    this.colorNameList.Add(colorName);
                }

                GetHSV(colorHex, out double h, out double s, out double v);

                if (v >= 0.2 && GoodSaturation(s, v) && this.saturatedColorDictionary.TryAdd(colorNameLower, colorHex))
                {
                    this.saturatedColorNameList.Add(colorName);
                }
            }
        }
        finally
        {
            this.semaphore.Release();
        }
    }

    public string GetHex(string colorName)
    {
        this.colorDictionary.TryGetValue(colorName.ToLowerInvariant(), out string hex);
        return hex;
    }

    public string GetRandomName()
    {
        int randomIndex = this.random.Next(0, this.colorNameList.Count);
        return this.colorNameList[randomIndex];
    }

    public string GetRandomSaturatedName(out string hex)
    {
        int randomIndex = this.random.Next(0, this.saturatedColorNameList.Count);
        string colorName = this.saturatedColorNameList[randomIndex];
        hex = this.GetHex(colorName);
        return this.saturatedColorNameList[randomIndex];
    }

    private static void GetHSV(string hex, out double hue, out double saturation, out double value)
    {
        var color = ColorTranslator.FromHtml(hex);

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

    private async Task<string> RequestColorNames()
    {
        var response = await this.httpClient.GetAsync(ColorNamesRequestUrl);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadAsStringAsync();
        }
        else
        {
            return string.Empty;
        }
    }

    private async Task RefreshColorNamesFile()
    {
        string colorNamesString = await this.RequestColorNames();
        if (colorNamesString != string.Empty)
        {
            File.WriteAllText(this.colorNamesFile, colorNamesString);
        }
    }
}
