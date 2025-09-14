namespace Goofbot.Modules;

using Goofbot.UtilClasses;
using System.IO;
using System.Threading.Tasks;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;

internal class SoundAlertModule : GoofbotModule
{
    private readonly string soundAlertsCSVFile;
    private readonly SoundAlertDictionary soundAlertDictionary;

    public SoundAlertModule(Bot bot, string moduleDataFolder)
        : base(bot, moduleDataFolder)
    {
        this.soundAlertsCSVFile = Path.Join(this.moduleDataFolder, "SoundAlerts.csv");
        this.soundAlertDictionary = new SoundAlertDictionary(this.soundAlertsCSVFile);
        this.bot.EventSubWebsocketClient.ChannelPointsCustomRewardRedemptionAdd += this.OnChannelPointsCustomRewardRedemptionAdd;
    }

    private async Task OnChannelPointsCustomRewardRedemptionAdd(object sender, ChannelPointsCustomRewardRedemptionArgs e)
    {
        string reward = e.Notification.Payload.Event.Reward.Title.ToLowerInvariant();
        string sound = this.soundAlertDictionary.TryGetRandomFromList(reward);

        await Task.Delay(1000);
        new SoundPlayer(sound);
    }
}
