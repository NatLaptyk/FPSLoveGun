# Love Gun — Unity FPS

> *Spread love, not sadness. Convert the city. Defeat the boss.*

---

## Story

The world has been overtaken by sadness. Unhappy people roam the streets, throwing gloom at anyone who gets close. The player — armed only with a Love Gun and a handful of Love Bombs — must work their way through the city, converting wave after wave of miserable citizens into joyful ones.

The story opens with a scripted cutscene outside the local café: a despondent NPC shuffles up to the entrance, the door swings open, and they disappear inside. A moment later, a **Watcher** — a flying creature made of concentrated unhappiness — bursts out and immediately targets the player. This is the first sign that something larger is at work.

From there the player fights through the streets and into a **stadium**, where wave after wave of NPCs descend from the seats and close in from all sides. The assault overwhelms the player's happiness meter. At the last moment, a vision of the player's **cat** floods the screen — a reminder that love still exists in the world. A golden shockwave radiates outward from the player, instantly converting every NPC in the stadium. The player is lifted into the air to watch, then teleported to the street for the final confrontation.

The **Final Boss** is a massive, rage-fuelled humanoid who runs, jumps, punches, fires a sadness pulse, and unleashes 360-degree rings of projectiles. Defeating it requires landing enough love shots (or stun-bombing it first for bonus damage). On death, the boss erupts into a burst of NPCs it had been holding captive — convert them all to complete the game.

---

## Controls

| Input | Action |
|---|---|
| `W A S D` | Move |
| `Mouse` | Look / Aim |
| `Left Shift` | Sprint |
| `Space` | Jump |
| `Left Mouse Button` | Fire Love Gun |
| `R` | Reload Love Gun |
| `G` | Throw Love Bomb |
| `Escape` | Pause / Resume |

---

## Gameplay Systems

### Love Gun (Primary Weapon)

The Love Gun is the player's main tool. Shots are aimed from the camera's centre ray (the crosshair), so they always travel exactly where you're looking regardless of where the gun model sits on screen. It has a configurable ammo count (default 30), fire rate, and reload time. The HUD updates in real time and shows a "RELOADING" state while the animation plays.

Very unhappy NPCs (marked `isVeryUnhappy`) are immune to single love shots — they require a Love Bomb.

### Love Bomb (Secondary Weapon)

Pressing `G` lobs a pink-trailed bomb in an arc in front of the player. It explodes on contact and deals area-of-effect love. The blast also **stuns** enemy bosses (Watcher and Final Boss), making them take multiplied damage from any follow-up shots for the duration of the stun. The player carries a limited supply (default 3); the HUD tracks the remaining count.

### Unhappy NPCs

Each NPC has an `unhappinessLevel` (1–5) representing how much love they need before converting. They patrol waypoints when idle, stop and face the player when detected, and throw sadness projectiles with a visible dark-blue trail. On conversion they change colour to yellow, emit a particle effect that follows them as they wander, lose all colliders (so they no longer block shots), and start wandering the area with the CityPeople animation system.

**Stadium NPCs** use a separate movement mode: they descend from elevated seating using direct transform movement (no NavMesh, since the bleachers are not baked), then crowd into a tightening ring around the player. The ring radius is set per-wave by the Section2Spawner and shrinks over time to increase pressure.

### Player Health (Happiness Meter)

The player's health is framed as a happiness meter (0–100). Sadness projectiles reduce it. At 25 % a flashing "Happiness level dangerously low!" warning appears. At 10 % the Cat Vision Event fires — the game intercepts the damage callback in the same frame, before a Game Over can trigger, making the event seamless.

---

## Enemies

### Unhappy Person

The standard enemy. Patrols a set of waypoints, throws sadness projectiles when the player enters detection range, and yields when hit with enough love. A "very unhappy" variant (purple tint) ignores single shots and requires a Love Bomb.

### Watcher

A flying creature that hovers at a fixed height above the ground using a raycast-based hover system (no NavMesh needed). It has two attacks: a close-range bite and a long-range projectile from its eye. Both attacks and hit reactions play animator triggers (`Attack1`, `Attack2`, `Hit`, `Stun`). When reduced to zero love tolerance it converts, erupting in a ring of NPC prefabs it had been containing.

The Watcher is first encountered in the café cutscene and appears again as a mid-fight summon called by the Final Boss during the stadium battle.

### Final Boss

A ground-based humanoid that uses NavMeshAgent pathfinding. It has five behaviours in priority order:

1. **Sadness Pulse (Attack3)** — AoE burst centred on the boss. Fires when the player is within pulse radius. Spawns a configurable VFX that is destroyed after the animation.
2. **Ring Shot (Attack4)** — fires N projectiles equally spaced across 360 degrees from around the boss's body. Fires at medium range. Projectiles travel flat (no gravity) at configurable speed.
3. **Heavy Attack (Attack2)** — a wide melee swing.
4. **Quick Attack (Attack1)** — a fast jab.
5. **Jump Dash** — when the player is far away, the boss sprints and arcs upward using `NavMeshAgent.baseOffset` to simulate a jump without physics or NavMesh rebaking.

