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
        this.blueGuyGrayscaleFile = Path.Combine(this.ModuleDataFolder, "BlueGuyGrayscale.png");
        this.blueGuyColorFile = Path.Combine(this.ModuleDataFolder, "BlueGuyColor.png");
        this.blueGuyEyesFile = Path.Combine(this.ModuleDataFolder, "BlueGuyEyes.png");
        this.speedGuyColorFile = Path.Combine(this.ModuleDataFolder, "SpeedGuyColor.png");

        this.guysFolder = Path.Combine(this.ModuleDataFolder, "Guys");
        Directory.CreateDirectory(this.guysFolder);

        Program.CommandDictionary.TryAddCommand(new Command("guy", this.GuyCommand, 1));

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
        commandArgs = commandArgs.Trim().ToLowerInvariant();
        await this.semaphore.WaitAsync();
        try
        {
            if (IsHexColorCode(commandArgs))
            {
                colorChanged = !commandArgs.Equals(this.lastHexColorCode);
                message = colorChanged ? ColorChangeString : SameColorString;

                this.CreateBlueGuyImage(commandArgs);
                this.lastHexColorCode = commandArgs;
            }
            else if (commandArgs.Equals("default") || commandArgs.Equals(DefaultColorName))
            {
                colorChanged = !DefaultColorName.Equals(this.lastHexColorCode);
                message = colorChanged ? ColorChangeString : SameColorString;

                this.lastHexColorCode = DefaultColorName;
                this.RestoreDefaultBlueGuy();
            }
            else if (commandArgs.Equals(SpeedGuy))
            {
                colorChanged = !SpeedGuy.Equals(this.lastHexColorCode);
                message = colorChanged ? ColorChangeString : SameColorString;

                this.lastHexColorCode = SpeedGuy;
                this.CreateBlueGuyImage(SpeedGuy);
            }
            else if (commandArgs.Equals(string.Empty))
            {
                colorChanged = false;
                message = NoArgumentString;
            }
            else if (commandArgs.Equals("random"))
            {
                colorChanged = true;
                string colorName = Program.ColorDictionary.GetRandomSaturatedName(out string hexColorCode);

                this.lastHexColorCode = hexColorCode;
                this.CreateBlueGuyImage(hexColorCode);

                string colorFile = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(colorName.ToLowerInvariant()).Replace(" ", string.Empty) + "Guy.png";
                try
                {
                    File.Copy(OtherOutputFile, Path.Combine(this.guysFolder, colorFile), false);
                }
                catch (IOException e)
                {
                    Console.WriteLine(e.ToString());
                }

                message = string.Format(RandomColorString, colorName);
            }
            else
            {
                if (Program.ColorDictionary.TryGetHex(commandArgs, out string hexColorCode))
                {
                    colorChanged = !hexColorCode.Equals(this.lastHexColorCode);
                    message = colorChanged ? ColorChangeString : SameColorString;
                    this.lastHexColorCode = hexColorCode;
                    this.CreateBlueGuyImage(hexColorCode);

                    string colorFileName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(commandArgs).Replace(" ", string.Empty) + "Guy.png";
                    try
                    {
                        File.Copy(OtherOutputFile, Path.Combine(this.guysFolder, colorFileName), false);
                    }
                    catch (IOException e)
                    {
                        Console.WriteLine(e.ToString());
                    }
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
