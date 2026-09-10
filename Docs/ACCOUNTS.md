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
  logged-in account's entries. Every account gets its own copy of the 38
  zone-picker seed entries (`characters.json`, see
  [`CHARACTERS_AND_BATTLEFRAMES.md`](CHARACTERS_AND_BATTLEFRAMES.md)), so the
  selection screen keeps working as a zone picker for every account.

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
* **Steam auto-login**: when the client skips the login screen via Steam it has
  no PIN credentials to sign with, so the login is rejected and the client falls
  back to the login form — enter account credentials there.

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
| `CharacterLimit` | int | Character slots reported on login (the zone-picker seed needs all 40) |
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
`ERR_INCORRECT_USERPASS` because no account had been stored.

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

* `CharacterRecord` gained an `AccountId` field. Records from before the account
  system (no `AccountId` in the JSON) deserialize as the **admin** account's, so
  an existing `characters.json` keeps working unchanged.
* Guid scheme (see [`CHARACTERS_AND_BATTLEFRAMES.md`](CHARACTERS_AND_BATTLEFRAMES.md) §3):
  * admin/legacy: `0x99aabbccddee0000 + zoneId`
  * other accounts: `0xaa00000000000000 | (accountId << 16) | zoneId`
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
* **takes over the account's zone-picker slot for the spawn zone (New Eden)**:
  the seed entry named "New Eden" is replaced by your named character — the
  other 37 zone entries stay as zone teleports. A second creation while a custom
  character already owns that slot fails with the original
  `ERR_DUPLICATE_CHARACTER` error,
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

Known remaining limitations: the creation form's color *choices* are stored as
the correct SDB item ids, but the ARGB color *values* the avatar renders with
stay at the default template's until appearance editing (NewYou) is served from
the static database; warpaint *patterns* and decals from the record are not yet
applied in-game (colors only, which is what the default entries carry anyway).

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
* The WebHostManager console logs every created account (`Created account
  <id> (<email>)`) and every rejection with its error code
  (`Rejected an account creation request: <code>`). Those lines are
  `Information`, so raise the Serilog `MinimumLevel` above its default
  `Warning` to see them.

**Character list is empty / shows the admin's characters**

* `api/v2/characters/list` identifies the account through the request's
  `X-Red5-Signature`; unauthenticated callers (curl, swagger) get the admin
  account's list — log in with the account in the client to see its own list.

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
