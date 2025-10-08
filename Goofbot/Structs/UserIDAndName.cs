namespace Goofbot.Structs;

internal readonly struct UserIDAndName(string userID, string userName)
{
    public readonly string UserID = userID;
    public readonly string UserName = userName;
}
