# LoveGun

A first-person shooter where the goal is to spread love, not destruction.

You play as a character armed with a **Love Gun** — a weapon that fires projectiles of pure affection. Your mission: roam a city overrun by sadness and make every unhappy person (and flying boss) feel loved before your own happiness runs out.

---

## Concept

LoveGun flips the FPS genre on its head. Instead of eliminating enemies, you convert them. Unhappy NPCs wander the streets projecting misery. Hit them enough with love and they transform — becoming happy, peaceful, and no longer a threat. The challenge isn't firepower; it's staying happy yourself while waves of sadness close in.

---

## How It Works

### Player

The player is a first-person character with WASD movement, sprinting, jumping, and mouse-look. A **happiness meter** replaces the traditional health bar — taking hits from sadness projectiles drains it. Reach zero and it's game over.

Weapons carried:

- **Love Gun** — primary weapon, fires rapid love projectiles from a magazine. Reserve ammo is restocked from pickups. Auto-reloads when the magazine empties.
- **Love Bomb Thrower** — secondary weapon, lobs an area-of-effect love bomb that affects multiple NPCs at once.

### Unhappy People (NPCs)

`UnhappyPerson` NPCs roam the city in a sad state. Hit them with enough love projectiles and their mood flips to `Happy`. Once happy they are no longer a threat and count toward the level's completion goal. The HUD tracks how many you've converted.

### Watchers (Flying Mini-Bosses)

Watchers are airborne enemies that hover, chase, and attack the player. They don't use NavMesh — they fly freely in 3D space. Like NPCs, they can be converted with enough love; while stunned they absorb love at a higher multiplier. On conversion, each Watcher spawns a wave of happy NPCs and drops a random pickup (ammo, health, or love bomb).

### Final Boss

The Final Boss is a ground-based humanoid commander with a full state machine (idle → chasing → attacking → stunned → phase transition → defeated). Key mechanics:

- **Phase system**: at 80% love received the boss teleports to a new part of the city (Phase 2), and again at 30% (Phase 3), keeping the fight mobile and unpredictable.
- **Watcher spawning**: the boss summons Watchers during the fight. All active Watchers are destroyed when the boss is defeated.
- **Sadness attacks**: the boss fires sadness projectiles that drain the player's happiness. These projectiles correctly ignore the boss itself.
- **Objective marker**: a world-space UI marker always tracks the boss's current position.

### Level Flow — Sections

The level is divided into named sections gated by progress:

1. **Café section** — the player must convert all assigned NPCs in the café area. Tracked by a `SectionTracker` component.
2. **Stadium section** — unlocks only after the café is cleared. A software gate (`cafeCleared` bool) and physical entry blockers both prevent access until `SectionTracker.onSectionComplete` fires and calls `Section2Spawner.UnlockEntrance()`.
3. **Stadium waves** — once the player enters the stadium, `Section2Spawner` begins spawning infinite waves of NPCs from seat positions. Waves run on a timer and overlap intentionally to overwhelm the player. The crowd forms a ring that slowly closes in. When the player's happiness drops to the critical threshold, a scripted **Cat Vision Event** fires and `StopWaves()` is called, ending the infinite loop.

### Pickups

Pickups are scattered across the city in configurable zones using `PickupSpawner`. Three types exist — ammo, health boosts, and love bombs — each with a configurable count and respawn delay. When a pickup is collected, its slot watches for the instance to be destroyed, then respawns it after the delay.

### Minimap

A top-down minimap camera renders coloured markers for the player, enemies, objectives, and points of interest. Markers use a static shader cache with fallbacks (`Unlit/Color` → `Sprites/Default` → `UI/Default`) so they render correctly in builds.

### Pause Menu

Pressing Escape opens a pause panel. `PopupController.IsPaused` is a static bool read by the player controller, door scripts, and any other system that should freeze during pause. The cursor is unlocked while paused and re-locked on resume.

### Environment

- **Doors** — interactive doors that open/close on mouse hover. All door scripts check `PopupController.IsPaused` before responding to input.
- **Cars** — ambient traffic: cars follow spline paths and are spawned/recycled by `CarSpawner`.
- **Music** — `MusicController` handles per-section background music with configurable start offsets, volume, and fade-out support.
- **Map boundary** — the playable area is bounded by a mesh with its Renderer disabled (invisible) but its Collider active, preventing the player from leaving. Linear fog is configured to appear only near the boundary (Start ≈ 80–100 units, End ≈ 150 units) so the playable city is clear.

