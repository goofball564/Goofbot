namespace Goofbot.UtilClasses.Bets;

internal class RouletteBet(long typeID, long payoutRatio, string betName)
    : Bet(typeID, payoutRatio, betName)
{
}
