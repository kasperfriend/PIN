# Accounts & Login — `accounts.json` Guide

> The account system replaced the hardcoded "everyone is account
> `0x1122334455667788`" login: credentials are now stored per account
> (`accounts.json`), the login is verified against the original Red5 signature
> scheme the client uses, and new accounts can be created from the client's
> account creation form.

---

## 1. Overview

* Every player needs an account to log in. The first run seeds the built-in
  **`admin` / `admin`** account.
* The client never sends the password. It derives a per-account **uid** from the
  email and a **secret** from email + password, and signs every web request with
  that secret (`X-Red5-Signature`). The server stores the same uid and secret and
  verifies the signature — a wrong password means a wrong signature, so the login
  fails with the original client error `ERR_INCORRECT_USERPASS`.
* New accounts are created with `POST api/v2/accounts` (the client's account
  creation form posts email/password with confirmations, birthday, country...).
  Failures use the original client error codes (`ERR_ACCOUNT_EXISTS`,
  `ERR_EMAIL_MISMATCH`, `ERR_PASSWORD_MISMATCH`, ...) so the client shows the
  proper localized message.
* Characters belong to accounts: the character selection screen shows only the
  logged-in account's entries, and **accounts start fresh** — a new account owns
  no characters until you create one in-game (see §7). The 38 zone-picker seed
  entries (`characters.json`, see
  [`CHARACTERS_AND_BATTLEFRAMES.md`](CHARACTERS_AND_BATTLEFRAMES.md)) belong to
  the built-in **admin** account only: they are the operator's dev tool for
  jumping into any zone straight from the selection screen. (An earlier build
  copied them to every account on first use, which read as "every new account
  already has the admin's characters"; on load, PIN prunes those stale copies —
  created characters are never touched.)

Implementation:

* `Lib/Shared.Common/Accounts/Red5Auth.cs` — the Red5 signature scheme
* `Lib/Shared.Common/Accounts/Red5Signature.cs` — `X-Red5-Signature` header parsing
* `Lib/Shared.Common/Accounts/AccountStore.cs` — the `accounts.json` store
* `Lib/Shared.Common/Accounts/AccountRecord.cs` — a stored account
* `Lib/Shared.Common/Characters/CharacterResolver.cs` — per-account character resolution
* `Lib/Shared.Common/Characters/CharacterCreation.cs` — character creation and name rules
* `WebHosts/WebHost.ClientApi/Accounts/AccountsController.cs` — login / create / status
* `WebHosts/WebHost.ClientApi/Characters/CharactersRepository.cs` — per-account character list
* Tests: `UdpHosts/GameServer.Tests/Red5AuthTests.cs`, `AccountStoreTests.cs`, `CharacterStoreTests.cs`, `CharacterCreationTests.cs`

---

## 2. File Location

Default:

```
<WebHostManager binary folder>/accounts.json
# e.g. WebHosts/WebHostManager/bin/Release/net10.0/accounts.json
# In release zip: next to WebHostManager.exe
```

Custom path via `WebHosts/WebHostManager/config/appsettings.json`:

```json
{
  "Firefall": {
    "Accounts": {
      "AccountStorePath": "C:\\PIN\\data\\accounts.json"
    }
  }
}
```

* Empty `AccountStorePath` = default location.
* The file is created and seeded on first run.
* Writes are atomic (`accounts.json.tmp` + move), so a crash cannot truncate it.
* An unreadable/corrupt file is moved to `accounts.json.corrupt` and the store
  reseeds — the servers never fail to boot over it.

Environment variable override works too (standard ASP.NET config):

```
Firefall__Accounts__AccountStorePath=/opt/pin/accounts.json
```

---

## 3. Logging In

* Start the servers, launch Firefall, and enter the **email and password** of an
  account — `admin` / `admin` for the seeded one, or an account you created.
* The password is never transmitted: the client signs the request
  (`X-Red5-Signature`) with a secret derived from email + password, and the
  server verifies that signature against the stored secret of the account the
  uid belongs to.
