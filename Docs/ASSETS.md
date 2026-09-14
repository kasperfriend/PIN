# Web assets — the asset stream and the high-resolution textures

Everything the Firefall client *streams* rather than *owns* comes from one place:
`WebHost.WebAsset`, port **4401** (http) / **44301** (https). Two lines in
`firefall.ini` point at it:

```ini
[FilePaths]
AssetStreamPath = "http://localhost:4401/AssetStream/%ENVMNEMONIC%-%BUILDNUM%/"
VTRemotePath    = "http://localhost:4401/vtex/%ENVMNEMONIC%-%BUILDNUM%/static.vtex"
```

Nothing about the assets is in the repository, on purpose: they are several
gigabytes of Red5's game data, and `.gitignore` keeps `Assets/` empty except for
a `.gitkeep` and the `README.md` that points here. A server that ships with an empty
asset folder is therefore *normal* — and it is also the reason the game looks the
way it does out of the box.

Related reading: [REMOTE_PLAY.md](REMOTE_PLAY.md) (which address to put in those
two lines when friends connect), [STATIC_DATABASE.md](STATIC_DATABASE.md) (the
*other* client-side file PIN reads, and it is read from disk, not streamed).

---

## 1. Why textures are blurry

Firefall's textures are a **virtual texture**: every surface in the world is
sampled out of one huge page table, and the sharp mips of that table are not in
the game's own archives. The install carries the low-resolution levels; the high
resolution levels live in three chunk files the client fetches from
`VTRemotePath` while you play.

`VTRemotePath` names a **base**, not a file: `…/vtex/<build>/static.vtex`. The
client appends a suffix to it for every part of the page table it wants, and the
suffixes are two kinds:

| Chunk | What it holds | Rough size (the community's numbers, not PIN's) |
|-------|---------------|------------------------------------------------|
| `static.vtex_idx` | the page-table index: which tile lives where | small |
| `static.vtex0` | the sharp mips, most terrain and props | ~9 GB |
| `static.vtex1` | the next level down | ~2 GB |
| `static.vtex2` | the level below that | ~0.9 GB |
| `static.vtex3` … `static.vtex6` | the coarsest levels — **these ship with the game** | small |

