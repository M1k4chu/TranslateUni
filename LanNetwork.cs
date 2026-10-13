using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
namespace TranslateUni;
public sealed record LanPacket(string Type,PlayerIdentity? Player=null,int Team=0,int Key=0,int X=-1,int Y=-1,int Width=172,int Height=46,int Id=-1,int Revision=0,string[]? Lines=null,int[]? Colors=null,string? Error=null,int TeamSize=0,string? RoomToken=null,PlayMode Mode=PlayMode.Singleplayer,int Protocol=1);
public sealed class LanPeer : IDisposable
{
    readonly TcpClient client; readonly StreamReader reader; readonly StreamWriter writer;readonly Stream transport;readonly int maxLineLength;
    public bool Secure {get;}
    readonly object writeLock=new(); public ConcurrentQueue<LanPacket> Inbox {get;}=[];public volatile bool Closed;
    public LanPeer(TcpClient client,Stream? transport=null,bool secure=false,int maxLineLength=600000)
    {this.client=client;this.transport=transport??client.GetStream();this.maxLineLength=maxLineLength;Secure=secure;client.NoDelay=true;client.SendTimeout=500;reader=new(this.transport,Encoding.UTF8);writer=new(this.transport,new UTF8Encoding(false)){AutoFlush=true};_ = Receive();}
    async Task Receive()
    {
        var chars=new char[4096];var line=new StringBuilder();
        try
        {
            while(!Closed)
            {
                int count=await reader.ReadAsync(chars.AsMemory());if(count==0)break;
                for(int i=0;i<count;i++)
                {
                    char ch=chars[i];if(ch=='\r')continue;
                    if(ch!='\n'){if(line.Length>=maxLineLength)return;line.Append(ch);continue;}
                    var packet=JsonSerializer.Deserialize<LanPacket>(line.ToString());line.Clear();
                    if(packet!=null){if(Inbox.Count>=128)return;Inbox.Enqueue(packet);}
                }
            }
        }
        catch(Exception ex)when(ex is IOException or JsonException or ObjectDisposedException){}
        finally{Closed=true;}
    }
    public void Send(LanPacket packet){lock(writeLock){if(Closed)return;try{writer.WriteLine(JsonSerializer.Serialize(packet));}catch(IOException){Closed=true;}}}
    public void Dispose(){Closed=true;try{transport.Dispose();}catch(IOException){}finally{client.Dispose();}}
    public static LanPacket Frame(Canvas frame,int id,int revision)=>new("frame",Id:id,Revision:revision,Width:frame.Width,Height:frame.Height,Lines:frame.Lines(),Colors:Enumerable.Range(0,frame.Height).SelectMany(y=>Enumerable.Range(0,frame.Width).Select(x=>(int)frame.ColorAt(x,y))).ToArray());
    public static Canvas Canvas(LanPacket packet)
    {
        int width=Math.Clamp(packet.Width,1,300),height=Math.Clamp(packet.Height,1,100);var frame=new Canvas(width,height);
        if(packet.Lines==null||packet.Colors==null||packet.Colors.Length!=width*height)return frame;
        for(int y=0;y<Math.Min(height,packet.Lines.Length);y++)for(int x=0;x<Math.Min(width,packet.Lines[y].Length);x++)frame.Text(x,y,packet.Lines[y][x].ToString(),(ConsoleColor)Math.Clamp(packet.Colors[y*width+x],0,15));return frame;
    }
}
public static class LanMultiplayer
{
    public const int Port=45873;
    public static void Open(List<Course> courses,PlayMode mode,PlayerIdentity profile,TerminalInput input)
    {
        bool joining=false,online=false;string address="",error="";int course=0,topic=0,difficulty=0,joinTeam=mode==PlayMode.Coop?0:1;
        var counts=new PlayerCountSelection();
        try{if(Console.WindowWidth<140 && OperatingSystem.IsWindows()){int width=Math.Min(172,Console.LargestWindowWidth);Console.SetBufferSize(Math.Max(Console.BufferWidth,width),Console.BufferHeight);Console.SetWindowSize(width,Console.WindowHeight);}}catch(IOException){}catch(ArgumentOutOfRangeException){}
        while(true)
        {
            int w=Console.WindowWidth,h=Console.WindowHeight;var c=new Canvas(w,h);c.Center(2,$"MULTIPLAYER - {mode} - {(online?"ONLINE":"LAN")}",ConsoleColor.Cyan);
            int controlsX=Math.Max(3,(w-75)/2);
            c.Button(controlsX,4,"HOST","host",selected:!joining);c.Button(controlsX+12,4,"JOIN","join",selected:joining);
            c.Button(controlsX+24,4,online?"F6 ONLINE":"F6 LAN","network");c.Text(controlsX+42,4,"TAB: Host / Join | ESC: Back");
            c.Center(6,joining?$"JOIN | Team {joinTeam+1}":$"HOST | {courses[course].Name} / {courses[course].Lessons[topic].Name} / {(Difficulty)difficulty}");
            if(joining&&mode==PlayMode.RivalryCoop)c.Button(controlsX,8,$"F5 TEAM {joinTeam+1} - SWITCH","team");
            else c.Center(8,joining?"Play the same mode as your host. Each player needs a unique name.":"F1: Course | F2: Topic | F3: Difficulty");
            int[] slots=counts.Slots(mode);
            counts.Draw(c,mode,joining,joinTeam);
            c.Center(20,online?(joining?"Paste the complete invite code from your host":"Type your public IP / DNS / VPN address, optionally :port"):joining?$"Type your host's LAN IP / name. Default port: {Port}.":"Create the lobby, then share your LAN address with the other players.");
            c.Text(5,22,"Address / invite:",ConsoleColor.Cyan);
            int editWidth=Math.Max(20,w-10);string edit=address+"_";
            for(int row=0;row<4&&row*editWidth<edit.Length;row++)c.Text(5,23+row,edit.Substring(row*editWidth,Math.Min(editWidth,edit.Length-row*editWidth)),ConsoleColor.Cyan);
            if(online)c.Wrapped(5,28,"The host PC runs the server. For direct internet access, forward its TCP port to this PC and allow the game through the firewall. A shared VPN also works.",Math.Max(20,w-10),3);
            c.Center(32,"Team captains choose player counts before starting. Names must be unique.");if(error.Length>0)c.Wrapped(5,34,error,Math.Max(20,w-10),3,ConsoleColor.Red);
            c.Button(controlsX,38,joining?"ENTER JOIN LOBBY":"ENTER CREATE LOBBY","connect",selected:true);c.Flush();
            Input key=input.Read();
            var setupHit=c.Buttons.LastOrDefault(b=>key.X>=b.X&&key.X<b.X+b.Width&&key.Y==b.Y);
            if(setupHit?.Action is "host" or "join"){joining=setupHit.Action=="join";error="";input.DiscardPending();continue;}
            if(setupHit?.Action=="network")key=new(ConsoleKey.F6);
            if(setupHit?.Action=="team")key=new(ConsoleKey.F5);
            if(setupHit?.Action=="connect")key=new(ConsoleKey.Enter);
            if(key.Key==ConsoleKey.Escape)return;
            if(key.Key==ConsoleKey.Tab){joining=!joining;if(joining)address="";input.DiscardPending();continue;}
            if(key.Key==ConsoleKey.F6){online=!online;address="";error="";input.DiscardPending();continue;}
            if(key.Key==ConsoleKey.F1)course=(course+1)%courses.Count;if(key.Key==ConsoleKey.F2)topic=(topic+1)%10;if(key.Key==ConsoleKey.F3)difficulty=(difficulty+1)%3;
            if(counts.Handle(key,c,mode,joining))continue;
            if(key.Key==ConsoleKey.F5&&joining&&mode==PlayMode.RivalryCoop)joinTeam=1-joinTeam;
            if((joining||online)&&key.Key==ConsoleKey.Backspace&&address.Length>0)address=address[..^1];
            else if((joining||online)&&key.Character>='!'&&key.Character<='~'&&address.Length<600)address+=key.Character;
            if(key.Key!=ConsoleKey.Enter)continue;
            try
            {
                input.DiscardPending();
                c.Center(40,joining?"Connecting to your host...":"Creating your lobby...",ConsoleColor.Yellow);c.Flush();
                if(joining)Join(address,profile,mode==PlayMode.Coop?0:joinTeam,input,mode==PlayMode.RivalryCoop?counts.First:0,mode,online);
                else Host(courses,mode,slots,course,topic,(Difficulty)difficulty,profile,input,online,address);
                return;
            }
            catch(Exception ex)when(ex is SocketException or IOException or InvalidOperationException or TimeoutException or ArgumentException or System.Security.Authentication.AuthenticationException or System.Security.Cryptography.CryptographicException or OperationCanceledException){error=ex is TimeoutException or OperationCanceledException?"Connection timed out. Check the address, TCP port forwarding / VPN, and firewall.":ex is System.Security.Authentication.AuthenticationException?"Server identity did not match the invite. Ask the host for the current complete code.":ex is System.Security.Cryptography.CryptographicException?"Unable to create secure room credentials. Restart the host and try again.":ex.Message;}
        }
    }
    static void Host(List<Course> courses,PlayMode mode,int[] slots,int course,int topic,Difficulty difficulty,PlayerIdentity profile,TerminalInput input,bool online,string address)
    {
        using var room=online?new OnlineRoom(address):null;
        var match=new LanMatch(courses,mode,slots,course,topic,difficulty,GameSeeds.New());var local=match.Add(profile,0);
        int port=room?.Invite.Port??Port;
        var listener=new TcpListener(Socket.OSSupportsIPv6?IPAddress.IPv6Any:IPAddress.Any,port);if(Socket.OSSupportsIPv6)listener.Server.DualMode=true;
        listener.Start(8);var peers=new Dictionary<LanPeer,int>();var pending=new Dictionary<TcpClient,Task<LanPeer>>();var accepted=new Dictionary<LanPeer,DateTimeOffset>();DateTimeOffset lastSend=DateTimeOffset.MinValue;
        try
        {
            while(true)
            {
                while(listener.Pending())
                {
                    var incoming=listener.AcceptTcpClient();if(peers.Count+pending.Count>=8){incoming.Dispose();continue;}
                    if(room!=null)pending.Add(incoming,OnlineTransport.Accept(incoming,room));
                    else {var peer=new LanPeer(incoming,maxLineLength:4096);peers.Add(peer,-1);accepted[peer]=DateTimeOffset.UtcNow;}
                }
                foreach(var handshake in pending.Where(p=>p.Value.IsCompleted).ToArray())
                {
                    pending.Remove(handshake.Key);
                    try{var peer=handshake.Value.GetAwaiter().GetResult();peers.Add(peer,-1);accepted[peer]=DateTimeOffset.UtcNow;}
                    catch(Exception ex)when(ex is IOException or SocketException or System.Security.Authentication.AuthenticationException or OperationCanceledException or ObjectDisposedException){handshake.Key.Dispose();}
                }
                foreach(var pair in peers.ToArray())
                {
                    var peer=pair.Key;
                    if(peer.Closed||pair.Value<0&&DateTimeOffset.UtcNow-accepted[peer]>TimeSpan.FromSeconds(10)){if(pair.Value>=0)match.Disconnect(pair.Value);peer.Dispose();peers.Remove(peer);accepted.Remove(peer);continue;}
                    while(peer.Inbox.TryDequeue(out var message))
                    {
                        if(pair.Value<0&&message.Type=="hello")
                        {
                            try
                            {
                                if(room!=null&&!room.Invite.Allows(message.RoomToken))throw new InvalidOperationException("Invite code rejected. Ask the host for the current code.");
                                if(message.Protocol!=1||message.Mode!=mode)throw new InvalidOperationException("Select the same multiplayer mode and game version as the host.");
                                if(match.Started)throw new InvalidOperationException("The match has already started. Join the next lobby.");
                                if(mode==PlayMode.RivalryCoop && message.Team==1 && message.TeamSize is >=1 and <=4 && match.Members.All(m=>m.Team!=1||!m.Connected))match.Slots[1]=message.TeamSize;
                                var member=match.Add(message.Player??new("Player"),message.Team);member.Width=Math.Clamp(message.Width,1,300);member.Height=Math.Clamp(message.Height,1,100);peers[peer]=member.Id;peer.Send(new("welcome",Id:member.Id));
                            }
                            catch(InvalidOperationException ex){peer.Send(new("error",Error:ex.Message));peer.Dispose();}
                            break;
                        }
                        if(pair.Value>=0&&message.Type is "input" or "resize")
                        {
                            var member=match.Members.Single(m=>m.Id==pair.Value);member.Width=Math.Clamp(message.Width,1,300);member.Height=Math.Clamp(message.Height,1,100);
                            if(message.Type=="input"&&message.Revision==match.Revision)match.Input(pair.Value,new((ConsoleKey)message.Key,message.X,message.Y),DateTimeOffset.UtcNow);
                        }
                    }
                }
                Input next=input.Read(30);if(next.Key==ConsoleKey.Q)break;
                if(next.Key==ConsoleKey.F9&&!match.Started&&room!=null){Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory,"data"));File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"data","online-invite.txt"),room.Invite.Code);}
                if(next.Key!=ConsoleKey.NoName||next.X>=0){int revision=match.Revision;match.Input(0,next,DateTimeOffset.UtcNow);if(revision!=match.Revision)input.DiscardPending();}
                int beforeTick=match.Revision;match.Tick(DateTimeOffset.UtcNow);if(beforeTick!=match.Revision)input.DiscardPending();
                int interval=match.Members.Any(m=>m.Animation.Count>0)?35:150;
                if(DateTimeOffset.UtcNow-lastSend<TimeSpan.FromMilliseconds(interval))continue;lastSend=DateTimeOffset.UtcNow;
                Canvas frame=match.Render(0,Console.WindowWidth,Console.WindowHeight,DateTimeOffset.UtcNow);
                if(!match.Started)
                {
                    frame.Text(3,frame.Height-3,"Host LAN IP: "+Addresses()+$" | TCP port {port} | Q: Exit");
                    if(room!=null)
                    {
                        int left=32+Math.Max(0,(frame.Width-Math.Min(172,frame.Width))/2),top=Math.Max(0,(frame.Height-Math.Min(46,frame.Height))/2);
                        frame.Text(left+3,top+23,"ONLINE INVITE (F9: Save complete code)",ConsoleColor.Cyan);
                        int codeWidth=Math.Max(20,Math.Min(140,frame.Width-32)-6);string code=room.Invite.Code;
                        for(int row=0;row<5&&row*codeWidth<code.Length;row++)frame.Text(left+3,top+24+row,code.Substring(row*codeWidth,Math.Min(codeWidth,code.Length-row*codeWidth)),ConsoleColor.Cyan);
                        frame.Wrapped(left+3,top+30,$"Share the complete code with your players. Forward TCP {port} to this PC (or use a shared VPN). Hosting stays on this computer.",Math.Min(140,frame.Width-32)-6,4);
                        frame.Text(left+3,top+34,"F9: Save invite to data/online-invite.txt");
                    }
                }
                try{frame.Flush();}catch(ArgumentOutOfRangeException){continue;}
                foreach(var peer in peers.Where(p=>p.Value>=0)) {var member=match.Members.Single(m=>m.Id==peer.Value);peer.Key.Send(LanPeer.Frame(match.Render(member.Id,member.Width,member.Height,DateTimeOffset.UtcNow),member.Id,match.Revision));}
            }
        }
        finally{foreach(var peer in peers.Keys){peer.Send(new("error",Error:"Host closed the match."));peer.Dispose();}foreach(var handshake in pending){handshake.Key.Dispose();_ = handshake.Value.ContinueWith(t=>{if(t.IsCompletedSuccessfully)t.Result.Dispose();else _ = t.Exception;});}listener.Stop();input.DiscardPending();}
    }
    static string Addresses(){try{return string.Join(", ",Dns.GetHostAddresses(Dns.GetHostName()).Where(ip=>ip.AddressFamily==AddressFamily.InterNetwork&&!IPAddress.IsLoopback(ip)));}catch(SocketException){return "check Windows network settings";}}
    static void Join(string address,PlayerIdentity profile,int team,TerminalInput input,int teamSize,PlayMode mode,bool online)
    {
        OnlineInvite? invite=online?OnlineInvite.Parse(address):null;LanPeer connection;
        if(invite!=null)connection=OnlineTransport.Connect(invite).GetAwaiter().GetResult();
        else {var endpoint=OnlineInvite.Endpoint(address);var client=new TcpClient();try{client.ConnectAsync(endpoint.Host,endpoint.Port).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();connection=new LanPeer(client);}catch{client.Dispose();throw;}}
        using var peer=connection;peer.Send(new("hello",profile,team,Width:Console.WindowWidth,Height:Console.WindowHeight,TeamSize:teamSize,RoomToken:invite?.Token,Mode:mode));int revision=-1,lastWidth=Console.WindowWidth,lastHeight=Console.WindowHeight;Canvas? frame=null;DateTimeOffset lastFrame=DateTimeOffset.UtcNow;
        while(!peer.Closed)
        {
            while(peer.Inbox.TryDequeue(out var packet))
            {
                if(packet.Type=="error")throw new IOException(packet.Error);
                if(packet.Type!="frame")continue;
                if(packet.Revision!=revision){input.DiscardPending();revision=packet.Revision;}
                frame=LanPeer.Canvas(packet);lastFrame=DateTimeOffset.UtcNow;
                if(frame.Width<=Console.WindowWidth&&frame.Height<=Console.WindowHeight){try{frame.Flush();}catch(ArgumentOutOfRangeException){}}
            }
            if(DateTimeOffset.UtcNow-lastFrame>TimeSpan.FromSeconds(15))throw new IOException("Connection timed out.");
            if(lastWidth!=Console.WindowWidth||lastHeight!=Console.WindowHeight){lastWidth=Console.WindowWidth;lastHeight=Console.WindowHeight;peer.Send(new("resize",Width:lastWidth,Height:lastHeight));}
            Input key=input.Read(30);if(key.Key==ConsoleKey.Q)return;
            if(key.Key!=ConsoleKey.NoName||key.X>=0)peer.Send(new("input",Key:(int)key.Key,X:key.X,Y:key.Y,Width:Console.WindowWidth,Height:Console.WindowHeight,Revision:revision));
            if(frame==null){Console.SetCursorPosition(0,0);Console.Write("Connected! Loading your team lobby... Q: Leave");}
        }
        throw new IOException("Disconnected from the host.");
    }
}

