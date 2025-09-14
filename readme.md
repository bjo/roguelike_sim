## Civilization Roguelike — Implementation Guide (for Coding Agent)

This is a refined, build-ready spec distilled from the ideation thread. Use it to scaffold the engine, UI, and content.

### Product Overview
- **Core loop**: Choose node → resolve crisis/boon → end-of-turn yields → advance era pressure → repeat.
- **Goal**: Progress Bronze → Information/Space and survive era-end bosses; win by Space launch.
- **Run length**: Target 2–3h; MVP slice 30–45m (Bronze + Classical).

### Core Systems
- **Resources (global)**: Food, Materials, Production, Knowledge, Unity, Gold.
- **Cities (1–5)**: each with resource bias, traits, stability, optional advisor.
- **Advisors**: passive modifiers, unlock special crisis options (Ares, Athena, Demeter, Mercury…).
- **Eras**: Bronze → Classical → Medieval → Discovery → Industrial → Modern → Information/Space; each adds 1 mechanic and scales intensity.
- **Node Map**: event cards (crisis/boon/shop/boss); simple requirements/outcomes.
- **Deck/Tech (light)**: mostly passive effects; a few one-shots.
- **Meta**: unlock advisors, city archetypes, rare events.

### Directory Structure
```
/engine   // pure TypeScript simulation, deterministic
/app      // React UI (Vite, Zustand/Redux Toolkit, Tailwind)
/content  // JSON content (eras, crises, advisors, cards)
/tools    // data validators, balance scripts
```

### Data Model (TypeScript)
```ts
export type ResourceType = 'food' | 'materials' | 'production' | 'knowledge' | 'unity' | 'gold';
export type Resources = Record<ResourceType, number>;

export type EraId = 'bronze' | 'classical' | 'medieval' | 'discovery' | 'industrial' | 'modern' | 'information';

export interface City {
  id: string;
  name: string;
  eraCreated: EraId;
  population: number; // abstract scale
  stability: number;   // local modifier to Unity tests
  resourceBias: Partial<Record<ResourceType, number>>;
  traits: string[];
  advisorId?: string;
}

export interface Effect {
  id: string;
  scope: 'global' | 'city' | 'advisor' | 'card';
  tags: string[];
  modifiers: Array<
    | { kind: 'resource_add'; resource: ResourceType; amount: number }
    | { kind: 'resource_mult'; resource: ResourceType; factor: number }
    | { kind: 'yield_add'; resource: ResourceType; cityId?: string; amount: number }
    | { kind: 'threshold_gate'; metric: 'unity' | 'entropy'; min?: number; max?: number; onFail: 'crisis' | 'block' }
  >;
  duration: 'instant' | 'turns' | 'permanent';
  remainingTurns?: number;
}

export interface CrisisOptionOutcome {
  chance: number; // 0..1
  deltas?: { type: ResourceType; amount: number }[];
  addEffects?: Effect[];
  cityChanges?: { cityId?: string; stabilityDelta?: number; populationDelta?: number }[];
}

export interface CrisisOption {
  id: string;
  label: string;
  costs?: { type: ResourceType; amount: number }[];
  tests?: { metric: 'unity' | 'knowledge' | 'production'; dc: number; advantageTags?: string[] }[];
  outcomes: CrisisOptionOutcome[];
  requiresTags?: string[];
}

export interface Crisis {
  id: string;
  era: EraId | 'any';
  title: string;
  description: string;
  intensity: 1 | 2 | 3;
  options: CrisisOption[];
}

export interface Node {
  id: string;
  kind: 'crisis' | 'boon' | 'shop' | 'boss';
  refId: string;
  position: { x: number; y: number; layer: number };
}

export interface RunState {
  seed: string;
  turn: number;
  era: EraId;
  eraTurn: number;
  entropy: number;
  resources: Resources;
  cities: City[];
  advisors: string[]; // advisor ids
  deck: string[];
  discard: string[];
  hand: string[];
  knownTechs: string[];
  activeEffects: Effect[];
  pendingNodes: Node[];
  log: { turn: number; message: string }[];
  flags: Record<string, boolean>;
}
```

