using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Figgie
{
    public class SmartPlayer : IAsyncPlayer
    {
        public void Initialize(IAsyncPlayerGame game)
        {
            
        }

        public void OnBidAsk(int playerId, Suit suit, bool bid, int price)
        {

        }

        public void OnOut(int playerId, Suit suit)
        {
        
        }

        public void OnFill(int buyerId, int sellerId, bool buyerAtMarket, Suit suit, int price)
        {
        
        }
    }
}
