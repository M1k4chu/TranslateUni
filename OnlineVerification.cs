using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
namespace TranslateUni;
public static partial class Verification
{
    static void CheckOnlineAndFullscreen(bool network)
    {
        Check(FullscreenLauncher.NeedsDedicatedHost(true,true,true,false),"Windows 11 prefers a dedicated Terminal window even with a visible console");
        Check(!FullscreenLauncher.NeedsDedicatedHost(true,true,false,false),"existing classic Windows 10 console can fullscreen directly");
        Check(FullscreenLauncher.NeedsDedicatedHost(false,false,true,false)&&FullscreenLauncher.NeedsDedicatedHost(true,false,true,true),"hidden consoles and explicit recovery get a dedicated host");
        Check(OnlineInvite.Endpoint("example.org")==("example.org",45873),"online endpoints support DNS with the default TCP port");
        Check(OnlineInvite.Endpoint("127.0.0.1:12345")==("127.0.0.1",12345),"custom online TCP port is retained");
        Check(OnlineInvite.Endpoint("[::1]:12345")==("::1",12345),"IPv6 addresses are supported");
        foreach(string bad in new[]{"","bad address","user@server","server:0","server:65536","server/path"})
        {bool rejected=false;try{OnlineInvite.Endpoint(bad);}catch(ArgumentException){rejected=true;}Check(rejected,"invalid endpoint rejected: "+bad);}
        if(!network)return;
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
        try
        {
            int port=((IPEndPoint)listener.LocalEndpoint).Port;
            using var room=new OnlineRoom("localhost:"+port);
            var invite=OnlineInvite.Parse(room.Invite.Code);
            Check(invite==room.Invite&&invite.Token.Length==32&&invite.Fingerprint.Length==64,"online invite preserves endpoint, room secret and SHA-256 server identity");
            Check(invite.Allows(invite.Token)&&!invite.Allows(null)&&!invite.Allows(new string('0',32)),"room authorization requires its current secret");
            bool incomplete=false;try{OnlineInvite.Parse("translateuni://localhost:12345/token");}catch(ArgumentException){incomplete=true;}Check(incomplete,"incomplete invite rejected before connecting");
            Task<LanPeer> clientTask=OnlineTransport.Connect(invite);
            using var serverPeer=OnlineTransport.Accept(listener.AcceptTcpClient(),room).GetAwaiter().GetResult();
            using var clientPeer=clientTask.GetAwaiter().GetResult();
            Check(serverPeer.Secure&&clientPeer.Secure,"online host and client negotiate real encrypted TLS streams");
            clientPeer.Send(new("hello",new("InternetPlayer",3),Mode:PlayMode.Coop,RoomToken:invite.Token));
            Check(SpinWait.SpinUntil(()=>!serverPeer.Inbox.IsEmpty,3000),"encrypted client delivers room handshake");
            serverPeer.Inbox.TryDequeue(out var hello);Check(hello?.Mode==PlayMode.Coop&&invite.Allows(hello.RoomToken),"host validates room secret before admitting player");
            var canvas=new Canvas(172,46);canvas.Text(2,3,"Online CO-OP",ConsoleColor.Magenta);
            serverPeer.Send(LanPeer.Frame(canvas,1,123));Check(SpinWait.SpinUntil(()=>!clientPeer.Inbox.IsEmpty,3000),"encrypted host delivers game frame");
            clientPeer.Inbox.TryDequeue(out var frame);Check(frame?.Revision==123&&LanPeer.Canvas(frame).Lines()[3].Contains("Online CO-OP"),"online client receives authoritative screen and input revision");
            clientPeer.Send(new("input",Key:(int)ConsoleKey.Enter,Revision:123));Check(SpinWait.SpinUntil(()=>!serverPeer.Inbox.IsEmpty,3000),"encrypted client sends gameplay vote");
            serverPeer.Inbox.TryDequeue(out var vote);Check(vote?.Key==(int)ConsoleKey.Enter&&vote.Revision==123,"online gameplay input matches the LAN match protocol");
            Task<LanPeer> badClient=OnlineTransport.Connect(invite with {Fingerprint=new string('0',64)});
            Task<LanPeer> rejectedServer=OnlineTransport.Accept(listener.AcceptTcpClient(),room);
            bool pinRejected=false;try{using var badPeer=badClient.GetAwaiter().GetResult();}catch(AuthenticationException){pinRejected=true;}
            try{using var rejectedPeer=rejectedServer.GetAwaiter().GetResult();}catch(Exception ex)when(ex is AuthenticationException or IOException){}
            Check(pinRejected,"TLS refuses a host whose certificate differs from the invite");
            using var oversizedClient=new TcpClient();oversizedClient.Connect(IPAddress.Loopback,port);
            using var boundedServer=new LanPeer(listener.AcceptTcpClient(),maxLineLength:4096);using var oversizedPeer=new LanPeer(oversizedClient);
            oversizedPeer.Send(new("hello",Error:new string('x',5000)));
            Check(SpinWait.SpinUntil(()=>boundedServer.Closed,3000)&&boundedServer.Inbox.IsEmpty,"server rejects oversized input before parsing or queuing it");
        }
        finally{listener.Stop();}
    }
}
