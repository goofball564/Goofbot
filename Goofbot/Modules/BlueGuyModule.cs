using Goofbot.Utils;
using ImageMagick;
using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;

namespace Goofbot.Modules
{
    internal partial class BlueGuyModule : GoofbotModule
    {
        private readonly string _blueGuyGrayscaleFile;
        private readonly string _blueGuyColorFile;
        private readonly string _blueGuyEyesFile;
        private readonly string _speedGuyColorFile;
        private readonly string _guysFolder;

        private const string OutputFile = "R:\\temp.png";
        private const string OtherOutputFile = "R:\\temp-1.png";
        private const string ColorOutputFile = "R:\\color.png";

        private const string DefaultColorName = "BlueGuy";
        private const string SpeedGuy = "SpeedGuy";

        private const string ColorChangeString = "Oooooh... pretty! OhISee";
        private const string UnknownColorString = "I'm not familiar with this color birbAnalysis";
        private const string NoArgumentString = "To change the Guy's color, try \"!guy purple\", \"!guy random\", or \"!guy #ff0000\"";
        private const string RandomColorString = "Let's try {0} LilAnalysis";
        private const string SameColorString = "The Guy is already that color Sussy";

        private string _lastColorCode = "";

        public BlueGuyModule(string moduleDataFolder, CommandDictionary commandDictionary) : base(moduleDataFolder)
        {
            _blueGuyGrayscaleFile = Path.Combine(_moduleDataFolder, "BlueGuyGrayscale.png");
            _blueGuyColorFile = Path.Combine(_moduleDataFolder, "BlueGuyColor.png");
            _blueGuyEyesFile = Path.Combine(_moduleDataFolder, "BlueGuyEyes.png");
            _speedGuyColorFile = Path.Combine(_moduleDataFolder, "SpeedGuyColor.png");

            _guysFolder = Path.Combine(_moduleDataFolder, "Guys");
            Directory.CreateDirectory(_guysFolder);

            var guyCommandLambda = (object obj, string args) => { return ((BlueGuyModule)obj).GuyCommand(args); };
            commandDictionary.TryAddCommand(new Command("!guy", this, guyCommandLambda, 1));
        }

        public string GuyCommand(string args)
        {
            args = args.ToLowerInvariant();
            string message = "";

            if (args != _lastColorCode)
            {
                message = ColorChangeString;
            }
            else
            {
                message = SameColorString;
            }

            if (IsColorHexCode(args))
            {
                CreateBlueGuyImage(args);
                _lastColorCode = args;
            }
            else if (args == "default" || args == DefaultColorName)
            {
                _lastColorCode = DefaultColorName;
                RestoreDefaultBlueGuy();
            }
            else if (args == SpeedGuy)
            {
                _lastColorCode = SpeedGuy;
                CreateBlueGuyImage(SpeedGuy);
            }
            else if (args == "")
            {
                message = NoArgumentString;
            }
            else if (args == "random")
            {
                string colorName = Program.ColorDictionary.GetRandomSaturatedName(out string hexColorCode);

                _lastColorCode = hexColorCode;
                CreateBlueGuyImage(hexColorCode);

                string colorFileName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(colorName.ToLowerInvariant()).Replace(" ", "") + "Guy.png";
                try
                {
                    File.Copy(OtherOutputFile, Path.Combine(_guysFolder, colorFileName), false);
                }
                catch (IOException)
                {

                }

                message = String.Format(RandomColorString, colorName);
            }
            else
            {
                string hexColorCode = Program.ColorDictionary.GetHex(args);
                if (hexColorCode != null)
                {
                    _lastColorCode = hexColorCode;
                    CreateBlueGuyImage(hexColorCode);

                    string colorFileName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(args).Replace(" ", "") + "Guy.png";
                    try
                    {
                        File.Copy(OtherOutputFile, Path.Combine(_guysFolder, colorFileName), false);
                    }
                    catch (IOException)
                    {

                    }
                }
                else
                {
                    message = UnknownColorString;
                }
            }

            return message;
        }

        private void CreateBlueGuyImage(string hexColorCode)
        {
            using (var images = new MagickImageCollection())
            {
                var first = new MagickImage(_blueGuyGrayscaleFile);

                if (hexColorCode == SpeedGuy)
                {
                    var speedBackground = new MagickImage(_speedGuyColorFile);

                    var croppedFlag = first.Clone();
                    croppedFlag.Composite(speedBackground, CompositeOperator.Atop);

                    first.Composite(croppedFlag, CompositeOperator.Overlay);

                    croppedFlag.Dispose();
                    speedBackground.Dispose();
                }
                else
                {
                    var solidColor = first.Clone();
                    solidColor.Colorize(new MagickColor(hexColorCode), (Percentage)100.0);
                    first.Composite(solidColor, CompositeOperator.Overlay);
                    solidColor.Write(ColorOutputFile, MagickFormat.Png);
                    solidColor.Dispose();
                }

                var second = new MagickImage(_blueGuyEyesFile);

                images.Add(first);
                images.Add(second);
                images.Coalesce();

                images.Write(OutputFile, MagickFormat.Png);

                images.Dispose();
                first.Dispose();
                second.Dispose();
            }
        }

        private void RestoreDefaultBlueGuy()
        {
            try
            {
                // true means it is allowed to overwrite the file
                File.Copy(_blueGuyColorFile, OtherOutputFile, true);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
        private static partial Regex MyRegex();

        private static bool IsColorHexCode(string args)
        {
            Match match = MyRegex().Match(args);
            return match.Success;
        }

        
    }
}
