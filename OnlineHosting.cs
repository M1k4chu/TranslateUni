using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;
using Org.BouncyCastle.X509;
namespace TranslateUni;

public sealed record OnlineInvite(string Host,int Port,string Token,string Fingerprint)
{
    public string Code=>new UriBuilder("translateuni",Host,Port,$"/{Token}/{Fingerprint}").Uri.AbsoluteUri;
    public static (string Host,int Port) Endpoint(string value)
    {
        if(string.IsNullOrWhiteSpace(value)||!Uri.TryCreate("tcp://"+value.Trim(),UriKind.Absolute,out var uri)||
            uri.Host.Length==0||uri.HostNameType==UriHostNameType.Unknown||uri.AbsolutePath!="/"||uri.Query.Length>0||uri.UserInfo.Length>0||uri.Fragment.Length>0)
            throw new ArgumentException("Enter the host's public IP / DNS name, optionally followed by :port.");
        int port=uri.Port<0?LanMultiplayer.Port:uri.Port;
        if(port is <1 or >65535)throw new ArgumentException("Port must be between 1 and 65535.");
        return(uri.Host.Trim('[',']'),port);
    }
    public static OnlineInvite Parse(string value)
    {
        if(!Uri.TryCreate(value.Trim(),UriKind.Absolute,out var uri)||uri.Scheme!="translateuni"||uri.UserInfo.Length>0||uri.Query.Length>0||uri.Fragment.Length>0)
            throw new ArgumentException("Paste the complete translateuni:// invite code from the host.");
        string[] parts=uri.AbsolutePath.Trim('/').Split('/');
        if(parts.Length!=2||parts[0].Length!=32||parts[1].Length!=64||parts.Any(p=>!p.All(Uri.IsHexDigit))||uri.Port is <1 or >65535||uri.Host.Length==0)
            throw new ArgumentException("The invite code is incomplete or invalid.");
        return new(uri.Host.Trim('[',']'),uri.Port,parts[0],parts[1]);
    }
    public bool Allows(string? token)
    {
        if(token==null||token.Length!=Token.Length)return false;
        return CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(token),System.Text.Encoding.UTF8.GetBytes(Token));
    }
}

public sealed class OnlineRoom : IDisposable
{
    public byte[] Certificate {get;}
    internal AsymmetricKeyParameter PrivateKey {get;private set;}
    public OnlineInvite Invite {get;}
    public OnlineRoom(string endpoint)
    {
        var target=OnlineInvite.Endpoint(endpoint);
        var random=new SecureRandom();var generator=new RsaKeyPairGenerator();generator.Init(new KeyGenerationParameters(random,2048));
        var pair=generator.GenerateKeyPair();PrivateKey=pair.Private;
        var cert=new X509V3CertificateGenerator();var name=new X509Name("CN=TranslateUni private room");
        cert.SetSerialNumber(BigInteger.ProbablePrime(128,random));cert.SetIssuerDN(name);cert.SetSubjectDN(name);
        cert.SetNotBefore(DateTime.UtcNow.AddMinutes(-5));cert.SetNotAfter(DateTime.UtcNow.AddDays(2));cert.SetPublicKey(pair.Public);
        cert.AddExtension(X509Extensions.BasicConstraints,true,new BasicConstraints(false));
        cert.AddExtension(X509Extensions.KeyUsage,true,new KeyUsage(KeyUsage.DigitalSignature));
        cert.AddExtension(X509Extensions.ExtendedKeyUsage,false,new ExtendedKeyUsage(KeyPurposeID.id_kp_serverAuth));
        Certificate=cert.Generate(new Asn1SignatureFactory("SHA256WITHRSA",pair.Private,random)).GetEncoded();
        Invite=new(target.Host,target.Port,Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),Convert.ToHexString(SHA256.HashData(Certificate)));
    }
    public void Dispose(){PrivateKey=null!;}
}

public static class OnlineTransport
{
    public static Task<LanPeer> Accept(TcpClient client,OnlineRoom room)=>Task.Run(()=>
    {
        var protocol=new TlsServerProtocol(client.GetStream());
        try
        {
            client.ReceiveTimeout=10000;client.SendTimeout=10000;
            using var timeout=new Timer(_=>client.Dispose(),null,10000,Timeout.Infinite);
            protocol.Accept(new RoomTlsServer(room));
            client.ReceiveTimeout=0;return new LanPeer(client,protocol.Stream,protocol.IsConnected,maxLineLength:4096);
        }
        catch{client.Dispose();throw;}
    });
    public static async Task<LanPeer> Connect(OnlineInvite invite)
    {
        var client=new TcpClient();var identity=new RoomTlsClient(invite);
        try
        {
            await client.ConnectAsync(invite.Host,invite.Port).WaitAsync(TimeSpan.FromSeconds(8));
            client.ReceiveTimeout=10000;client.SendTimeout=10000;
            using var timeout=new Timer(_=>client.Dispose(),null,10000,Timeout.Infinite);
            var protocol=new TlsClientProtocol(client.GetStream());
            await Task.Run(()=>protocol.Connect(identity));
            client.ReceiveTimeout=0;return new LanPeer(client,protocol.Stream,protocol.IsConnected);
        }
        catch(Exception ex)when(identity.CertificateMismatch){client.Dispose();throw new AuthenticationException("Server certificate does not match the invite.",ex);}
        catch{client.Dispose();throw;}
    }
    static readonly int[] Suites=[CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256,CipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384];
    sealed class RoomTlsServer(OnlineRoom room) : DefaultTlsServer(new BcTlsCrypto(new SecureRandom()))
    {
        protected override ProtocolVersion[] GetSupportedVersions()=>[ProtocolVersion.TLSv12];
        protected override int[] GetSupportedCipherSuites()=>Suites;
        public override int GetHandshakeTimeoutMillis()=>10000;
        protected override TlsCredentialedSigner GetRsaSignerCredentials()
        {
            var crypto=(BcTlsCrypto)Crypto;
            var chain=new Org.BouncyCastle.Tls.Certificate([crypto.CreateCertificate(room.Certificate)]);
            return new BcDefaultTlsCredentialedSigner(new TlsCryptoParameters(m_context),crypto,room.PrivateKey,chain,new SignatureAndHashAlgorithm(Org.BouncyCastle.Tls.HashAlgorithm.sha256,SignatureAlgorithm.rsa));
        }
    }
    sealed class RoomTlsClient(OnlineInvite invite) : DefaultTlsClient(new BcTlsCrypto(new SecureRandom()))
    {
        public bool CertificateMismatch;
        protected override ProtocolVersion[] GetSupportedVersions()=>[ProtocolVersion.TLSv12];
        protected override int[] GetSupportedCipherSuites()=>Suites;
        public override int GetHandshakeTimeoutMillis()=>10000;
        public override TlsAuthentication GetAuthentication()=>new Pin(this,invite.Fingerprint);
        sealed class Pin(RoomTlsClient owner,string fingerprint) : TlsAuthentication
        {
            public void NotifyServerCertificate(TlsServerCertificate server)
            {
                var chain=server.Certificate;
                if(chain.IsEmpty||!CryptographicOperations.FixedTimeEquals(SHA256.HashData(chain.GetCertificateAt(0).GetEncoded()),Convert.FromHexString(fingerprint)))
                {owner.CertificateMismatch=true;throw new TlsFatalAlert(AlertDescription.bad_certificate);}
            }
            public TlsCredentials GetClientCredentials(Org.BouncyCastle.Tls.CertificateRequest request)=>null!;
        }
    }
}
