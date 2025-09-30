namespace Goofbot.UtilClasses.Bets;

internal class BaccaratBet(long typeID, double payoutRatio, string betName)
    : Bet(typeID, payoutRatio, betName)
{
}
