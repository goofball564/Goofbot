namespace Goofbot.Modules;

using Goofbot.UtilClasses;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TwitchLib.Client.Events;
using Goofbot.UtilClasses.Cards;
using static Goofbot.UtilClasses.ColorDictionary;
using static Goofbot.UtilClasses.Cards.DeckOfTarotCards;

internal partial class RandomModule : GoofbotModule
{
    private readonly List<string> listOfDays = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];
    private readonly List<string> listOfMonths = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];

    private readonly DeckOfPlayingCards cards = new (0);
    private readonly DeckOfTarotCards tarotCards = new ();

    public RandomModule(Bot bot, string moduleDataFolder)
        : base(bot, moduleDataFolder)
    {
        this.bot.CommandDictionary.TryAddCommand(new Command("flip", this.FlipCommand));
        this.bot.CommandDictionary.TryAddCommand(new Command("roll", this.RollCommand));
        this.bot.CommandDictionary.TryAddCommand(new Command("random", this.RandomCommand));
    }

    [GeneratedRegex("^[d|D][1-9]{1}[0-9]{0,}$")]
    private static partial Regex DiceRegex();

    private async Task RandomCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        string displayName = eventArgs.Command.ChatMessage.DisplayName;

        int index;
        switch (commandArgs.ToLowerInvariant())
        {
            case "card":
                index = RandomNumberGenerator.GetInt32(this.cards.Count);
                Card card = this.cards.Peek(index);
                this.bot.SendMessage($"{card} @{displayName}", isReversed);
                break;
            case "tarot":
                index = RandomNumberGenerator.GetInt32(this.tarotCards.Count);
                TarotCard tarotCard = this.tarotCards.Peek(index);
                this.bot.SendMessage($"{tarotCard} @{displayName}", isReversed);
                break;
            case "tarot card":
                goto case "tarot";
            case "day":
                index = RandomNumberGenerator.GetInt32(this.listOfDays.Count);
                this.bot.SendMessage($"{this.listOfDays[index]} @{displayName}", isReversed);
                break;
            case "month":
                index = RandomNumberGenerator.GetInt32(this.listOfMonths.Count);
                this.bot.SendMessage($"{this.listOfMonths[index]} @{displayName}", isReversed);
                break;
            case "color":
                ColorNameAndHexColorCode color = await this.bot.ColorDictionary.GetRandomColorAsync();
                this.bot.SendMessage($"{color.ColorName} - {color.HexColorCode} @{displayName}", isReversed);
                break;
            case "colour":
                goto case "color";
            default:
                break;
        }
    }

    private async Task FlipCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        string displayName = eventArgs.Command.ChatMessage.DisplayName;
        switch (commandArgs.ToLowerInvariant())
        {
            case "coin":
                string result = RandomNumberGenerator.GetInt32(2) == 1 ? "Heads" : "Tails";
                this.bot.SendMessage($"{result} @{displayName}", isReversed);
                break;
            case "table":
                this.bot.SendMessage("(╯°□°)╯︵ ┻━┻", isReversed);
                break;
            default:
                this.bot.SendMessage("🤸", isReversed);
                break;
        }
    }

    private async Task RollCommand(string commandArgs, bool isReversed, OnChatCommandReceivedArgs eventArgs)
    {
        int roll;
        string displayName = eventArgs.Command.ChatMessage.DisplayName;
        switch (commandArgs.ToLowerInvariant())
        {
            case string dieType when DiceRegex().IsMatch(dieType):
                try
                {
                    int dieFaces = Convert.ToInt32(dieType.Substring(1));
                    roll = RandomNumberGenerator.GetInt32(dieFaces) + 1;
                    this.bot.SendMessage($"You rolled {roll} @{displayName}", isReversed);
                }
                catch (OverflowException)
                {
                    this.bot.SendMessage("This die has too many faces for poor Goofbot to comprehend :(", isReversed);
                }

                break;
            case "die":
                roll = RandomNumberGenerator.GetInt32(6) + 1;
                this.bot.SendMessage($"You rolled {roll} @{displayName}", isReversed);
                break;
            case "dice":
                int roll1 = RandomNumberGenerator.GetInt32(6) + 1;
                int roll2 = RandomNumberGenerator.GetInt32(6) + 1;
                this.bot.SendMessage($"You rolled {roll1} and {roll2} @{displayName}", isReversed);
                break;
            case "joint":
                this.bot.SendMessage("widepeepoHigh", isReversed);
                break;
            case "blunt":
                goto case "joint";
            default:
                this.bot.SendMessage("BIGCAT", isReversed);
                break;
        }
    }
}
