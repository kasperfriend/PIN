# PIN - Pirate Intelligence Network

The Accord may think their Shared Intelligence Network is unique and impenetrable, but not everyone agrees with their restricted access and constant surveillance. That's why PIN, the Pirate Intelligence Network, has been created.

*Fight the Accord - Kill the Chosen*

https://user-images.githubusercontent.com/920861/134824107-03e9f99c-b420-47c7-b742-efe68967161c.mp4

## Usage

**Note:** If you want to play around with the configuration, see the Development section below

1. Install Firefall via Steam (paste `steam://install/227700` into address bar of web browser)
2. Edit the `firefall.ini` located in `steamapps\common\Firefall`
3. Add content from below
4. Download and extract the [latest PIN release](https://github.com/themeldingwars/PIN/releases/latest) as a complete archive. Keep `GameServer.exe` and every adjacent file together in the extracted folder. `GameServer.exe` is a single-file build: if you see a `GameServer.dll` next to it, that folder holds an outdated release — delete it and re-extract the latest archive.
5. On its first launch, GameServer automatically finds a normal Steam Firefall installation and writes the paths to `GameServer.config.json`. If it cannot find your copy, set `StaticDBPath`, `MapsPath`, and `AssetDBPath` in that file before trying again; see [GameServer config](#gameserver-config).
6. Make a backup copy of the original `FirefallClient.exe` in `Firefall\system\bin`
7. Replace the `FirefallClient.exe` with the patched `FirefallClient.exe` from the PIN release
   - The patched client is **not built by CI** (it is an external binary). Attach it
     manually to the release's **Assets** on the GitHub release page, then download it
     from there. A release produced by CI contains the three servers only.
8. Make sure the [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) is installed
9. Trust self-signed development certificates by running `dotnet dev-certs https --trust`
   (that covers a server advertising `localhost`; if `Firefall:PublicHost` names an
   address instead, PIN issues its own certificate for it and
   `WebHostManager.exe --trust-cert` is the matching one-liner — see
   [Connecting with friends](#connecting-with-friends))
10. Start all three applications:
   - GameServer
   - MatrixServer
   - WebHostManager
11. Start Firefall
12. Login to the server:
    - Enter the email and password of a PIN account — the first run seeds the built-in **`admin` / `admin`** account — or create a new account (see [Account system](#account-system))
    - A **Steam-launched client** signs with an opaque Steam session ticket instead of typed credentials: PIN then provisions a `steam-<steamid>@pin.local` account for that Steam user automatically (stable across sessions, no password) and the login continues (see [Accounts & Login](Docs/ACCOUNTS.md))
13. Load into the game by pressing the "Enter World" button

### Account system

Accounts are stored in `accounts.json`, created next to the `WebHostManager`
binary on first run and seeded with the built-in `admin`/`admin` account (the
account the login previously hardcoded). Login verifies the credentials the way
the original game did: the client signs every request with a secret derived
from email + password (the Red5 signature scheme), and the server checks that
signature against the stored account — a wrong password is a wrong signature,
so it fails with the client's usual `ERR_INCORRECT_USERPASS` error.

A **Steam-launched client** does not sign with typed credentials at all: its
login and account-creation flow opens with a request signed by an opaque Steam
session ticket (which embeds the SteamID64). PIN provisions an account for it
on first sight (`steam-<steamid>@pin.local`, reused for that Steam user
forever after, signatures not verified — the ticket is the credential), which
is also what un-freezes the client's "Create" button: the flow it starts with
that button is a ticket login, and a PIN that rejected it stopped the whole
creation before the form was ever sent.

New accounts can be created from the client's account creation form
(`POST api/v2/accounts`) or any HTTP client:

```sh
curl -k -X POST https://localhost:44302/api/v2/accounts \
  -H "Content-Type: application/json" \
  -d '{"email":"player@example.com","password":"hunter2"}'
```

The client posts the email/password pair only — it validates its own
confirmation boxes, so `confirm_email`/`confirm_password` are optional (they are
checked when a caller does send them). A body that is not JSON is read as a form
body, and the WebAccounts stand-in (the catch-all host) serves a creation on any
path ending in `accounts`.

Every step of the flow is logged at `Warning`, the level WebHostManager shows by
default: the creation request that arrived (password redacted), whether the
account was created or rejected and with which client error code, and — for a
rejected login — whether the account was unknown or the password wrong. See
[Docs/ACCOUNTS.md](Docs/ACCOUNTS.md) §10 for reading them.

Characters belong to accounts: the selection screen shows only the logged-in
account's characters, and **accounts start fresh** — no characters until you
create one in-game. The 38 zone-picker entries in `characters.json` are the
built-in admin account's dev tool for jumping into any zone from the selection
screen. Character creation works too — the client's creation form creates a
real, persisted character for the logged-in account (starting in New Eden), as
many as the account's limit allows, with the original name rules and error
codes. See [`Docs/ACCOUNTS.md`](Docs/ACCOUNTS.md) §7-§8 for both.

The store location is configurable:

```json
"Firefall": {
  "Accounts": {
    "AccountStorePath": ""
  }
}
```

Leave `AccountStorePath` empty to use the default location.

> **Full guide:** See [`Docs/ACCOUNTS.md`](Docs/ACCOUNTS.md) for the file
> format, the signature scheme, per-account characters, and troubleshooting
> (including how to reset a lost admin password).

### Character persistence

Characters are stored in `characters.json`, created next to the `WebHostManager`
binary on first run and seeded with the built-in entries. The same store backs
both the character selection screen and the GRPC `GameServerAPI` the GameServer
calls on login, so the character you pick is the character you spawn as.

The GRPC endpoint is hosted by `WebHostManager` on port `5201` (plain HTTP/2, no
TLS) and must match `GrpcChannelAddress` in the GameServer settings. Both are
configurable:

```json
"Firefall": {
  "GameServerApi": {
    "Port": 5201,
    "CharacterStorePath": ""
  }
}
```

Leave `CharacterStorePath` empty to use the default location. If `WebHostManager`
is not running, the GameServer logs a warning and falls back to a hardcoded
character.

> **Full guide:** See [`Docs/CHARACTERS_AND_BATTLEFRAMES.md`](Docs/CHARACTERS_AND_BATTLEFRAMES.md) for how to edit `characters.json`, change battleframes, add custom characters, configure visuals, and understand the GUID/zone scheme.

### GameServer config

`GameServer.config.json` sits next to `GameServer.exe` and holds the Firefall
installation paths:

```json
{
  "StaticDBPath": "C:\\Program Files\\Steam\\steamapps\\common\\Firefall\\system\\db\\clientdb.sd2",
  "MapsPath": "C:\\Program Files\\Steam\\steamapps\\common\\Firefall\\system\\maps",
  "AssetDBPath": "C:\\Program Files\\Steam\\steamapps\\common\\Firefall\\system\\assetdb",
  "CachePath": ""
}
```

`StaticDBPath` is required; the server will not start until it points at a
`clientdb.sd2` that exists. `MapsPath` and `AssetDBPath` are optional for a
minimal zone/collision setup but should point at the matching Firefall folders.

The server scans for a Firefall client installation on startup and writes the
detected paths into `GameServer.config.json` automatically when they are empty.
It looks in the Steam install location (read from the Windows registry, or the
default `Program Files` folders) and in every library listed in
`libraryfolders.vdf`, then in common standalone install folders, and finally
around the server executable/working directory. Any values you have set in the
file are kept as-is, and empty values never override paths configured in
`App.config`. If you want to point the server at a specific copy, set
`PIN_FIREFALL_PATH` to the Firefall install directory (or `PIN_STEAM_PATH` to
the Steam directory) before starting it.

After a local build it lands in
`UdpHosts\GameServer\bin\Release\net10.0\GameServer.config.json`. In the
GitHub release archive it is at the root of the zip (`Publish\`), next to
`GameServer.exe`. `GameServer.config.example.json` is shipped alongside it as
a fallback template.

The remaining settings (`Port`, `ZoneId`, `ClientVersion`, `GrpcChannelAddress`,
the `serilog:` logging keys, ...) still live in the XML `App.config`, which ships
next to `GameServer.exe` as `GameServer.dll.config`. GameServer parses that file
directly from disk, so editing it works the same way in a local build and in the
single-file release build.

### Connecting with friends

**You (the host)** — put the address friends reach you on (your RadminVPN,
Hamachi or LAN IP) into `config\appsettings.json` next to `WebHostManager.exe`,
and restart it:

```json
"Firefall": {
  "PublicHost": "26.11.22.33",
  "AdvertiseHttps": true
}
```

Keep `AdvertiseHttps` on: the client refuses a plain http oracle URL, so an
http-only server gets a friend as far as the character list and no further. There is
no certificate to make — PIN issues one for whatever address it advertises, keeps it
in `certs\` next to `WebHostManager.exe`, and serves the half players need at
`http://<your-address>:4400/certificate.cer`. Let the client on *this* machine trust
it with `WebHostManager.exe --trust-cert` (once; it writes your user's certificate
store, `--machine` the local machine's), then click **Allow access** — with *both*
Private and Public ticked — when Windows Defender Firewall asks about
WebHostManager, MatrixServer and GameServer. That is the whole host setup: no
port forwarding, no router changes, the VPN tunnels through NAT by itself.

**Your friends** — take `certificate.cer` off the host once (`certutil -addstore -f
Root pin.cer` in an elevated shell) and put that same address in their own
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

…plus the patched `FirefallClient.exe` and an account of their own, created on
first launch (or with `POST api/v2/accounts`). Nothing else: every other address
— the API hosts, the MatrixServer, and the GameServer behind it — is handed to
their client by your server, because `PublicHost` is what the capability response
and the oracle ticket advertise. The client finds the game server through the
MatrixServer, so there is no second address to configure.

> **Full guide:** [`Docs/REMOTE_PLAY.md`](Docs/REMOTE_PLAY.md) — what binds where,
> the firewall and port table, how PIN's own certificate works and how a player
> trusts it, `curl` checks that prove the setup, and troubleshooting
> (including the MTU trap over a VPN tunnel).

### Troubleshooting

**`unable to locate server. Oracle URL http://… not configured for HTTPS (request must be secure)`**

`Firefall:AdvertiseHttps` is `false`. Login and the character list run over plain
http, but the client will not ask an `http://` URL for the ticket that names its game
server, so **Enter World** is where it stops — and the log says so while the client is
still on the character screen. Set `AdvertiseHttps` to `true` and restart: PIN issues a
certificate for whatever address `PublicHost` names, and
`WebHostManager.exe --trust-cert` trusts it on the server machine
([`Docs/REMOTE_PLAY.md`](Docs/REMOTE_PLAY.md) §6).

**Login form flashes red at the username and password, on a server that advertises an address**

The client is being handed `https://<address>:443xx` URLs and refuses the certificate
waiting there: the ASP.NET Core development certificate is issued for `localhost`, so
it does not validate for an address, and a remote player has no reason to trust a
self-signed certificate he was never given. With `AdvertiseHttps: true` PIN issues the
right certificate (`certs\pin-<host>.cer`); trust it on each machine that runs a
client — `--trust-cert` on the host, `certutil -addstore -f Root pin.cer` after
downloading `http://<address>:4400/certificate.cer` elsewhere. Playing on the server
machine alone: leave `PublicHost` at `localhost`, where
`dotnet dev-certs https --trust` is all there is to it.

If the login form flashes red **even after the certificate is trusted**, and
`curl -k https://<address>:44302/api/v1/oracle/ticket -X POST` also fails with
`schannel: failed to receive handshake` (on `localhost` too), the server was
serving TLS from a PEM-loaded key, which Schannel on Windows cannot sign a handshake
with — an older build's bug. Update PIN: the hosts now serve from the
`certs\pin-<host>.pfx` they issue, and an old `.key`/`.crt` pair is migrated into one
on first start (no certificate reinstall needed). See
[`Docs/REMOTE_PLAY.md`](Docs/REMOTE_PLAY.md) §8.

**`GameServer terminated: CodeBase is not supported on assemblies loaded from a single-file bundle`**

An outdated release. GameServer used to read `App.config` through
`ConfigurationManager`, which locates its file via `Assembly.CodeBase` - an API
that does not exist inside a single-file executable, so the server died on its
first settings lookup, before it ever opened `GameServer.config.json`. Setting
the Firefall paths by hand therefore changed nothing. Download the latest PIN
release (or build from source); no configuration change is needed.

**`StaticDBPath is not configured ...` / `StaticDB file not found at ...`**

The Firefall installation was not auto-detected, or the configured path is
wrong. Set `StaticDBPath` in `GameServer.config.json` to the full path of
`system\db\clientdb.sd2`, or point `PIN_FIREFALL_PATH` at the install directory.

### firefall.ini

```ini
[Config]
OperatorHost = "localhost:4400"

[FilePaths]
AssetStreamPath = "http://localhost:4401/AssetStream/%ENVMNEMONIC%-%BUILDNUM%/"
VTRemotePath = "http://localhost:4401/vtex/%ENVMNEMONIC%-%BUILDNUM%/static.vtex"

[UI]
PlayIntroMovie = false
```

This is per-player client configuration: when you play with others, every player
replaces `localhost` in his own `firefall.ini` with the address of the machine
running the servers — see [Connecting with friends](#connecting-with-friends).

### Features

- Loading into any zone (WebHostManager)
- Basic character movement, including jetpacks and gliders(in work now, trying to get fixed)
- Switch between battleframes with preconfigured loadouts
- Customize character appearance at NewYou terminals (the appearance loadout and the cosmetics catalogue are served by `WebHost.ClientApi`)
- Call down vehicles and some deployables
- Health, damage, shields, bleedout/death/respawn and fall damage (see `Docs/HEALTH_SYSTEM.md`)
- Projectile combat against spawned NPCs (`Docs/SPAWNING_AND_COMBAT.md`)
- Server side NPC AI: spawned mobs notice you, chase, shoot back, give up when they are dragged too far from their spawn point (see `Docs/NPC_AI.md`)

### Limitations

- NPC AI is a in works: basic agro pathfinding, animations, no projectiles and the per monster SDB behaviour trees are ignored
- Most of the UI doesn't work properly
- Most abilities are not fully working
- Vehicles only have physics if a player is driving it (client-side)
- No Encounters
- No PvP

## Development

1. Install Visual Studio or JetBrains Rider
   - Include the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) component or install it separately
2. Recursive clone the repository `git clone --recurse-submodules https://github.com/kasperfriend/PIN.git`
3. Build the solution
4. Edit `GameServer.config.json` produced by the build in `UdpHosts\GameServer\bin\Release\net10.0` (copy `GameServer.config.example.json` to `GameServer.config.json` if it is missing) and set `StaticDBPath`, `MapsPath`, and `AssetDBPath` to your Firefall installation.
5. Trust self-signed development certificates by running `dotnet dev-certs https --trust`
   (or `WebHostManager --trust-cert` once, for a server that advertises a LAN/VPN
   address: that is the certificate PIN issues for `Firefall:PublicHost`)
6. Start multiple targets at once
   - Visual Studio: Create a `Multiple Startup Projects` target that start WebHostManager, GameServer and MatrixServer
   - Rider: Create a `Compound` target that starts WebHostManager, GameServer and MatrixServer
7. Edit the `firefall.ini` located in `steamapps\common\Firefall`
8. Add content from above
9. Start Firefall

### Web Hosts

CatchAll (4499 / 44399) is used for now, until the specific APIs are implemented.

| Host       | HTTP | HTTPS | Catch All |
|------------|------|-------|-----------|
| Operator   | 4400 | 44300 | ❌        |
| WebAsset   | 4401 | 44301 | ✔️        |
| ClientApi  | 4402 | 44302 | ❌        |
| InGame     | 4403 | 44303 | ❌        |
| WebAccount | 4404 | 44304 | ✔️        |
| Frontend   | 4405 | 44305 | ✔️        |
| Store      | 4406 | 44306 | ✔️        |
| Chat       | 4407 | 44307 | ❌        |
| Replay     | 4408 | 44308 | ✔️        |
| Web        | 4409 | 44309 | ✔️        |
| Market     | 4410 | 44310 | ✔️        |
| RedHanded  | 4411 | 44311 | ✔️        |

### UDP Servers

| Host          | UDP   |
|---------------|-------|
| Matrix Server | 25000 |
| Game Server   | 25001 |

### Binding

Every Kestrel host listens on the addresses its `Firefall:WebHosts:<host>:urls`
entry in `WebHosts/WebHostManager/config/appsettings.json` names — `*` by
default, i.e. every interface (IPv4 and IPv6), so a host serves both a local
client on `localhost` and players reaching it over LAN or VPN. Both UDP servers
bind `IPAddress.Any` and always have. The GRPC port 5201 listens on every
interface too, but it is the GameServer's call into WebHostManager on the same
box and should stay closed to other machines.

Which address clients are *told* to use is a separate setting,
`Firefall:PublicHost` (`localhost` by default), together with
`Firefall:AdvertiseHttps` for the scheme — and the address that gets advertised
decides the TLS certificate the hosts serve, because a client validating
`https://26.11.22.33:44302` needs a certificate that names `26.11.22.33`. PIN
issues that one itself (`Firefall:Certificate`, `TlsCertificateStore`) and hands the
public half out over plain http, so the certificate is never the thing that stops a
setup — see [`Docs/REMOTE_PLAY.md`](Docs/REMOTE_PLAY.md).
