namespace Goofbot.UtilClasses;

using System;
using System.Collections.Generic;
using System.IO;

internal class SoundAlertDictionary
{
    private readonly Random random = new ();
    private readonly Dictionary<string, string[]> soundAlertDictionary = [];

    public SoundAlertDictionary(string soundAlertCSVFile)
    {
        foreach (string line in File.ReadLines(soundAlertCSVFile))
        {
            string[] csv = line.Split(",");

            if (csv[2].Contains('.'))
            {
                // If csv[2] contains . it is a file name.
                string redemption = csv[0];
                string sound = csv[2];
                sound = Path.Join(Path.GetDirectoryName(soundAlertCSVFile), sound);
                string[] sounds = [sound];
                this.soundAlertDictionary.TryAdd(redemption.ToLowerInvariant(), sounds);
            }
            else
            {
                // Otherwise, it is a folder name.
                string redemption = csv[0];
                string folder = csv[2];
                folder = Path.Join(Path.GetDirectoryName(soundAlertCSVFile), folder);
                string[] sounds = Directory.GetFiles(folder);
                this.soundAlertDictionary.TryAdd(redemption.ToLowerInvariant(), sounds);
            }
        }
    }

    public string TryGetRandomFromList(string key)
    {
        if (this.soundAlertDictionary.TryGetValue(key, out string[] sounds))
        {
            int randomIndex = this.random.Next(0, sounds.Length);
            return sounds[randomIndex];
        }
        else
        {
            return string.Empty;
        }
    }
}
