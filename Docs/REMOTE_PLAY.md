# Playing with others — LAN, RadminVPN, Hamachi, Tailscale

PIN was built to be played on the machine that runs it, but nothing in it is
loopback-only by design. This page explains the three things that decide whether a
second player can join — **what the servers bind to**, **what they tell the client to
connect to**, and **whether that client is willing to accept the certificate it meets
at the end of the address** — and walks through a RadminVPN setup end to end.

Everything here is configuration: since the `PublicHost` work, no code has to be
edited to let somebody else in. One caveat up front, because it costs people the most
time: the client insists that the URL it fetches a game server ticket from is `https`
— not PIN's taste, the shipped client's — so a remote server has to serve TLS, and
PIN issues the certificate for that itself and hands the half players need to them
([§6](#6-tls-pick-one)).

---

## 0. The short version (RadminVPN, two machines)

**On the host (the machine running the servers):**

1. Install [RadminVPN](https://www.radmin-vpn.com/), create a network, and read
   your VPN address from `ipconfig` → adapter `Radmin VPN` (typically `26.x.x.x`).
2. In `config\appsettings.json` next to `WebHostManager.exe`, set:
   ```json
   "Firefall": {
     "PublicHost": "26.11.22.33",
     "AdvertiseHttps": true
   }
   ```
   Keep `AdvertiseHttps` on. The client refuses an oracle URL that is not `https`
   ([§6.3](#63-option-c--plain-http-what-it-is-good-for-and-what-it-is-not)), and
   there is no certificate to make by hand: PIN issues one for whatever address it
   advertises and keeps it in `certs\` next to the binary.
3. Let the client on *this* machine trust it — one command, and it is the whole
   ceremony on the host:
   ```
   WebHostManager.exe --trust-cert
   ```
   It writes `certs\pin-26.11.22.33.cer` (the public half — safe to hand to anybody)
   into the Windows certificate store; `--machine` writes the local machine store
   instead of your user's and needs an elevated shell. The `.key` sitting next to it
   is the other half and is nobody's business.
4. Open the firewall for the three servers (see [§4.3](#43-firewall)):
   ```
   netsh advfirewall firewall add rule name="PIN" dir=in action=allow profile=any program="C:\PIN\WebHostManager.exe"
   netsh advfirewall firewall add rule name="PIN" dir=in action=allow profile=any program="C:\PIN\MatrixServer.exe"
   netsh advfirewall firewall add rule name="PIN" dir=in action=allow profile=any program="C:\PIN\GameServer.exe"
   ```
5. Start WebHostManager, MatrixServer and GameServer.

**On the player's machine:**

6. Install RadminVPN, join the same network, verify with `ping 26.11.22.33`.
7. Install Firefall via Steam, replace `system\bin\FirefallClient.exe` with the
   patched one from the PIN release. (No .NET runtime, no server files needed.)
8. Take the certificate off the host and trust it (elevated shell):
   ```
   curl -o pin.cer http://26.11.22.33:4400/certificate.cer
   certutil -addstore -f Root pin.cer
   ```
   Without this the client reaches the login form and blinks at it: it is being
   handed `https://26.11.22.33:443xx` URLs and has no reason to believe the
   certificate waiting there. The route is served in the clear on purpose — see
   [§6.1](#61-option-a--let-pin-issue-the-certificate-recommended).
9. Edit `steamapps\common\Firefall\firefall.ini`:
   ```ini
   [Config]
   OperatorHost = "26.11.22.33:4400"

   [FilePaths]
   AssetStreamPath = "http://26.11.22.33:4401/AssetStream/%ENVMNEMONIC%-%BUILDNUM%/"
   VTRemotePath = "http://26.11.22.33:4401/vtex/%ENVMNEMONIC%-%BUILDNUM%/static.vtex"

   [UI]
   PlayIntroMovie = false
   ```
   The ini stays on `http` and on the plain ports: both ports answer whatever they
   are asked for, only the *advertised* URLs have to be TLS.
10. Create an account and log in — see [§5.4](#54-account).

Check [§7](#7-verify-it-before-you-bother-your-friend) first if you want to be sure
the host is serving what you think it is.

---

## 1. How PIN decides addresses

There are two independent layers. Mixing them up is the usual reason a "it works
on my machine" server stays unreachable.

### 1.1 Binding — where the sockets listen

| Server | Ports | Bind address | Where it is set |
|--------|-------|--------------|-----------------|
| MatrixServer (UDP) | 25000 | `IPAddress.Any` — every interface | `Lib/Shared.Udp/PacketServer.cs:30` (hardcoded, and correct) |
| GameServer (UDP) | 25001 | `IPAddress.Any` — every interface | same code path |
| GRPC GameServerApi | 5201 | `ListenAnyIP` — every interface | `WebHosts/WebHost.GameServerApi/GameServerApiHost.cs` |
| The 9 Kestrel web hosts | 4400-4411, 4499, 44300-44311, 44399 | `*` — every interface (IPv4 `0.0.0.0` **and** IPv6 `[::]`) | `Firefall:WebHosts:<host>:urls` in `config/appsettings.json`, applied in `Lib/Shared.Web/BaseWebServer.cs` |

So the UDP servers were always reachable from anywhere that can route to the
machine; the web hosts used to be pinned to `localhost` and now bind every
interface by default. `*` is deliberately used instead of the literal `0.0.0.0`
so that `localhost` on the server box keeps working over IPv6 as well —
`0.0.0.0` binds IPv4 only. Use `0.0.0.0` if you want IPv4-only, or put
`localhost` back if you want the machine to serve nobody but itself.

Nothing has to be forwarded on a router for a VPN: RadminVPN, Hamachi, Tailscale
and ZeroTier all give you a flat virtual network with a real address per peer and
tunnel through NAT themselves. Port forwarding on your router is only needed if
you want players to connect over your **public** IP — which you should not do
(see [§9](#9-security)).

### 1.2 Advertising — what the client is told to connect to

This is the half that decides whether a remote player actually gets in. The
client learns every address from two responses:

* `GET /check` on the Operator host — `WebHosts/WebHost.OperatorApi/Capability/CapabilityRepository.cs`
  answers with eleven host URLs (clientapi, ingame, chat, store, market, frontend,
  web, replay, web assets, web accounts, rhsigscan).
* `POST /api/v1/oracle/ticket` on the ClientApi host —
  `WebHosts/WebHost.ClientApi/Oracle/OracleController.cs` answers with the
  **matrix address** plus `datacenter`, `hostname` and an operator override for
  the ingame/clientapi hosts.

Both used to hardcode `localhost`, which on the player's machine means *his own
PC*: the sockets were open, and the client was still sent home. They now build
their answers through `Lib/Shared.Web/Config/PublicUrls.cs`, which takes the
hostname from `Firefall:PublicHost` and each port from that host's configured
bind URLs — so the configuration stays the only source of truth for ports, and
the advertised scheme follows `Firefall:AdvertiseHttps`.

One of those eleven URLs is not interchangeable with the others: `clientapi_host`
is where the client asks for its oracle ticket, and the client *refuses* that one
unless it starts with `https` — `Oracle URL http://… not configured for HTTPS
(request must be secure)` at **Enter World**, which is a client policy, not a
server error. `Firefall:AdvertiseHttps = false` therefore does not mean "less
secure", it means "unreachable world" (see [§6.3](#63-option-c--plain-http-what-it-is-good-for-and-what-it-is-not)).
And because a client that dials an address has to be handed a certificate naming
that address, the scheme decision and `PublicHost` are not independent: the
certificate is derived from the advertised address by
`Lib/Shared.Common/Certificates/TlsCertificateStore.cs` —
[§6.1](#61-option-a--let-pin-issue-the-certificate-recommended).

**Why the oracle ticket's matrix address is the important one:** the
MatrixServer's `HUGG` reply (`UdpHosts/MatrixServer/Packets/MatrixPacketHugg.cs`,
sent from `UdpHosts/MatrixServer/MatrixServer.cs`) carries a sequence start and
the GameServer **port** — there is no IP field in the packet. The client dials
the GameServer at the address it used for the Matrix handshake. Setting
`PublicHost` therefore covers the UDP game connection as well; there is nothing
separate to configure for it.

### 1.3 The client's own two addresses

`firefall.ini` on **each player's machine** names the entry point
(`OperatorHost`) and the asset stream (`AssetStreamPath`, `VTRemotePath`). That
is client configuration, not server configuration — PIN cannot set it, so every
player edits his own copy ([§0](#0-the-short-version-radminvpn-two-machines) step 7).

---

## 2. Configuration reference

All of it lives in `config\appsettings.json` under the `Firefall` key:

* from a source build: `WebHosts\WebHostManager\bin\Release\net10.0\config\appsettings.json`
* from a release archive: `config\appsettings.json` next to `WebHostManager.exe`

| Key | Default | What it does |
|-----|---------|--------------|
| `PublicHost` | `localhost` | Hostname or IP handed to clients by `/check` and the oracle ticket. Set it to the address players reach you on (`26.11.22.33`, `192.168.1.50`, `pin.example.com`). |
| `AdvertiseHttps` | `true` | `true`: advertise `https://…`. `false`: advertise `http://…` — which the client accepts for everything except the oracle URL, so it stops a player at character selection. Keep it on; [§6](#6-tls-pick-one) is about the certificate. |
| `RedirectHttpToHttps` | `false` | Whether a request that arrives in the clear is bounced with a 307 to the host's TLS port. Off by default: both ports answer everything anyway, and a redirect a client will not follow just hides the http half. The `certificate.cer`/`certificate.pem` routes stay plain whatever this says. |
| `MatrixPort` | `25000` | UDP port advertised as the matrix address. Must match the MatrixServer's `Port`. |
| `Certificate:Path` | `""` | A `.pfx`/`.p12` to serve on the https endpoints instead of the one PIN issues. Its subject alternative name has to contain `PublicHost`, or every validating client refuses it. |
| `Certificate:Password` | `""` | Password of that `.pfx`, if it has one. |
| `Certificate:StorePath` | `""` | Where PIN keeps its own `pin-<host>.key`/`.crt`/`.cer`. Empty means `certs` next to `WebHostManager.exe`. |
| `Certificate:AutoIssue` | `true` | `false`: never create a key next to the binary, even for an advertised address the development certificate cannot cover — for a server whose TLS is arranged elsewhere. |
| `WebHosts:<host>:urls` | `https://*:443xx;http://*:44xx` | Kestrel bind addresses per host, `;`-separated. |

`WebHosts` is keyed by host namespace — `WebHost.OperatorApi`, `WebHost.WebAsset`,
`WebHost.ClientApi`, `WebHost.InGameApi`, `WebHost.WebAccount`, `WebHost.Frontend`,
`WebHost.Store`, `WebHost.Chat`, `WebHost.Replay`, `WebHost.Web`, `WebHost.Market`,
`WebHost.RedHanded`, `WebHost.CatchAll`. The lookup is not forgiving: rename or
delete an entry for a host that actually starts (Chat, CatchAll, ClientApi,
OperatorApi, InGameApi, Store, Replay, Market, WebAsset — see
`WebHosts/WebHostManager/Program.cs`) and that host dies during startup with a
`Host terminated unexpectedly` line.

WebHostManager adds environment variables to its configuration last, so they
override the file — a one-off setup needs no edit at all, and survives the build
overwriting `config\appsettings.json` (it is copied to the output with
`CopyToOutputDirectory=Always`, so in a source build an edit made to
`bin\…\config\appsettings.json` is lost on the next `dotnet build`; edit
`WebHosts/WebHostManager/config/appsettings.json` in the repo instead, that is
the file being copied):

```
set Firefall__PublicHost=26.11.22.33
set Firefall__AdvertiseHttps=true
WebHostManager.exe --trust-cert
WebHostManager.exe
```

(The first run of `--trust-cert` is also what creates `certs\pin-26.11.22.33.*`;
it resolves the certificate exactly the way the hosts do, installs the public half,
and exits. Players on other machines get that `.cer` from
`http://26.11.22.33:4400/certificate.cer` instead.)

Restart WebHostManager after changing any of this — the hosts are built once at
startup.

---

## 3. What the servers log when it is right

| Line | Meaning |
|------|---------|
| `Listening on 0.0.0.0:25000` / `0.0.0.0:25001` | MatrixServer / GameServer bound to every interface |
| `Now listening on: http://[::]:4400` (or `http://0.0.0.0:4400` without IPv6) | Kestrel bound to every interface |
| `Starting GRPC GameServerAPI on port 5201` | The internal character API is up |
| `Issued a TLS certificate for 26.11.22.33 (valid until …) into …\certs` | PIN made the certificate the advertised address needs, and says where the copy players install is |
| `Players have to trust PIN's own certificate …` | The one thing left to do: `--trust-cert` here, `certificate.cer` for everybody else |
| `Firefall:AdvertiseHttps is false, so the hosts advertise plain http …` | The configuration a player will blame on the network: the client refuses the http oracle URL, so the world is unreachable |

`[::]` is not a mistake — that is the IPv6 any-address, and it accepts IPv4 too.

---

## 4. Host setup

### 4.1 RadminVPN

1. Both machines install RadminVPN. One of you creates a network (name +
   password), the other joins it. Peers appear in the list once both are online.
2. `ipconfig` → the `Radmin VPN` adapter shows your address, normally in
   `26.0.0.0/8`. That address — not your `192.168.x.x` LAN address and not your
   public IP — is what goes into `PublicHost` and into every player's
   `firefall.ini`.
3. From the player's machine: `ping <your-26-address>`. If that fails, nothing
   else will work; reconnect the VPN and look at the adapter again (a
   `169.254.x.x` address means the tunnel never came up).
4. Windows usually classifies the Radmin adapter as a **Public** network, which
   is why the firewall rules below use `profile=any`.

RadminVPN tunnels peer-to-peer through NAT, so no router configuration is
involved. The same steps work for Hamachi (`25.x.x.x`), Tailscale (`100.x.x.x`)
or ZeroTier — only the address range differs. Strict corporate or campus networks
that block the VPN's own traffic are the one thing neither PIN nor this guide can
fix.

### 4.2 The config edit

```json
"Firefall": {
  "PublicHost": "26.11.22.33",
  "AdvertiseHttps": true,
  "MatrixPort": 25000,
  ...
}
```

Leave the `WebHosts` urls at `*`, and leave `Certificate` alone: PIN issues the
certificate for `26.11.22.33` on the first start and reuses it afterwards, so
`certs\pin-26.11.22.33.*` is created once and never again.

If your VPN address changes, `PublicHost` (plus the players' ini files) needs
updating, and PIN issues a *second* certificate for the new address next to the
first — the players of the old one keep trusting what they already installed, and
the new address needs its own `--trust-cert` / `certificate.cer`. Set a hostname
in `PublicHost` instead of an address and one certificate covers every renumbering.

### 4.3 Firewall

Per-program is the least error-prone — it covers every port each server uses and
follows you if you change them:

```
netsh advfirewall firewall add rule name="PIN WebHostManager" dir=in action=allow profile=any program="C:\PIN\WebHostManager.exe"
netsh advfirewall firewall add rule name="PIN MatrixServer" dir=in action=allow profile=any program="C:\PIN\MatrixServer.exe"
netsh advfirewall firewall add rule name="PIN GameServer" dir=in action=allow profile=any program="C:\PIN\GameServer.exe"
```

(Adjust the paths; run the shell as administrator. Remove with
`netsh advfirewall firewall delete rule name="PIN WebHostManager"`.)

Or by port, if you prefer:

| Ports | Protocol | Needed by remote players |
|-------|----------|--------------------------|
| 4400, 44300 | TCP | yes — Operator, the ini's `OperatorHost`, serves `/check` |
| 4401, 44301 | TCP | yes — WebAsset, the ini's asset stream |
| 4402, 44302 | TCP | yes — ClientApi: login, characters, oracle ticket |
| 4403, 44303 | TCP | yes — InGame |
| 4407, 44307 | TCP | yes — Chat (advertised by `/check`) |
| 4499, 44399 | TCP | yes — CatchAll: frontend, store, web, market, accounts, rhsigscan, replay |
| 25000 | UDP | yes — MatrixServer handshake |
| 25001 | UDP | yes — GameServer, the actual game traffic |
| 4404-4406, 4408-4411 (+443xx) | TCP | not today — those hosts are answered by CatchAll |
| 5201 | TCP | **no** — GRPC between GameServer and WebHostManager on the same box; keep it closed |

---

## 5. Player setup

### 5.1 Files

A player needs the game, not the server:

* Firefall via Steam (`steam://install/227700`)
* the patched `FirefallClient.exe` from the PIN release, replacing
  `Firefall\system\bin\FirefallClient.exe` (back up the original first)
* the [latest PIN release](https://github.com/themeldingwars/PIN/releases/latest)
  is **not** required on a player-only machine — no `GameServer.exe`, no .NET
  runtime

### 5.2 firefall.ini

`steamapps\common\Firefall\firefall.ini`:

```ini
[Config]
OperatorHost = "26.11.22.33:4400"

[FilePaths]
AssetStreamPath = "http://26.11.22.33:4401/AssetStream/%ENVMNEMONIC%-%BUILDNUM%/"
VTRemotePath = "http://26.11.22.33:4401/vtex/%ENVMNEMONIC%-%BUILDNUM%/static.vtex"

[UI]
PlayIntroMovie = false
```

Every `localhost` in the original snippet becomes the host's address — and these
two stay `http` on the plain ports. Both halves of every host answer what they are
asked for, so nothing in the ini has to become TLS; what has to be trusted is what
the *server* advertises, i.e. the `https://…:443xx` URLs in `/check` and the oracle
ticket, and the certificate behind them
([§6.1](#61-option-a--let-pin-issue-the-certificate-recommended)). Turning
`RedirectHttpToHttps` on makes these three lines bounce to their TLS ports instead —
which works once the certificate is trusted, and looks like a dead server before
that, which is why it is off.

### 5.3 Certificates

A server advertising `localhost` uses the ASP.NET Core development certificate and
needs `dotnet dev-certs https --trust` on that one machine — the README's setup, and
nothing else is required as long as nobody else connects.

A server advertising an address (LAN, VPN, hostname) serves the certificate PIN
issued *for that address* instead, because the development certificate names only
`localhost` and a client dialling `26.11.22.33` refuses it. Every machine that runs
a client has to trust that one — the host with `WebHostManager --trust-cert`, the
players with `certificate.cer` ([§0](#0-the-short-version-radminvpn-two-machines)
step 8). See [§6](#6-tls-pick-one).

### 5.4 Account

Accounts live in `accounts.json` **on the host** (`Docs/ACCOUNTS.md`). A player
either creates one from the client's creation form or from any HTTP client:

```sh
curl -X POST http://26.11.22.33:4402/api/v2/accounts \
  -H "Content-Type: application/json" \
  -d '{"email":"player@example.com","password":"hunter2"}'
```

The built-in `admin`/`admin` account works too, but two people sharing one
account is not what you want. Accounts start with no characters — each player
creates his own in-game. Two players can be online in the same zone at the same
time: characters are keyed per account and their guids live in distinct ranges
(`Docs/ACCOUNTS.md` §7).

---

## 6. TLS: pick one

The client is the one that decides this: it will not ask a `http://` URL for an
oracle ticket, so a server that advertises plain http leaves a player able to log
in, able to open the character list, and unable to enter the world. TLS it is —
the only question is whose certificate.

### 6.1 Option A — let PIN issue the certificate (recommended)

Nothing to configure. On the first start that advertises an address, PIN creates
`certs\pin-<host>.key` and `certs\pin-<host>.crt` next to `WebHostManager.exe`
(and a `pin-<host>.cer` for the players), and serves that on every https endpoint:
a self-signed certificate for the address in `Firefall:PublicHost`, in the same
shape as the ASP.NET Core development certificate — its own trust anchor, server
authentication, RSA 2048, five years — plus `localhost`, `127.0.0.1` and `::1` in
the subject alternative name, so one certificate also covers whatever a player's
own `firefall.ini` points at.

The code is `Lib/Shared.Common/Certificates/TlsCertificateStore.cs`; the rules it
follows are worth knowing because they are the rules your players' trust decisions
follow:

* **It is issued once.** The file on disk is the source of truth, so restarting the
  server does not invalidate a certificate a player installed. It is replaced when
  it stops naming the advertised address or when less than a month of its validity
  is left.
* **`PublicHost` decides the names.** Change the advertised address and you get a
  new file next to the old one, not a new version of it. (This is why a hostname in
  `PublicHost` is kinder than a VPN address: the certificate survives renumbering.)
* **Only the public half is shared.** The `.key` is written with owner-only
  permissions on Unix and is never served; `GET /certificate.cer` (and
  `/certificate.pem`) on the operator host return the DER/PEM of the certificate,
  which is the part a player has to install anyway.
* **Those two routes answer in the clear even with `RedirectHttpToHttps` on.**
  Fetching the thing that establishes trust cannot require the trust to already be
  there.
* **The client still has to be told to trust it** — on Windows, into "Trusted Root
  Certification Authorities". That is `WebHostManager --trust-cert` on this machine
  and `certutil -addstore -f Root pin.cer` on the others. Windows will not accept a
  name mismatch or an untrusted chain on an address, and a refused TLS connection is
  a login form that flashes red rather than a message about certificates.

### 6.2 Option B — serve a certificate you made yourself

For a certificate from your own CA, a wildcard name, or a longer lifetime than a
self-signed one gives you: create it with the advertised address in its SAN, point
`Firefall:Certificate:Path` (and `:Password`) at the `.pfx`, and PIN serves that
instead of issuing anything. `certs\` is then not written at all, and
`--trust-cert` refuses to touch the store for a certificate it did not make —
hand out the public half yourself.

**1. Create it on the host** (PowerShell as administrator; replace the IP):

```powershell
$cert = New-SelfSignedCertificate `
  -DnsName pin.local `
  -CertStoreLocation Cert:\LocalMachine\My `
  -KeyExportPolicy Exportable `
  -KeyUsage DigitalSignature, KeyEncipherment `
  -NotAfter (Get-Date).AddYears(5) `
  -TextExtension @(
    "2.5.29.37={text}1.3.6.1.5.5.7.3.1",
    "2.5.29.17={text}IPAddress=26.11.22.33&DNS=pin.local")

$pw = ConvertTo-SecureString -String "change-me" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath C:\PIN\pin.pfx -Password $pw
Export-Certificate    -Cert $cert -FilePath C:\PIN\pin.cer
```

The `2.5.29.17` extension is the SAN: `IPAddress=` is what makes a connection to
a bare IP valid, `DNS=` covers a hostname. `2.5.29.37` is the server
authentication EKU.

**2. Serve it** — in `config\appsettings.json`:

```json
"AdvertiseHttps": true,
"Certificate": {
  "Path": "C:\\PIN\\pin.pfx",
  "Password": "change-me"
}
```

An unreadable or missing file is logged (`Could not load the TLS certificate …`)
and the host falls back to the development certificate rather than refusing to
start; a certificate that does not name `PublicHost` is served with a warning
saying which clients will refuse it.

**3. Trust it on every player machine** — send them `pin.cer` (the public half;
never the `.pfx`) and have them run, as administrator:

```
certutil -addstore -f Root C:\path\to\pin.cer
```

or double-click → Install Certificate → Local Machine → place in **Trusted Root
Certification Authorities**.

### 6.3 Option C — plain http: what it is good for, and what it is not

```json
"AdvertiseHttps": false
```

Advertising `http://` is honest about what the client will accept for *most* of it:
`/check`, the account and character APIs, the asset stream and the catch-all hosts
all answer in the clear, which is why the http ports are what
`curl`/`Test-NetConnection` checks in [§7](#7-verify-it-before-you-bother-your-friend)
run against. It is not enough for the oracle: the client checks the URL it is about
to send its ticket request to and refuses an insecure one —

```
unable to locate server.
Oracle URL http://26.11.22.33:4402 not configured for HTTPS (request must be secure)
```

— so **Enter World** is where a plain http server stops, after a login that worked.
WebHostManager says this at start-up (`Firefall:AdvertiseHttps is false, so the
hosts advertise plain http …`) precisely so it is not diagnosed as a firewall
problem. Use the mode for probing the API from outside the machine, for a
reverse proxy that terminates TLS in front of PIN, or for a LAN you are willing to
read plaintext on up to the character selection screen; use [§6.1](#61-option-a--let-pin-issue-the-certificate-recommended)
for playing.

---

## 7. Verify it before you bother your friend

On the host:

```
netstat -ano | findstr ":4400 :44300 :25000 :25001"
```

Expect `0.0.0.0:` or `[::]:` — a `127.0.0.1:` line means that host is still
pinned to loopback.

From the **player's** machine, the requests that prove the whole chain:

```sh
# 1. What the client is told. Every host in this JSON must carry your 26.x address,
#    in the scheme the client expects for it - https, and 443xx ports.
curl http://26.11.22.33:4400/check?environment=production&build=1973

# 2. The certificate every client has to trust, and what it names.
curl -s -o pin.cer http://26.11.22.33:4400/certificate.cer
openssl x509 -in pin.cer -inform DER -noout -subject -enddate -ext subjectAltName

# 3. The oracle ticket: matrix_url must be "26.11.22.33:25000". Note the port - with
#    https advertised, 4402 is still listening and still answers, which is what makes
#    this a usable probe of a server a client cannot validate the TLS of.
curl -X POST http://26.11.22.33:4402/api/v1/oracle/ticket

# 4. The TLS half, and whether the name matches: without -k this fails until the
#    certificate above is trusted, which is exactly what the client sees.
curl -k -s https://26.11.22.33:44302/api/v1/oracle/ticket -X POST
```

(Keys come back snake_case — `clientapi_host`, `matrix_url` — because the hosts
serialize with a snake-case naming policy.) PowerShell equivalents for
reachability and for what the certificate actually claims:

```powershell
Test-NetConnection 26.11.22.33 -Port 4400
([Security.Cryptography.X509Certificates.X509Certificate2]"pin.cer").DnsNameList
```

If `/check` still says `localhost`, `PublicHost` was not picked up: wrong
`config\appsettings.json` (a release build reads the one next to
`WebHostManager.exe`, not the one in the repo), or WebHostManager was not
restarted. If it says `https://localhost:443xx` while `PublicHost` is right, the
`Firefall` section is being read from somewhere else (an environment variable wins
over the file). If `pin.cer` names an address nobody dials, the server has been
advertising something else since the certificate was first issued — the file is
reused on purpose, and a *new* advertised address gets a new file next to it.

---

## 8. Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Player cannot reach anything, `ping` works | Firewall, or the adapter is on the Public profile and the rules are Private-only | `profile=any` rules ([§4.3](#43-firewall)) |
| `Test-NetConnection` on 4400 fails but UDP works | That host is bound to `localhost` | `urls` must be `*`; check `netstat` |
| `/check` returns `localhost` URLs | `PublicHost` not read | Right `appsettings.json`, then restart ([§7](#7-verify-it-before-you-bother-your-friend)) |
| Login works, "Enter World" hangs | UDP 25000/25001 blocked, or `MatrixPort` ≠ the MatrixServer's `Port` | Open UDP; the GameServer port itself is handed out by the MatrixServer and is hardcoded to 25001 |
| `unable to locate server. Oracle URL http://… not configured for HTTPS (request must be secure)` | `AdvertiseHttps: false`: login and the character list work over http, the client refuses only the oracle URL, so it fails at **Enter World** | `AdvertiseHttps: true` and restart; the certificate is PIN's own ([§6.1](#61-option-a--let-pin-issue-the-certificate-recommended)) |
| Login form flashes red and says nothing, on a server that advertises an address | The client cannot validate the certificate at the advertised address: no root trust for PIN's own, or the dev certificate for a non-`localhost` address, or a configured `.pfx` whose SAN does not name it | Install `certificate.cer` (host: `WebHostManager --trust-cert`); with your own certificate, its SAN has to contain the advertised address ([§6.2](#62-option-b--serve-a-certificate-you-made-yourself)) |
| `Could not load the TLS certificate` / `does not name … in its subject alternative name` in the log | `Certificate:Path` points at a file nobody can read, or at a certificate for another address | Fix the path/password, or drop `Certificate:Path` and let PIN issue one |
| `Could not issue a TLS certificate … into …` in the log | The output folder is read-only, or the store path is not writable | `Certificate:StorePath` to a writable folder; nothing else about the server changes, only the fallback to the development certificate stays |
| Requests get redirected to `https://…:443xx` and fail | `RedirectHttpToHttps` is on, and the client in front of it does not follow a 307 it cannot validate | Turn it off — both ports answer everything regardless ([§2](#2-configuration-reference)) |
| In world, but no movement / rubber-banding / silent stalls | Path MTU: `PacketServer` sets `MTU = 1400` **and** `DontFragment = true`, so a datagram too big for the VPN tunnel is dropped instead of fragmented | Compare `netsh interface ipv4 show subinterfaces` with 1400 + tunnel overhead; lower `PacketServer.MTU` (`Lib/Shared.Udp/PacketServer.cs:14`) or raise the adapter MTU |
| Both players see the same character | Shared account | One account per player ([§5.4](#54-account)) |
| `ERR_INCORRECT_USERPASS` for a password that works locally | Accounts live on the host, not the player's machine | Create the account against the host ([§5.4](#54-account)) |
| Worked yesterday, broken today | VPN address changed | Re-read `ipconfig`, update `PublicHost` and every player's ini — and trust the new certificate, which is a new file next to the old one ([§7](#7-verify-it-before-you-bother-your-friend)) |
| Host serves the LAN but not the VPN peer | `PublicHost` is a LAN address the VPN peer cannot route to | Use the `26.x.x.x` address |

---

## 9. Security

Binding `*` means everything that can route to the machine can reach the API — on
a home network that includes every other device on your LAN, and on a VPN network
every member of that VPN. That is the point, and it is fine for a session between
people you know; it is not a service you should expose further:

* **Do not port-forward these ports to the internet.** PIN's login is not
  hardened against internet exposure (`Docs/ACCOUNTS.md` §11): the Red5 signature
  scheme is SHA1-based protocol fidelity, not a security boundary.
* With `AdvertiseHttps: false` the APIs are plaintext, including the login and the
  character list (the client only insists on TLS for the oracle URL). RadminVPN
  encrypts the tunnel, but other members of the same VPN network can read it.
* **PIN's own certificate is a LAN certificate.** Its private key is
  `certs\pin-<host>.key` next to the binary — owner-only permissions on Unix, and
  on Windows whatever the ACL of the folder you extracted to is, so put a server
  other people can log into somewhere they cannot read. Whoever holds that file can
  impersonate the server to every machine that installed the matching `.cer`;
  whoever holds only the `.cer` can do nothing at all, which is why it is the file
  the operator host serves. Neither is a defence against a VPN you do not control,
  and a self-signed root is not a substitute for a name you own: for anything beyond
  friends on a VPN, serve your own certificate ([§6.2](#62-option-b--serve-a-certificate-you-made-yourself)).
* Trust is per machine and per store: `--trust-cert` writes the current user's root
  store (or the local machine's with `--machine`), nothing else. PIN never installs a
  certificate on its own, never touches a configured certificate's store, and the
  routes that hand out the public half are the only ones that answer in the clear
  with `RedirectHttpToHttps` on.
* Keep the GRPC port 5201 closed to everything but the local machine.
* To go back to a machine-private server, set every `urls` entry back to
  `https://localhost:443xx;http://localhost:44xx` — `PublicHost` can stay as it
  is, since nothing outside can reach the sockets anyway.

---

## 10. Known limits

* **The GameServer port in the handshake is hardcoded.** `MatrixServer.cs` sends
  `HUGG` with port 25001 regardless of what `Port` the GameServer was configured
  with. Changing `Port` in `GameServer.dll.config` without touching the
  MatrixServer breaks remote joins (and local ones). Keep it at 25001.
* **`GrpcChannelAddress` stays `http://localhost:5201`.** That is correct while
  WebHostManager and GameServer run on the same box. Splitting them across
  machines means editing `GrpcChannelAddress` in `GameServer.dll.config` *and*
  opening 5201 — and 5201 has no authentication at all, so only do that inside a
  VPN you control.
* **No relay, no NAT punch-through of its own.** PIN relies on the VPN or on
  plain IP reachability; if the tunnel cannot be established, the servers cannot
  help.
* **The client's https requirement is in the client.** The refusal of a
  non-secure oracle URL is a check the shipped binary makes before it asks for a
  ticket, and PIN answers it the only way a server can: by serving TLS with a
  certificate that names the advertised address. There is no configuration that
  makes a plain http world reachable, and the trust step is per machine
  (`--trust-cert` here, the downloaded `.cer` there) — PIN will not write to a
  trust store on its own.
* **The zone is the one the GameServer was started with** (`ZoneId`, default 448)
  — every player lands in the same zone, which is also what makes a co-op session
  work without a zone browser.