* A wrong email or password returns the original
  `{"code": "ERR_INCORRECT_USERPASS", ...}` error (HTTP 500), which the client
  displays as its usual "check your username and password" message.
* **Steam auto-login**: a Steam-launched client signs its requests with an
  opaque **Steam session ticket** instead of derived credentials (the uid in
  the signature is a ~234-byte blob, not the 28-character uid of an email).
  PIN provisions an account for that ticket automatically on first sight —
  keyed to the Steam account embedded in it, so the same Steam user keeps
  their characters across sessions — and logs it at `Warning`:

  ```
  [.. WRN] Provisioned account 26294423 (steam-76561198143720022@pin.local) for the
           client's opaque login ticket (Steam account 76561198143720022): ...
  ```

  A `TicketAuth` account has no password (the ticket's signature can only be
  verified with a Steam backend, which PIN does not have: the ticket *is* the
  credential). Typed logins of named accounts work exactly as before — they
  are what `admin`/`admin` and `POST api/v2/accounts` accounts are for.

### The Red5 signature scheme (authentic behaviour)

The scheme is the one reverse engineered by the themeldingwars community
(FauFau's `Auth`/`Red5Sig`, also used by the RIN.WebAPI server for the same
client build). PIN reimplements it byte-for-byte; `Red5AuthTests` pins it
against vectors captured from the real client:

```
uid    = base64(SHA1(lowercase(email) + "-red5salt-2239nknn234j290j09rjdj28fh8fnj234k"))
secret = hex(SHA1(...)) of "lowercase(email)-password-red5salt-7nc9bsj4j734ughb8r8dhb8938h8by987c4f7h47b",
           re-hashed 200 times (client protocol v2)
token  = HMAC-SHA1(secret zero-padded to 64 bytes, headerData)   // hex
header = "Red5 <token> ver=2&tc=<unix>&nonce=<hex>&uid=<urlencoded uid>&host=...&path=...&hbody=...&cid=0 <token2>"
```

Only `lowercase(email)` folding and the fixed salts are special: the email is
lowercased byte-wise (ASCII `A-Z` only), and both salts are baked into the
client binary. Because uid and secret are pure functions of email and password,
the server never needs the plaintext password after creation.

---

## 4. `accounts.json` Structure

A JSON list of accounts (written by `AccountStore`, sorted by id):

```json
[
  {
    "AccountId": 26294422,
    "Email": "admin",
    "Uid": "DbrG3K3fyUO2EAhdmfSAe4P1ZTM=",
    "Secret": "5cf5b41968c00b17daf1a81a709e41a1d9bf0c55",
    "PasswordHash": "<base64 PBKDF2>",
    "IsDev": false,
    "IsAdmin": true,
    "CharacterLimit": 40,
    "Language": "en",
    "Country": null,
    "Birthday": null,
    "EmailOptIn": false,
    "ReferralKey": null,
    "CreatedAt": "2013-01-17T18:21:35Z",
    "LastLoginAt": "2026-09-10T12:00:00Z"
  }
]
```

### `AccountRecord` fields

