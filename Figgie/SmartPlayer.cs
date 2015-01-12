using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Figgie
{
    public class SmartPlayer : IAsyncPlayer
    {
        private Random _random;
        private IAsyncPlayerGame _game;
        private int _handSize;
        private double[] _suitGoalPr;
        private double[] _suitGoalPrConf;
        private DateTime _lastAct;

        private double InitialSuitGoalPr(int count)
        {
            if (_handSize == 8)
            {
                double upper = 0.29;
                double lower = 0.05;

                return lower + (upper - lower) * ((double)count / 8);
            }
            else if (_handSize == 10)
            {
                double upper = 0.30;
                double lower = 0.01;

                return lower + (upper - lower) * ((double)count / 10);
            }
            else
            {
                return 0.25;
            }
        }

        private int ToPrice(double value)
        {
            return (int)Math.Round(value);
        }

        private void Normalize()
        {
            for (int i = 0; i < _suitGoalPr.Length; i++)
                _suitGoalPr[i] = Math.Max(0, _suitGoalPr[i]);
            for (int i = 0; i < _suitGoalPrConf.Length; i++)
                _suitGoalPrConf[i] = Math.Min(Math.Max(0.1, _suitGoalPrConf[i]), 0.6);
            double sum = _suitGoalPr.Sum();
            for (int i = 0; i < _suitGoalPr.Length; i++)
                _suitGoalPr[i] /= sum;
        }

        public void Initialize(IAsyncPlayerGame game)
        {
            _random = new Random();
            _game = game;
            _lastAct = DateTime.FromFileTimeUtc(0);
            _handSize = 40 / game.NumberOfPlayers;

            _suitGoalPr = new double[Suits.All.Count];
            _suitGoalPrConf = new double[Suits.All.Count];
            foreach (var suit in Suits.All)
            {
                _suitGoalPr[(int)suit] = InitialSuitGoalPr(_game.Hand.Count(suit));
                _suitGoalPrConf[(int)suit] = 0.25;
            }
            Normalize();

            ActOnFair();
            PeriodicActOnFair();
        }

        private void PeriodicActOnFair()
        {
            Task.Delay(_random.Next(5000, 9000)).ContinueWith(t =>
                {
                    ActOnFair();
                    PeriodicActOnFair();
                });
        }

        private void ActOnFair()
        {
            DateTime now = DateTime.UtcNow;

            if ((now - _lastAct).TotalSeconds < 4)
                return;

            _lastAct = now;

            foreach (var suit in Suits.All)
            {
                double fairLow = (_suitGoalPr[(int)suit] - _suitGoalPrConf[(int)suit]) * 10;
                double fairHigh = (_suitGoalPr[(int)suit] + _suitGoalPrConf[(int)suit]) * 10;

                if (_suitGoalPr[(int)suit] > 0.05)
                {
                    // Adjust for bonus
                    double adj = (_suitGoalPr[(int)suit] - 0.05) * 12;
                    int count = _game.Hand.Count(suit);

                    if (count <= 3)
                        adj /= 1.5;
                    else if (count >= 6)
                        adj /= 2;
                    else if (count >= 7)
                        adj /= 3;

                    adj += 2;

                    fairLow += adj;
                    fairHigh += adj;
                }

                double fairMid = (fairLow + fairHigh) / 2;
                bool hit = false;
                bool lift = false;

                if (_game.Market(suit).BestBid.GetValueOrDefault(0) > fairMid)
                {
                    if (_random.Next(0, 6) == 0)
                        hit = true;
                }
                else if (_game.Market(suit).BestAsk.GetValueOrDefault(int.MaxValue) < fairMid)
                {
                    if (_random.Next(0, 6) == 0)
                        lift = true;
                }

                if (hit)
                {
                    _game.Market(suit).Sell();
                }
                else if (lift)
                {
                    _game.Market(suit).Buy();
                }
                else
                {
                    Task.Delay(_random.Next(500, 8000)).ContinueWith(t =>
                    {
                        int bid = ToPrice(fairLow);
                        int ask = ToPrice(fairHigh);

                        if (bid != 0)
                            _game.Market(suit).Bid(bid);
                        if (ask != 0)
                            _game.Market(suit).Ask(ask);
                    });
                }
            }
        }

        public void OnBidAsk(int playerId, Suit suit, bool bid, int price)
        {
            if (playerId == _game.PlayerId)
                return;

            double impliedPr = (double)price / 10;

            if (bid)
            {
                if (_suitGoalPr[(int)suit] < impliedPr)
                {
                    _suitGoalPr[(int)suit] += 0.03;
                    _suitGoalPrConf[(int)suit] += 0.03;
                }
                else
                {
                    _suitGoalPrConf[(int)suit] -= 0.03;
                }

                Normalize();
            }
            else
            {
                if (_suitGoalPr[(int)suit] > impliedPr)
                {
                    _suitGoalPr[(int)suit] -= 0.03;
                    _suitGoalPrConf[(int)suit] += 0.03;
                }
                else
                {
                    _suitGoalPrConf[(int)suit] -= 0.03;
                }

                Normalize();
            }

            ActOnFair();
        }

        public void OnOut(int playerId, Suit suit)
        {
            if (playerId == _game.PlayerId)
                return;

            ActOnFair();
        }

        public void OnFill(int buyerId, int sellerId, bool buyerAtMarket, Suit suit, int price)
        {
            if ((buyerId == _game.PlayerId && !buyerAtMarket) ||
                (sellerId == _game.PlayerId && buyerAtMarket))
            {
                // We got a fill, so adjust our fair.
                _suitGoalPr[(int)suit] += (buyerAtMarket ? -1 : 1) * 0.03;
                _suitGoalPrConf[(int)suit] += 0.01;
                Normalize();
                ActOnFair();
            }
        }
    }
}
