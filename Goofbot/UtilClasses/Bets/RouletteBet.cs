namespace Goofbot.UtilClasses.Bets;

internal class RouletteBet(long typeID, double payoutRatio, string betName)
    : Bet(typeID, payoutRatio, betName)
{
}
