namespace Goofbot.UtilClasses.Cards;

using Goofbot.UtilClasses.Enums;
using System;

internal class PlayingCard(PlayingCardSuit suit, PlayingCardRank rank)
    : Card
{
    public readonly PlayingCardSuit Suit = suit;
    public readonly PlayingCardRank Rank = rank;

    public override string ToString()
    {
        return $"{Enum.GetName(this.Rank)} of {Enum.GetName(this.Suit)}";
    }

    public string RankString()
    {
        string rank = Enum.GetName(this.Rank).ToLowerInvariant();
        string n = Program.StartsWithVowel(rank) ? "n" : string.Empty;
        return $"a{n} {rank}";
    }
}