| Field | Type | Description |
|-------|------|-------------|
| `AccountId` | ulong | Numeric account id, returned to the client as `account_id`. New accounts count up from 26294423. |
| `Email` | string | Email/username, ASCII-lowercased and trimmed at creation |
| `Uid` | string | Red5 uid derived from the email — the lookup key of every signed request |
| `Secret` | string | Red5 auth secret derived from email + password — verifies request signatures |
| `PasswordHash` | string | `base64(16 salt bytes + 32 PBKDF2-HMACSHA256 bytes)` of the password, for direct verification |
| `IsDev` | bool | Reports `is_dev` to the client |
| `IsAdmin` | bool | Marks the built-in seeded account (also the fallback for unauthenticated API calls) |
| `TicketAuth` | bool | The account was provisioned from an opaque client login ticket (a Steam session ticket) instead of email + password; its signatures are not verified (see §3) |
| `CharacterLimit` | int | Character slots reported on login (40; the admin account's zone-picker entries count against it) |
| `Language` | string | UI language chosen via `api/v2/accounts/change_language` |
| `Country` / `Birthday` / `EmailOptIn` / `ReferralKey` | mixed | Values from the account creation form |
| `CreatedAt` | DateTime | Account creation |
| `LastLoginAt` | DateTime? | Last successful login |

### Adding an account by hand

Prefer the in-game creation form (or the API below). If you must edit the file
by hand, the `Uid` and `Secret` have to match what the client derives from the
credentials — with the scheme above, e.g. for `player@example.com` / `hunter2`:

```
Uid    = base64(SHA1("player@example.com" + UserIdSalt))
Secret = 200-round SHA1 chain of "player@example.com-hunter2" + UserAuthSalt, hex
```

Simplest alternative: create a throwaway account through the form and rename
its `Email`/`Uid`/`Secret` afterwards is *not* equivalent — the derived values
change with the email, so just use the form.

---

## 5. Creating accounts

### In the client

The client's account creation form posts to `POST /api/v2/accounts`:

```json
{
  "email": "player@example.com",
  "password": "hunter2",
  "birthday": "1990-01-01",
  "country": "US",
  "email_optin": false,
  "referral_key": "",
  "steam_session_ticket": null,
  "steam_user_id": null,
  "steam_cdkey": null
}
```

> **The client sends no `confirm_email` / `confirm_password`** — it checks its
> two confirmation boxes in its own UI and posts the email/password pair only
> (the request shape the reference implementation the client was reverse
> engineered against stores: RIN.WebAPI's `CreateAccountReq`). Those two fields
> are *optional* here: when a caller does send them they are validated against
> their partner field, but a missing one is never an error. Requiring them used
> to reject every creation with `ERR_EMAIL_MISMATCH` (HTTP 500) before an
> account was stored, which left the client frozen in its post-create login
> loop.

The endpoint is served by **both** hosts the client may post it to: the ClientApi
host (`https://localhost:44302`, where the rest of the client API lives) and the
catch-all host (`https://localhost:44399`), which is what the WebHostManager's
operator capability response advertises as the client's **WebAccounts** service.
That matters: the catch-all answers every request it does not implement with an
empty `200`, so before it served this endpoint a creation form posted there was
read by the client as "account created" — and the login that follows failed with
`ERR_INCORRECT_USERPASS` because no account had been stored. For the same reason
the catch-all host serves a creation for **any** POST whose path ends in
`accounts` (`AccountCreation.IsCreationPath`) rather than only the literal
`api/v2/accounts`: which prefix the client addresses that service with cannot be
pinned down from the server side, and the empty `200` it would otherwise answer
with is the one reply the client cannot recover from.

The body is read by hand (`Shared.Web.AccountCreationEndpoint`), never by
`[FromBody]` model binding, so no content type the client picks can make the
framework answer `415`/`400` with a body the client cannot parse. JSON is read
first and a `application/x-www-form-urlencoded` body
(`email=...&password=...`) second; `email_optin` binds whether it arrives as a
boolean, a string or a number. Whatever the request looks like the answer is
JSON — `{}` on success, `{"code": ..., "message": ...}` with HTTP 500 on
failure — and an unexpected exception inside the endpoint is caught and
answered the same way, so the client is never left waiting.

Every attempt is logged at **`Warning`**, the level the WebHostManager shows by
default, with the request it arrived with and its outcome:

```
[.. WRN] Account creation request POST /api/v2/accounts on localhost:44302 (application/json, 178 characters): {"email":"player@example.com","password": "***",...}
[.. WRN] Created account 26294423 (player@example.com)
[.. WRN] Rejected the account creation request: ERR_ACCOUNT_EXISTS (An account with this email already exists)
```

Passwords are redacted from those lines by `AccountCreation.RedactForLog` — PIN
stores no plaintext password and does not log one either.

On success the account is stored (`200` with the empty object `{}` the original
service returned) and is immediately playable; on failure the client shows the
localized message for the returned error code. Validation rules (mirroring the
original service):

* email required (`ERR_NO_EMAIL`), must be unused (`ERR_ACCOUNT_EXISTS`), must
  not contain whitespace and must be at most 254 characters (`ERR_UNKNOWN`)
* password required (`ERR_PASSWORD_MISMATCH`)
* `confirm_email` / `confirm_password` are checked against their partner field
  only when they are actually sent (`ERR_EMAIL_MISMATCH` /
  `ERR_PASSWORD_MISMATCH`); emails are compared trimmed and ASCII-folded,
  passwords verbatim

`birthday`, `country`, `email_optin` and `referral_key` are stored as given; the
Steam fields of a Steam-linked creation are accepted and ignored (PIN has no
Steam backend).

### With curl (for a headless server)

```sh
curl -k -X POST https://localhost:44302/api/v2/accounts \
  -H "Content-Type: application/json" \
  -d '{"email":"player@example.com","password":"hunter2"}'
```

The confirmation fields are accepted here too, so this still works:

```sh
curl -k -X POST https://localhost:44302/api/v2/accounts \
  -H "Content-Type: application/json" \
  -d '{"email":"player@example.com","confirm_email":"player@example.com","password":"hunter2","confirm_password":"hunter2"}'
```

---

## 6. Characters per account

* Accounts start fresh: a new account owns **no** characters until it creates
  one (§7). Only the built-in admin account carries the 38 zone-picker seed
  entries — its copies of zone entries beyond "New Eden" are what make the
  selection screen double as a zone picker for an operator.
* A `characters.json` written by the build that seeded every account is
  migrated on load: the untouched seed copies of non-admin accounts are removed
  (and logged at `Warning`), so those accounts come back fresh. Created
  characters, the admin's entries and hand-added entries with non-seed names
  are never touched.
* `CharacterRecord` gained an `AccountId` field. Records from before the account
  system (no `AccountId` in the JSON) deserialize as the **admin** account's, so
  an existing `characters.json` keeps working unchanged.
* Guid scheme (see [`CHARACTERS_AND_BATTLEFRAMES.md`](CHARACTERS_AND_BATTLEFRAMES.md) §3):
  * admin/legacy, first slot: `0x99aabbccddee0000 + zoneId`
  * other accounts, first slot: `0xaa00000000000000 | (accountId << 16) | zoneId`
  * every account, slots above the first: `0xaa00000000000000 | (slot << 48) | (accountId << 16) | zoneId`
    (`slot` = 1..255; what lets one account own several created characters)
* The zone id always stays in the low 16 bits, which the GameServer relies on
  when spawning the player into the selected zone.
* Character resolution for GameServer requests (login guid, session saves,
  battleframe saves) lives in `CharacterResolver`: exact guid, then a
  low-byte-clobbered guid match, then the account bits of the guid plus the
  exact zone id from the command payload, then the legacy admin zone fallback.
  The old "zone id only" lookup would have been ambiguous the moment two
  accounts own the same zone.

---

## 7. Creating characters

The client's character creation form works: after logging in, "create character"
posts to `POST api/v1/characters` with the name, starting battleframe
(`start_class_id`) and the head/voice/color choices. The created character:

* belongs to the logged-in account (identified through the same verified
  `X-Red5-Signature` as the login),
* starts at battleframe progression level 1 with the chosen chassis,
* **takes the account's next free slot in the spawn zone (New Eden)**: the first
  character replaces the admin account's untouched "New Eden" seed entry (an
  operator keeps the other 37 zone entries as zone teleports), and further
  characters get their own guids (`slot` bits 48..55), so an account can hold as
  many characters as its limit — not just one. Creating past the account's
  character limit fails with the original `ERR_DUPLICATE_CHARACTER` error,
* **wears its chosen chassis' own stock armor colors**: the record's
  `Visuals.Warpaint` is stamped at creation with the 7 colors the chassis
  ships with in the static database (the same lookup the GameServer uses in
  game, precomputed into `ChassisDefaultWarpaints`), so the new character
  looks like that frame out of the box — not like the admin account's
  hardcoded purple Raptor. (Earlier builds left the warpaint empty or copied
  the admin's paint, and the client renders either as its built-in purple
  default avatar, which is how new characters used to "duplicate" the admin's
  look. Existing stores are refreshed automatically at boot: created
  characters carrying the inherited purple or an empty warpaint are
  re-painted with their chassis' stock colors, and a Warning log line says how
  many were fixed.)
* appears in the character list the client re-fetches right after creation.

Name rules (`POST api/v1/characters/validate_name`, reported with the original
client error codes): 4–40 characters, ASCII letters/digits/spaces only, must not
start with a digit, and must not be taken by another character (names are
reserved **across accounts**; the built-in zone entries do not reserve their
zone names).

### Appearance — get what you select

The in-game character is rendered from the same record values the selection
screen preview uses, so what you pick is what you play as:

* **Battleframe armor**: the record's `Visuals.Warpaint` (the 7 packed
  light-dark colors, slots in order armor1-3 / bodysuit1-2 / glow1-2) is worn
  on the chassis in-game instead of the chassis' default SDB warpaint — a
  purple preview spawns as a purple battleframe, not the grey standard armor.
