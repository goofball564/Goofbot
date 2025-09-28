namespace Goofbot.UtilClasses.Cards;

using System;
using System.Collections.Generic;

internal class DeckOfTarotCards
{
    private readonly List<string> trumps = ["The Fool", "I. The Magician", "II. The High Priestess", "III. The Empress",
        "IV. The Emperor", "V. The Hierophant", "VI. The Lovers", "VII. The Chariot", "VIII. Strength",
        "IX. The Hermit", "X. Wheel of Fortune", "XI. Justice", "XII. The Hanged Man", "XIII. Death",
        "XIV. Temperance", "XV. The Devil", "XVI. The Tower", "XVII. The Star", "XVIII. The Moon",
        "XIX. The Sun", "XX. Judgement", "XXI. The World"];

    private readonly List<TarotCard> cards = [];

    public DeckOfTarotCards()
    {
        foreach (TarotSuit suit in Enum.GetValues(typeof(TarotSuit)))
        {
            foreach (TarotRank rank in Enum.GetValues(typeof(TarotRank)))
            {
                cards.Add(new TarotSuitCard(suit, rank));
            }
        }

        foreach (string trump in trumps)
        {
            cards.Add(new TarotTrumpCard(trump));
        }
    }

    public enum TarotSuit
    {
        Wands,
        Cups,
        Swords,
        Pentacles,
    }

    public enum TarotRank
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
        Page,
        Knight,
        King,
        Queen,
    }

    public int Count
    {
        get
        {
            return cards.Count;
        }
    }

    public TarotCard Peek(int index)
    {
        return cards[index];
    }

    public abstract class TarotCard
    {
        public abstract override string ToString();
    }

    public class TarotSuitCard : TarotCard
    {
        public readonly TarotSuit Suit;
        public readonly TarotRank Rank;

        public TarotSuitCard(TarotSuit suit, TarotRank rank)
        {
            Suit = suit;
            Rank = rank;
        }

        public override string ToString()
        {
            return $"{Enum.GetName(Rank)} of {Enum.GetName(Suit)}";
        }
    }

    public class TarotTrumpCard : TarotCard
    {
        public string Trump;

        public TarotTrumpCard(string trump)
        {
            Trump = trump;
        }

        public override string ToString()
        {
            return Trump;
        }
    }
}