---

## Script Reference

```
Assets--Scripts/
├── Enemies/
│   ├── FinalBossAI.cs          — Final boss state machine, phase transitions, Watcher spawning
│   ├── SadnessProjectile.cs    — Boss/Watcher projectile; ignores the Final Boss itself
│   ├── TriggerZone.cs          — Generic trigger that fires a UnityEvent on player entry
│   └── UnhappyPerson.cs        — NPC mood state machine (Unhappy → Happy)
│
├── LoveWeapons/
│   ├── LoveGun.cs              — Primary weapon: magazine + reserve ammo, auto-reload
│   ├── LoveProjectile.cs       — Projectile fired by the Love Gun
│   ├── LoveBombThrower.cs      — Secondary weapon: lobs area-of-effect love bombs
│   └── LoveBombProjectile.cs   — Physics projectile with AoE love burst on impact
│
├── Managers/
│   ├── GameManager.cs          — Win/lose conditions, global happy-people counter
│   ├── HUDManager.cs           — Updates ammo, happiness, and people-count displays
│   ├── SectionTracker.cs       — Tracks NPC progress for a single level section
│   ├── Section2Spawner.cs      — Stadium wave spawner, gated by café completion
│   ├── PickupSpawner.cs        — Zone-based city pickup placement with respawn timers
│   ├── CafeEntryEvent.cs       — Fires events when the player enters the café
│   ├── CatVisionEvent.cs       — Scripted low-health sequence; stops stadium waves
│   ├── CheckpointManager.cs    — Saves/restores player position at checkpoints
│   ├── EventManager.cs         — General-purpose event sequencer
│   ├── StadiumEventController.cs — Orchestrates the stadium encounter
│   ├── MinimapMarker.cs        — Renders a coloured icon on the minimap camera layer
│   ├── MinimapCamera.cs        — Top-down minimap camera setup
│   ├── MinimapDirectionArrow.cs— Rotates a minimap arrow to match player facing
│   ├── HintMessage.cs          — Displays timed hint text on the HUD
│   └── MusicController.cs      — Per-section music with start offset, volume, fade
│
├── Player/
│   ├── PlayerController.cs     — FPS movement, mouse-look, sprint, jump
│   ├── PlayerHealth.cs         — Happiness meter, low-health callbacks, invincibility flag
│   └── PickupRotator.cs        — Spins pickup world objects for visual appeal
│
├── UI/
│   ├── PopupController.cs      — Pause menu; exposes static IsPaused bool
│   ├── ObjectiveMarker.cs      — World-space UI marker that tracks a target transform
│   ├── SectionCompletePopup.cs — Shows a message when a section is cleared
│   └── MainMenu.cs             — Main menu scene controller
│
├── Environment/
│   ├── CarSpawner.cs           — Spawns and recycles ambient traffic cars
│   ├── CarFollower.cs          — Moves a car along a CarPath spline
│   └── CarPath.cs              — Defines a looping spline path for cars
│
├── Flow/
│   └── SequenceTrigger.cs      — Fires ordered UnityEvents on player entry
│
└── DevTools/
    └── DebugSkipController.cs  — Dev shortcut to skip sections during testing

Watcher--Scripts/
└── WatcherAI.cs                — Flying mini-boss: chase, attack, stun, convert, drop pickups

Doors/
├── opencloseDoor.cs            — Animated double door (hover to open)
├── opencloseDoor1.cs           — Variant double door
└── opencloseStallDoor.cs       — Narrower stall/shop door variant
```

---

## Key Design Decisions

**Love as ammunition** — ammo scarcity creates tension. Running out of love projectiles mid-fight means you can't convert enemies and their sadness keeps draining you.

**Happiness as health** — framing the health bar as "happiness" reinforces the game's theme. The player is emotionally affected by the environment, not just physically.

**Section gating** — areas unlock in sequence so players experience the narrative beats in order (city → café → stadium → boss) without a hard linear path forcing them.

**Phase teleportation** — the Final Boss relocating between city zones prevents camping and forces the player to keep moving, which increases exposure to ambient NPCs along the way.

---

## Built With

- **Unity** (C#)
- **Unity NavMesh** for ground-based NPC and boss pathfinding
- **Unity Animator** for character animation state machines
- **Unity UI** (Canvas, UnityEvents) for HUD and menus
