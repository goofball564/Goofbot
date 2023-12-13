using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Goofbot
{
    internal class ColorDictionary
    {
        private List<string> colorNameList = new List<string>();
        private Dictionary<string, string> colorDictionary = new Dictionary<string, string>();

        private List<string> saturatedColorNameList = new List<string>();
        private Dictionary<string, string> saturatedColorDictionary = new Dictionary<string, string>();

        private Random random = new Random();

        public ColorDictionary(string colorNamesFile)
        {
            Console.WriteLine("Building Color Dictionary");
            dynamic colorNamesJson = Program.ParseJsonFile(colorNamesFile);
            foreach(var color in colorNamesJson.colors)
            {
                string colorName = Convert.ToString(color.name);
                string colorNameLower = colorName.ToLowerInvariant();
                string colorHex = Convert.ToString(color.hex).ToLowerInvariant();

                if (!colorDictionary.ContainsKey(colorNameLower))
                {
                    colorDictionary.Add(colorNameLower, colorHex);
                    colorNameList.Add(colorName);
                }
                else
                {
                    Console.WriteLine("Color Collision: " + colorName);
                }

                GetHSV(colorHex, out double h, out double s, out double v);

                if (v >= 0.2 && GoodS(s, v) && !saturatedColorDictionary.ContainsKey(colorNameLower))
                {
                    saturatedColorDictionary.Add(colorNameLower, colorHex);
                    saturatedColorNameList.Add(colorName);
                }
            }

            Console.WriteLine("Color Dictionary Built");
        }

        public string GetHex(string colorName)
        {
            colorDictionary.TryGetValue(colorName, out string hex);
            return hex;
        }

        public string GetRandomName()
        {
            int randomIndex = random.Next(0, colorNameList.Count);
            return colorNameList[randomIndex];
        }

        public string GetRandomSaturatedName()
        {
            int randomIndex = random.Next(0, saturatedColorNameList.Count);
            Console.WriteLine(saturatedColorNameList.Count);
            return saturatedColorNameList[randomIndex];
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

        private static bool GoodS(double saturation, double value)
        {
            value *= 100;
            saturation *= 100;

            
            return saturation >= (((100 - value) / 2.0) + 30.0);
        }
    }
}
