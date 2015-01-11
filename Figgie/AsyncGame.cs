using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Figgie
{
    public interface IAsyncPlayer
    {
        void Initialize(IAsyncPlayerGame game);
        void OnBidAsk(int playerId, Suit suit, bool bid, int price);
        void OnOut(int playerId, Suit suit);
        void OnFill(int buyerId, int sellerId, bool buyerAtMarket, Suit suit, int price);
    }

    public interface IAsyncPlayerGame
    {
        int NumberOfPlayers { get; }
        int PlayerId { get; }
        IReadOnlyHand Hand { get; }
        IAsyncPlayerMarket Market(Suit suit);
    }

    public interface IAsyncPlayerMarket
    {
        int? BestBid { get; }
        int? BestAsk { get; }
        Task<bool> Bid(int price);
        Task<bool> Ask(int price);
        Task Out();

        Task<bool> Buy();
        Task<bool> Sell();
    }

    public class AsyncGame : Game
    {
        public const int InformationMinDelay = 500;
        public const int InformationMaxDelay = 2000;
        public const int ExecutionMinDelay = 250;
        public const int ExecutionMaxDelay = 1000;

        private class AsyncPlayer : IPlayer
        {
            private IAsyncPlayer _player;

            public AsyncPlayer(IAsyncPlayer player)
            {
                _player = player;
            }

            public AsyncGame Game { get; set; }

            private Task DelayInformation()
            {
                return Task.Delay(Game.Random.Next(InformationMinDelay, InformationMaxDelay));
            }

            public void Initialize(IPlayerGame game)
            {
                AsyncPlayerGame asyncPlayerGame = new AsyncPlayerGame(Game, game);
                DelayInformation().ContinueWith(t => _player.Initialize(asyncPlayerGame));
            }

            public void OnBidAsk(int playerId, Suit suit, bool bid, int price)
            {
                DelayInformation().ContinueWith(t => _player.OnBidAsk(playerId, suit, bid, price));
            }

            public void OnOut(int playerId, Suit suit)
            {
                DelayInformation().ContinueWith(t => _player.OnOut(playerId, suit));
            }

            public void OnFill(int buyerId, int sellerId, bool buyerAtMarket, Suit suit, int price)
            {
                DelayInformation().ContinueWith(t => _player.OnFill(buyerId, sellerId, buyerAtMarket, suit, price));
            }
        }

        private class AsyncPlayerGame : IAsyncPlayerGame
        {
            private AsyncGame _game;
            private IPlayerGame _playerGame;
            private AsyncPlayerMarket[] _markets;

            public AsyncPlayerGame(AsyncGame game, IPlayerGame playerGame)
            {
                _game = game;
                _playerGame = playerGame;
                _markets = new AsyncPlayerMarket[Suits.All.Count];

                foreach (var suit in Suits.All)
                    _markets[(int)suit] = new AsyncPlayerMarket(game, playerGame.Market(suit));
            }

            public int NumberOfPlayers
            {
                get { return _playerGame.NumberOfPlayers; }
            }

            public int PlayerId
            {
                get { return _playerGame.PlayerId; }
            }

            public IReadOnlyHand Hand
            {
                get { return _playerGame.Hand; }
            }

            public IAsyncPlayerMarket Market(Suit suit)
            {
                return _markets[(int)suit];
            }
        }

        private class AsyncPlayerMarket : IAsyncPlayerMarket
        {
            private AsyncGame _game;
            private IPlayerMarket _market;

            public AsyncPlayerMarket(AsyncGame game, IPlayerMarket market)
            {
                _game = game;
                _market = market;
            }

            public int? BestBid
            {
                get { lock (_game.Lock) return _market.BestBid; }
            }

            public int? BestAsk
            {
                get { lock (_game.Lock) return _market.BestAsk; }
            }

            private Task DelayExecution()
            {
                return Task.Delay(_game.Random.Next(ExecutionMinDelay, ExecutionMaxDelay));
            }

            public Task<bool> Bid(int price)
            {
                return DelayExecution().ContinueWith(t => { lock (_game.Lock) return _market.Bid(price); });
            }

            public Task<bool> Ask(int price)
            {
                return DelayExecution().ContinueWith(t => { lock (_game.Lock) return _market.Ask(price); });
            }

            public Task Out()
            {
                return DelayExecution().ContinueWith(t => { lock (_game.Lock) _market.Out(); });
            }

            public Task<bool> Buy()
            {
                return DelayExecution().ContinueWith(t => { lock (_game.Lock) return _market.Buy(); });
            }

            public Task<bool> Sell()
            {
                return DelayExecution().ContinueWith(t => { lock (_game.Lock) return _market.Sell(); });
            }
        }

        private object _lock = new object();

        public AsyncGame(IAsyncPlayer[] players)
            : base(players.Select(player => new AsyncPlayer(player)).ToArray())
        { }

        public object Lock
        {
            get { return _lock; }
        }

        public override void Start()
        {
            foreach (var player in Players)
                ((AsyncPlayer)player.ExternalPlayer).Game = this;

            base.Start();
        }

        public override void End()
        {
            base.End();
        }
    }
}
