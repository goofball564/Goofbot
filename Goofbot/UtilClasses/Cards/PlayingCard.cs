namespace Goofbot.UtilClasses.Cards;

using System;
using static Goofbot.UtilClasses.Cards.PlayingCard;

internal class PlayingCard(PlayingCardSuit suit, PlayingCardRank rank)
    : Card
{
    public readonly PlayingCardSuit Suit = suit;
    public readonly PlayingCardRank Rank = rank;

    public enum PlayingCardSuit
    {
        Spades,
        Hearts,
        Diamonds,
        Clubs,
    }

    public enum PlayingCardRank
    {
        Ace,
        Two,
        Three,
        Four,
        Five,
        Six,
        Seven,
        Eight,
        Nine,
        Ten,
        Jack,
        Queen,
        King,
    }

    public override string ToString()
    {
        return $"{Enum.GetName(Rank)} of {Enum.GetName(Suit)}";
    }
}
