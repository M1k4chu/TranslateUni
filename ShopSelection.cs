namespace TranslateUni;
public sealed partial class Run
{
    public bool CanBuy(int index)=>Phase==Phase.Shop&&index>=0&&index<ShopOffers.Count
        &&ShopOffers[index].Tier==ShopTier(ShopOffers[index].Id)+1&&ShopOffers[index].Tier<=MaxShopTier&&Cash>=ShopOffers[index].Cost;
    public int[] BuyableShopItems(IEnumerable<int>? options=null)=>(options??Enumerable.Range(0,ShopOffers.Count)).Where(CanBuy).Distinct().Order().ToArray();
}
public static class ShopSelection
{
    public static int Normalize(int current,int[] choices)=>choices.Contains(current)?current:choices.FirstOrDefault(-1);
    public static int Move(int current,ConsoleKey key,int[] choices,bool oneRow=false)
    {
        current=Normalize(current,choices);if(current<0)return -1;
        int direction=key is ConsoleKey.A or ConsoleKey.LeftArrow or ConsoleKey.W or ConsoleKey.UpArrow?-1:key is ConsoleKey.D or ConsoleKey.RightArrow or ConsoleKey.S or ConsoleKey.DownArrow?1:0;
        if(direction==0)return current;
        bool vertical=!oneRow&&key is ConsoleKey.W or ConsoleKey.UpArrow or ConsoleKey.S or ConsoleKey.DownArrow;
        if(vertical)
        {
            int row=current/3+direction;return choices.Where(i=>i/3==row).OrderBy(i=>Math.Abs(i%3-current%3)).ThenBy(i=>i).FirstOrDefault(current);
        }
        return direction>0?choices.Where(i=>i>current).FirstOrDefault(current):choices.Where(i=>i<current).LastOrDefault(current);
    }
}
