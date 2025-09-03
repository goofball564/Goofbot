namespace Goofbot.Utils;

using System;

internal class RunSplitEventArgs : EventArgs
{
    public int CurrentSplitIndex { get; set; }

    public int SegmentCount { get; set; }
}