The boss also periodically summons Watchers (up to a configured maximum alive at once).

On defeat, the boss plays its death animation, then triggers a one-shot explosion VFX, ejects a ring of NPC prefabs via `NavMeshAgent.Warp()`, auto-converts all of them, fires the `onDefeated` event (after the count is updated so the win condition sees the correct numbers), and destroys itself.

---

## Scripted Events

### Café Entry Event (`CafeEntryEvent.cs`)

Triggered by a BoxCollider trigger zone near the café. The sequence:

1. Player movement is disabled.
2. A sad NPC spawns and the camera smoothly pans to face them.
3. The NPC walks to the café entrance using NavMeshAgent.
4. The door opens via `Animator.Play("Opening")`.
5. The NPC walks inside using direct transform movement (the café interior has no NavMesh bake).
6. The NPC is hidden; the door closes.
7. A Watcher spawns at the exit and immediately aggros onto the player.
8. Player control is restored.

### Cat Vision Event (`CatVisionEvent.cs`)

Triggered automatically the first time the player's happiness drops to or below 10 % (the callback fires synchronously inside `TakeSadness` before a Game Over can run). The full sequence:

1. Time slows to 15 %. Stadium music fades out.
2. A dark vignette closes in.
3. A cat image fades in with a caption ("You are not alone…").
4. The cat image fades out; time returns to 1×; the player is fully healed.
5. A white flash fires.
6. A golden shockwave ring expands outward from the player's feet, converting every NPC it touches.
7. The player is lifted into the air simultaneously (using direct position override with the CharacterController briefly disabled).
8. The screen fades to black.
9. The player is teleported to the street; the Final Boss is activated; the boss's minimap objective marker appears.
10. The screen fades back in and control is restored.

---

## Level Structure

The level is divided into sections, each tracked by a `SectionTracker` component. A section holds references to the specific NPCs that belong to it and fires `onSectionComplete` when enough of them are converted. This lets sections chain together independently of the global GameManager count.

**Section 1 — City Streets:** Standard patrol NPCs. The café cutscene plays on entry.

**Section 2 — Stadium:** Triggered by the player entering a BoxCollider at the stadium entrance. The `Section2Spawner` fires three overlapping waves (no wait for clear — the intent is to overwhelm the player). Each wave re-uses the same seat spawn points. NPCs are registered with GameManager upfront (all waves, not per-wave) so the HUD counter is correct from the start. Exit blockers (invisible walls) close when the player enters and open once all waves have spawned. The Cat Vision fires automatically when health drops low enough during this section.

**Section 3 — Street / Final Boss:** The player arrives via teleport. The Final Boss is already active. Defeating it completes the game.

---

## HUD & Minimap

The HUD (`HUDManager`) displays the happiness meter, ammo count, bomb count, and people-converted progress (e.g. "65 / 185"). The minimap uses a top-down orthographic camera and a set of `MinimapMarker` components — each NPC has one, coloured by type (unhappy, happy, objective). A `MinimapDirectionArrow` on the player always points toward the nearest active objective marker (e.g. the boss). Objective markers expose `Show()` and `Hide()` so the boss marker can be enabled on teleport and hidden on defeat.

---

## Music System

All music is managed by `MusicController` components. The key convention throughout the project is: **keep the GameObject disabled by default, then call `SetActive(true)` to start playback via `OnEnable`**. This prevents music from auto-playing at scene load.

- **Background music** plays from GameManager's AudioSource on `Start()`.
- **Stadium music** is enabled when the player enters the stadium trigger.
- **Boss music** is enabled the first time the boss aggros the player (guarded by a `musicStarted` bool to prevent double-play if the player re-enters aggro range).
- **Win music** is enabled via `winMusicController.gameObject.SetActive(true)` inside `GameManager.WinGame()`. Using `SetActive` instead of `AudioSource.Play()` ensures the music works even if the MusicController GameObject was kept disabled.

`MusicController.FadeOut()` is called to fade stadium music during the Cat Vision and boss music on defeat.

---

## Code Architecture

