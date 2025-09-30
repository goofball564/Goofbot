namespace Goofbot.UtilClasses.Cards
{
    internal class DeckOfPlayingCards(int remainingCardsToRequireReshuffle = 0)
        : ShoeOfPlayingCards(1, remainingCardsToRequireReshuffle)
    {
    }
}
