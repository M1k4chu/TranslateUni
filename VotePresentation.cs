namespace TranslateUni;

public sealed partial class GameApp
{
    int[]? lanCards,lanShopItems;
    public void LanChoices(IEnumerable<VoteChoice>? choices)
    {
        var options=choices?.ToArray();
        lanCards=options?.Where(v=>v.Action==(Screen==Screen.Loans?"loanTake":"play")).Select(v=>v.Index).Where(i=>i>=0).Distinct().Order().ToArray();
        lanShopItems=options?.Where(v=>v.Action=="buy").Select(v=>v.Index).Distinct().Order().ToArray();
    }
}

public sealed partial class LanMatch
{
    public static readonly ConsoleColor[] PlayerColors=[ConsoleColor.Cyan,ConsoleColor.Magenta,ConsoleColor.Yellow,ConsoleColor.Green,ConsoleColor.Blue,ConsoleColor.Red,ConsoleColor.White,ConsoleColor.DarkCyan];
    static readonly HashSet<string> VotingActions=["play","skip","reroll","buy","shopReroll","next","continue","companyContinue","loans","loanBack","loanTake","loanRepay","endless"];
    int[] CardChoices(LanMember member)
    {
        var tied=Teams[member.Team].Ballot.TiedChoices;
        return tied==null?[0,1,2]:tied.Where(c=>c.Action==(member.View.Screen==Screen.Loans?"loanTake":"play")).Select(c=>c.Index).Where(i=>i>=0).Order().ToArray();
    }
    int NavigateChoices(int current,int direction,int[] choices)
    {
        if(choices.Length==0)return -1;int position=Array.IndexOf(choices,current);
        return choices[Math.Clamp(position<0?(direction>0?0:choices.Length-1):position+direction,0,choices.Length-1)];
    }
    Canvas VotingCore(LanMember member,int width,int height,IEnumerable<VoteChoice>? previousChoices=null)
    {
        var tied=previousChoices??Teams[member.Team].Ballot.TiedChoices;
        member.View.LanChoices(tied);member.View.LanSelection(member.Card,member.Shop);
        Canvas frame=member.View.RenderCoreForLan(width,height);
        if(member.View.Screen==Screen.Shop)frame.Button(3,31,"E SKIP BUY","skip");
        if(tied!=null)
        {
            foreach(var button in frame.Buttons.Where(b=>VotingActions.Contains(b.Action)&&!tied.Any(v=>v.Action==b.Action)).ToArray())
            {frame.Text(button.X,button.Y,new string(' ',button.Width));frame.Buttons.Remove(button);}
            frame.Text(3,32,new string(' ',width-36));
            frame.Text(3,32,"REVOTE: tied options only | WASD / numbers | ENTER: Vote",ConsoleColor.Yellow);
        }
        return frame;
    }
    void VoteMarkers(Canvas c,LanMember viewer,IReadOnlyDictionary<int,VoteChoice>? previousVotes=null,int? rightEdge=null)
    {
        var ballot=Teams[viewer.Team].Ballot;
        var team=Teams[viewer.Team];
        var votes=previousVotes??(ballot.Votes.Count==0&&(team.ShopReady||team.EndlessReady)?team.ReadyVotes:ballot.Votes);
        foreach(var group in votes.Where(v=>Members[v.Key].Connected).GroupBy(v=>v.Value))
        {
            var choice=group.Key;
            HitBox? target=choice.Action is "play" or "loanTake"?c.Buttons.FirstOrDefault(b=>b.Action=="select"&&b.Index==choice.Index&&
                (choice.Action=="loanTake")== (viewer.View.Screen==Screen.Loans)):
                choice.Action=="buy"?c.Buttons.FirstOrDefault(b=>b.Action=="shopSelect"&&b.Index==choice.Index):c.Buttons.FirstOrDefault(b=>b.Action==choice.Action);
            if(target==null)continue;
            var voters=group.OrderBy(v=>Members[v.Key].Color).ToArray();
            bool card=target.Action=="select",shop=target.Action=="shopSelect";
            int row=card||shop?target.Y+target.Height-1:target.Y+1;
            int top=(c.Height-Math.Min(46,c.Height))/2;
            if(!card&&!shop&&target.Y-top==28&&viewer.View.Screen is Screen.Table or Screen.Events or Screen.Shop)row=target.Y-1;
            if(choice.Action=="endless")row=target.Y-1;
            if(choice.Action=="skip"&&viewer.View.Screen==Screen.Shop)row=target.Y-1;
            int maxWidth=card||shop?target.Width-4:Math.Max(target.Width,c.Buttons.Where(b=>b.Y==target.Y&&b.X>target.X).Select(b=>b.X-target.X-1).DefaultIfEmpty(target.Width+16).Min());
            maxWidth=Math.Min(maxWidth,(rightEdge??c.Width)-target.X);
            int spacing=maxWidth>=voters.Length*4?4:maxWidth>=voters.Length*2?2:1;
            for(int i=0;i<voters.Length;i++)
            {
                var voter=Members[voters[i].Key];int column=i;
                int y=row,x=target.X+(card||shop?2:0);
                if(card||shop){column=i%4;y=row-i/4;spacing=5;}
                if((card||shop)&&i==4)c.Text(target.X+1,y,new string(' ',target.Width-2));
                c.Text(x+column*spacing,y,spacing>=4?$"[●{voter.Color+1}]":spacing>=2?$"●{voter.Color+1}":"●",PlayerColors[voter.Color]);
            }
        }
    }
}
