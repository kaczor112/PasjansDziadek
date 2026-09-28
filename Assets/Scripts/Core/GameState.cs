using System;
using System.Collections.Generic;

namespace Pasjans.Core
{
    public enum Suit
    {
        Clubs = 0,
        Diamonds = 1,
        Hearts = 2,
        Spades = 3
    }

    public enum PileKind
    {
        Stock = 0,
        Waste = 1,
        Foundation = 2,
        Tableau = 3
    }

    /// <summary>Opisuje stos; talon i odrzucone karty zawsze mają indeks zero.</summary>
    [Serializable]
    public struct PileRef : IEquatable<PileRef>
    {
        public PileKind kind;
        public int index;

        public PileRef(PileKind kind, int index = 0)
        {
            this.kind = kind;
            this.index = index;
        }

        public bool Equals(PileRef other)
        {
            return kind == other.kind && index == other.index;
        }

        public override bool Equals(object obj)
        {
            return obj is PileRef && Equals((PileRef)obj);
        }

        public override int GetHashCode()
        {
            return ((int)kind * 397) ^ index;
        }
    }

    /// <summary>Ranga karty ma wartość od 1 (as) do 13 (król), a identyfikator zależy od koloru i rangi.</summary>
    [Serializable]
    public sealed class CardData
    {
        public int id;
        public Suit suit;
        public int rank;
        public bool faceUp;

        public bool IsRed { get { return suit == Suit.Diamonds || suit == Suit.Hearts; } }

        public CardData() { }

        public CardData(Suit suit, int rank, bool faceUp = false)
        {
            if ((int)suit < 0 || (int)suit > 3)
                throw new ArgumentOutOfRangeException(nameof(suit));
            if (rank < 1 || rank > 13)
                throw new ArgumentOutOfRangeException(nameof(rank));

            this.id = (int)suit * 13 + rank - 1;
            this.suit = suit;
            this.rank = rank;
            this.faceUp = faceUp;
        }

        public CardData Clone()
        {
            return new CardData { id = id, suit = suit, rank = rank, faceUp = faceUp };
        }
    }

    [Serializable]
    public sealed class PileData
    {
        public List<CardData> cards = new List<CardData>();
    }

    /// <summary>
    /// Dane zapisu zgodne z JSON. Listy są uporządkowane od spodu do wierzchu.
    /// Aktywny stan należy do gry; kopię pobiera się przez ExportState.
    /// Osobne obiekty stosów omijają ograniczenie JsonUtility dla zagnieżdżonych list.
    /// </summary>
    [Serializable]
    public sealed class GameState
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        // Pierwsze przejście dobiera 3 karty, drugie 2, a kolejne po 1.
        public int drawCount = 3;
        public int moveCount;
        public string gameId = Guid.NewGuid().ToString("N");
        // Wynik kończy się po odkryciu ostatniej zakrytej karty w kolumnach.
        // Dodatkowe pola zachowują zgodność ze starszymi zapisami.
        public long completedUtcTicks;
        public int finalMoves;
        public List<CardData> stock = new List<CardData>();
        public List<CardData> waste = new List<CardData>();
        public PileData[] foundations = CreatePiles(4);
        public PileData[] tableau = CreatePiles(7);

        private static PileData[] CreatePiles(int count)
        {
            var piles = new PileData[count];
            for (int i = 0; i < count; i++)
                piles[i] = new PileData();
            return piles;
        }
    }
}
