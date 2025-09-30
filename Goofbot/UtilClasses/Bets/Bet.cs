namespace Goofbot.UtilClasses.Bets;

internal class Bet(long typeID, double payoutRatio, string betName)
{
    public readonly long TypeID = typeID;
    public readonly double PayoutRatio = payoutRatio;
    public readonly string BetName = betName;
}
