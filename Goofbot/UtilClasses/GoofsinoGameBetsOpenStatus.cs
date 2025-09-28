namespace Goofbot.UtilClasses;

using Microsoft.VisualStudio.Threading;
using System.Threading.Tasks;

internal class GoofsinoGameBetsOpenStatus
{
    private readonly AsyncReaderWriterLock betsOpenLock = new ();

    private bool betsOpenBackValue = true;

    public async Task<bool> GetBetsOpenAsync()
    {
        using (await this.betsOpenLock.ReadLockAsync())
        {
            return this.betsOpenBackValue;
        }
    }

    public async Task SetBetsOpenAsync(bool betsOpen)
    {
        using (await this.betsOpenLock.WriteLockAsync())
        {
            this.betsOpenBackValue = betsOpen;
        }
    }
}
