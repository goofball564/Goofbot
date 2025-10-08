namespace Goofbot.UtilClasses.Cards;

using Goofbot.UtilClasses.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

internal class BlackjackHand : HandOfCards<PlayingCard>
{
    public static readonly Dictionary<PlayingCardRank, int> CardValues = new ()
    {
        { PlayingCardRank.Ace, 1 },
        { PlayingCardRank.Two, 2 },
        { PlayingCardRank.Three, 3 },
        { PlayingCardRank.Four, 4 },
        { PlayingCardRank.Five, 5 },
        { PlayingCardRank.Six, 6 },
        { PlayingCardRank.Seven, 7 },
        { PlayingCardRank.Eight, 8 },
        { PlayingCardRank.Nine, 9 },
        { PlayingCardRank.Ten, 10 },
        { PlayingCardRank.Jack, 10 },
        { PlayingCardRank.Queen, 10 },
        { PlayingCardRank.King, 10 },
    };

    public readonly string UserID;
    public readonly string UserName;
    public readonly BlackjackHandType Type;

    public BlackjackHand(string userID = Goofsino.TheHouseID, string userName = "The dealer", BlackjackHandType type = BlackjackHandType.Normal)
    {
        this.UserID = userID;
        this.UserName = userName;
        this.Type = type;
    }

    public override string ToString()
    {
        return string.Join(", ", this.cards.Select(c => Enum.GetName(c.Rank).ToLowerInvariant()));
    }

    public int GetValue(out bool soft)
    {
        soft = false;

        int value = 0;
        foreach (PlayingCard card in this.cards)
        {
            value += CardValues[card.Rank];
        }

        // there can only be one ace valued 11 in a hand (11 * 2 > 21)
        if (this.cards.Any(c => c.Rank == PlayingCardRank.Ace) && value + 10 <= 21)
        {
            value += 10;
            soft = true;
        }

        return value;
    }

    public bool HandIsTwoMatchingRanks()
    {
        return (this.Count == 2) && (this[0].Rank == this[1].Rank);
    }

    public bool HandHasTwoCards()
    {
        return this.Count == 2;
    }

    public bool HasBlackjack()
    {
        return this.GetValue(out bool _) == 21;
    }

    public bool HasBust()
    {
        return this.GetValue(out bool _) > 21;
    }
}
