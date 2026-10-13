# XIII: There Is No Thirteenth Floor

A tiny first-person liminal-horror game for the **Tuesday the 13th Jam** (13 October 2026).
Theme: **"Unlucky XIII"**. Secondary theme: **liminal horror**.

You are a guest in a hotel corridor that keeps repeating. Your room is on the 13th floor.

- If you see an **omen** (anything that is not as it should be), **turn back** through the door you came in.
- If nothing is wrong, **keep walking forward** through the far door.
- Get it right and you go up a floor. Get it wrong and you're back on **Floor I**.
- Reach **Floor XIII**, the floor hotels skip.

There are exactly **13 omens**, and each one is a bad-luck superstition or a liminal wrongness:

| # | Omen | What changes |
|---|------|--------------|
| 1 | The black cat | A black cat crosses your path, then watches you |
| 2 | The ladder | A ladder leans across the hall |
| 3 | The broken mirror | The mirror is cracked and there are shards on the carpet |
| 4 | The open umbrella | The umbrella is out of its stand and open indoors |
| 5 | The fallen horseshoe | The brass horseshoe above the far door drops off its nail with a clang as you approach |
| 6 | Room 13 | One door number reads **13** |
| 7 | The backward clock | The clock runs backward and ticks too fast |
| 8 | The watching portrait | The portrait's eyes follow you |
| 9 | The red light | One ceiling light is red and flickering |
| 10 | The other guest | A tall figure stands at the end of the hall and is gone when you get close |
| 11 | The footsteps | Hard heels walk behind you, out of step with you, and take two more steps after you stop |
| 12 | The open door | A room door hangs open, with whispering inside |
| 13 | The thirteenth sign | The EXIT sign reads **XIII** |

On Floor XIII you see all 13 at once, and the door behind you won't open.

Each run starts in the lobby of **Hotel Tredecim**. There's a desk bell you can ring (E), a key rack where key 13 is missing, and a lift whose floor indicator skips XIII. Walk into the lift to go up to Floor I.

Once per run, on a random floor between IV and IX, the power fails halfway down the corridor. That's the **jump scare**. That floor always hides a real omen as well, so turning back is still the right call.

**Controls:** WASD or arrow keys to walk, mouse to look, Shift to hurry, **Esc or P to pause**.
The pause menu has Resume, Exit to menu, volume sliders (master, ambience, footsteps, effects), mouse sensitivity, and the omen journal.
Settings and the journal are saved between sessions, including in the browser build.

Everything is generated from code: geometry is built from Unity primitives and every sound is synthesised at startup.
There are no art or audio assets to import.

---

## Setup (about 10 minutes)

1. **Unity Hub → Installs → Unity 6 → Add modules → WebGL Build Support.** You need this for the jam's web build.
2. **Unity Hub → New project → "Universal 3D"** template (Built-in RP works too). Name it anything.
3. Close Unity. Copy this repo's `Assets/XIII` folder into the new project's `Assets/` folder.
   - To keep the project itself in this git repo, copy the new project's `Packages/` and `ProjectSettings/` folders into this repo, then use **Unity Hub → Add → Add project from disk** and pick this repo folder. The `.gitignore` is already set up for Unity.
4. Open the project. When the scripts finish compiling, run **XIII → 1. Setup Project** from the top menu bar. This:
   - creates the materials in `Assets/XIII/Resources` (so shaders make it into the WebGL build),
   - creates and opens `Assets/XIII/XIII.unity` with fog and ambient light set, and adds it to the build,
   - sets the WebGL player settings (Gzip with decompression fallback, which works on itch.io).
5. Press **Play**. Click the Game view to check in.

If you see an "Input System / active input handling" error, go to **Project Settings → Player → Active Input Handling** and pick **Both**. The scripts already support either backend, so this shouldn't come up.

## Testing quickly

Select the `XIII` object in the scene. In the Inspector:

- **Debug Omen**: set it to 0–12 to force that omen into every corridor after the first, so you can check each one.
- **Debug Start Floor**: set it to `12` to reach the ending in one step, or `13` to start on the finale corridor. Anything above 1 skips the lobby.
- **Debug Skip Lobby**: start each run in the corridor.
- **Debug Scare Floor**: set it to `1` to get the jump scare on the very first corridor after the lobby.

Set them back to `-1`, `1`, off and `0` before you build.

## Web build and upload

1. **XIII → 2. Build WebGL**. The build goes to `Builds/WebGL/`.
2. Zip the **contents** of `Builds/WebGL` so that `index.html` sits at the root of the zip.
3. On itch.io, create a new project with **Kind: HTML**, upload the zip, tick **"This file will be played in the browser"**, set the viewport to **1280 × 720**, and enable the **fullscreen button**.

## Credits / tools (the jam asks you to cite these)

- Design & direction: AidanPotter15
- Engine: Unity 6
- Code: co-written with Claude Code (Anthropic)
- Art & audio: procedural, generated in code (no asset packs)

These credits also appear on the in-game title and end screens. Edit `XIIIGame.Author` if you want a different name.

## Code map

| File | What it does |
|------|--------------|
| `Scripts/XIIIGame.cs` | Game loop, floor counter, omen choice, transitions, title, pause and end screens (IMGUI) |
| `Scripts/Settings.cs` | Volume, mouse sensitivity and the omen journal, saved between sessions |
| `Scripts/Corridor.cs` | Builds the corridor, applies each omen, and stages the jump scare |
| `Scripts/Lobby.cs` | Builds the Hotel Tredecim lobby |
| `Scripts/HotelBuilder.cs` | Shared palette and primitive helpers for the lobby and corridor |
| `Scripts/OmenBehaviours.cs` | Cat, eyes, clock, flicker, vanishing guest, falling horseshoe, footsteps |
| `Scripts/PlayerController.cs` | First-person movement, head bob, footsteps |
| `Scripts/Sfx.cs` | Procedural audio (hum, steps, heels, clink, door, stinger, bell, whisper, tick, thud) |
| `Scripts/Mats.cs` | Material factory (URP or Built-in) |
| `Scripts/XInput.cs` | Works with the new Input System or the legacy Input Manager |
| `Scripts/Editor/XIIISetup.cs` | `XIII` menu: project setup and WebGL build |
| `Shaders/XIIIText.shader` | Depth-tested, fogged text for signs and door numbers |
