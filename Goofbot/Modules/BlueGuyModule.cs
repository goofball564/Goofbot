namespace Goofbot.Modules;

using Goofbot.Utils;
using ImageMagick;
using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using TwitchLib.Client.Events;
internal partial class BlueGuyModule : GoofbotModule
{
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

    private readonly string blueGuyGrayscaleFile;
    private readonly string blueGuyColorFile;
    private readonly string blueGuyEyesFile;
    private readonly string speedGuyColorFile;
    private readonly string guysFolder;

    private string lastColorCode = string.Empty;

    public BlueGuyModule(string moduleDataFolder, CommandDictionary commandDictionary)
        : base(moduleDataFolder)
    {
        this.blueGuyGrayscaleFile = Path.Combine(this.ModuleDataFolder, "BlueGuyGrayscale.png");
        this.blueGuyColorFile = Path.Combine(this.ModuleDataFolder, "BlueGuyColor.png");
        this.blueGuyEyesFile = Path.Combine(this.ModuleDataFolder, "BlueGuyEyes.png");
        this.speedGuyColorFile = Path.Combine(this.ModuleDataFolder, "SpeedGuyColor.png");

        this.guysFolder = Path.Combine(this.ModuleDataFolder, "Guys");
        Directory.CreateDirectory(this.guysFolder);

        var guyCommandLambda = async (object module, string commandArgs, OnChatCommandReceivedArgs eventArgs) => { return ((BlueGuyModule)module).GuyCommand(commandArgs); };
        commandDictionary.TryAddCommand(new Command("guy", this, guyCommandLambda, 1));
    }

    public string GuyCommand(string args)
    {
        args = args.ToLowerInvariant();
        string message;

        if (args != this.lastColorCode)
        {
            message = ColorChangeString;
        }
        else
        {
            message = SameColorString;
        }

        if (IsColorHexCode(args))
        {
            this.CreateBlueGuyImage(args);
            this.lastColorCode = args;
        }
        else if (args == "default" || args == DefaultColorName)
        {
            this.lastColorCode = DefaultColorName;
            this.RestoreDefaultBlueGuy();
        }
        else if (args == SpeedGuy)
        {
            this.lastColorCode = SpeedGuy;
            this.CreateBlueGuyImage(SpeedGuy);
        }
        else if (args == string.Empty)
        {
            message = NoArgumentString;
        }
        else if (args == "random")
        {
            string colorName = Program.ColorDictionary.GetRandomSaturatedName(out string hexColorCode);

            this.lastColorCode = hexColorCode;
            this.CreateBlueGuyImage(hexColorCode);

            string colorFileName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(colorName.ToLowerInvariant()).Replace(" ", string.Empty) + "Guy.png";
            try
            {
                File.Copy(OtherOutputFile, Path.Combine(this.guysFolder, colorFileName), false);
            }
            catch (IOException)
            {
            }

            message = string.Format(RandomColorString, colorName);
        }
        else
        {
            string hexColorCode = Program.ColorDictionary.GetHex(args);
            if (hexColorCode != null)
            {
                this.lastColorCode = hexColorCode;
                this.CreateBlueGuyImage(hexColorCode);

                string colorFileName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(args).Replace(" ", string.Empty) + "Guy.png";
                try
                {
                    File.Copy(OtherOutputFile, Path.Combine(this.guysFolder, colorFileName), false);
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

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColorCodeRegex();

    private static bool IsColorHexCode(string args)
    {
        Match match = HexColorCodeRegex().Match(args);
        return match.Success;
    }

    private void CreateBlueGuyImage(string hexColorCode)
    {
        using (var images = new MagickImageCollection())
        {
            var first = new MagickImage(this.blueGuyGrayscaleFile);

            if (hexColorCode == SpeedGuy)
            {
                var speedBackground = new MagickImage(this.speedGuyColorFile);

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

            var second = new MagickImage(this.blueGuyEyesFile);

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
            File.Copy(this.blueGuyColorFile, OtherOutputFile, true);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}