### Engine API (pure functions)
```ts
export interface CreateRunParams { seed?: string; difficulty?: 'story' | 'standard' | 'hard'; }
export function createRun(p?: CreateRunParams): RunState;
export function listPlayableNodes(s: RunState): Node[];
export function playNode(s: RunState, nodeId: string, optionId?: string, cardIds?: string[]): RunState;
export function endTurn(s: RunState): RunState; // yields + upkeep + clocks
export function advanceEraIfReady(s: RunState): RunState;
export function isRunOver(s: RunState): { over: boolean; reason?: string };
export function saveRun(s: RunState): string; // JSON
export function loadRun(json: string): RunState;
```

### Algorithms (rules of thumb)
- City yield each end turn: sum bias + traits + global effects → add to resources.
- Costs first, then RNG outcomes (seeded) on crisis resolution; log everything.
- Unity gates: <2 unrest, <0 revolt risk, < -3 collapse checks.
- Era change pushes new global effects and scales crisis tables.

### City Actions (per-city, per-turn)

Each era consists of 5–10 turns. On each turn, every city may perform 1 action (configurable). Actions translate directly into resource deltas and engine effects.

Schema (engine-facing):
```ts
// engine/actions.ts
export type ActionCategory =
  | 'growth' | 'build' | 'military' | 'trade' | 'knowledge' | 'unity'
  | 'governance' | 'expansion' | 'infrastructure' | 'diplomacy' | 'space';

export interface CityActionDefinition {
  id: string;
  label: string;
  category: ActionCategory;
  eraUnlock: EraId; // earliest era usable
  actionPointCost: 1; // reserved for future multi-AP
  cooldownTurns?: number; // optional city-local cooldown
  costs?: { type: ResourceType; amount: number }[];
  tests?: { metric: 'unity' | 'knowledge' | 'production'; dc: number; advantageTags?: string[] }[];
  immediateDeltas?: { type: ResourceType; amount: number }[]; // applies on resolution
  addEffects?: Effect[]; // temporary or permanent modifiers
  project?: { id: string; label: string; progressRequired: number; progressPerTurnTag?: 'production' }; // multi-turn
  requiresTags?: string[]; // e.g., advisor tags, city traits
}

export interface CityActionInstance {
  cityId: string;
  actionId: string;
  turn: number;
}
```

Resolution order per turn:
```text
1) Player selects ≤1 action per city → resolve costs/tests → apply immediate deltas/effects.
2) Update active projects: add progress (usually equals city Production yield).
3) End-of-turn: compute city yields → apply upkeep/entropy → log.
```

Action catalog (representative, scalable by era/content):

- Growth
  - Harvest: immediateDeltas +2 food; cooldownTurns 1; eraUnlock: bronze.
  - Build Fields: costs 2 materials → addEffects yield_add {resource:'food', amount:+1} (city, permanent); eraUnlock: bronze.
  - Granary: costs 3 materials, 1 production → addEffects yield_add food +1 and threshold_gate to lessen famine; eraUnlock: bronze.

- Build / Infrastructure
  - Workshop: costs 3 materials → addEffects yield_add production +1 (city, permanent); eraUnlock: bronze.
  - Roads: costs 2 materials → addEffects trade actions yield +1 gold in this city; eraUnlock: classical.
  - Aqueduct: costs 4 materials → addEffects yield_add food +1 and stability +1 (as effect); eraUnlock: classical.

- Military / Defense
  - Train Militia: costs 2 production, 1 materials → addEffects tags ['military'] and city stability +1 for 3 turns; eraUnlock: bronze.
  - Fortify: costs 2 materials → addEffects reduces raider crisis intensity for this city (3 turns); eraUnlock: bronze.
  - Drill Army: costs 3 production → addEffects advantageTags ['military'] for tests this era; eraUnlock: classical.

- Trade / Economy
  - Caravan: costs 2 food → immediateDeltas +2 gold; if Roads present, +1 extra gold; eraUnlock: classical.
  - Market: costs 3 materials → addEffects yield_add gold +1 (city, permanent); eraUnlock: classical.
  - Mint (Discovery+): costs 3 materials, 2 production → addEffects resource_mult gold x1.15; eraUnlock: discovery.

- Knowledge / Culture
  - Research: costs 1 production → immediateDeltas +2 knowledge; eraUnlock: bronze.
  - Library: costs 3 materials → addEffects yield_add knowledge +1 (city, permanent); eraUnlock: classical.
  - Printing Press: project {progressRequired: 6, perTurn=production} → on complete addEffects global yield_add knowledge +1, unity -1 gate on some crises; eraUnlock: discovery.

