namespace Goofbot.UtilClasses;

internal class RouletteBet(long typeID, long payoutRatio, string betName)
    : Bet(typeID, payoutRatio, betName)
{
}
