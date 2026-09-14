# Web assets

This folder is what the client streams from: `WebHost.WebAsset` (port 4401 / 44301) serves
everything under here, and `firefall.ini` names two paths into it —

```ini
[FilePaths]
AssetStreamPath = "http://localhost:4401/AssetStream/%ENVMNEMONIC%-%BUILDNUM%/"
VTRemotePath    = "http://localhost:4401/vtex/%ENVMNEMONIC%-%BUILDNUM%/static.vtex"
```

`%ENVMNEMONIC%-%BUILDNUM%` is the build of *your* client, not something PIN configures: a
Steam install substitutes `prod-1962`.

## High-resolution textures

The one thing here that changes how the game looks is the virtual-texture chunks. The client
probes them at startup (`HEAD /vtex/prod-1962/static.vtex0`, then `.vtex1` and `.vtex2`) and,
when nothing answers, renders the low-resolution mips baked into its own archives — a blurry
world, at every graphics setting, because the setting controls the streamer and there is no
stream to run.

**`static.vtex_idx` and `static.vtex3` … `static.vtex6` are not those chunks.** They are the
page table a stock Firefall install already carries in its own `system\vt`. Copying them here
produces a folder that is full and a game that is exactly as blurry as before, with no 404 in
the log to explain it. The three that matter are `static.vtex0`, `static.vtex1` and
`static.vtex2` — roughly 12 GB, and no longer downloadable from Red5's CDN.

Put the chunks where the probe looks for them:

```
Assets\vtex\prod-1962\static.vtex0     ~9 GB, the sharp mips
Assets\vtex\prod-1962\static.vtex1     ~2 GB
Assets\vtex\prod-1962\static.vtex2     ~0.9 GB
```

…or just drop the three files straight into `Assets\` — same names, no
subfolder:

```
Assets\static.vtex0
Assets\static.vtex1
Assets\static.vtex2
```

A chunk request no root answers at its literal path is retried by file name, at
the top of each root and then inside `vtex\`, so this folder holding the three
files is a complete answer for any build a client names. (The `vtex/<build>/`
layout stays the one the log can match one by one, and it is what to use when
several builds share a server. Anything that is *not* a chunk — an
`AssetStream/...` path — is only ever served by its literal path.)

That is the folder **next to `WebHostManager.exe`** (`E:\PIN\Assets` for an extracted release),
not `WebHosts\WebHost.WebAsset\Assets` in a source tree — the build copies that one here. A
`Firefall:Assets:Paths` entry in `config\appsettings.json` adds roots without copying anything,
and it may point straight at the client's own copies:

```json
"Firefall": { "Assets": { "Paths": [ "C:\\Program Files\\Steam\\steamapps\\common\\Firefall\\system\\vt" ] } }
```

Any chunk file found at the top level of a root is served for whatever build the client asked
for; the rest is answered by literal path. The host logs what it found in every root at
startup — and warns that textures will stay blurry when no root holds a chunk file.

Full background — the probe, the loose-file fallback, and `curl` checks — is in
`Docs/ASSETS.md` in the repository (it is not part of the release archive).

## What is *not* here

Nothing in the repository. The folder is empty on purpose (`.gitignore`): Red5's assets are
several gigabytes of copyrighted game data, so they ship with the game, not with the server.