- Unity / Legitimacy
  - Festival: costs 1 food, 1 gold → immediateDeltas +2 unity; addEffects +1 unity per turn for 2 turns; eraUnlock: bronze.
  - Edict (Policy): costs 2 knowledge → addEffects city stability +1 (permanent); eraUnlock: classical.
  - Propaganda (Industrial+): costs 2 gold → addEffects resource_mult unity x1.1 for 3 turns; eraUnlock: industrial.

- Governance / Taxation
  - Tax: immediateDeltas +3 gold; addEffects unity -1 for 2 turns ('resentment'); eraUnlock: bronze.
  - Reform: costs 2 knowledge, 1 unity → addEffects resource_mult production x1.1 (global, 5 turns); eraUnlock: medieval.

- Expansion
  - Settle Outpost: costs 3 food, 3 materials → project {progressRequired: 6} → on complete create new city with bias; eraUnlock: bronze.
  - Annex (Conquest): tests production vs dc; costs 2 production → on success add city; on fail unity -2; eraUnlock: classical.

- Diplomacy / Espionage
  - Embassy: costs 2 gold → addEffects unlock special crisis options this era; eraUnlock: classical.
  - Spy Network: costs 2 gold, 1 knowledge → addEffects advantageTags ['espionage'] for tests (3 turns); eraUnlock: modern.

- Industrial / Late Game
  - Factory Shift: costs 1 unity → addEffects yield_add production +2 and unity -1 per turn (city, 3 turns); eraUnlock: industrial.
  - Rail Hub: costs 5 materials → addEffects resource_mult materials x1.2 and trade +1 gold; eraUnlock: industrial.

- Space (Final)
  - Space Project: project {progressRequired: 20, perTurn=production} with additional costs per milestone (knowledge/gold); on complete → victory; eraUnlock: information.

Notes:
- Era unlocks gate actions; later eras can replace earlier versions (e.g., Caravan → Trade Route contract).
- Numerical values are placeholders for balancing.


### MVP Milestones
1) Engine skeleton (createRun, endTurn, crisis resolve, deterministic RNG).
2) Content v0 (Bronze + Classical): 10–15 crises, 4 advisors, 6–8 cards, 2 bosses.
3) Minimal UI: resource bars, city panels, node picker, option modal, log.
4) Save/Load (LocalStorage), seed sharing.
5) Balance pass to hit 30–45m.

---

Rogue-like game based on progression throughout the ages following a civilization. Short per-game time (~2-3 hrs?). Simple mechanics. Very addicting. Single (or very small team) indie developer. Deep backend experience but limited front-end/graphics experience. Recommend the key game mechanics and development stack for me.


Great refinements — these push it toward something deeper and still shippable. Let’s work through each point.

---

## 1. Age Breakdown

Your 7-era sequence is strong and reads cleanly:

**Bronze → Classical → Medieval → Discovery (Renaissance/Exploration) → Industrial → Modern → Information/Space**

This keeps novelty coming at a steady pace. Each era should ideally introduce:

* A new mechanic (disease, trade routes, ideology, tech decay, etc.).
* A new *pressure* (resource drain, stability hit, external aggression).
* A new *opportunity* (powerful card type, wonders, city specialization).

---

## 2. Resource Categories

You’re right that 3 is a bit flat for a civilization arc. Adding **Production** and **Unity/Stability** gives more texture. A compact but expressive set could be:

* **Food** → drives population & growth.
* **Materials** → raw inputs for building & construction.
* **Production** → conversion capacity (turns Materials into Wonders, or scales efficiency).
* **Knowledge** → tech, discoveries, soft power.
* **Unity** → cohesion, stability, ability to survive shocks.

Optional stretch (if you want Civ-like flavor without runaway complexity):

* **Culture** (long-term identity, boosts Unity & expansion).
* **Faith** (early-game stabilizer, later morphs into Ideology/Legitimacy).

⚖️ *Rule of thumb*: 5 resources is the sweet spot for a roguelike length. More than that risks cognitive overhead in a 2–3h run. Faith/Culture could appear as **derived mechanics** (e.g., special events or advisor tracks) rather than core tracked resources.