```
Assets/Scripts/
├── Player/
│   ├── PlayerController.cs       — WASD + mouse look + jump + sprint
│   ├── PlayerHealth.cs           — happiness meter; onLowHealth / onNearDeath callbacks
│   └── PickupRotator.cs          — rotates pickup items in the world
│
├── LoveWeapons/
│   ├── LoveGun.cs                — primary weapon; centre-ray aiming; ammo + reload
│   ├── LoveBombThrower.cs        — secondary weapon; arc throw; area stun
│   ├── LoveProjectile.cs         — love shot behaviour on hit
│   └── LoveBombProjectile.cs     — bomb physics + explosion radius
│
├── Enemies/
│   ├── UnhappyPerson.cs          — patrol AI; sadness throw; stadium crowd mode; conversion
│   ├── FinalBossAI.cs            — NavMesh boss; 4 attacks; watcher summons; defeat sequence
│   ├── SadnessProjectile.cs      — sadness shot; owner ignore; player damage on hit
│   └── TriggerZone.cs            — reusable zone that fires UnityEvents on player enter/exit
│
├── Managers/
│   ├── GameManager.cs            — win/lose; total count; pause; restart
│   ├── HUDManager.cs             — ammo; health; people counter; reload indicator
│   ├── SectionTracker.cs         — per-section happy progress; chains sections together
│   ├── EventManager.cs           — NPC activation sequencing between sections
│   ├── Section2Spawner.cs        — overlapping stadium waves; crowd radius shrink
│   ├── CatVisionEvent.cs         — full scripted vision + shockwave + teleport sequence
│   ├── CafeEntryEvent.cs         — opening café cutscene; door animation; Watcher spawn
│   ├── StadiumEventController.cs — stadium-level orchestration helper
│   ├── MinimapCamera.cs          — top-down minimap render texture setup
│   ├── MinimapMarker.cs          — per-NPC/objective marker; Show() / Hide()
│   └── MinimapDirectionArrow.cs  — player HUD arrow pointing at nearest objective
│
├── Environment/
│   ├── CarSpawner.cs             — spawns cars on road paths at intervals
│   ├── CarFollower.cs            — Rigidbody.MovePosition car movement along a path
│   └── CarPath.cs                — waypoint path definition for car routes
│
├── MusicController.cs            — looping audio with start offset, volume, and fade
├── PopupController.cs            — generic timed popup UI
├── SectionCompletePopup.cs       — "Section Clear!" overlay
├── MainMenu.cs                   — main menu play / quit
└── UI/
    └── ObjectiveMarker.cs        — world-space UI marker that projects onto minimap

Watcher--Scripts/
└── WatcherAI.cs                  — flying enemy; hover; bite + projectile attacks; eject NPCs
```

### Key Design Patterns

**ILovable\<T\> interface** — both `WatcherAI` and `FinalBossAI` implement `ILovable<bool>`, where the bool indicates whether the hit came from a Love Bomb (triggering stun). `UnhappyPerson` uses a simpler `ReceiveLove(int)` signature since it has no stun state.

**MusicController activation pattern** — keep the GameObject disabled, call `SetActive(true)` to trigger `OnEnable → StartPlayback`. This is the standard for all music in the project.

**NavMesh.Warp() for NPC placement** — used whenever NPCs are instantiated at runtime to snap them to the nearest NavMesh surface without Rigidbody or gravity side-effects.

**onDefeated event ordering** — the boss fires `onDefeated` only after `RegisterAdditionalPeople()` and all `ReceiveLove(999)` calls complete. This prevents `isGameWon = true` from being set before `PersonMadeHappy()` processes the ejected NPCs.

**Stadium NPC movement** — stadium seats are elevated geometry not covered by the NavMesh bake. The `Section2Spawner` disables each NPC's `NavMeshAgent` immediately after instantiation and overrides the position back to the seat. All stadium movement uses direct `transform.MoveTowards`, with the agent re-enabled on conversion so happy NPCs can wander away on NavMesh.

---

## Setup Checklist

- [ ] Tag the Player GameObject as `"Player"`.
- [ ] Bake a NavMesh covering walkable areas (streets, stadium field, not bleachers).
- [ ] Assign `playerMovementScript` and `playerCamera` to both `CafeEntryEvent` and `CatVisionEvent`.
- [ ] Keep all `MusicController` GameObjects **disabled** in the scene by default.
- [ ] Keep the `FinalBoss` GameObject **disabled** — `CatVisionEvent` activates it on teleport.
- [ ] Place the boss `MinimapMarker` on a **separate always-active GameObject**, not on the boss itself.
- [ ] Wire `Section2Spawner.onAllWavesComplete` → activate the boss (or leave to `CatVisionEvent`).
- [ ] Wire the final `SectionTracker.onSectionComplete` → `GameManager.TriggerWin`.
- [ ] Set `GameManager.useGlobalWinCondition = false` if using `SectionTrackers`.

---

## Built With

- **Unity** (2022 LTS or later recommended)
- **NavMeshAgent** — NPC and boss pathfinding
- **TextMeshPro** — HUD and caption text
- **Unity Animator** — NPC and boss animation state machines
- **Unity Events (UnityEvent)** — decoupled section chaining and boss defeat callbacks
- **CityPeople** — third-party NPC animation asset used after NPC conversion
