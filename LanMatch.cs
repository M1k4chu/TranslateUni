using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
namespace TranslateUni;

public enum PlayMode { Singleplayer, Coop, Rivalry, RivalryCoop }
public sealed record PlayerIdentity(string Name,int Avatar=0);
public static class PlayerAvatars
{
    static readonly string[][] raw=[ [" /\\ ","(oo)"," /| "," /\\ "],[".__.","|oo|","|__|","/  \\"],[" /\\ ","(==)"," /| ","/  \\"],["____","|o |","|__|"," /\\ "],[".__.","(oO)","/||\\","/  \\"],[" /\\ ","[oo]"," || ","/  \\"] ];
    public static readonly string[][] All=raw.Select(sprite=>sprite.Select(row=>row.PadRight(4)[..4]).ToArray()).ToArray();
}
public sealed record VoteChoice(string Action,int Index=-1)
{
    public string Label=>Action switch {"play"=>$"Card {Index+1}","buy"=>$"Buy {Index+1}","loanTake"=>$"Loan {Index+1}","loans"=>"Open loans","loanBack"=>"Leave loans","loanRepay"=>"Repay loan","shopReroll"=>"Refresh shop","next"=>"Next month","continue" or "companyContinue"=>"Continue","skip"=>"Skip",_=>Action};
}
public sealed class TeamBallot
{
    public Dictionary<int,VoteChoice> Votes {get;}=[];
    public HashSet<VoteChoice>? TiedChoices {get;private set;}
    public DateTimeOffset? Deadline {get;private set;}
    public bool Revote=>TiedChoices!=null;
    public bool Cast(int player,VoteChoice choice,DateTimeOffset now)
    {
        if(TiedChoices!=null && !TiedChoices.Contains(choice)) return false;
        Deadline??=now.AddSeconds(20); Votes[player]=choice; return true;
    }
    public (bool Done,VoteChoice? Winner,bool Skipped) Resolve(IReadOnlyCollection<int> players,DateTimeOffset now)
    {
        if(Deadline==null || now<Deadline && players.Any(id=>!Votes.ContainsKey(id))) return (false,null,false);
        var eligible=Votes.Where(v=>players.Contains(v.Key)).GroupBy(v=>v.Value).Select(g=>(Choice:g.Key,Count:g.Count())).ToList();
        if(eligible.Count==0) { Clear();return(true,null,true); }
        int maximum=eligible.Max(v=>v.Count); var tied=eligible.Where(v=>v.Count==maximum).Select(v=>v.Choice).ToList();
        if(tied.Count==1) { var winner=tied[0]; Clear();return(true,winner,false); }
        if(Revote) { Clear();return(true,null,true); }
        TiedChoices=tied.ToHashSet(); Votes.Clear(); Deadline=now.AddSeconds(10); return(false,null,false);
    }
    public void Clear(){Votes.Clear();TiedChoices=null;Deadline=null;}
}
public sealed class LanMember(int id,PlayerIdentity identity,int team,GameApp view)
{
    public int Id {get;}=id; public PlayerIdentity Identity {get;}=identity; public int Team {get;}=team;
    public GameApp View {get;}=view; public int Card=-1,Shop; public bool Connected=true;
    public int Width=172,Height=46,ScorePage,Color; public string Notice="";
    public List<AnimationFrame> Animation=[]; public DateTimeOffset AnimationStart;
    public Canvas? Animated(DateTimeOffset now)
    {
        double elapsed=(now-AnimationStart).TotalMilliseconds;
        foreach(var frame in Animation){elapsed-=frame.Delay;if(elapsed<0)return frame.Canvas;}
        return null;
    }
}
public sealed class LanTeam(Run run)
{
    public Run Run {get;}=run; public TeamBallot Ballot {get;}=new(); public bool ShopReady,EndlessReady;
    public IReadOnlyDictionary<int,VoteChoice> ReadyVotes {get;set;}=new Dictionary<int,VoteChoice>();
}
public sealed partial class LanMatch
{
    public PlayMode Mode {get;} public int[] Slots {get;} public List<LanMember> Members {get;}=[];
    public List<LanTeam> Teams {get;}=[]; public bool Started {get;private set;}
    public DateTimeOffset? DecisionDeadline {get;private set;} public DateTimeOffset? ShopDeadline {get;private set;}
    public int Revision {get;private set;} public string Status {get;private set;}="Waiting for players";
    readonly List<Course> courses; readonly Course course; readonly int topic; readonly Difficulty difficulty; readonly int seed;
    bool decisionBarrier=true; int month=1;
    public LanMatch(List<Course> courses,PlayMode mode,int[] slots,int courseIndex,int topic,Difficulty difficulty,int seed)
    {this.courses=courses;Mode=mode;Slots=slots;course=courses[courseIndex];this.topic=topic;this.difficulty=difficulty;this.seed=seed;}
    public LanMember Add(PlayerIdentity identity,int team)
    {
        if(Started || team<0 || team>=Slots.Length || Members.Count(m=>m.Team==team&&m.Connected)>=Slots[team]) throw new InvalidOperationException("That team is full or the match has started.");
        string name=new(identity.Name.Where(c=>char.IsLetterOrDigit(c)||c is ' ' or '_' or '-').Take(14).ToArray());
        if(string.IsNullOrWhiteSpace(name) || Members.Any(m=>m.Connected&&m.Identity.Name.Equals(name,StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Use a unique player name.");
        var member=new LanMember(Members.Count,new(name,Math.Clamp(identity.Avatar,0,PlayerAvatars.All.Length-1)),team,new GameApp(courses,new Storage(Path.Combine(Path.GetTempPath(),"TranslateUni-LAN-"+Guid.NewGuid().ToString("N")))));
        member.Color=Enumerable.Range(0,8).First(color=>Members.All(m=>!m.Connected||m.Color!=color));
        Members.Add(member); Revision++;return member;
    }
    public bool Start(DateTimeOffset now)
    {
        if(Started || Slots.Where((count,team)=>Members.Count(m=>m.Team==team&&m.Connected)!=count).Any()) return false;
        foreach(int count in Slots) Teams.Add(new(new Run(course,topic,difficulty,seed)));
        foreach(var member in Members) member.View.AttachLanRun(Teams[member.Team].Run);
        Started=true;DecisionDeadline=now.AddMinutes(3);Status="Decision phase";Revision++;return true;
    }
    public void Disconnect(int id)
    { var member=Members.FirstOrDefault(m=>m.Id==id);if(member!=null){member.Connected=false;Status=$"{member.Identity.Name} disconnected";Revision++;} }
    public void Input(int id,Input input,DateTimeOffset now)
    {
        var member=Members.Single(m=>m.Id==id); if(!member.Connected)return;
        if(member.Animation.Count>0)return;
        if(member.Width<140||member.Height<42){member.Notice="Resize to at least 140 x 42.";return;}
        if(!Started){if(id==0 && input.Key==ConsoleKey.Enter) Start(now);return;}
        if(input.Key is ConsoleKey.F7 or ConsoleKey.F8){member.ScorePage=Math.Max(0,member.ScorePage+(input.Key==ConsoleKey.F8?1:-1));return;}
        var team=Teams[member.Team];Run run=team.Run;
        member.View.LanSelection(member.Card,member.Shop);
        Canvas frame=VotingCore(member,Math.Min(140,Math.Max(108,member.Width-32)),Math.Min(46,member.Height));
        VoteChoice? choice=null;
        if(input.X>=0)
        {
            int left=32+Math.Max(0,(member.Width-Math.Min(172,member.Width))/2),top=Math.Max(0,(member.Height-Math.Min(46,member.Height))/2);
            int x=input.X-left,y=input.Y-top;
            HitBox? hit=frame.Buttons.LastOrDefault(b=>x>=b.X&&x<b.X+b.Width&&y>=b.Y&&y<b.Y+b.Height);
            if(hit!=null){ if(hit.Action=="select")member.Card=hit.Index;else if(hit.Action=="shopSelect")member.Shop=hit.Index;else choice=new(hit.Action,hit.Index); }
        }
        else
        {
            int digit=input.Key is >=ConsoleKey.D1 and <=ConsoleKey.D9?input.Key-ConsoleKey.D1:-1;
            if(member.View.Screen is Screen.Table or Screen.Events or Screen.Booster or Screen.Loans)
            {
                int[] allowed=CardChoices(member);
                if(digit is >=0 and <3&&allowed.Contains(digit))member.Card=digit;
                else if(input.Key is ConsoleKey.A or ConsoleKey.W or ConsoleKey.LeftArrow or ConsoleKey.UpArrow)member.Card=NavigateChoices(member.Card,-1,allowed);
                else if(input.Key is ConsoleKey.D or ConsoleKey.S or ConsoleKey.RightArrow or ConsoleKey.DownArrow)member.Card=NavigateChoices(member.Card,1,allowed);
                if(input.Key==ConsoleKey.Enter)choice=member.View.Screen==Screen.Loans?new(run.HasActiveLoan?"loanRepay":"loanTake",member.Card):new("play",member.Card);
                if(input.Key==ConsoleKey.E)choice=new(member.View.Screen==Screen.Loans?"loanBack":"skip");if(input.Key==ConsoleKey.R&&member.View.Screen!=Screen.Loans)choice=new("reroll");
            }
            else if(member.View.Screen==Screen.Shop)
            {
                int[] allowed=team.Ballot.TiedChoices?.Where(v=>v.Action=="buy").Select(v=>v.Index).Order().ToArray()??[0,1,2,3,4,5];
                if(digit is >=0 and <6&&allowed.Contains(digit))member.Shop=digit;
                else if(input.Key is ConsoleKey.A or ConsoleKey.LeftArrow)member.Shop=NavigateChoices(member.Shop,-1,allowed);
                else if(input.Key is ConsoleKey.D or ConsoleKey.RightArrow)member.Shop=NavigateChoices(member.Shop,1,allowed);
                else if(input.Key is ConsoleKey.W or ConsoleKey.UpArrow)member.Shop=team.Ballot.Revote?NavigateChoices(member.Shop,-1,allowed):Math.Max(0,member.Shop-3);
                else if(input.Key is ConsoleKey.S or ConsoleKey.DownArrow)member.Shop=team.Ballot.Revote?NavigateChoices(member.Shop,1,allowed):Math.Min(5,member.Shop+3);
                if(input.Key==ConsoleKey.Enter)choice=new("buy",member.Shop);if(input.Key==ConsoleKey.R)choice=new("shopReroll");
            }
            if(input.Key==ConsoleKey.Spacebar && run.Phase==Phase.Shop)choice=new("next");
            if(input.Key==ConsoleKey.E && member.View.Screen is Screen.Shop or Screen.Reward)choice=new("skip");
            if(input.Key==ConsoleKey.Enter && member.View.Screen==Screen.Reward)choice=new("next");
            if(input.Key==ConsoleKey.Enter && member.View.Screen==Screen.Result)choice=new("continue");
            if(input.Key==ConsoleKey.Enter && member.View.Screen==Screen.SpecialEvent)choice=new("companyContinue");
            if(input.Key==ConsoleKey.P && member.View.Screen==Screen.Loans)choice=new("loanRepay");
            if(input.Key==ConsoleKey.L&&member.View.Screen!=Screen.Loans)choice=new("loans");
            if(input.Key==ConsoleKey.Escape&&member.View.Screen==Screen.Loans)choice=new("loanBack");
            if(member.View.Screen==Screen.End&&run.Phase==Phase.Won&&!run.Endless&&input.Key==ConsoleKey.E)choice=new("endless");
            if(member.View.Screen==Screen.End&&input.Key==ConsoleKey.Spacebar)choice=new("skip");
            if(member.View.Screen==Screen.End && (input.Key is ConsoleKey.Enter or ConsoleKey.R or ConsoleKey.Escape || input.Key==ConsoleKey.E&&choice==null)){member.Notice="Q: Leave the match. Start a new lobby for a rematch.";return;}
            if(member.View.Screen==Screen.Pause && input.Key==ConsoleKey.M){member.Notice="Q: Leave the match.";return;}
            if(choice==null && digit<0 && input.Key is not (ConsoleKey.W or ConsoleKey.A or ConsoleKey.S or ConsoleKey.D or ConsoleKey.UpArrow or ConsoleKey.DownArrow or ConsoleKey.LeftArrow or ConsoleKey.RightArrow))
            {
                member.View.Handle(input,frame);
                member.Animation=member.View.TakeFeedbackFrames(frame.Width,frame.Height);member.AnimationStart=now;
            }
        }
        if(choice!=null)
        {
            var changing=VotingActions;
            if(choice.Action is "main" or "retry" || choice.Action=="back"&&member.View.Screen==Screen.End)member.Notice="Q: Leave the match. Start a new lobby for a rematch.";
            else if(choice.Action=="endless"&&(run.Phase!=Phase.Won||run.Endless||Teams.Any(t=>t.Run.Phase is not (Phase.Won or Phase.Lost))))member.Notice="Endless opens when every company finishes its year.";
            else if(!changing.Contains(choice.Action)) member.View.LanAction(choice.Action,choice.Index);
            else if(decisionBarrier && run.Phase!=Phase.Decisions || team.ShopReady) member.Notice="Waiting for the other company.";
            else
            {
                if(choice.Action is "play" or "loanTake" && choice.Index<0)choice=choice with {Index=member.Card};
                if(choice.Action=="buy"&&choice.Index<0)choice=choice with {Index=member.Shop};
                if(choice.Action=="loanRepay"&&!run.HasActiveLoan||choice.Action=="loanTake"&&!run.CanBorrow)member.Notice=run.LoanStatus;
                else if(choice.Action is "play" or "loanTake" && choice.Index<0)member.Notice="Select a card before voting.";
                else if(Members.Count(m=>m.Team==member.Team&&m.Connected)>1)
                {
                    if(!team.Ballot.Cast(id,choice,now))member.Notice="Revote only on the tied choices.";
                    else member.Notice=$"Voted: {choice.Label}";
                }
                else Apply(member,choice,now,previousVotes:new Dictionary<int,VoteChoice>{{member.Id,choice}});
            }
        }
        Tick(now);
    }
    void Apply(LanMember member,VoteChoice choice,DateTimeOffset now,IEnumerable<VoteChoice>? previousChoices=null,IReadOnlyDictionary<int,VoteChoice>? previousVotes=null)
    {
        var team=Teams[member.Team];var run=team.Run;
        if(choice.Action is "loans" or "loanBack")
        {
            foreach(var voter in Members.Where(m=>m.Team==member.Team&&m.Connected)){voter.View.LanChoices(null);voter.View.LanAction(choice.Action,-1);voter.Card=choice.Action=="loans"?0:-1;voter.Notice="";}
            team.Ballot.Clear();Revision++;return;
        }
        StatSnapshot before=run.CaptureStats();Phase previous=run.Phase;
        if(choice.Action=="loanTake")foreach(var voter in Members.Where(m=>m.Team==member.Team)){if(voter.View.Screen!=Screen.Loans)voter.View.LanAction("loans",-1);voter.View.LanSelection(choice.Index,voter.Shop);}
        var sources=Members.Where(m=>m.Team==member.Team).ToDictionary(m=>m.Id,m=>VotingCore(m,Math.Min(140,m.Width-32),Math.Min(46,m.Height),previousChoices));
        foreach(var voter in Members.Where(m=>m.Team==member.Team))VoteMarkers(sources[voter.Id],voter,previousVotes,sources[voter.Id].Width-30);
        if(choice.Action=="endless")team.EndlessReady=true;
        else if(choice.Action=="next") { if(run.Phase==Phase.Shop)team.ShopReady=true; }
        else { if(choice.Action=="loanTake"&&member.View.Screen!=Screen.Loans)member.View.LanAction("loans",-1); member.View.LanSelection(choice.Index>=0?choice.Index:member.Card,member.Shop); member.View.LanAction(choice.Action,choice.Index); }
        bool animateCards=member.View.TakeAnimationFrames().Count>0;
        foreach(var other in Members.Where(m=>m.Team==member.Team))
        {
            other.Card=-1;other.Notice="";
            other.View.LanChoices(null);
            if(!ReferenceEquals(other,member)&&choice.Action is not ("loanTake" or "loanRepay"))other.View.AttachLanRun(run);
            other.Animation=animateCards?CardAnimation.Build(sources[other.Id],choice.Action is "play" or "loanTake"?choice.Index:-1):[];
            other.Animation.AddRange(other.View.LanFeedback(before,previous,Math.Min(140,other.Width-32),Math.Min(46,other.Height)));
            other.AnimationStart=now;
        }
        team.Ballot.Clear();Revision++;
        if(team.ShopReady||team.EndlessReady)team.ReadyVotes=previousVotes??new Dictionary<int,VoteChoice>();
        var winners=Teams.Where(t=>t.Run.Phase==Phase.Won&&!t.Run.Endless&&Members.Any(m=>m.Team==Teams.IndexOf(t)&&m.Connected)).ToArray();
        if(winners.Length>0&&winners.All(t=>t.EndlessReady)&&Teams.All(t=>t.Run.Phase is Phase.Won or Phase.Lost))
        {
            foreach(var winner in winners){winner.Run.EnterEndless();winner.ShopReady=winner.EndlessReady=false;winner.ReadyVotes=new Dictionary<int,VoteChoice>();}
            decisionBarrier=false;DecisionDeadline=ShopDeadline=null;Status="Endless rewards and shop";
            foreach(var voter in Members){voter.Card=-1;voter.Notice="";voter.View.AttachLanRun(Teams[voter.Team].Run);voter.Animation.Clear();}
            Revision++;
        }
    }
    public void Tick(DateTimeOffset now)
    {
        if(!Started)return;
        foreach(var member in Members.Where(m=>m.Animation.Count>0 && m.Animated(now)==null)){member.Animation.Clear();Revision++;}
        foreach(var team in Teams)
        {
            var voters=Members.Where(m=>m.Team==Teams.IndexOf(team)&&m.Connected).Select(m=>m.Id).ToArray();
            if(voters.Length==0 && team.Run.Phase is not (Phase.Won or Phase.Lost)){team.Run.ForfeitLan();team.Ballot.Clear();foreach(var member in Members.Where(m=>m.Team==Teams.IndexOf(team))){member.Animation.Clear();member.View.AttachLanRun(team.Run);}Revision++;}
            bool wasRevote=team.Ballot.Revote;
            var previousChoices=team.Ballot.TiedChoices;
            var previousVotes=team.Ballot.Deadline!=null?new Dictionary<int,VoteChoice>(team.Ballot.Votes):null;
            var resolution=team.Ballot.Resolve(voters,now);
            if(!wasRevote && team.Ballot.Revote)
            {
                foreach(var voter in Members.Where(m=>m.Team==Teams.IndexOf(team)))
                {
                    voter.Notice="REVOTE: choose a surviving option.";
                    if(!CardChoices(voter).Contains(voter.Card))voter.Card=-1;
                }
                Revision++;
            }
            if(resolution.Done)
            {
                var actor=Members.First(m=>m.Team==Teams.IndexOf(team)&&m.Connected);
                if(resolution.Winner!=null) Apply(actor,resolution.Winner,now,previousChoices,previousVotes);
                else {team.Run.SkipLanChoice();foreach(var member in Members.Where(m=>m.Team==actor.Team)){member.Card=-1;member.View.AttachLanRun(team.Run);member.Notice="Tied revote: choice skipped.";}Revision++;}
            }
        }
        if(decisionBarrier && (now>=DecisionDeadline || Teams.All(t=>t.Run.Phase!=Phase.Decisions)))
        {
            foreach(var team in Teams){team.Run.FinishDecisions();team.Ballot.Clear();}
            foreach(var member in Members){member.Card=-1;member.View.AttachLanRun(Teams[member.Team].Run);}
            decisionBarrier=false;DecisionDeadline=null;Status="Events and monthly results";Revision++;
        }
        if(!decisionBarrier && ShopDeadline==null && Teams.Any(t=>t.Run.Phase==Phase.Shop)){ShopDeadline=now.AddMinutes(2);Status="Shop phase";Revision++;}
        var alive=Teams.Where(t=>t.Run.Phase is not (Phase.Won or Phase.Lost)).ToList();
        if(alive.Count==0){DecisionDeadline=ShopDeadline=null;Status="Match complete: "+string.Join(" / ",Teams.Select((t,i)=>(t,i)).OrderByDescending(v=>v.t.Run.Cleared).ThenByDescending(v=>v.t.Run.Reports.Sum(r=>r.Profit)).Select(v=>$"Team {v.i+1}: {v.t.Run.Cleared} quotas, ${v.t.Run.Reports.Sum(r=>r.Profit):N0}"));return;}
        if(ShopDeadline!=null && now>=ShopDeadline)
        {
            foreach(var team in alive)
            {
                team.Run.SkipEvents();team.Run.ContinueResult();team.Run.AcknowledgeCompanyEvent();team.Run.SkipBooster();
                if(team.Run.Phase==Phase.Shop)team.ShopReady=true;team.Ballot.Clear();
            }
            foreach(var member in Members){member.Animation.Clear();member.Card=-1;member.View.AttachLanRun(Teams[member.Team].Run);}
            alive=Teams.Where(t=>t.Run.Phase is not (Phase.Won or Phase.Lost)).ToList();
            Revision++;
            if(alive.Count==0){Tick(now);return;}
        }
        if(alive.All(t=>t.ShopReady))
        {
            var previousStats=Teams.Select(t=>t.Run.CaptureStats()).ToArray();
            foreach(var team in alive){team.Run.NextMonth();team.ShopReady=false;team.ReadyVotes=new Dictionary<int,VoteChoice>();}
            month++;decisionBarrier=true;DecisionDeadline=now.AddMinutes(3);ShopDeadline=null;Status=$"Month {month}: decisions";
            foreach(var member in Members){member.Card=-1;member.Notice="";member.View.AttachLanRun(Teams[member.Team].Run);member.Animation=member.View.LanFeedback(previousStats[member.Team],Phase.Shop,Math.Min(140,member.Width-32),Math.Min(46,member.Height));member.AnimationStart=now;}Revision++;
        }
    }
    public Canvas Render(int id,int width,int height,DateTimeOffset now)
    {
        var member=Members.Single(m=>m.Id==id);member.Width=width;member.Height=height;
        if(width<140||height<42){var small=new Canvas(width,height);small.Center(height/2,"Multiplayer needs at least 140 x 42. Resize the terminal.",ConsoleColor.Yellow);return small;}
        int coreWidth=Math.Min(140,width-32),coreHeight=Math.Min(46,height),left=Math.Max(0,(width-coreWidth-32)/2),top=(height-coreHeight)/2;
        var canvas=new Canvas(width,height);
        if(!Started)
        {
            canvas.Center(top+3,"LAN LOBBY - "+Mode,ConsoleColor.Cyan);
            canvas.Center(top+5,"Players: "+Members.Count(m=>m.Connected)+" / "+Slots.Sum());
            canvas.Center(top+7,id==0?"ENTER: Start when every slot is filled":"Waiting for the host to start");
            canvas.Center(top+9,"Team slots: "+string.Join(" / ",Slots));
        }
        else
        {
            member.View.LanSelection(member.Card,member.Shop);
            Canvas? motion=member.Animated(now);
            canvas.Paste(motion!=null&&motion.Width==coreWidth&&motion.Height==coreHeight?motion:VotingCore(member,coreWidth,coreHeight),left+32,top);
            var team=Teams[member.Team];
            void message(int row,string value,ConsoleColor color){canvas.Text(left+35,top+row,value[..Math.Min(value.Length,coreWidth-36)],color);}
            if(team.Run.Phase is Phase.Won or Phase.Lost)
            {
                bool canEndless=team.Run.Phase==Phase.Won&&!team.Run.Endless;
                for(int row=15;row<=32;row++)if(row!=17||!canEndless)message(row,new string(' ',coreWidth-36),ConsoleColor.Gray);
                bool finished=Teams.All(t=>t.Run.Phase is Phase.Won or Phase.Lost);
                message(15,finished?"MATCH FINISHED":"Your run ended. Waiting for the other company.",ConsoleColor.Cyan);
                if(finished)
                {
                    var ranked=Teams.Select((t,i)=>(t,i)).OrderByDescending(v=>v.t.Run.Cleared).ThenByDescending(v=>v.t.Run.Reports.Sum(r=>r.Profit)).ToArray();
                    bool tied=ranked.Length>1&&ranked[0].t.Run.Cleared==ranked[1].t.Run.Cleared&&Math.Abs(ranked[0].t.Run.Reports.Sum(r=>r.Profit)-ranked[1].t.Run.Reports.Sum(r=>r.Profit))<.005;
                    message(18,Mode==PlayMode.Coop?(team.Run.Cleared>=12?"A year of teamwork!":"Team run ended"):tied?"TIE - equal quotas and total profit":$"TEAM {ranked[0].i+1} WINS",ConsoleColor.Yellow);
                    for(int i=0;i<ranked.Length;i++)message(19+i,$"Team {ranked[i].i+1}: {ranked[i].t.Run.Cleared} quotas | Profit ${ranked[i].t.Run.Reports.Sum(r=>r.Profit):N0}",ConsoleColor.Green);
                }
                if(team.EndlessReady)message(23,"Endless ready - waiting for the other winning team",ConsoleColor.Green);
                message(32,canEndless?"E: Vote endless | SPACE: Vote skip | Q: Leave":"Q: Leave | Start a new lobby for a rematch",ConsoleColor.Cyan);
            }
            if(decisionBarrier && team.Run.Phase!=Phase.Decisions || team.ShopReady){message(31,new string(' ',coreWidth-36),ConsoleColor.Gray);message(31,"READY - waiting for the other company",ConsoleColor.Green);}
            else if(member.Notice.Length>0){int row=member.View.Screen==Screen.Shop?33:31;message(row,new string(' ',coreWidth-36),ConsoleColor.Gray);message(row,member.Notice,ConsoleColor.Gray);}
        }
        DrawStandings(canvas,left,top,coreHeight,member.ScorePage);
        if(Started)
        {
            string clock=DecisionDeadline!=null?$"DECISIONS {Time(DecisionDeadline.Value-now)}":ShopDeadline!=null?$"SHOP {Time(ShopDeadline.Value-now)}":Teams.All(t=>t.Run.Phase is Phase.Won or Phase.Lost)?"MATCH COMPLETE":"EVENTS / MONTHLY RESULTS";
            canvas.Text(left+1,top+coreHeight-5,clock,ConsoleColor.Cyan);
            var ballot=Teams[member.Team].Ballot;
            if(ballot.Deadline!=null)
            {
                canvas.Text(left+1,top+coreHeight-4,$"{(ballot.Revote?"REVOTE":"VOTE")} {Time(ballot.Deadline.Value-now)} | E: Skip",ConsoleColor.Yellow);
                if(ballot.Revote){string choices=string.Join(" / ",ballot.TiedChoices!.Select(v=>v.Label));canvas.Text(left+1,top+coreHeight-3,choices[..Math.Min(30,choices.Length)],ConsoleColor.Yellow);}
            }
            else if(Teams[member.Team].Run.Phase is not (Phase.Won or Phase.Lost)&&Members.Count(m=>m.Team==member.Team&&m.Connected)>1)canvas.Text(left+1,top+coreHeight-4,"ENTER: Vote | E: Skip",ConsoleColor.Yellow);
        }
        if(Started&&member.Animation.Count==0)VoteMarkers(canvas,member,rightEdge:left+32+coreWidth-30);
        return canvas;
    }
    static string Time(TimeSpan time)=>$"{Math.Max(0,(int)time.TotalSeconds)/60:00}:{Math.Max(0,(int)time.TotalSeconds)%60:00}";
    void DrawStandings(Canvas c,int x,int y,int height,int page)
    {
        c.Text(x+1,y+1,"MONTHLY PROFIT STANDINGS",ConsoleColor.Cyan);
        int visible=Math.Max(1,(height-6)/7);page=Math.Clamp(page,0,Math.Max(0,(Members.Count-1)/visible));
        foreach(var member in Members.Skip(page*visible).Take(visible))
        {
            int row=y+4+Members.Skip(page*visible).Take(visible).ToList().IndexOf(member)*7;
            var color=PlayerColors[member.Color];for(int i=0;i<4;i++)c.Text(x+1,row+i,PlayerAvatars.All[member.Identity.Avatar][i],color);
            c.Text(x+7,row,$"●{member.Color+1} {member.Identity.Name}",color);c.Text(x+7,row+1,$"Team {member.Team+1} "+(member.Connected?"":"OFFLINE"));
            if(Started){var run=Teams[member.Team].Run;void item(int offset,string value,ConsoleColor ink)=>c.Text(x+7,row+offset,value[..Math.Min(24,value.Length)],ink);item(2,run.LastReport==null?"Month profit: --":$"M{run.LastReport.Month}: ${run.LastReport.Profit:N0}",color);item(3,$"Total: ${run.Reports.Sum(r=>r.Profit):N0}",ConsoleColor.Gray);var ballot=Teams[member.Team].Ballot;if(ballot.Votes.TryGetValue(member.Id,out var vote))item(4,vote.Label,color);}
        }
        c.Text(x+1,y+height-2,"F7/F8: Pages | Q: Leave");
    }
}
public sealed partial class Run
{
    public void ForfeitLan(){Phase=Phase.Lost;}
    public void SkipLanChoice(){if(Phase==Phase.Decisions){Actions--;if(Actions<=0)FinishDecisions();else DealHand();}else if(Phase==Phase.Events)SkipEvents();else if(Phase==Phase.Booster)SkipBooster();}
}
public sealed partial class GameApp
{
    public void AttachLanRun(Run run){Game=run;selected=-1;tutorialStep=-1;recorded=true;SyncScreen();}
    public void LanSelection(int card,int shop){selected=card;loanIndex=card;shopIndex=shop;}
    public Canvas RenderCoreForLan(int width,int height)=>RenderCore(width,height);
    public void LanAction(string action,int index){var frame=RenderCore(140,46);frame.Buttons.Add(new(0,0,1,1,action,index));Handle(new(X:0,Y:0),frame);}
    public List<AnimationFrame> LanFeedback(StatSnapshot before,Phase previous,int width,int height)
    {
        if(Game==null)return [];
        var after=Game.CaptureStats();pendingStatsFrom=before!=after?before:null;pendingStatsTo=before!=after?after:null;
        pendingResultAnimation=Game.Phase==Phase.Result&&previous!=Phase.Result;
        return TakeFeedbackFrames(width,height);
    }
}