---

## 3. City-Based Foundation

This is a big step forward: anchoring the run in *cities* rather than just abstract resources.

* **Start with one city** (the “capital”).
* **Expansion paths**:

  * **Settle** (costs Food + Materials, risk of overextension).
  * **Conquer** (fight an external mini-civ node; higher risk, higher reward).
  * **Trade/Ally** (Knowledge + Unity path; city joins you as vassal).

Each city provides:

* A **resource bias** (e.g., river city = more Food, mountain city = more Materials).
* A **patron god / advisor class** (like a relic you pick at city founding). Examples:

  * *Ares* / Warlord → +Production in wars.
  * *Athena* / Strategist → Knowledge yields on defense.
  * *Demeter* / Nurturer → extra Food per harvest event.
  * *Mercury* / Trader → +Unity from trade events.

Cities become a natural **deck-building vector**: each expansion adds a city card + its specialization. Managing 3–5 cities over a short run feels rich but not overwhelming.

---

## 4. Mechanics Synergy

With these changes, the loop tightens up:

1. **Era advance** escalates crises, introduces new mechanics.
2. **Cities** are your “board state,” growing horizontally.
3. **Resources** fuel expansion, survival, and tech.
4. **Advisors/patron gods** shape each city’s specialization, nudging playstyle.
5. **Unity** acts as the glue: too low and cities revolt / collapse.

Meta-progression (between runs): unlock new patron gods, new expansion types (colonies, space outposts), and rare city archetypes.

---

## 5. Why This Works for a Solo Dev

* Resources are **numbers on UI** (no need for animations or tiles).
* Cities = **cards/panels with icons** (patron portrait, resource modifiers).
* Expansion/conquest = **event encounters**, not map battles.
* You keep the roguelike “node map” as the spine, and cities live as persistent modifiers.

---

✅ My recommendation:
Stick to **5 main resources** (Food, Materials, Production, Knowledge, Unity). Treat **Culture/Faith** as event-driven modifiers or city traits. This gives depth without exploding scope.

---

Do you want me to sketch how **a single run might look in detail** (say, first 15 minutes with 1 city → 2 cities, sample crises, tech picks), so you can sanity-check pacing? That’ll make the abstraction more tangible.


Perfect — with **six core resources** we’ve got a “civilization roguelike economy” that is rich but still legible:

* **Food** → population growth, prevents famine.
* **Materials** → raw inputs (stone, wood, iron, etc.).
* **Production** → capacity to turn inputs into outputs (wonders, armies, projects).
* **Knowledge** → technology, discoveries, soft power.
* **Unity** → cohesion/stability, legitimacy, prevents revolt.
* **Gold** → universal trade medium, taxes, mercenaries, diplomacy.

This set covers survival, growth, and trade-off tension beautifully.

---

# High-Level Era Progression

Each era lasts \~15–20 minutes. You face **recurring minor crises** (nodes on the map) and an **era-end boss crisis** (major hurdle). Advancing to the next era grants:

* new card/tech pool unlocked,
* +1 global rule (a new mechanic/pressure),
* scaling of crisis intensity.

Here’s a brainstorm pass:

---

## 1. **Bronze Age** (Cities, First Trade)

* **Theme:** Forming cities, basic agriculture, early metalworking.
* **Key decisions:**

  * Found 2nd city or stay tall?
  * Which patron god/advisor to lock in?
  * Start taxing (Gold) vs. investing in Unity.
* **Minor crises:**

  * *Famine* (Food shortfall → lose pop/Unity).
  * *Raiders* (Materials/Gold stolen unless you spend Production).
  * *Flood/Drought* (swingy Food events).
* **Era-end crisis:** **Neighboring City-Rival** → you must either pay tribute (Gold), fight (Production + Materials), or ally (Unity + Knowledge). Passing it cements your civ as “dominant.”

---

## 2. **Classical Age** (Empires, Culture, Faith)

* **Theme:** Organized states, philosophy, polytheistic religions.
* **New mechanic:** **Faith/Culture events** as Unity modifiers.
* **Key decisions:**

  * Centralize power (Unity → higher yields) vs. pluralize (Knowledge/Gold → trade routes).
  * Specialize cities (food center, production hub, cultural capital).