**Only the first three levels change how the game looks.** Levels `3` to `6`, and
the index, are files a stock install of Firefall already carries in its own
`system\vt`; the ~12 GB of `0`/`1`/`2` are what Red5 streamed from
`dl-production.firefallthegame.com/vtex/<build>/` instead, and what players of
2014 copied into their install to turn streaming off. A server holding the
second kind answers every probe the client makes and changes nothing on screen —
see [“But I already put the vtex files in Assets”](#but-i-already-put-the-vtex-files-in-assets),
which is the single most common way to end up here.

So the graphics options are not lying: "maxed out" controls the *streamer* —
how eagerly the client pulls tiles — and with nothing to stream it renders
blurry at every setting.

What the client does at startup is a probe, and the probe is what you see in the
`WebHostManager` log:

```
[12:54:06 ERR] NotFound: http://localhost:4401/vtex/prod-1962/static.vtex0 ;; HEAD ;;
[12:54:06 ERR] NotFound: ... static.vtex1 ...
[12:54:06 ERR] NotFound: ... static.vtex2 ...
```

`HEAD` requests answered with 404s: **no chunk file answers, so the client keeps
its low-resolution copy.** The `prod-1962` in the path is *your* client's build —
`%ENVMNEMONIC%-%BUILDNUM%` substituted — and not a PIN setting.

> Those three lines are the only "errors" here that are about the textures. The
> others people paste from the same log (`/api/v3/armies/1/ranks`,
> `/api/v3/trade/products/...`, `/api/v2/characters/.../market/listings`,
> `/api/v3/squad_builder/lfp`) are endpoints PIN has not implemented; they answer
> 404 through the catch-all host and cost you nothing but a UI tab.

### "But I already put the vtex files in Assets"

This is the trap, and it looks exactly like the empty-folder case from inside the
game. A stock Firefall install's `system\vt` is not empty — it holds
`static.vtex_idx` and `static.vtex3` … `static.vtex6`, the page table the game
ships with. Copy that folder into `Assets\`, restart, and:

```
asset root E:\PIN\Assets serves loose: static.vtex_idx (13.1 MB), static.vtex3 (…), static.vtex4 (…), static.vtex5 (…), static.vtex6 (…)
asset root E:\PIN\Assets holds none of static.vtex0, static.vtex1, static.vtex2 - those are the sharp mips no Firefall install carries, so a client fed from here is exactly as blurry as one fed from an empty folder
```

followed by

```
[WRN] Blurry textures ahead: every chunk file these roots hold is one the client's own install already carries - …
```

The host is not refusing to serve them: a chunk dropped loose at the top of a
root is found by name and served for whatever build the client asks for
(`static.vtex_idx` and every `static.vtexN` included). It is serving files the
client already has. Two symptoms follow from that, and both are expected:

- **No 404 in the log and no change in the game.** Every probe is answered; the
  answer is a mip level the client already holds.
- **`vt debug` shows no traffic from the server.** With no sharp level to fetch
  there is nothing to stream — the streamer has a page table and no destination.

The fix is to obtain `static.vtex0`, `static.vtex1` and `static.vtex2` for your
client's build. Red5's CDN (`dl-production.firefallthegame.com`) was shut down
with the game and no longer answers, so those three files now come from whoever
kept a copy: another player's `system\vt` folder (a machine that had the HD pack
downloaded before the shutdown), or a community re-upload. If you find them,
they are worth keeping — they are ~12 GB and there is no longer an official
source.

## 2. What PIN serves, and from where

`WebHost.WebAsset` is a static file host over an ordered list of **asset roots**:

| Order | Root | Where it comes from |
|-------|------|---------------------|
| 1 | `<content root>\Assets` | the folder next to `WebHostManager.exe` (`E:\PIN\Assets` in an extracted release); the build copies `WebHosts\WebHost.WebAsset\Assets` there |
| 2+ | every folder named in `Firefall:Assets:Paths` | `config\appsettings.json` next to `WebHostManager.exe`; relative paths anchor at the same content root |

A request is answered by the first root that holds the file, so `Assets` keeps
precedence and the rest is "more places to look" — which is the point: the
chunks do not have to be *copied* to the server to be served by it.

Two defaults inside that host decide whether a chunk on disk is a chunk the
client can load, and both are set in `WebHosts/WebHost.WebAsset/WebServer.cs`:

- **`ServeUnknownFileTypes = true`.** `.vtex0` is not a registered mime type, and
  the static-file middleware rejects an unknown type with a 404 *before it looks
  at the disk*. With the default (`false`) the folder can hold the files at
  exactly the right path and every probe still says NotFound — the most common
  reason "I put them there and nothing changed" is true.
- **Ranges, `ETag` and `Last-Modified`** come from the middleware itself: a
  client resuming a streamed tile needs the first, and it re-checks a chunk it
  cached against the other two. `HEAD` is answered with the real `Content-Length`,
  which is what the probe asks for.

On top of the literal paths there is one deliberate shortcut: a chunk request
`vtex/<build>/static.vtexN` that no root answers literally is retried by **file
name** — at the top of each root, then inside its `vtex` folder — because that is
where the chunks actually live in the wild (the client's own `system\vt` folder
holds them exactly that way, as does any folder you unpacked them into). So all
three placements answer the same request, in this order of precedence: a file at
the path the client named wins over a loose one, whichever root either of them is
in, and the fallback is a way to find the right file rather than a way to prefer
one folder over another:

| Put `static.vtex0` at | Answered |
|-----------------------|----------|
| `Assets\vtex\prod-1962\static.vtex0` | the literal reply, for that build |
| `Assets\static.vtex0` | by name, for **any** build the client names |
| `Assets\vtex\static.vtex0` | by name, for any build |

"Drop the three files in the `Assets` folder" is therefore a working instruction,
and that is the point: it is what a person who read *"put the vtex files in the
Assets folder"* does, and the alternative is a server that holds the textures and
still renders them blurry because of one directory in a path.

Anything that is not a chunk (`AssetStream/...`, a `.webm`, a `.json`) is only
ever answered by its literal path: the fallback is scoped to the chunk names the
client asks for — `static.vtex_idx` and every `static.vtexN` — so it cannot make
the host serve something nobody asked for by a path it invented.

**A sharp set is three files.** `static.vtex0` is the big one, but the client
probes all three sharp levels and each holds a different part of the page table,
so a root with one or two of them sharpens what they cover and leaves the rest
soft. The startup report says exactly that, naming what is missing:

```
asset root E:\PIN\Assets serves loose: static.vtex0 (8912.4 MB)
asset root E:\PIN\Assets has no static.vtex1, static.vtex2 - a client probes all three, so the rest of the page table stays at its cached resolution
```

## 3. Enabling the HD textures

Pick one. Both are answered by the same host, and the log line at startup says
which roots it found and what each holds.

### 3.1 Copy them next to the server

Three files, straight into the folder — nothing else in `Assets\` is required:

```
E:\PIN\Assets\static.vtex0
E:\PIN\Assets\static.vtex1
E:\PIN\Assets\static.vtex2
```

Under the build folder is the path the client actually asks for, and the answer
the log can match one-to-one:

```
E:\PIN\Assets\vtex\prod-1962\static.vtex0     (plus .vtex1, .vtex2) - the literal path
E:\PIN\Assets\vtex\static.vtex0               (the vtex folder, no build directory)
```

`Assets` (and `Assets\vtex`) are yours to create; nothing in them is tracked. Any
`WebHosts\WebHost.WebAsset\Assets` in a *source tree* is only what the build
copies into the output, so filling it in after building (or running the release
folder) is a folder the server never sees. The release
folder *is* the content root: that is where `accounts.json` lives, and it is
where `Assets` has to be.

### 3.2 Don't copy them — point at a folder that has them

`config\appsettings.json`, next to `WebHostManager.exe`:

```json
"Firefall": {
  "Assets": {
    "Paths": [
      "C:\\Program Files\\Steam\\steamapps\\common\\Firefall\\system\\vt"
    ]
  }
}
```

The folder named has to be the one *holding* the files (or holding a `vtex/`
tree): `...\Firefall\system` would need `system\vt\static.vtex0`, and PIN does
not walk your install looking for it. Remote players are served by the same
list — a root on your machine is their download server, which is how a friend
with a 30 MB install gets the sharp mips without downloading 12 GB.

Restart `WebHostManager` after either change. The startup report is the check:

```
Web asset host serves 2 root(s), with high-resolution texture chunks for prod-1962:
  asset root E:\PIN\Assets serves prod-1962: static.vtex0 (8912.4 MB), static.vtex1 (1900 MB), static.vtex2 (880 MB);
  asset root C:\...\Firefall\system\vt serves loose: static.vtex0 (8912.4 MB), ...
```

Loose chunks - all three of them in `Assets\` - are a complete answer too, and the
host says so in its own words instead of warning:

```
Web asset host serves 1 root(s) with high-resolution texture chunks held loose,
answered for any build a client names: asset root E:\PIN\Assets serves loose:
static.vtex0 (8912.4 MB), static.vtex1 (1900 MB), static.vtex2 (880 MB)
```

and when nothing is configured you get the line that tells you what you are about
to see in game:

```
[WRN] Blurry textures ahead: no asset root holds a chunk file for a client to fetch - E:\PIN\Assets.
      Copy static.vtex0, static.vtex1 and static.vtex2 straight into E:\PIN\Assets,
      or into E:\PIN\Assets\vtex\<env>-<build> to answer one build by the exact name
      the client asks for, or point Firefall:Assets:Paths at a folder that already
      holds them (the client's own system\vt does). See Docs/ASSETS.md
```

### 3.3 On the client

- `Options → Network → Advanced → Texture streaming quality`: leave it on
  (highest) to stream from the server, or set it to disable once the files are in
  the client's own `system\vt` — same trick the 2014 guides used, and it means
  the server is not carrying 12 GB per player.
- Cached tiles live in `%LocalAppData%\Red 5 Studios\Firefall\cache\vt\*.vtcache*`.
  Delete those after changing what the server serves: a cache the client believes
  is current is a client that never asks again.
- A first zone load after enabling this is slow, and can show black patches while
  the pages fill in. That is the streaming working, not broken.

## 4. Checking it without starting the game

```sh
# The probe the client makes: a 200 with Content-Length means the chunk is served.
curl -sI http://localhost:4401/vtex/prod-1962/static.vtex0 | head -5

# A range request, i.e. a tile fetch: 206 with Content-Range, and 1024 bytes back.
curl -s -o /dev/null -w '%{http_code} %{size_download}\n' \
     -H 'Range: bytes=0-1023' http://localhost:4401/vtex/prod-1962/static.vtex0

# Which host the client is told to use for *its own* UI assets (a capability answer).
curl -s 'http://localhost:4400/check?environment=prod&build=1962' | grep -o '"web_asset_host":"[^"]*"'
```

That last one is expected to say **4499/44399**, the catch-all: the texture and
asset streams come from `firefall.ini`, which names port 4401 directly, and
`web_asset_host` is what the client loads its own store UI from - where the
catch-all's empty 200 is a better answer than a static host's 404. Do not
"fix" it by repointing it at the asset host: that changes nothing about
blurry textures and turns a benign reply into a 404.

The URL is the client's question, so it is the same URL whichever folder the file
is in: `Assets\static.vtex0` answers this `404`-free probe just as `Assets\vtex\
prod-1962\static.vtex0` does. A `404` with the file present on disk means the name
is wrong (`static.vtex`, no extension digits) or the host is not the folder being
read - compare with the startup report, which prints both sides.

## 5. Limits

- **No cache headers, no compression.** The chunks are already compressed
  binary; `HttpsCompression` is left at the middleware default and nothing sets
  `max-age` on purpose — a server whose files change under the client has to let
  the client notice.
- **A GET of `static.vtex0` is 9 GB.** The client asks for tiles, not files; a
  browser pointed at that URL will download the chunk, which is a fine way to
  verify a root and a poor way to browse.
- **The asset stream is not a game patch.** `AssetStreamPath` answers requests
  for streamed assets by path; PIN does not synthesize or restream anything, it
  hands out the bytes in the roots. Missing pieces stay missing, and the client
  behaves exactly as it did before this folder existed.
- **Serving to other players is bandwidth.** Twelve gigabytes of chunks per
  first zone load, over a VPN tunnel, is a number worth knowing before you invite
  someone to a server "with the nice textures".
