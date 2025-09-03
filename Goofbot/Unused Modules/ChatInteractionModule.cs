namespace Goofbot.Modules;

using TwitchLib.Client;

internal class ChatInteractionModule
{
    private readonly TwitchClient client;
    private readonly string channel;

    public ChatInteractionModule(TwitchClient client, string channel)
    {
        this.client = client;
        this.channel = channel;
    }
}
