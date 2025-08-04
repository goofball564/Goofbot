using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using ImageMagick;

namespace Goofbot.Modules
{
    internal partial class BlueGuyModule
    {
        private const string BlueGuyGrayscaleFile = "Stuff\\BlueGuyGrayscale.png";
        private const string BlueGuyColorFile = "Stuff\\BlueGuyColor.png";
        private const string BlueGuyEyesFile = "Stuff\\BlueGuyEyes.png";

        private const string SpeedGuyColorFile = "Stuff\\SpeedGuyColor.png";

        private const string OutputFile = "R:\\temp.png";
        private const string OtherOutputFile = "R:\\temp-1.png";
        private const string ColorOutputFile = "R:\\color.png";

        private const string DefaultColorName = "BlueGuy";
        private const string SpeedGuy = "SpeedGuy";

        private readonly ColorDictionary _colorDictionary;
        private string _lastColorCode = "";

        public event EventHandler<EventArgs> ColorChange;
        public event EventHandler<string> UnknownColor;
        public event EventHandler<EventArgs> NoArgument;
        public event EventHandler<string> RandomColor;
        public event EventHandler SameColor;

        public BlueGuyModule()
        {
            _colorDictionary = new ColorDictionary(Program.ColorNamesFile);
        }

        protected virtual void OnColorChange()
        {
            ColorChange?.Invoke(this, new EventArgs());
        }

        protected virtual void OnUnknownColor(string colorName)
        {
            UnknownColor?.Invoke(this, colorName);
        }

        protected virtual void OnNoArgument()
        {
            NoArgument?.Invoke(this, new EventArgs());
        }

        protected virtual void OnRandomColor(string colorName)
        {
            RandomColor?.Invoke(this, colorName);
        }

        protected virtual void OnSameColor()
        {
            SameColor?.Invoke(this, EventArgs.Empty);
        }

        public void OnGuyCommand(object sender, string args)
        {
            if (IsColorHexCode(args))
            {
                args = args.ToLowerInvariant();
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
            else if (args.ToLower() == "random")
            {
                string colorName = _colorDictionary.GetRandomSaturatedName();
                string hexColorCode = _colorDictionary.GetHex(colorName.ToLowerInvariant());
                if (hexColorCode != null)
                {
                    hexColorCode = hexColorCode.ToLowerInvariant();
                    if (hexColorCode != _lastColorCode)
                    {
                        _lastColorCode = hexColorCode;
                        CreateBlueGuyImage(hexColorCode);
                        OnRandomColor(colorName);

                        string colorFileName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(colorName.ToLower()).Replace(" ", "") + "Guy.png";
                        try
                        {
                            File.Copy(OtherOutputFile, Path.Combine(Program.GuysFolder, colorFileName), false);
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
                    OnUnknownColor(args);
                }
            }
            else if (args == "")
            {
                OnNoArgument();
            }
            else
            {
                string hexColorCode = _colorDictionary.GetHex(args.ToLower());
                if (hexColorCode != null)
                {
                    hexColorCode = hexColorCode.ToLowerInvariant();
                    if (hexColorCode != _lastColorCode)
                    {
                        _lastColorCode = hexColorCode;
                        CreateBlueGuyImage(hexColorCode);
                        OnColorChange();

                        string colorFileName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(args.ToLower()).Replace(" ", "") + "Guy.png";
                        try
                        {
                            File.Copy(OtherOutputFile, Path.Combine(Program.GuysFolder, colorFileName), false);
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
                    OnUnknownColor(args);
                }
            }
        }

        private static void CreateBlueGuyImage(string hexColorCode)
        {
            using (var images = new MagickImageCollection())
            {
                var first = new MagickImage(BlueGuyGrayscaleFile);

                if (hexColorCode == SpeedGuy)
                {
                    var speedBackground = new MagickImage(SpeedGuyColorFile);

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
                

                var second = new MagickImage(BlueGuyEyesFile);

                images.Add(first);
                images.Add(second);
                images.Coalesce();

                images.Write(OutputFile, MagickFormat.Png);

                images.Dispose();
                first.Dispose();
                second.Dispose();
            }
        }

        private static void RestoreDefaultBlueGuy()
        {
            try
            {
                // true means it is allowed to overwrite the file
                File.Copy(BlueGuyColorFile, OtherOutputFile, true);
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
