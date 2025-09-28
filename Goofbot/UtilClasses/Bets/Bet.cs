namespace Goofbot.UtilClasses;

internal class Bet(long typeID, long payoutRatio, string betName)
{
    public readonly long TypeID = typeID;
    public readonly long PayoutRatio = payoutRatio;
    public readonly string BetName = betName;
}
