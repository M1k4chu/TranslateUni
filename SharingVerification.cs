namespace TranslateUni;
public static partial class Verification
{
    static void CheckSharing()
    {
        const string address="192.168.1.42:45873";
        string invite=new OnlineInvite("example.test",45873,new string('A',32),new string('B',64)).Code;
        foreach(int width in new[]{140,172,237})foreach(int height in new[]{42,46,67})
        {
            var sharing=new LobbySharing(address,invite);Canvas hidden=new(width,height);sharing.Draw(hidden,45873);
            Check(!hidden.Lines().Any(l=>l.Contains(address)||l.Contains("translateuni://")||l.Contains(new string('A',16))),"lobby address and invite are hidden by default");
            string? copied=null;var copy=hidden.Buttons.Single(b=>b.Action=="copyInvite");
            Check(sharing.Handle(new(X:copy.X,Y:copy.Y),hidden,value=>{copied=value;return true;})&&copied==invite&&!sharing.Visible,"hidden invite button copies the complete code without revealing it");
            var show=hidden.Buttons.Single(b=>b.Action=="sharingToggle");sharing.Handle(new(X:show.X,Y:show.Y),hidden);
            Canvas visible=new(width,height);sharing.Draw(visible,45873);
            Check(visible.Lines().Any(l=>l.Contains(address))&&visible.Lines().Any(l=>l.Contains("translateuni://")),"show toggle reveals both lobby connection details");
            var lastLine=visible.Buttons.Where(b=>b.Action=="copyInvite").OrderBy(b=>b.Y).Last();
            copied=null;sharing.Handle(new(X:lastLine.X,Y:lastLine.Y),visible,value=>{copied=value;return true;});
            Check(copied==invite,"clicking any wrapped invite line copies the whole invite");
            sharing.Handle(new(ConsoleKey.F10),visible);Canvas again=new(width,height);sharing.Draw(again,45873);
            Check(!again.Lines().Any(l=>l.Contains(address)||l.Contains("translateuni://")),"hide toggle removes all connection details again");
        }
        var lan=new LobbySharing(address+", 10.0.0.42:45873",null);string? first=null;
        lan.Handle(new(ConsoleKey.F11),null,value=>{first=value;return true;});
        Check(first==address,"LAN copy produces one usable endpoint instead of a comma-separated list");
        var failure=new LobbySharing(address,invite);failure.Handle(new(ConsoleKey.F11),null,_=>false);
        Check(failure.Notice.Contains("unavailable")&&!failure.Visible,"clipboard failure gives feedback without revealing the invite");
    }
}