* **Hair**: the hair and facial-hair meshes are worn as the leading head
  accessories both on the selection screen and in-game (the original service's
  data model: `hair == head_accessories[0]`), and the body colors (skin, lip,
  eye, hair, facial hair) flow through unchanged.
* Switching battleframes in-game keeps your warpaint — it belongs to the
  character, not the chassis.

Appearance edited at a New You terminal is stored back on the record: the
screen's `visual_loadouts` save is applied to the character (with its posted
palette ids resolved to ARGB through the same precomputed
`CharacterColorPalettes` table creation uses), so the character logs in wearing
the look the player saved.

Known remaining limitations: warpaint *patterns* and decals from the record are
not yet applied in-game (colors only, which is what the default entries carry
anyway).

---

## 8. Playing with more than one person

The GameServer is built for several simultaneous players (each connection is its
own `NetworkPlayer` with its own zone, entities are scoped per player, movement
and chat are relayed between them), and the account system removes what used to
block it in practice:

* Before accounts, every login shared the same built-in character guids — two
  players picking the same zone entry collided on the same character id and the
  second connection was closed. Each account now owns its own characters with
  distinct guid ranges, so two players can be online in the **same zone at the
  same time** as two separate entities.
* Each player's `PlayerId` (used to address controller keyframes to a client) is
  now derived from their character guid instead of the one hardcoded value that
  every player used to share.
