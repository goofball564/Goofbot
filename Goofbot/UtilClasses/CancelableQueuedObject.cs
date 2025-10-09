namespace Goofbot.UtilClasses;

internal class CancelableQueuedObject<T>(T value)
{
    public readonly T Value = value;

    public bool Canceled { get; private set; } = false;

    public void Cancel()
    {
        this.Canceled = true;
    }
}
