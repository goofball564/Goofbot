namespace Goofbot.Structs;
internal readonly struct UserNameAndCount(string userName, long count)
{
    public readonly string UserName = userName;
    public readonly long Count = count;
}
