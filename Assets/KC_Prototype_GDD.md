# GAME DESIGN DOCUMENT (GDD)
## Medieval Colony Sim - 15 Day Prototype

**Version:** 1.0  
**Created:** February 6, 2026  
**Developer:** Vaibhav Shrivastava  
**Timeline:** 15 Days (5 hours/day = 75 hours total)  
**Platform:** PC (Windows)  
**Engine:** Unity 3D  
**Target:** Playable vertical slice to test core gameplay loop

---

## EXECUTIVE SUMMARY

**High Concept:**  
A 3D low-poly medieval colony simulator where players build and manage a thriving settlement through strategic resource management and expansion.

**Inspiration:**  
Kingdoms and Castles (70% similar gameplay) with simplified scope for rapid prototyping.

**Core Pillars:**
1. **Peaceful Building** - Satisfying city expansion without combat stress
2. **Resource Management** - Strategic decisions about resource allocation
3. **Growth & Scale** - Watch settlement grow from small hamlet to bustling town

**Prototype Goal:**  
Answer the question: "Is the core building loop fun enough to continue development?"

---

## SCOPE - WHAT'S IN / WHAT'S OUT

### ✅ IN SCOPE (Must-Have for Prototype)

**Core Loop:**
- Place buildings on grid-based map
- Gather 3 resources (Food, Wood, Stone)
- Population grows with housing
- Citizens auto-assign to buildings
- Simple economy (buildings cost resources)

**Buildings (6 types minimum):**
1. **House** - Provides population capacity
2. **Farm** - Generates Food
3. **Woodcutter** - Generates Wood
4. **Quarry** - Generates Stone
5. **Storage** - Increases resource cap
6. **Town Hall** - Central building (aesthetic + spawn point)

**Systems:**
- Grid-based placement (10x10 minimum map)
- Camera controls (pan, zoom, rotate)
- Basic UI (resource counters, building menu)
- Win condition (reach 50 population)

**Art Style:**
- Low-poly 3D (Kingdoms and Castles style)
- Free assets (Quaternius medieval pack)
- Simple flat textures
- Minimalist aesthetic

---

### ❌ OUT OF SCOPE (Post-Prototype)

