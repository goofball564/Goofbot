namespace Goofbot.UtilClasses.Cards;

using System.Collections;
using System.Collections.Generic;

internal class HandOfCards<T> : IEnumerable
    where T : Card
{
    protected readonly List<T> cards = [];

    public int Count
    {
        get { return this.cards.Count; }
    }

    public T this[int index]
    {
        get => this.cards[index];
        private set => this.cards[index] = value;
    }

    public IEnumerator GetEnumerator()
    {
        foreach (T card in this.cards)
        {
            yield return card;
        }
    }

    public void Add(T card)
    {
        this.cards.Add(card);
    }

    public void RemoveAt(int index)
    {
        this.cards.RemoveAt(index);
    }
}
