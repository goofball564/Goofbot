using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Goofbot
{
    internal class ColorDictionary
    {
        public const string ColorNamesRequestUrl = "https://api.color.pizza/v1/";

        private readonly Random _random = new();
        private readonly string _colorNamesFile;
        private readonly HttpClient _httpClient = new();
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        private List<string> _colorNameList;
        private Dictionary<string, string> _colorDictionary;

        private List<string> _saturatedColorNameList;
        private Dictionary<string, string> _saturatedColorDictionary;

        public ColorDictionary(string colorNamesFile)
        {
            _colorNamesFile = colorNamesFile;
        }

        public async Task Initialize(bool forceRedownload = false)
        {
            await Refresh(forceRedownload);
        }

        public async Task Refresh(bool forceRedownload = false)
        {
            await _semaphore.WaitAsync();
            try
            {
                _colorNameList = [];
                _colorDictionary = [];
                _saturatedColorNameList = [];
                _saturatedColorDictionary = [];

                Console.WriteLine("Building Color Dictionary");


                if (forceRedownload || !File.Exists(_colorNamesFile))
                {
                    await RefreshColorNamesFile();
                }

                dynamic colorNamesJson = Program.ParseJsonFile(_colorNamesFile);
                foreach (var color in colorNamesJson.colors)
                {
                    string colorName = Convert.ToString(color.name);
                    string colorNameLower = colorName.ToLowerInvariant();
                    string colorHex = Convert.ToString(color.hex).ToLowerInvariant();

                    if (!_colorDictionary.ContainsKey(colorNameLower))
                    {
                        _colorDictionary.Add(colorNameLower, colorHex);
                        _colorNameList.Add(colorName);
                    }
                    else
                    {
                        Console.WriteLine("Color Collision: " + colorName);
                    }

                    GetHSV(colorHex, out double h, out double s, out double v);

                    if (v >= 0.2 && GoodSaturation(s, v) && !_saturatedColorDictionary.ContainsKey(colorNameLower))
                    {
                        _saturatedColorDictionary.Add(colorNameLower, colorHex);
                        _saturatedColorNameList.Add(colorName);
                    }
                }

                Console.WriteLine("Color Dictionary Built");
            }
            finally
            {
                _semaphore.Release();
            }    
        }

        public string GetHex(string colorName)
        {
            _colorDictionary.TryGetValue(colorName.ToLowerInvariant(), out string hex);
            return hex;
        }

        public string GetRandomName()
        {
            int randomIndex = _random.Next(0, _colorNameList.Count);
            return _colorNameList[randomIndex];
        }

        public string GetRandomSaturatedName(out string hex)
        {
            int randomIndex = _random.Next(0, _saturatedColorNameList.Count);
            string colorName = _saturatedColorNameList[randomIndex];
            hex = GetHex(colorName);
            return _saturatedColorNameList[randomIndex];
        }

        private static void GetHSV(string hex, out double hue, out double saturation, out double value)
        {
            var color = ColorTranslator.FromHtml(hex);

            int max = Math.Max(color.R, Math.Max(color.G, color.B));
            int min = Math.Min(color.R, Math.Min(color.G, color.B));

            hue = color.GetHue();
            saturation = (max == 0) ? 0 : 1.0 - (1.0 * min / max);
            value = max / 255.0;
        }

        private static bool GoodSaturation(double saturation, double value)
        {
            value *= 100;
            saturation *= 100;

            
            return saturation >= (((100 - value) / 2.0) + 30.0);
        }

        private async Task<string> RequestColorNames()
        {
            var response = await _httpClient.GetAsync(ColorNamesRequestUrl);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
            else
            {
                return "";
            }
            
        }
        private async Task RefreshColorNamesFile()
        {
            string colorNamesString = await RequestColorNames();
            if (colorNamesString != "")
            {
                File.WriteAllText(_colorNamesFile, colorNamesString);
            }
        }
    }
}