**Explicitly NOT in 15-day prototype:**
- ❌ Combat/Defense systems
- ❌ Multiple maps/biomes
- ❌ Tech tree/research
- ❌ Seasonal system
- ❌ Day/night cycle
- ❌ Building upgrades (add later)
- ❌ Sound effects/music
- ❌ Save/Load system
- ❌ Settings menu
- ❌ Multiple win conditions
- ❌ Disasters/random events
- ❌ Citizen pathfinding (citizens don't move)
- ❌ Building animations (static is fine)
- ❌ Particle effects (juice comes later)

**Reasoning:**  
Focus 100% on core loop. If that's not fun, nothing else matters.

---

## DETAILED FEATURE BREAKDOWN

### 1. GRID SYSTEM

**Description:**  
Tile-based grid where all buildings snap to alignment.

**Technical Specs:**
- 10x10 grid minimum (expandable to 20x20 if time permits)
- Tile size: 1 Unity unit = 1 grid cell
- Visual grid overlay (toggleable)
- Placement validation (green = valid, red = blocked)

**Acceptance Criteria:**
- [ ] Buildings snap to grid on placement
- [ ] Visual feedback shows valid/invalid placement
- [ ] Can't place buildings on occupied tiles
- [ ] Can't place buildings outside map bounds

---

### 2. CAMERA CONTROLS

**Description:**  
Top-down camera with bird's eye view, Kingdoms and Castles style.

**Technical Specs:**
- Orthographic or perspective (test both, pick what feels better)
- Pan: WASD or Arrow Keys or Middle Mouse Drag
- Zoom: Mouse Scroll Wheel (min/max limits)
- Rotate: Q/E keys or Right Mouse Drag (optional, test if needed)

**Acceptance Criteria:**
- [ ] Smooth camera movement (no jitter)
- [ ] Can view entire map when zoomed out
- [ ] Can see building details when zoomed in
- [ ] Camera doesn't go outside map bounds

---

### 3. BUILDING PLACEMENT

**Description:**  
Click building in menu → Click on map to place.

**Technical Specs:**
- Ghost preview follows mouse cursor
- Left click to confirm placement
- Right click or ESC to cancel
- Deduct resources on placement
- Instantiate building model

**Buildings Data (Simple Version):**

| Building | Size | Cost (Food/Wood/Stone) | Generates | Notes |
|----------|------|------------------------|-----------|-------|
| House | 2x2 | 0/10/5 | +5 Population | Max citizens increases |
| Farm | 2x2 | 10/5/0 | +2 Food/sec | Requires 1 citizen |
| Woodcutter | 2x2 | 5/0/5 | +1 Wood/sec | Requires 1 citizen |
| Quarry | 3x3 | 5/10/0 | +1 Stone/sec | Requires 2 citizens |
| Storage | 2x2 | 5/5/5 | +100 cap | Increases resource storage |
| Town Hall | 3x3 | 0/0/0 | None | Free, placed at start |

**Acceptance Criteria:**
- [ ] Can select building from menu
- [ ] Ghost preview shows placement location
- [ ] Can't place if insufficient resources
- [ ] Can't place on occupied tiles
- [ ] Building appears instantly on placement (no construction time)

---

### 4. RESOURCE SYSTEM

**Description:**  
Track 3 resources: Food, Wood, Stone. Buildings generate resources over time.

**Technical Specs:**
- Resources stored as integers
- Starting resources: Food=50, Wood=50, Stone=50
- Default storage cap: 100 per resource
- Each Storage building adds +100 cap
- Resource generation ticks every second
- UI displays current/max for each resource

**Resource Generation:**
```
Every 1 second:
- Check all production buildings (Farm, Woodcutter, Quarry)
- If building has assigned citizen(s):
  - Add production amount to resource pool
  - Clamp to max storage capacity
```

**Acceptance Criteria:**
- [ ] Resources displayed in UI
- [ ] Resources generate passively from buildings
- [ ] Can't exceed storage cap
- [ ] Buildings only produce if citizens assigned

---

### 5. POPULATION SYSTEM

**Description:**  
Citizens are abstract numbers, not physical entities (no pathfinding needed).

**Technical Specs:**
- Current Population = integer
- Max Population = 5 per House
- Citizens auto-assign to nearest unfilled production building
- Each building has required citizens (Farm=1, Woodcutter=1, Quarry=2)

**Auto-Assignment Logic:**
```
When new citizen spawns:
1. Find nearest production building with empty slot
2. Assign citizen to that building
3. Update building UI to show "Workers: X/Y"
4. Enable resource generation for that building
```

**Population Growth:**
- Start with 5 citizens
- Each House adds capacity for 5 more
- No natural growth in prototype (too complex)
- Population increases only when houses built (instant)

**Acceptance Criteria:**
- [ ] Population displayed in UI (Current/Max)
- [ ] Building houses increases max population
- [ ] Citizens auto-assign to production buildings
- [ ] Buildings show worker count (visual indicator)

---

### 6. USER INTERFACE (UI)

**Description:**  
Minimalist UI showing essential info only.

**UI Elements:**

**Top Bar (HUD):**
- Food: [icon] 45/100
- Wood: [icon] 32/100
- Stone: [icon] 18/100
- Population: [icon] 15/25

**Building Menu (Bottom or Side Panel):**
- Grid of building icons (6 buttons)
- Shows building name + cost
- Greyed out if can't afford
- Click to select for placement

**Info Panel (Optional):**
- Displays selected building info
- Shows worker assignments
- Production rate

**Acceptance Criteria:**
- [ ] Resources visible at all times
- [ ] Population visible at all times
- [ ] Can access building menu easily
- [ ] Visual feedback for affordability

---

### 7. WIN CONDITION

**Description:**  
Reach 50 population to "win" the prototype test.

**Technical Specs:**
- Check population every second
- If population >= 50: Show "Victory!" message
- Simple popup or text overlay
- No actual "game over" - just acknowledgment

**Acceptance Criteria:**
- [ ] Win message displays when reaching 50 population
- [ ] Can continue playing after win (no forced exit)

---

## TECHNICAL ARCHITECTURE

### Project Structure
```
Assets/
├── Scenes/
│   └── MainGame.unity
├── Scripts/
│   ├── GridSystem.cs
│   ├── BuildingPlacer.cs
│   ├── ResourceManager.cs (Singleton)
│   ├── PopulationManager.cs (Singleton)
│   ├── CameraController.cs
│   ├── Building.cs (base class)
│   └── UIManager.cs
├── Prefabs/
│   ├── Buildings/
│   │   ├── House.prefab
│   │   ├── Farm.prefab
│   │   ├── Woodcutter.prefab
│   │   ├── Quarry.prefab
│   │   ├── Storage.prefab
│   │   └── TownHall.prefab
│   └── UI/
│       └── BuildingButton.prefab
├── Models/ (Quaternius assets)
└── Materials/
    └── GridMaterial.mat
```

### Core Classes

**GridSystem.cs**
- Manages grid coordinates
- Validates placement
- Stores tile occupancy data

**BuildingPlacer.cs**
- Handles placement input
- Ghost preview rendering
- Communicates with GridSystem

**ResourceManager.cs (Singleton)**
- Tracks current resources
- Handles spending/earning
- Updates UI

**PopulationManager.cs (Singleton)**
- Tracks population count
- Auto-assigns citizens
- Updates UI

**Building.cs (Base Class)**
- Stores building data (type, size, cost, production)
- Worker assignment logic
- Resource generation tick

**CameraController.cs**
- Input handling (WASD, zoom, rotate)
- Movement smoothing
- Boundary clamping

**UIManager.cs**
- Updates resource display
- Handles building menu buttons
- Shows win message

---

## ART & ASSETS

### Asset Source
**Quaternius Medieval Fantasy Pack** (FREE)
- Link: https://quaternius.com/packs/medievalfantasy.html
- Includes: Houses, farms, trees, rocks, fences
- Style: Low-poly, flat colors
- License: CC0 (completely free)

### Color Palette (Simple)
- Grass: #7CB342 (green)
- Dirt paths: #8D6E63 (brown)
- Buildings: Varied (wood brown, stone grey)
- UI: Dark grey background, white text

### Visual Style Reference
- Kingdoms and Castles (exact target)
- Townscaper (minimalist appeal)
- Fabledom (cute low-poly)

---

## DEVELOPMENT TIMELINE (15 Days)

### Week 1: Foundation (Days 1-7)

**Day 1: Setup & Grid**
- Unity project setup (3D template)
- Import Quaternius assets
- Grid system implementation
- Visual grid overlay
- **Goal:** Can see grid, place test cubes

**Day 2: Camera & Input**
- Camera controller (pan, zoom)
- Mouse input detection
- Grid coordinate mapping
- **Goal:** Can navigate around map smoothly

**Day 3: Building Placement (Basic)**
- BuildingPlacer script
- Ghost preview system
- Placement validation
- **Goal:** Can place 1 building type (test cube)

**Day 4: Resource System**
- ResourceManager singleton
- Resource tracking (Food, Wood, Stone)
- Basic UI display (text only)
- **Goal:** Resources shown in UI

**Day 5: First 3 Buildings**
- Create House, Farm, Woodcutter prefabs
- Implement building costs
- Resource deduction on placement
- **Goal:** Can place 3 different buildings

**Day 6: Resource Generation**
- Production tick system
- Building.cs generates resources
- Test resource flow
- **Goal:** Buildings passively generate resources

**Day 7: Population Basics**
- PopulationManager singleton
- Population increase with houses
- Display population in UI
- **Goal:** Building houses increases population

---

### Week 2: Depth (Days 8-14)

**Day 8: Citizen Assignment**
- Auto-assign citizens to buildings
- Worker requirement system
- Visual worker indicators
- **Goal:** Citizens assigned, buildings produce only if staffed

**Day 9: Remaining Buildings**
- Add Quarry, Storage, Town Hall
- Implement storage capacity
- Resource cap system
- **Goal:** All 6 buildings functional

**Day 10: Building Menu UI**
- Create building selection menu
- Button prefabs for each building
- Affordability visual feedback
- **Goal:** Can select buildings from UI

**Day 11: UI Polish**
- Resource icons/sprites
- Better layout (top bar HUD)
- Building info panel
- **Goal:** UI looks clean and readable

**Day 12: Win Condition**
- Victory check (50 population)
- Win message popup
- **Goal:** Game has a clear goal

**Day 13: Bug Fixing**
- Test all systems together
- Fix major bugs
- Balance tweaking (production rates)
- **Goal:** Stable build, no crashes

**Day 14: Playtest Prep**
- Build executable
- Test on fresh machine (if possible)
- Write playtest instructions
- **Goal:** Ready for external testing

---

### Day 15: Playtest & Evaluation

**Morning:**
- Share build with 2-3 friends
- Observe them playing (don't guide)
- Take notes on confusion points

**Afternoon:**
- Collect feedback
- Analyze: Is core loop fun?
- **DECISION POINT:** Continue or pivot?

**Evening:**
- Document learnings
- Plan next steps (if continuing)
- Celebrate completion! 🎉

---

## SUCCESS METRICS (Day 15 Evaluation)

### Primary Question:
**"Is the core building loop satisfying?"**

### Evaluation Criteria:

**✅ PROTOTYPE SUCCEEDS IF:**
- Playtesters spent 20+ minutes playing without prompting
- Feedback includes words like "satisfying", "want more", "addictive"
- Core loop of "build → gather → expand" feels good
- At least 1 playtester asked "when can I play full version?"

**❌ PROTOTYPE FAILS IF:**
- Playtesters quit within 5-10 minutes
- Feedback is "boring", "repetitive", "what's the point?"
- No emotional response to building city
- You yourself don't want to play it

### Feedback Questions for Playtesters:
1. On a scale 1-10, how fun was the building process?
2. What frustrated you the most?
3. What did you enjoy the most?
4. Would you play this if it had more features? (yes/no)
5. What feature would make you DEFINITELY play this?

---

## RISK MANAGEMENT

### Top Risks & Mitigation:

**Risk 1: Scope Creep**
- **Mitigation:** Strictly follow "Out of Scope" list. No exceptions.
- **Daily check:** "Is this essential for core loop test?"

**Risk 2: Technical Blockers**
- **Mitigation:** Use AI help (Claude, ChatGPT) for specific problems
- **Backup:** Simplify feature if blocked >2 hours

**Risk 3: Art/Assets Issues**
- **Mitigation:** Quaternius pack is comprehensive, if missing something use cubes
- **Acceptance:** Prototype can look rough, gameplay matters

**Risk 4: Motivation Loss**
- **Mitigation:** Daily check-ins with Claude, public commitment
- **Backup:** Show progress to friends/family for accountability

**Risk 5: Core Loop Not Fun**
- **Mitigation:** Early testing (Day 10), pivot mid-development if needed
- **Acceptance:** This is why we prototype - to learn quickly

---

## POST-PROTOTYPE ROADMAP (If Successful)

### Phase 2 Features (Week 3-6):
- Building upgrades (Level 2, 3)
- Tech tree (unlock new buildings)
- Simple threats (bandits, wolves)
- Sound effects + background music
- Save/Load system
- Multiple maps

### Phase 3 Features (Month 2-3):
- Seasonal system (the USP we discussed)
- Advanced economy (trading, taxes)
- Citizen pathfinding (visual movement)
- Disasters (fire, famine)
- Steam integration

### Full Release Target:
- 6 months from prototype start (optimistic)
- 12 months realistic (solo dev + job search)

---

## APPENDIX A: QUICK REFERENCE

### Starting Values:
- Resources: 50 Food, 50 Wood, 50 Stone
- Population: 5/5 (starts at cap, need to build houses)
- Storage Cap: 100 per resource
- Map Size: 10x10 grid

### Building Quick Reference:
```
House:      2x2, Cost: 0F/10W/5S,  Effect: +5 max pop
Farm:       2x2, Cost: 10F/5W/0S,  Produce: +2 Food/sec, Workers: 1
Woodcutter: 2x2, Cost: 5F/0W/5S,   Produce: +1 Wood/sec, Workers: 1
Quarry:     3x3, Cost: 5F/10W/0S,  Produce: +1 Stone/sec, Workers: 2
Storage:    2x2, Cost: 5F/5W/5S,   Effect: +100 all caps
Town Hall:  3x3, Cost: FREE,       Effect: Aesthetic
```

### Controls:
- WASD / Arrows: Move camera
- Mouse Scroll: Zoom
- Q/E: Rotate (optional)
- Left Click: Confirm placement
- Right Click/ESC: Cancel placement

---

## APPENDIX B: ASSET LINKS & TOOLS

### Required Downloads:
1. **Quaternius Medieval Fantasy Pack**
   - https://quaternius.com/packs/medievalfantasy.html
   - Download → Import to Unity

2. **Unity Version**
   - Unity 2021.3 LTS or newer
   - 3D template

### Recommended Tools:
- **Trello** (project management): https://trello.com
- **Visual Studio** (code editor): Already have
- **Discord/Reddit** (accountability): r/gamedev

### Helpful Resources:
- Unity Grid System Tutorial: Search "Unity Tilemap 3D"
- Camera Controller: Search "Unity RTS camera controller"
- Singleton Pattern: Search "Unity Singleton pattern tutorial"

---

## DOCUMENT CONTROL

**Created By:** Claude + Vaibhav  
**Last Updated:** February 6, 2026  
**Version:** 1.0  
**Status:** LOCKED (No changes during 15-day sprint unless critical)

**Approval:**
- [ ] Developer (Vaibhav) - Read and understood scope
- [ ] Ready to start Day 1 tomorrow

---

## FINAL NOTES

**Remember:**
1. **Scope is locked** - resist urge to add features
2. **Done is better than perfect** - prototype is for learning
3. **Daily check-ins** - accountability keeps momentum
4. **Have fun** - you're building something!

**If you're reading this on Day 1 morning:**
🎉 **CONGRATULATIONS on starting!** 🎉

Follow the daily breakdown. Trust the process. See you on Day 15!

---

**Next Steps:**
1. Read this entire GDD
2. Download Quaternius assets
3. Review Day 1 tasks
4. Set up Unity project
5. Start building!

**Good luck, Vaibhav! Let's make something cool! 🚀**
