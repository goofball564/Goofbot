using ImageMagick;
using System;
using System.Drawing;
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

        private string _lastColorCode = "";

        public event EventHandler<EventArgs> ColorChange;
        public event EventHandler<string> UnknownColor;
        public event EventHandler<EventArgs> NoArgument;
        public event EventHandler<string> RandomColor;
        public event EventHandler SameColor;

        public BlueGuyModule(string moduleDataFolder, TwitchClient twitchClient, TwitchAPI twitchAPI) : base(moduleDataFolder, twitchClient, twitchAPI)
        {
            _blueGuyGrayscaleFile = Path.Combine(_moduleDataFolder, "BlueGuyGrayscale.png");
            _blueGuyColorFile = Path.Combine(_moduleDataFolder, "BlueGuyColor.png");
            _blueGuyEyesFile = Path.Combine(_moduleDataFolder, "BlueGuyEyes.png");
            _speedGuyColorFile = Path.Combine(_moduleDataFolder, "SpeedGuyColor.png");

            _guysFolder = Path.Combine(_moduleDataFolder, "Guys");
            Directory.CreateDirectory(_guysFolder);
        }

        protected virtual void OnColorChange()
        {
            _twitchClient.SendMessage(Program.TwitchChannelUsername, "Oooooh... pretty! OhISee");
        }

        protected virtual void OnUnknownColor()
        {
            _twitchClient.SendMessage(Program.TwitchChannelUsername, "I'm not familiar with this color birbAnalysis");
        }

        protected virtual void OnNoArgument()
        {
            _twitchClient.SendMessage(Program.TwitchChannelUsername, "To change the Guy's color, try \"!guy purple\", \"!guy random\", or \"!guy #ff0000\"");
        }

        protected virtual void OnRandomColor(string colorName)
        {
            _twitchClient.SendMessage(Program.TwitchChannelUsername, String.Format("Let's try {0} LilAnalysis", colorName));
        }

        protected virtual void OnSameColor()
        {
            _twitchClient.SendMessage(Program.TwitchChannelUsername, "The Guy is already that color Sussy");
        }

        protected override void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            string command = Program.ParseMessageForCommand(e, out string args);
            if (command.Equals("!guy"))
            {
                OnGuyCommand(this, args);
            }
        }

        public void OnGuyCommand(object sender, string args)
        {
            args = args.ToLowerInvariant();
            if (IsColorHexCode(args))
            {
                if (args != _lastColorCode)
                {
                    _lastColorCode = args;
                    CreateBlueGuyImage(args);
                    OnColorChange();
                }
                else
                {
                    OnSameColor();
                }
            }
            else if (args.ToLowerInvariant() == "default" || args == DefaultColorName)
            {
                if (_lastColorCode != DefaultColorName)
                {
                    _lastColorCode = DefaultColorName;
                    RestoreDefaultBlueGuy();
                    OnColorChange();
                }
                else
                {
                    OnSameColor();
                }

            }
            else if (args == SpeedGuy)
            {
                if (_lastColorCode != SpeedGuy)
                {
                    _lastColorCode = SpeedGuy;
                    CreateBlueGuyImage(SpeedGuy);
                    OnColorChange();
                }
                else
                {
                    OnSameColor();
                }

            }
            else if (args.ToLowerInvariant() == "random")
            {
                string colorName = Program.ColorDictionary.GetRandomSaturatedName(out string hexColorCode);
                if (hexColorCode != null)
                {
                    if (hexColorCode != _lastColorCode)
                    {
                        _lastColorCode = hexColorCode;
                        CreateBlueGuyImage(hexColorCode);
                        OnRandomColor(colorName);

                        string colorFileName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(colorName.ToLowerInvariant()).Replace(" ", "") + "Guy.png";
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
                        OnSameColor();
                    }
                }
                else
                {
                    OnUnknownColor();
                }
            }
            else if (args == "")
            {
                OnNoArgument();
            }
            else
            {
                string hexColorCode = Program.ColorDictionary.GetHex(args);
                if (hexColorCode != null)
                {
                    if (hexColorCode != _lastColorCode)
                    {
                        _lastColorCode = hexColorCode;
                        CreateBlueGuyImage(hexColorCode);
                        OnColorChange();

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
                        OnSameColor();
                    }

                }
                else
                {
                    OnUnknownColor();
                }
            }
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
