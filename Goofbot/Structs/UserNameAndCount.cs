namespace Goofbot.Structs;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

internal readonly struct UserNameAndCount(string userName, long count)
{
    public readonly string UserName = userName;
    public readonly long Count = count;
}
