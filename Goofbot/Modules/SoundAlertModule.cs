namespace Goofbot.Modules;
using System.IO;
using System.Threading.Tasks;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using Goofbot.Utils;

internal class SoundAlertModule : GoofbotModule
{
    private readonly string soundAlertsCSVFile;
    private readonly SoundAlertDictionary soundAlertDictionary;

    public SoundAlertModule(string moduleDataFolder)
        : base(moduleDataFolder)
    {
        this.soundAlertsCSVFile = Path.Join(this.ModuleDataFolder, "SoundAlerts.csv");
        this.soundAlertDictionary = new SoundAlertDictionary(this.soundAlertsCSVFile);
        Program.EventSubWebsocketClient.ChannelPointsCustomRewardRedemptionAdd += this.OnChannelPointsCustomRewardRedemptionAdd;
    }

    private async Task OnChannelPointsCustomRewardRedemptionAdd(object sender, ChannelPointsCustomRewardRedemptionArgs e)
    {
        string reward = e.Notification.Payload.Event.Reward.Title.ToLowerInvariant();
        string sound = this.soundAlertDictionary.TryGetRandomFromList(reward);

        await Task.Delay(1000);
        new SoundPlayer(sound);
    }
}
