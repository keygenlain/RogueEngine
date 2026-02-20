# RogueEngine

RogueEngine is a TypeScript/React foundation for building ASCII-based roguelikes.

## Core Modules

- **ASCII Renderer** (`src/engine/asciiRenderer.ts`)
  - Unicode character cells with foreground/background color metadata
  - Layered rendering pipeline: `Base`, `Entities`, `Particles`, `UI`
  - Field-of-view (FOV) calculation support
- **Visual Scripting System** (`src/engine/visualScripting.ts`)
  - Node graph with `Event` and `Action` nodes
  - Supported events: `OnPlayerStep`, `OnDeath`, `OnTurnStart`
  - Supported actions: `SpawnEntity`, `ModifyHealth`, `ShowMessage`
- **Drag-and-Drop Menu Builder** (`src/engine/menuBuilder.tsx`)
  - Grid-snapped HUD/menu editor component
  - Draggable widget palette for quick UI assembly

## HTML5 Export

RogueEngine is a browser-first React application and exports to HTML5 using Vite:

```bash
npm run build
```

The generated HTML5 bundle is emitted to `dist/`.

## Development

```bash
npm install
npm run dev
```
