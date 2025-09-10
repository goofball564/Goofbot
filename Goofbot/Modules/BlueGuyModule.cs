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

    private const string DefaultColorCode = "BlueGuy";
    private const string SpeedGuyColorCode = "SpeedGuy";

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

    private string lastColorCode = string.Empty;

    public BlueGuyModule(Bot bot, string moduleDataFolder, CancellationToken cancellationToken)
        : base(bot, moduleDataFolder, cancellationToken)
    {
        this.blueGuyGrayscaleFile = Path.Join(this.moduleDataFolder, "BlueGuyGrayscale.png");
        this.blueGuyColorFile = Path.Join(this.moduleDataFolder, "BlueGuyColor.png");
        this.blueGuyEyesFile = Path.Join(this.moduleDataFolder, "BlueGuyEyes.png");
        this.speedGuyColorFile = Path.Join(this.moduleDataFolder, "SpeedGuyColor.png");

        this.guysFolder = Path.Join(this.moduleDataFolder, "Guys");
        Directory.CreateDirectory(this.guysFolder);

        this.bot.CommandDictionary.TryAddCommand(new Command("guy", this.GuyCommand));

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
            commandArgs = DefaultColorCode;
        }

        await this.semaphore.WaitAsync();
        try
        {
            if (IsHexColorCode(commandArgs) || commandArgs.Equals(DefaultColorCode) || commandArgs.Equals(SpeedGuyColorCode))
            {
                colorChanged = !commandArgs.Equals(this.lastColorCode, StringComparison.OrdinalIgnoreCase);
                message = colorChanged ? ColorChangeString : SameColorString;

                this.lastColorCode = commandArgs;
                this.SetBlueGuyImage(commandArgs);
            }
            else if (commandArgs.Equals(string.Empty))
            {
                colorChanged = false;
                message = NoArgumentString;
            }
            else if (commandArgs.Equals("random", StringComparison.OrdinalIgnoreCase))
            {
                string colorName = this.bot.ColorDictionary.GetRandomSaturatedName(out string hexColorCode);

                colorChanged = true;
                message = string.Format(RandomColorString, colorName);

                this.lastColorCode = hexColorCode;
                this.SetBlueGuyImage(hexColorCode);

                this.WriteCurrentBlueGuyImageToFile(colorName);
            }
            else
            {
                if (this.bot.ColorDictionary.TryGetHex(commandArgs, out string hexColorCode))
                {
                    colorChanged = !hexColorCode.Equals(this.lastColorCode, StringComparison.OrdinalIgnoreCase);
                    message = colorChanged ? ColorChangeString : SameColorString;

                    this.lastColorCode = hexColorCode;
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
        this.bot.TwitchClient.SendMessage(this.bot.TwitchChannelUsername, message);
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

    private void SetBlueGuyImage(string colorCode)
    {
        if (colorCode.Equals(DefaultColorCode))
        {
            this.RestoreDefaultBlueGuy();
        }
        else
        {
            using var images = new MagickImageCollection();
            using var grayscaleImage = new MagickImage(this.blueGuyGrayscaleFile);
            using var eyesImage = new MagickImage(this.blueGuyEyesFile);

            if (colorCode.Equals(SpeedGuyColorCode))
            {
                using var speedBackground = new MagickImage(this.speedGuyColorFile);
                using var croppedBackground = grayscaleImage.Clone();
                croppedBackground.Composite(speedBackground, CompositeOperator.Atop);
                grayscaleImage.Composite(croppedBackground, CompositeOperator.Overlay);
            }
            else
            {
                // colorCode is a hex color code
                using var solidColor = grayscaleImage.Clone();
                solidColor.Colorize(new MagickColor(colorCode), (Percentage)100.0);
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
