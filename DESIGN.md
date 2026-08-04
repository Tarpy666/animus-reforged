# ⚙️ l1l-@p3p — Main Design Document

> **THE SYSTEM — THIS IS THE RULE.**
>
> We use **mods, remastered mods, and AI logic**. The **UI goes INTO the game**.
> Every other contemporary allocation is still **IP distinction** — now it has to be
> coordinated **through modded games and assets**. We use **real assets** by using
> **modded and remastered games only**.

This document is the single source of truth for how the system is built. It lives in
every repo. If a repo does something that contradicts this, this document wins.

---

## 1. The system in one sentence

**The girls (AI) + the PipBoy keyboard (all tools) float over a unified hub of many
modded & remastered game environments, served by your own local distro server, rendered
with WebGPU — and every pixel of UI is built from real assets extracted from mods, never
AI-generated placeholders.**

## 2. The non-negotiable rules

1. **Real assets only.** Every icon, button, curvature, panel, piece of UI must be built
   from a real asset (GLB/FBX/DAE/Collada) extracted from modded or remastered games,
   or from CC-licensed artist uploads (Sketchfab). No AI-generated placeholder art.
2. **Modded/remastered games are the environment.** A mod or remaster of a game is the
   massive world we work in. The AI logic and UI overlay INTO that game — the game is
   the backdrop, the girls are the interface, the keyboard is the backend.
3. **IP coordination goes through mods.** If we need a character, a door, a button, a
   diorama — it comes out of a modded game's asset tree, credited to its creator. We do
   not synthesize lookalikes. We extract the real thing and credit the artist.
4. **Nothing fake.** Every feature claim must be backed by a real file on disk: real
   model weights, real assets, real server code. If a feature is a stub, it is labeled
   a stub in the diagnostics.
5. **Your machine is the server.** This Linux workspace is where code is written. The
   distro server (`p3p-server.mjs`) runs on YOUR device — Windows, Mac, Pi, phone — and
   the app connects to it. No paid services, no cloud dependency.

## 3. Architecture

```
┌──────────────────────────────────────────────────────────────┐
│  YOUR DEVICE (Windows/Mac/Linux/Pi — runs the distro server) │
│                                                              │
│  p3p-server.mjs (Bun)                                        │
│   ├─ serves the app (index.html + models + three.js)         │
│   ├─ bridge WebSocket ws://127.0.0.1:4747                    │
│   ├─ launches games from p3p-games.json                      │
│   └─ STREAM RELAY: ffmpeg captures the game window →         │
│      MJPEG /stream/live over the LAN                         │
└──────────────┬───────────────────────────────────────────────┘
               │ HTTP (LAN) + ws://127.0.0.1:4747
┌──────────────▼───────────────────────────────────────────────┐
│  THE APP (any browser, any phone)                            │
│                                                              │
│  WebGPU renderer (three/webgpu)                              │
│   ├─ streamed game (the modded world)  ← z-index 1           │
│   ├─ the girls overlay INTO it  (Maddiesun + Carys)          │
│   ├─ PipBoy keyboard (all tools live here)                   │
│   ├─ chat panel / diagnostics / bridge                       │
│   └─ speech bubbles, logcat, GPU status                      │
└──────────────────────────────────────────────────────────────┘
```

### Core files

| File | Role |
|---|---|
| `index.html` | The whole app — WebGPU scene, girls, keyboard, chat, bridge, stream view |
| `p3p-server.mjs` | Cross-platform distro server: app server + bridge + game launcher + stream relay |
| `p3p-games.json` | Game registry — every modded/remastered game with per-platform launch configs |
| `run.md` | Run-on-any-device guide (Bun one-liner, LAN URLs, diagnostics) |
| `DESIGN.md` | This document |
| `PLAN.md` | Animus Reforged remaster rollout plan |
| `public/models/` | Real assets: demon-teto GLB (the girls), pipboy-new DAE (keyboard), frink lab, skybox |

## 4. The assets

### The girls — the AI interface
- **Source:** `public/models/demon-teto/source/demon-teto.glb` (real Sketchfab asset, credited).
- **Maddiesun** — the primary AI companion. Left position, 1.6 units tall.
- **Carys** — the sister model. Right position, slightly taller (1.78), warmer hue.
- Both are clones of the one GLB with **per-girl cloned materials** (no shared-material tint bug).
- They overlay the game, talk via speech bubbles, appear in the keyboard deck, the
  favicon, and the manifest.

### The PipBoy keyboard — the backend
- **Source:** `public/models/pipboy-new/retro/model/model.dae` — "Retro-Modernized PIP BOY
  (Editable Screen)" from Sketchfab (real, CC Attribution, texture wiring repaired by hand).
- All tools live on its keys: chat, terminal, network, dev server, camera, settings.

### The environment — modded & remastered games only
- Current in-app world: **Frink's Lab** (`public/models/frink`) + real skybox dome, as the
  playable local level when no game stream is up.
- When a game launches via the distro server, its **window is streamed in** (`/stream/live`)
  and the girls + keyboard overlay it. The game IS the environment.

## 5. The stream relay (game → app)

- `p3p-server.mjs` finds `ffmpeg` (free) and captures the running game window:
  - Windows: `gdigrab` (window title or desktop)
  - Linux: `x11grab` (DISPLAY)
  - macOS: `avfoundation`
- Output: **MJPEG** over `multipart/x-mixed-replace` at `/stream/live`, plus single
  frames at `/api/frame`, status at `/api/stream`.
- The app shows it fullscreen under the girls; **GAME** button toggles
  streamed-game vs local-world environments. Bridge messages (`stream.state`) auto-switch.

## 6. The game registry

`p3p-games.json` — one unified registry, not one game. Each entry:
- `id` / `name`
- `dir` — folder the command runs in
- `launch.<platform>` — `windows` / `mac` / `linux` / `any`, command + args
- `capture` — stream relay settings (window title, fps, resolution)

Already registered: AC1 (Animus Reforged remaster), AC1 direct, DOOM (GZDoom), Quake
(vkQuake), Morrowind (OpenMW), Oblivion, Skyrim (SKSE), Fallout 3, Fallout NV (NVSE),
GTA SA / VC, Half-Life, Half-Life 2, Minecraft, RetroArch, DOSBox, ScummVM.

## 7. The bridge protocol

`ws://127.0.0.1:4747` — JSON messages:

| Type | Direction | Meaning |
|---|---|---|
| `ping` / `pong` | both | keepalive |
| `game.state` | server→app | game launched/closed |
| `game.log` | server→app | game stdout/stderr |
| `stream.state` | server→app | stream live/stopped |
| `ai.say` | server→app | AI speech bubble |
| `overlay.chat` | app→server | talk to the girls |
| `overlay.cmd` | app→server | `launch` / `stop` / `stream` / `frame` |
| `cmd.result` | server→app | command result |

## 8. How to run

See `run.md`. Short version:

```bash
# any device
curl -fsSL https://bun.sh/install | bash   # or powershell irm bun.sh/install.ps1 | iex
bun p3p-server.mjs
# open http://127.0.0.1:5173/ — or http://<this-ip>:5173/ from a phone
```

## 9. Credits & IP

- Every asset in `public/models/` is real: Sketchfab artist uploads (demon-teto,
  Retro-Modernized PIP BOY, Frink Laboratory, skybox) or CC-licensed models. Textures
  rewired by hand where exporters stripped them.
- Modded/remastered games are the environment layer. Mod authors keep their credit.
- We never ship non-IP generated filler. If a thing is not a real asset, it doesn't go in.
