# Basic Colony Sim Gameplay Guide

## Overview

Basic Colony Sim is a small grid-based colony builder where you grow a settlement by placing roads and buildings, assigning villagers to production work, and keeping the economy alive long enough to complete the quest chain.

The current project is not only a peaceful builder anymore. In the current implementation, the colony also has:

- villager movement and hauling
- food consumption at the end of each day
- job priority management
- enemy waves starting on Day 3
- defeat if the Town Hall is destroyed
- a quest chain that currently drives the main victory condition

## What You Do As The Player

Your main job is to expand a functioning settlement.

You place roads first, then place production and housing buildings next to those roads, and the game automatically assigns villagers to jobs based on available population and job priority. Those villagers physically travel to work sites, gather resources, carry them to a drop-off building, and return to work.

In practice, the loop is:

1. Build roads to open valid building spots.
2. Build Wood Cutters and Farms to stabilize wood and food.
3. Build Houses to increase population.
4. Build a Quarry when you need stone.
5. Build Storage to raise resource caps and create more drop-off capacity.
6. Adjust job priorities if the colony is staffed badly.
7. Survive daily food upkeep and enemy pressure.
8. Progress through quests until the victory quest is completed.

## Core Rules Of This Build

### Placement Rules

- Buildings snap to the grid.
- Buildings can only be placed on valid empty tiles.
- Buildings must be adjacent to a road area.
- Roads are placed on the grid and occupy tiles.
- Road placement can be dragged in straight horizontal and vertical segments.

### Resource Rules

The colony uses three resources:

- Food
- Wood
- Stone

Verified starting values in code:

- Food: `20`
- Wood: `30`
- Stone: `0`
- Base storage cap: `100` for each resource

Storage buildings increase all caps by `50`.

### Population Rules

- Population starts at `5/5` in `PopulationManager`.
- Houses add `+5` population capacity.
- The current Town Hall data also has `+5` population capacity.
- Workers are auto-assigned to buildings that need labor.
- If there are not enough free workers, higher-priority jobs can pull workers from lower-priority jobs.

### Villager Work Rules

Villagers are not abstract counters in this build. They exist as moving units.

Their work flow is:

1. Leave home or a nearby anchor building.
2. Walk to the workplace.
3. Harvest resources over time.
4. Carry cargo to a drop-off building.
5. Deposit resources.
6. Return to work.

Roads matter for logistics because villagers move slower off-road. Their movement speed is reduced to `50%` when not on a road tile.

## Controls

### Camera

- `WASD` or arrow keys: move camera
- Mouse wheel: zoom
- `Q` / `E`: rotate camera

### Building And Road Placement

- Left click: place building or place road drag path
- Right click or `Esc`: cancel current placement mode
- `R`: toggle road placement mode

### Selection And Management

- Left click a placed building: open building info
- In building info, production buildings can be toggled on or off
- Demolish mode is handled through the demolish UI button
- While demolish mode is active, left click a building or road to remove it

## Buildings

These are the current building values from the live building data assets.

| Building | Cost | Size | Workers | Output | Other Effects |
|---|---|---:|---:|---|---|
| Town Hall | Free | 3x3 | 0 | None | Drop-off point, currently has `+5` population capacity, defeat target |
| House | 5 Food, 15 Wood, 5 Stone | 1x1 | 0 | None | `+5` population capacity |
| Farm | 0 Food, 10 Wood, 0 Stone | 1x1 | 1 | 1 Food per harvest | Harvest time `3s` |
| Wood Cutter | 5 Food, 0 Wood, 0 Stone | 2x2 | 2 | 1 Wood per harvest | Harvest time `2s` |
| Quary | 10 Food, 15 Wood, 0 Stone | 3x3 | 2 | 1 Stone per harvest | Harvest time `2s` |
| Storage | 10 Food, 10 Wood, 10 Stone | 2x2 | 0 | None | Drop-off point, `+50` to all storage caps |

Notes:

- `Quary` is spelled that way in the current asset data.
- Production happens through villager harvesting and drop-off, not by instantly adding resources globally every second.
- A production building stops contributing if it has no workers or if you toggle it off.

## Day Cycle, Hunger, And Survival Pressure

### Day Cycle

- A day lasts `60` seconds by default.
- The UI shows the current day and a countdown to the next day.

### Food Consumption

At the end of each day, the colony consumes:

- `2 food per villager per day`

If you do not have enough food:

- some villagers are treated as hungry
- production efficiency is reduced
- the hungry warning UI appears

The current starvation logic reduces production efficiency based on how much of the population was fed, with a minimum clamp of `0.5`.

### Enemy Waves

- Enemy waves begin on Day `3`
- When a wave begins, villagers are sent home
- A survival timer starts for `120` seconds
- Enemies spawn around the colony and move toward the Town Hall
- If they destroy the Town Hall, the game triggers defeat and pauses
- After the survival timer ends, villagers return to work

## Quests And Current Win Condition

The quest chain is the clearest explanation of intended progression in the current build.

Current quest flow:

1. Build 1 Wood Cutter
2. Build 1 Farm
3. Assign workers to 2 different buildings
4. Build 1 Quarry
5. Reach 15 population
6. Build 1 Storage
7. Reach 30 population
8. Collect 100 food
9. Collect 100 wood
10. Reach 100 population
11. Survive 15 days

Important implementation note:

- The visible `WinCondition` script has a `100 population` win display function, but the actual quest-driven victory is triggered by the final quest: `Survive 15 Days`.
- Defeat happens immediately if the Town Hall is destroyed.

## How A Typical Early Game Should Go

If someone is opening the project and asking "how do I play this build?", the intended opening looks like this:

1. Use the camera to inspect the starting Town Hall area.
2. Place roads outward from the existing settlement.
3. Build a Wood Cutter first so wood income begins.
4. Build a Farm so daily food upkeep does not crush production.
5. Watch worker assignment and idle villager count.
6. Add Houses when you need more labor.
7. Add a Quarry when stone unlocks as a bottleneck.
8. Build Storage before hitting resource caps too often.
9. Reorder job priorities if food or wood stalls.
10. Prepare for Day 3 onward when enemies start attacking.

## What Is Happening Under The Hood

This section is useful for anyone reading the project rather than only playing it.

- Resources are only added after villagers deliver cargo to a drop-off building.
- Town Hall and Storage both act as drop-off buildings.
- Buildings can be selected to inspect worker count, capacity, drop-off status, and production data.
- Production can be manually disabled per production building, which releases its workers for reassignment.
- The UI also shows idle villagers, which is important for spotting labor shortages.

## Important Caveats For This Project

- Some older design notes in the repo describe a simpler peaceful prototype, but the current codebase includes combat pressure and villager simulation.
- Some CSV economy notes are partially outdated compared to the live scripts and asset data.
- The gameplay guide above reflects the current implemented behavior in `Assets/Scripts` and the current `BuildingData` / `Quest` assets.