* Two characters of the **same** account still cannot be online simultaneously
  (the duplicate-login guard closes the second connection — standard MMO
  behaviour).
* PvP remains unimplemented (see the README's limitations); co-op play,
  NPC combat and chat work per the shard's existing systems.

---

## 9. Configuration Reference

`WebHosts/WebHostManager/config/appsettings.json`:

```json
{
  "Firefall": {
    "GameServerApi": {
      "Port": 5201,
      "CharacterStorePath": ""
    },
    "Accounts": {
      "AccountStorePath": ""
    }
  }
}
```

The GameServer is not involved in login verification: it still receives the
character over gRPC (`GetCharacterAndBattleframeVisuals`) after the web login
succeeded.

---

## 10. Troubleshooting

**Login always fails with `ERR_INCORRECT_USERPASS`**

* Wrong email or password (both produce the same error, by design).
* Check `accounts.json` exists next to WebHostManager and contains the account's
  `Uid`.
* If you deleted/renamed accounts by hand, remember `Uid`/`Secret` are derived
  from the exact email string (case-folded ASCII) and password.

**A new account cannot log in**

* Creation and login must use the same email spelling (case does not matter,
  surrounding spaces are trimmed on creation).
* The account must appear in `accounts.json` after creation.
* The WebHostManager console logs the whole flow at `Warning` — its default
  level, so nothing has to be reconfigured to see it:
  * `Account store <path> holds <n> account(s): <emails>` on start — which file
    is in play, and whether the account is in it.
  * `Account creation request POST <path> on <host> (<content type>, <n>
    characters): <body>` for every creation the server is handed, with the
    password redacted.
  * `Created account <id> (<email>)` or `Rejected the account creation request:
    <code> (<message>)`.
  * `Rejected a login: <reason> (uid <uid>)`, where the reason is
    `UnknownAccount` (no account with that email is stored — the creation never
    landed), `SignatureMismatch` (the account exists, the password is wrong),
    `MalformedSignature` or `MissingSignature`. A uid that is an opaque client
    ticket is summarized (`FAAAAG2yUnc70KSp... (234 bytes, Steam account
    76561198143720022)`) instead of dumped in full, and is provisioned for
    rather than rejected (see §3), so a `UnknownAccount` next to one means the
    provisioning was not reached — check for the `Provisioned account` line.
  * `Provisioned account <id> (<email>) for the client's opaque login ticket`
    or `Accepted the client's opaque login ticket ... for account <id>` — the
    Steam-launch flow working as intended.

**The client freezes when I press "Create"**

The client shows nothing for a creation it cannot make sense of, so a frozen
creation form is always a question about what the server answered — read the
`Warning` lines above:

* **`Rejected a login: UnknownAccount (uid FAAAA...)` and no creation request
  at all** — the login the client sends *before* creating anything was signed
  with an opaque ticket and rejected by a PIN older than the ticket support:
  the client's flow dies on that rejection and never sends the creation. With
  the current PIN this login provisions (or reuses) a `steam-...@pin.local`
  account and the flow proceeds — see §3.
* **No `Account creation request` line at all** — the client never reached a
  running host: check that WebHostManager is the build you just compiled (the
  `Account store <path> ...` line on start names the folder it is running
  from), that the client's `firefall.ini` still points at it
  (`OperatorHost = "localhost:4400"`), and that the capability response
  (`GET https://localhost:44300/check`) advertises hosts that are listening.
* **`Catch-all host swallowed POST <path> ...`** — the client posted its
  creation to a path whose last segment is not `accounts`; that line carries the
  body it sent, which is what the endpoint needs to be taught next.
* **`Rejected the account creation request: <code>`** — the client shows the
  localized message for that code; `ERR_UNKNOWN` with `No account data received`
  means the body carried neither JSON nor form fields the endpoint recognized
  (the logged body shows what it actually was).
* **`Created account ...` followed by `Rejected a login: UnknownAccount`** — the
  account was stored and then not found again, which means two different store
  files are in play (see the `Account store <path>` line of each start).

**Character list is empty / shows the admin's characters**

* An empty list is what a fresh account looks like: accounts start with no
  characters, and the client walks you into character creation from there. Only
  the admin account carries the 38 zone-picker seed entries.
* `api/v2/characters/list` identifies the account through the request's
  `X-Red5-Signature`; unauthenticated callers (curl, swagger) get the admin
  account's list — log in with the account in the client to see its own list.
* If a non-admin account still shows zone entries after upgrading: they are
  stale copies from the build that seeded every account. They are pruned on the
  next start (watch for `Removed ... zone-picker seed entries of non-admin
  accounts` at `Warning`) — or delete `characters.json` to reseed from scratch.

**Lost the admin password**

* Stop WebHostManager, delete `accounts.json` (and `characters.json` if you also
  want fresh character entries), start again — the `admin`/`admin` account is
  reseeded.

---

## 11. Security Notes

PIN is a local/LAN game-server emulator:

* The Red5 scheme is SHA1-based because the original client protocol requires
  it; it is protocol fidelity, not a security boundary.
* Plaintext passwords are never stored: only the derived Red5 secret and a
  PBKDF2-HMACSHA256 (10k iterations, 16-byte salt) hash.
* The login endpoint is intentionally not hardened against internet exposure —
  don't put a PIN server on the public internet.
