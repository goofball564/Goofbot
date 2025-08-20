/*

        // react to game or livesplit
        // track stats?
        // FrankerZ and Deadlole
        // react to messages (on received)
        // read and write game memory
        // track internal state (count things?)
        // chat commands
        // pure text commands
        // arguments?
        // special programmed commands
        // BLUE GUY
        // chatter greetings
        // timers
        // cycle through list?
        // any configuration?
        // interface to easily modify commands?
        // 


        private async void CommandParsingModule_OnRefreshColorsCommand(object sender, string e)
        {
            await Program.ColorDictionary.Refresh(true);
        }

        *//*private void CommandParsingModule_OnQueueModeCommand(object sender, string args)
        {
            const string successResponse = "Aye Aye, Captain! FrankerZ 7";
            args = args.ToLowerInvariant();
            if (args == "on")
            {
                SpotifyModule.QueueMode = true;
                Client.SendMessage(Channel, successResponse);
            }
            else if (args == "off")
            {
                SpotifyModule.QueueMode = false;
                Client.SendMessage(Channel, successResponse);
            }
            else
            {
                Client.SendMessage(Channel, "?");
            }
        }*/

        /*private void CommandParsingModule_OnTimeoutNotElapsed(object sender, TimeSpan timeUntilTimeoutElapses)
        {
            _client.SendMessage(_channel, String.Format("Please wait {0} minutes and {1} seconds, then try again.", timeUntilTimeoutElapses.Minutes, timeUntilTimeoutElapses.Seconds));
        }*//*

        private void CommandParsingModule_OnNotBroadcaster(object sender, EventArgs e)
        {
            TwitchClient.SendMessage(_channel, "I don't answer to you, peasant! OhMyDog (this command is for Goof's use only)");
        }

        *//*private void PiperServerModule_OnRunStart(object sender, int runCount)
        {
            // Client.SendMessage(Channel, String.Format("Run {0}! dinkDonk Give it up for run {1}! dinkDonk", runCount, runCount));
            SpotifyModule.QueueMode = true;
        }*/

        /*private void PipeServerModule_OnRunReset(object sender, int runCount)
        {
            if (!SpotifyModule.FarmMode)
                SpotifyModule.QueueMode = false;
        }*/

        /*private void PipeServerModule_OnRunSplit(object sender, RunSplitEventArgs e)
        {
            if (e.CurrentSplitIndex < 5)
            {
                SpotifyModule.QueueMode = true;
            }
            else
            {
                SpotifyModule.QueueMode = false;
            }
        }*//*

        }
    }
}
*/