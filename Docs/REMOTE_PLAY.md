# Playing with others — LAN, RadminVPN, Hamachi, Tailscale

PIN was built to be played on the machine that runs it, but nothing in it is
loopback-only by design. This page explains the two things that decide whether a
second player can join — **what the servers bind to** and **what they tell the
client to connect to** — and walks through a RadminVPN setup end to end.

Everything here is configuration: since the `PublicHost` work, no code has to be
edited to let somebody else in.

---

## 0. The short version (RadminVPN, two machines)

**On the host (the machine running the servers):**

1. Install [RadminVPN](https://www.radmin-vpn.com/), create a network, and read
   your VPN address from `ipconfig` → adapter `Radmin VPN` (typically `26.x.x.x`).
2. In `config\appsettings.json` next to `WebHostManager.exe`, set:
   ```json
   "Firefall": {
     "PublicHost": "26.11.22.33",
     "AdvertiseHttps": false
   }
   ```
3. Open the firewall for the three servers (see [§4.3](#43-firewall)):
   ```
   netsh advfirewall firewall add rule name="PIN" dir=in action=allow profile=any program="C:\PIN\WebHostManager.exe"
   netsh advfirewall firewall add rule name="PIN" dir=in action=allow profile=any program="C:\PIN\MatrixServer.exe"
   netsh advfirewall firewall add rule name="PIN" dir=in action=allow profile=any program="C:\PIN\GameServer.exe"
   ```
4. Start WebHostManager, MatrixServer and GameServer.

**On the player's machine:**

5. Install RadminVPN, join the same network, verify with `ping 26.11.22.33`.
6. Install Firefall via Steam, replace `system\bin\FirefallClient.exe` with the
   patched one from the PIN release. (No .NET runtime, no server files needed.)
7. Edit `steamapps\common\Firefall\firefall.ini`:
   ```ini
   [Config]
   OperatorHost = "26.11.22.33:4400"

   [FilePaths]
   AssetStreamPath = "http://26.11.22.33:4401/AssetStream/%ENVMNEMONIC%-%BUILDNUM%/"
   VTRemotePath = "http://26.11.22.33:4401/vtex/%ENVMNEMONIC%-%BUILDNUM%/static.vtex"

   [UI]
   PlayIntroMovie = false
   ```
8. Create an account and log in — see [§5.4](#54-account).

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
| `AdvertiseHttps` | `true` | `true`: advertise `https://…` and redirect plain requests to TLS. `false`: advertise `http://…` and stop redirecting, so no certificate is involved at all. |
| `MatrixPort` | `25000` | UDP port advertised as the matrix address. Must match the MatrixServer's `Port`. |
| `Certificate:Path` | `""` | Optional `.pfx`/`.p12` used for the https endpoints instead of the ASP.NET Core development certificate. Empty keeps the dev certificate. |
| `Certificate:Password` | `""` | Password of that `.pfx`, if it has one. |
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
set Firefall__AdvertiseHttps=false
WebHostManager.exe
```

Restart WebHostManager after changing any of this — the hosts are built once at
startup.

---

## 3. What the servers log when it is right

| Line | Meaning |
|------|---------|
| `Listening on 0.0.0.0:25000` / `0.0.0.0:25001` | MatrixServer / GameServer bound to every interface |
| `Now listening on: http://[::]:4400` (or `http://0.0.0.0:4400` without IPv6) | Kestrel bound to every interface |
| `Starting GRPC GameServerAPI on port 5201` | The internal character API is up |

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
  "AdvertiseHttps": false,
  "MatrixPort": 25000,
  ...
}
```

Leave the `WebHosts` urls at `*`. If your VPN address changes, only `PublicHost`
(and the players' ini files) need updating.

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

Every `localhost` in the original snippet becomes the host's address. With
`AdvertiseHttps: true` you can keep these http/4400-style URLs — the hosts
redirect to their TLS port — but then the certificate has to be trusted
([§6.2](#62-option-b--keep-https-and-use-a-certificate-that-matches-your-address)).

### 5.3 Certificates

With `AdvertiseHttps: false` there is nothing to install: PIN serves plain http
and never redirects. With https, the ASP.NET Core development certificate is
issued for `localhost` and trusted only on the host, so a remote client cannot
validate it — see [§6](#6-tls-pick-one).

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

### 6.1 Option A — plain http (`AdvertiseHttps: false`)

The recommended setup for a VPN between friends:

```json
"AdvertiseHttps": false
```

The http endpoints were always bound next to the https ones; this setting makes
PIN advertise them and switches off `UseHttpsRedirection`, which would otherwise
answer every plain request with a 307 to the TLS port — a redirect a remote
client cannot follow without trusting the development certificate. No
certificate, nothing to install on player machines, and the traffic still runs
inside RadminVPN's encrypted tunnel.

Flip it back to `true` and restart to return to https; nothing else changes.

### 6.2 Option B — keep https and use a certificate that matches your address

If you want TLS end to end, PIN has to serve a certificate whose subject or SAN
contains the address players dial, and every player has to trust it.

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
start.

**3. Trust it on every player machine** — send them `pin.cer` (the public half;
never the `.pfx`) and have them run, as administrator:

```
certutil -addstore -f Root C:\path\to\pin.cer
```

or double-click → Install Certificate → Local Machine → place in **Trusted Root
Certification Authorities**.

If your VPN address changes the certificate no longer matches; re-issue it, or
give the host a stable hostname the players use instead of the IP.

---

## 7. Verify it before you bother your friend

On the host:

```
netstat -ano | findstr ":4400 :44300 :25000 :25001"
```

Expect `0.0.0.0:` or `[::]:` — a `127.0.0.1:` line means that host is still
pinned to loopback.

From the **player's** machine, the two requests that prove the whole chain:

```sh
# 1. What the client is told. Every host in this JSON must carry your 26.x address.
curl http://26.11.22.33:4400/check?environment=production&build=1973

# 2. The oracle ticket: matrix_url must be "26.11.22.33:25000".
curl -X POST http://26.11.22.33:4402/api/v1/oracle/ticket
```

(Keys come back snake_case — `clientapi_host`, `matrix_url` — because the hosts
serialize with a snake-case naming policy. With https add `-k` and use the 443xx
ports.) PowerShell equivalent for reachability:
`Test-NetConnection 26.11.22.33 -Port 4400`.

If `/check` still says `localhost`, `PublicHost` was not picked up: wrong
`config\appsettings.json` (a release build reads the one next to
`WebHostManager.exe`, not the one in the repo), or WebHostManager was not
restarted.

---

## 8. Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Player cannot reach anything, `ping` works | Firewall, or the adapter is on the Public profile and the rules are Private-only | `profile=any` rules ([§4.3](#43-firewall)) |
| `Test-NetConnection` on 4400 fails but UDP works | That host is bound to `localhost` | `urls` must be `*`; check `netstat` |
| `/check` returns `localhost` URLs | `PublicHost` not read | Right `appsettings.json`, then restart ([§7](#7-verify-it-before-you-bother-your-friend)) |
| Login works, "Enter World" hangs | UDP 25000/25001 blocked, or `MatrixPort` ≠ the MatrixServer's `Port` | Open UDP; the GameServer port itself is handed out by the MatrixServer and is hardcoded to 25001 |
| TLS/certificate error in the client | Development certificate cannot validate against an IP | Option A ([§6.1](#61-option-a--plain-http-advertisehttps-false)) or Option B ([§6.2](#62-option-b--keep-https-and-use-a-certificate-that-matches-your-address)) |
| Requests get redirected to `https://…:443xx` and fail | `AdvertiseHttps` is still `true` while you meant to serve http | Set it to `false` and restart |
| In world, but no movement / rubber-banding / silent stalls | Path MTU: `PacketServer` sets `MTU = 1400` **and** `DontFragment = true`, so a datagram too big for the VPN tunnel is dropped instead of fragmented | Compare `netsh interface ipv4 show subinterfaces` with 1400 + tunnel overhead; lower `PacketServer.MTU` (`Lib/Shared.Udp/PacketServer.cs:14`) or raise the adapter MTU |
| Both players see the same character | Shared account | One account per player ([§5.4](#54-account)) |
| `ERR_INCORRECT_USERPASS` for a password that works locally | Accounts live on the host, not the player's machine | Create the account against the host ([§5.4](#54-account)) |
| Worked yesterday, broken today | VPN address changed | Re-read `ipconfig`, update `PublicHost` and every player's ini |
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
* With `AdvertiseHttps: false` the APIs are plaintext. RadminVPN encrypts the
  tunnel, but other members of the same VPN network can read it.
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
* **The zone is the one the GameServer was started with** (`ZoneId`, default 448)
  — every player lands in the same zone, which is also what makes a co-op session
  work without a zone browser.