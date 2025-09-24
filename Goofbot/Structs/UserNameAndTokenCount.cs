namespace Goofbot.Structs;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

internal readonly struct UserNameAndTokenCount(string userName, long tokenCount)
{
    public readonly string UserName = userName;
    public readonly long TokenCount = tokenCount;
}
