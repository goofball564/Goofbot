namespace Goofbot.Modules;

using Goofbot.Utils;
using ImageMagick;
using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
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

    private readonly SemaphoreSlim semaphore = new (1, 1);
    private readonly System.Timers.Timer timer = new (TimeSpan.FromMinutes(20));

    private string lastHexColorCode = string.Empty;

    public BlueGuyModule(string moduleDataFolder)
        : base(moduleDataFolder)
    {
        this.blueGuyGrayscaleFile = Path.Join(this.ModuleDataFolder, "BlueGuyGrayscale.png");
        this.blueGuyColorFile = Path.Join(this.ModuleDataFolder, "BlueGuyColor.png");
        this.blueGuyEyesFile = Path.Join(this.ModuleDataFolder, "BlueGuyEyes.png");
        this.speedGuyColorFile = Path.Join(this.ModuleDataFolder, "SpeedGuyColor.png");

        this.guysFolder = Path.Join(this.ModuleDataFolder, "Guys");
        Directory.CreateDirectory(this.guysFolder);

        Program.CommandDictionary.TryAddCommand(new Command("guy", this.GuyCommand));

        this.timer.AutoReset = true;
        this.timer.Elapsed += this.GuyTimerCallback;
    }

    public void StartTimer()
    {
        this.timer.Start();
    }

    public async Task<string> GuyCommand(string commandArgs, OnChatCommandReceivedArgs eventArgs = null, bool isReversed = false)
    {
        string message;
        bool colorChanged = false;

        if (commandArgs.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            commandArgs = DefaultColorName;
        }

        await this.semaphore.WaitAsync();
        try
        {
            if (IsHexColorCode(commandArgs) || commandArgs.Equals(DefaultColorName) || commandArgs.Equals(SpeedGuy))
            {
                colorChanged = !commandArgs.Equals(this.lastHexColorCode, StringComparison.OrdinalIgnoreCase);
                message = colorChanged ? ColorChangeString : SameColorString;

                this.lastHexColorCode = commandArgs;
                this.SetBlueGuyImage(commandArgs);
            }
            else if (commandArgs.Equals(string.Empty))
            {
                colorChanged = false;
                message = NoArgumentString;
            }
            else if (commandArgs.Equals("random"))
            {
                string colorName = Program.ColorDictionary.GetRandomSaturatedName(out string hexColorCode);

                colorChanged = true;
                message = string.Format(RandomColorString, colorName);

                this.lastHexColorCode = hexColorCode;
                this.SetBlueGuyImage(hexColorCode);

                this.WriteCurrentBlueGuyImageToFile(colorName);
            }
            else
            {
                if (Program.ColorDictionary.TryGetHex(commandArgs, out string hexColorCode))
                {
                    colorChanged = !hexColorCode.Equals(this.lastHexColorCode, StringComparison.OrdinalIgnoreCase);
                    message = colorChanged ? ColorChangeString : SameColorString;

                    this.lastHexColorCode = hexColorCode;
                    this.SetBlueGuyImage(hexColorCode);

                    this.WriteCurrentBlueGuyImageToFile(commandArgs);
                }
                else
                {
                    colorChanged = false;
                    message = UnknownColorString;
                }
            }

            if (colorChanged)
            {
                this.timer.Stop();
                this.timer.Start();
            }
        }
        finally
        {
            this.semaphore.Release();
        }

        return message;
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColorCodeRegex();

    private static bool IsHexColorCode(string args)
    {
        return HexColorCodeRegex().Match(args).Success;
    }

    private async void GuyTimerCallback(object source, ElapsedEventArgs e)
    {
        string message = await this.GuyCommand("random");
        Program.TwitchClient.SendMessage(Program.TwitchChannelUsername, message);
    }

    private void WriteCurrentBlueGuyImageToFile(string colorName)
    {
        string colorFileName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(colorName).Replace(" ", string.Empty) + "Guy.png";
        try
        {
            File.Copy(OtherOutputFile, Path.Join(this.guysFolder, colorFileName), false);
        }
        catch (IOException e)
        {
            Console.WriteLine(e.ToString());
        }
    }

    private void SetBlueGuyImage(string hexColorCode)
    {
        if (hexColorCode.Equals(DefaultColorName))
        {
            this.RestoreDefaultBlueGuy();
        }
        else
        {
            using var images = new MagickImageCollection();
            using var grayscaleImage = new MagickImage(this.blueGuyGrayscaleFile);
            using var eyesImage = new MagickImage(this.blueGuyEyesFile);

            if (hexColorCode.Equals(SpeedGuy))
            {
                using var speedBackground = new MagickImage(this.speedGuyColorFile);
                using var croppedBackground = grayscaleImage.Clone();
                croppedBackground.Composite(speedBackground, CompositeOperator.Atop);
                grayscaleImage.Composite(croppedBackground, CompositeOperator.Overlay);
            }
            else
            {
                using var solidColor = grayscaleImage.Clone();
                solidColor.Colorize(new MagickColor(hexColorCode), (Percentage)100.0);
                grayscaleImage.Composite(solidColor, CompositeOperator.Overlay);
                solidColor.Write(ColorOutputFile, MagickFormat.Png);
            }

            images.Add(grayscaleImage);
            images.Add(eyesImage);
            images.Coalesce();

            images.Write(OutputFile, MagickFormat.Png);
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
