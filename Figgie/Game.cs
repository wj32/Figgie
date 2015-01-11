using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Figgie
{
    public enum Suit : int
    {
        Hearts = 0,
        Diamonds,
        Clubs,
        Spades
    }

    public class Suits
    {
        public static readonly IReadOnlyCollection<Suit> All =
            new Suit[] { Suit.Hearts, Suit.Diamonds, Suit.Clubs, Suit.Spades };

        public static Suit Opposite(Suit suit)
        {
            switch (suit)
            {
                case Suit.Hearts:
                    return Suit.Diamonds;
                case Suit.Diamonds:
                    return Suit.Hearts;
                case Suit.Clubs:
                    return Suit.Spades;
                case Suit.Spades:
                    return Suit.Clubs;
                default:
                    throw new ArgumentException("suit");
            }
        }
    }

    public interface IReadOnlyHand
    {
        int Count(Suit suit);
    }

    public class Hand : IReadOnlyHand
    {
        private int[] _counts = new int[Suits.All.Count];

        public Hand()
        { }

        public Hand(IEnumerable<Suit> cards)
        {
            foreach (var card in cards)
                _counts[(int)card]++;
        }

        private Hand(int[] counts)
        {
            Array.Copy(counts, _counts, _counts.Length);
        }

        public int Count(Suit suit)
        {
            return _counts[(int)suit];
        }

        public void Add(Suit suit, int n = 1)
        {
            _counts[(int)suit] += n;
        }

        public void Remove(Suit suit, int n = 1)
        {
            _counts[(int)suit] -= n;
        }

        public Hand Clone()
        {
            return new Hand(_counts);
        }
    }

    public interface IPlayer
    {
        void Initialize(IPlayerGame game);
        void OnBidAsk(int playerId, Suit suit, bool bid, int price);
        void OnOut(int playerId, Suit suit);
        void OnFill(int buyerId, int sellerId, bool buyerAtMarket, Suit suit, int price);
    }

    public interface IPlayerGame
    {
        int NumberOfPlayers { get; }
        int PlayerId { get; }
        IReadOnlyHand Hand { get; }
        IPlayerMarket Market(Suit suit);
    }

    public interface IPlayerMarket
    {
        int? BestBid { get; }
        int? BestAsk { get; }
        bool Bid(int price);
        bool Ask(int price);
        void Out();

        bool Buy();
        bool Sell();
    }

    public class Game
    {
        public class Player
        {
            public int Cash;
            public Hand Hand = new Hand();

            public IPlayer ExternalPlayer;
            public IPlayerGame PlayerGame;
        }

        private class Market
        {
            public Game Game;
            public Suit Suit;
            public Tuple<int, int> BestBid;
            public Tuple<int, int> BestAsk;

            public bool Bid(int price, int playerId)
            {
                if (Game._ended || price < 0)
                    return false;

                if (BestBid == null || price > BestBid.Item1)
                {
                    // Cross
                    if (BestAsk != null && price >= BestAsk.Item1)
                        return Buy(playerId);

                    BestBid = new Tuple<int, int>(price, playerId);
                    Game.BroadcastOnBidAsk(playerId, Suit, true, price);
                    return true;
                }

                return false;
            }

            public bool Ask(int price, int playerId)
            {
                if (Game._ended || price < 0)
                    return false;
                // No short selling allowed.
                if (Game._players[playerId].Hand.Count(Suit) <= 0)
                    return false;

                if (BestAsk == null || price < BestAsk.Item1)
                {
                    // Cross
                    if (BestBid != null && price <= BestBid.Item1)
                        return Sell(playerId);

                    BestAsk = new Tuple<int, int>(price, playerId);
                    Game.BroadcastOnBidAsk(playerId, Suit, false, price);
                    return true;
                }

                return false;
            }

            public void Out(int playerId)
            {
                if (Game._ended)
                    return;
                if (BestBid != null && BestBid.Item2 == playerId)
                    BestBid = null;
                if (BestAsk != null && BestAsk.Item2 == playerId)
                    BestAsk = null;
                Game.BroadcastOnOut(playerId, Suit);
            }

            private void Settle(int buyerId, int sellerId, bool buyerAtMarket, int price)
            {
                Game._players[buyerId].Cash -= price;
                Game._players[sellerId].Cash += price;
                Game._players[buyerId].Hand.Add(Suit);
                Game._players[sellerId].Hand.Remove(Suit);
                Game.BroadcastOnFill(buyerId, sellerId, buyerAtMarket, Suit, price);
            }

            public bool Buy(int buyerId)
            {
                if (Game._ended || BestAsk == null)
                    return false;

                int price = BestAsk.Item1;
                int sellerId = BestAsk.Item2;

                if (sellerId == buyerId)
                    return false;

                Settle(buyerId, sellerId, true, price);
                BestBid = null;
                BestAsk = null;

                return true;
            }

            public bool Sell(int sellerId)
            {
                if (Game._ended || BestBid == null)
                    return false;
                // No short selling allowed.
                if (Game._players[sellerId].Hand.Count(Suit) <= 0)
                    return false;

                int price = BestBid.Item1;
                int buyerId = BestBid.Item2;

                if (buyerId == sellerId)
                    return false;

                Settle(buyerId, sellerId, false, price);
                BestBid = null;
                BestAsk = null;

                return true;
            }
        }

        private class PlayerGame : IPlayerGame
        {
            private Game _game;
            private int _playerId;
            private PlayerMarket[] _markets;

            public PlayerGame(Game game, int playerId)
            {
                _game = game;
                _playerId = playerId;
                _markets = new PlayerMarket[Suits.All.Count];

                foreach (var suit in Suits.All)
                    _markets[(int)suit] = new PlayerMarket(game, playerId, suit);
            }

            public int NumberOfPlayers
            {
                get { return _game._players.Length; }
            }

            public int PlayerId
            {
                get { return _playerId; }
            }

            public IReadOnlyHand Hand
            {
                get { return _game._players[_playerId].Hand; }
            }

            public IPlayerMarket Market(Suit suit)
            {
                return _markets[(int)suit];
            }
        }

        private class PlayerMarket : IPlayerMarket
        {
            private Game _game;
            private int _playerId;
            private Suit _suit;
            private Market _market;

            public PlayerMarket(Game game, int playerId, Suit suit)
            {
                _game = game;
                _playerId = playerId;
                _suit = suit;
                _market = _game._markets[(int)_suit];
            }

            public int? BestBid
            {
                get
                {
                    var best = _market.BestBid;
                    if (best != null)
                        return best.Item1;
                    else
                        return null;
                }
            }

            public int? BestAsk
            {
                get
                {
                    var best = _market.BestAsk;
                    if (best != null)
                        return best.Item1;
                    else
                        return null;
                }
            }

            public bool Bid(int price)
            {
                return _market.Bid(price, _playerId);
            }

            public bool Ask(int price)
            {
                return _market.Ask(price, _playerId);
            }

            public void Out()
            {
                _market.Out(_playerId);
            }

            public bool Buy()
            {
                return _market.Buy(_playerId);
            }

            public bool Sell()
            {
                return _market.Sell(_playerId);
            }
        }

        private Random _random;
        private Player[] _players;
        private Market[] _markets;
        private Suit _goalSuit;
        private bool _ended;

        public Game(IPlayer[] players)
            : this(players, (new Random((int)DateTime.Now.ToFileTime())).Next())
        { }

        public Game(IPlayer[] players, int seed)
        {
            _random = new Random(seed);

            if (players.Length != 4 && players.Length != 5)
                throw new ArgumentException("Number of players must be 4 or 5.");

            int buyIn = 200 / players.Length;
            int handSize = 40 / players.Length;

            var suitSizes = new List<int> { 8, 10, 10, 12 };
            Shuffle(suitSizes);
            _goalSuit = Suits.Opposite((Suit)suitSizes.IndexOf(12));
            var cards =
                Enumerable.Repeat(Suit.Hearts, suitSizes[(int)Suit.Hearts])
                .Concat(Enumerable.Repeat(Suit.Diamonds, suitSizes[(int)Suit.Diamonds]))
                .Concat(Enumerable.Repeat(Suit.Clubs, suitSizes[(int)Suit.Clubs]))
                .Concat(Enumerable.Repeat(Suit.Spades, suitSizes[(int)Suit.Spades]))
                .ToList();
            Shuffle(cards);

            _markets = new Market[Suits.All.Count];
            foreach (var suit in Suits.All)
                _markets[(int)suit] = new Market { Game = this, Suit = suit };

            _players = new Player[players.Length];

            for (int i = 0; i < players.Length; i++)
            {
                _players[i] = new Player
                {
                    Cash = -buyIn,
                    Hand = new Hand(cards.Take(handSize)),
                    ExternalPlayer = players[i],
                    PlayerGame = new PlayerGame(this, i)
                };
                cards.RemoveRange(0, handSize);

                this.OnBidAsk += players[i].OnBidAsk;
                this.OnOut += players[i].OnOut;
                this.OnFill += players[i].OnFill;
            }

            if (cards.Count != 0)
                throw new InvalidOperationException("state");
        }

        public bool Ended
        {
            get { return _ended; }
        }

        public Player[] Players
        {
            get { return _players; }
        }

        public Suit GoalSuit
        {
            get { return _goalSuit; }
        }

        protected Random Random
        {
            get { return _random; }
        }

        private void Shuffle<T>(IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = _random.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public virtual void Start()
        {
            foreach (var player in _players)
                player.ExternalPlayer.Initialize(player.PlayerGame);
        }

        public virtual void End()
        {
            if (_ended)
                return;
            _ended = true;

            var playersByGoal = _players
                .Select(player => Tuple.Create(player, player.Hand.Count(_goalSuit)))
                .OrderByDescending(p => p.Item2)
                .ToList();
            int pot = 200;

            foreach (var p in playersByGoal)
            {
                int payoff = p.Item2 * 10;
                pot -= payoff;
                p.Item1.Cash += payoff;
            }

            var bestPlayer = playersByGoal.First();
            var bestPlayers = playersByGoal.TakeWhile(p => p.Item2 == bestPlayer.Item2).ToList();
            int bonus = pot / bestPlayers.Count;

            foreach (var p in bestPlayers)
            {
                pot -= bonus;
                p.Item1.Cash += bonus;
            }
        }

        public delegate void OnBidAskHandler(int playerId, Suit suit, bool bid, int price);
        public event OnBidAskHandler OnBidAsk;

        public delegate void OnOutHandler(int playerId, Suit suit);
        public event OnOutHandler OnOut;

        public delegate void OnFillHandler(int buyerId, int sellerId, bool buyerAtMarket, Suit suit, int price);
        public event OnFillHandler OnFill;

        private void BroadcastOnBidAsk(int playerId, Suit suit, bool bid, int price)
        {
            var handler = OnBidAsk;
            if (handler != null)
                handler(playerId, suit, bid, price);
        }

        private void BroadcastOnOut(int playerId, Suit suit)
        {
            var handler = OnOut;
            if (handler != null)
                handler(playerId, suit);
        }

        private void BroadcastOnFill(int buyerId, int sellerId, bool buyerAtMarket, Suit suit, int price)
        {
            var handler = OnFill;
            if (handler != null)
                handler(buyerId, sellerId, buyerAtMarket, suit, price);
        }
    }
}
