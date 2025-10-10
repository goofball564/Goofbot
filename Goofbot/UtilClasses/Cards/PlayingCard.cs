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

    public string ToShortformString()
    {
        string rankShortform;
        if (this.Rank >= PlayingCardRank.Two && this.Rank <= PlayingCardRank.Ten)
        {
            rankShortform = ((int)this.Rank).ToString();
        }
        else
        {
            rankShortform = Enum.GetName(this.Rank).Substring(0, 1);
        }

        string suitShortform = Program.GetSuitEmoji(this.Suit);

        return rankShortform + suitShortform;
    }
}
