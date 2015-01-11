using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Figgie
{
    public partial class MainForm : Form
    {
        public const int GameDurationInSeconds = 60 * 4;

        private class UserPlayer : IAsyncPlayer
        {
            private MainForm _form;

            public UserPlayer(MainForm form)
            {
                _form = form;
            }

            public IAsyncPlayerGame Game { get; private set; }

            public void Initialize(IAsyncPlayerGame game)
            {
                Game = game;
                _form.BeginInvoke(new Action(() => _form.buttonExecute.Enabled = true));
            }

            public void OnBidAsk(int playerId, Suit suit, bool bid, int price)
            { }

            public void OnOut(int playerId, Suit suit)
            { }

            public void OnFill(int buyerId, int sellerId, bool buyerAtMarket, Suit suit, int price)
            { }
        }

        private AsyncGame _game;
        private UserPlayer _userPlayer;
        private int _secondsElapsed;

        public MainForm()
        {
            InitializeComponent();
            labelHeader.Text = "";
            buttonExecute.Enabled = false;
        }

        private string GetPlayerName(int playerId)
        {
            switch (playerId)
            {
                case 0: return "Avocado";
                case 1: return "Banana";
                case 2: return "Coconut";
                case 3: return "Durian";
                case 4: return "Grape";
                default: throw new ArgumentException("playerId");
            }
        }

        private void WriteLine(string line = "")
        {
            textMessages.AppendText(line + "\n");
        }

        private string HandToString(IReadOnlyHand hand)
        {
            return string.Format("{0}H, {1}D, {2}C, {3}S",
                hand.Count(Suit.Hearts), hand.Count(Suit.Diamonds),
                hand.Count(Suit.Clubs), hand.Count(Suit.Spades));
        }

        private void UpdateHand()
        {
            textHand.Text = HandToString(_game.Players[4].Hand);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            _userPlayer = new UserPlayer(this);
            var players = new IAsyncPlayer[]
            {
                new SmartPlayer(),
                new SmartPlayer(),
                new SmartPlayer(),
                new SmartPlayer(),
                _userPlayer
            };

            _game = new AsyncGame(players);

            timerGame.Interval = 1000;
            timerGame.Start();
            _game.Start();

            textCommand.SelectAll();
            textCommand.Select();
            UpdateHand();

            _game.OnBidAsk += game_OnBidAsk;
            _game.OnOut += game_OnOut;
            _game.OnFill += game_OnFill;
        }

        private void game_OnBidAsk(int playerId, Suit suit, bool bid, int price)
        {
            this.BeginInvoke(new Action(() =>
                {
                    string message =
                        bid
                        ? suit.ToString() + " " + price.ToString() + " BID"
                        : suit.ToString() + " AT " + price.ToString();
                    WriteLine(GetPlayerName(playerId) + "\t \"" + message + "\"");
                }));
        }

        private void game_OnOut(int playerId, Suit suit)
        {
            this.BeginInvoke(new Action(() =>
            {
                string message = suit.ToString() + " I'M OUT";
                WriteLine(GetPlayerName(playerId) + "\t \"" + message + "\"");
            }));
        }

        private void game_OnFill(int buyerId, int sellerId, bool buyerAtMarket, Suit suit, int price)
        {
            this.BeginInvoke(new Action(() =>
            {
                int sourceId = buyerAtMarket ? buyerId : sellerId;
                int targetId = buyerAtMarket ? sellerId : buyerId;
                string message = suit.ToString() + " " + (buyerAtMarket ? "TAKE'EM" : "SOLD");
                WriteLine(GetPlayerName(sourceId) + "\t \"" + message + "\"");
                string fillMessage =
                    buyerAtMarket
                    ? " paid " + price.ToString() + " for " + suit.ToString()[0] + " from " + GetPlayerName(targetId)
                    : " sold " + suit.ToString()[0] + " at " + price.ToString() + " to " + GetPlayerName(targetId);
                WriteLine("\t" + GetPlayerName(sourceId) + " " + fillMessage);

                UpdateHand();
            }));
        }

        private void timerGame_Tick(object sender, EventArgs e)
        {
            if (++_secondsElapsed == GameDurationInSeconds)
            {
                timerGame.Stop();
                WriteLine("\nGAME OVER.");
                WriteLine("The goal suit is " + _game.GoalSuit.ToString() + "!");

                lock (_game.Lock)
                {
                    int pot = 200;

                    foreach (var player in _game.Players)
                    {
                        int numberOfGoal = player.Hand.Count(_game.GoalSuit);
                        WriteLine(GetPlayerName(player.PlayerGame.PlayerId) + "\t has " +
                            numberOfGoal.ToString() + " " + _game.GoalSuit.ToString() + " and receives " +
                            (numberOfGoal * 10).ToString() + " cash");
                        pot -= numberOfGoal * 10;
                    }

                    var playersByGoal = _game.Players
                        .OrderByDescending(player => player.Hand.Count(_game.GoalSuit))
                        .ToList();
                    var bestPlayers = playersByGoal
                        .TakeWhile(player => player.Hand.Count(_game.GoalSuit) == playersByGoal.First().Hand.Count(_game.GoalSuit))
                        .ToList();

                    foreach (var player in bestPlayers)
                    {
                        WriteLine(GetPlayerName(player.PlayerGame.PlayerId) + "\t receives a bonus of " +
                            (pot / bestPlayers.Count).ToString() + "!");
                    }

                    _game.End();
                    WriteLine();
                }

                foreach (var player in _game.Players)
                {
                    WriteLine(GetPlayerName(player.PlayerGame.PlayerId) + "\t has " +
                        player.Cash.ToString() + " cash and their hand is");
                    WriteLine("\t\t\t" + HandToString(player.Hand));
                }
            }
            else
            {
                labelHeader.Text = "You are " + GetPlayerName(4) + ". Time remaining: " +
                    (new TimeSpan(0, 0, GameDurationInSeconds - _secondsElapsed)).ToString();
            }
        }

        private void buttonExecute_Click(object sender, EventArgs e)
        {
            if (_userPlayer.Game != null)
            {
                string command = textCommand.Text.Trim().ToUpperInvariant();
                textCommand.Text = "";
                textCommand.SelectAll();
                textCommand.Select();

                try
                {
                    char suitChar = command[0];
                    Suit suit;

                    switch (suitChar)
                    {
                        case 'H': suit = Suit.Hearts; break;
                        case 'D': suit = Suit.Diamonds; break;
                        case 'C': suit = Suit.Clubs; break;
                        case 'S': suit = Suit.Spades; break;
                        default: throw new ArgumentException("suitChar");
                    }

                    command = command.Substring(2);

                    if (command.StartsWith("AT ") || command.StartsWith("A "))
                    {
                        int price = int.Parse(command.Split(' ')[1]);
                        _userPlayer.Game.Market(suit).Ask(price);
                    }
                    else if (command.EndsWith(" BID") || command.EndsWith(" B") || command.EndsWith(" BI"))
                    {
                        int price = int.Parse(command.Split(' ')[0]);
                        _userPlayer.Game.Market(suit).Bid(price);
                    }
                    else if (command == "OUT" || command == "O" || command == "OU")
                    {
                        _userPlayer.Game.Market(suit).Out();
                    }
                    else if (command == "BUY" || command == "TAKE" || command == "T" || command == "TA" || command == "TAK")
                    {
                        _userPlayer.Game.Market(suit).Buy();
                    }
                    else if (command == "SELL" || command == "SOLD" || command == "S" || command == "SO" || command == "SOL")
                    {
                        _userPlayer.Game.Market(suit).Sell();
                    }
                    else
                    {
                        throw new ArgumentException("command");
                    }
                }
                catch
                {
                    WriteLine("Invalid command");
                }
            }
        }
    }
}