* **Minor crises:**

  * *Plague* (Food/Unity hit).
  * *Religious schism* (Unity test).
  * *Slave revolt* (Production loss if Unity too low).
* **Era-end crisis:** **Barbarian Invasions** → requires either strong military (Production), high Unity (defection avoided), or pay-off (Gold).

---

## 3. **Medieval Age** (Feudalism, Faith vs. State)

* **Theme:** Castles, knights, guilds, crusades.
* **New mechanic:** **Feudal contracts** (Production tied to Unity/Gold upkeep).
* **Key decisions:**

  * Strong central king vs. decentralized lords (Unity vs. Gold trade-off).
  * Invest in defense or trade expansion.
* **Minor crises:**

  * *Peasant uprisings* (Food shortage + Unity).
  * *Noble rebellion* (low Unity = major loss).
  * *Guild demands* (Gold tax events).
* **Era-end crisis:** **Great Crusade / Mongol Horde** → requires balancing Food (supply), Production (armies), and Gold (mercs).

---

## 4. **Discovery / Renaissance** (Exploration, Printing Press)

* **Theme:** Exploration, global trade, new ideas.
* **New mechanic:** **Global trade routes** (Gold engine, but introduces inflation).
* **Key decisions:**

  * Fund explorers (Gold + Unity risk) vs. focus inward.
  * Printing press = mass Knowledge but destabilizes Unity.
* **Minor crises:**

  * *Inflation/debt spiral* (Gold mismanagement).
  * *Religious reformation* (Unity shaken).
  * *Colonial disease outbreaks*.
* **Era-end crisis:** **Age of Conquest** → must project power abroad (Knowledge + Production + Gold) or risk falling behind.

---

## 5. **Industrial Age** (Factories, Pollution, Revolution)

* **Theme:** Machines, mass production, capitalism.
* **New mechanic:** **Pollution/worker unrest** (Unity decay per Production surge).
* **Key decisions:**

  * Exploit factories (Production spike, Unity risk) vs. gradual reform.
  * Invest in empire-wide infrastructure (Materials + Gold sink).
* **Minor crises:**

  * *Labor strikes* (Production lost, Unity hit).
  * *Industrial accidents*.
  * *Revolutionary sentiment* (Unity test, Gold to bribe).
* **Era-end crisis:** **Worker Revolution** → survive mass unrest by having high Unity, strong army (Production), or wealth to suppress (Gold).

---

## 6. **Modern Age** (World Wars, Ideologies, Finance)

* **Theme:** Nation-states, WWs, capitalism vs. socialism.
* **New mechanic:** **Ideology tracks** (choose between Democracy, Fascism, Communism, etc. → unique buffs + Unity risks).
* **Key decisions:**

  * Align cities under one ideology or risk fracture.
  * Military expansion vs. diplomacy.
* **Minor crises:**

  * *Stock market crash* (Gold collapse).
  * *World War conflict* (Production/Materials sink).
  * *Espionage/coups* (Unity threat).
* **Era-end crisis:** **Global War** → must either ally bloc (Unity + Knowledge), fight (Production), or finance proxy wars (Gold).

---

## 7. **Information/Space Age** (Globalization, AI, Space Race)

* **Theme:** Cold War → globalization → information economy.
* **New mechanic:** **Entropy clock** (each turn escalates; must “outrun” collapse).
* **Key decisions:**

  * Invest in Space Race (Knowledge + Production + Gold).
  * Manage cyber warfare, nuclear deterrence, environmental collapse.
* **Minor crises:**

  * *Climate disasters* (Food/Materials wiped).
  * *Cyber sabotage* (Gold + Knowledge loss).
  * *Nuclear brinkmanship* (Unity collapse risk).
* **Era-end crisis:** **Final Challenge (Space Colonization or Global Collapse)** → survive to launch humanity off-world, or perish.

---

# Design Notes

* **Gold** as a sixth resource makes the midgame pop (from Discovery onward it becomes a central pivot).
* **Unity** is the *hard check* at multiple stages — if too low, no amount of resources saves you.
* **Era-end crises** act like bosses: demand multi-resource solutions and define pacing.
* **Each era adds 1 new mechanic** → keeps novelty but prevents info overload.

---

Would you like me to **diagram the resource interdependencies** (who fuels what, who drains what, where bottlenecks emerge by era)? That’d give you a systems map to tune balance early.
