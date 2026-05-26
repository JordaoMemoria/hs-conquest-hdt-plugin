# HS Conquest Helper — HDT plugin

In-game WR overlay for [Hearthstone Deck Tracker](https://github.com/HearthSim/Hearthstone-Deck-Tracker) that pulls matchup data from your Tab-4 matrix on [hsconquest.netlify.app](https://hsconquest.netlify.app).

## What it does

When a game starts, the plugin shows a small overlay:

```
   Druid vs Death Knight
   My deck: [▼ Aggro Druid           ]
   Opp deck: [▼ Unholy Death Knight   ]
            58.1%
          (53 games)
```

Both classes are detected from HDT automatically. You pick the **archetype** for each side from the dropdowns; the dropdown options are pre-filtered to the matching class. The WR appears once both are set.

## Setup (one time, ~30 seconds)

1. **Get a sync URL.** Open [hsconquest.netlify.app](https://hsconquest.netlify.app) → Tab 4 → scroll to the "Sync to HDT plugin" card → click **Sync to plugin** → copy the URL it shows.

2. **Install the plugin.**
   - Download `HsConquestPlugin.dll` (see [Building](#building) below or grab one from GitHub Actions).
   - Place it in `%AppData%\HearthstoneDeckTracker\Plugins\HsConquest\HsConquestPlugin.dll`. Create the folder if it doesn't exist.
   - Restart HDT.

3. **Configure.** In HDT: `Options → Tracker → Plugins`, find "HS Conquest Helper", click **Settings**.
   - Paste your sync URL into the box.
   - Click **Reload matrix**. You should see "OK — N archetypes".
   - **Save**. That's it — no per-deck mapping required.

4. **Play a game.** The overlay appears at the top-left of your screen with two dropdowns pre-filtered to each side's class. Pick your archetype and the opponent's archetype (often turn 1–2 once they've shown their hand). The WR appears.

## Building

The plugin is a .NET Framework 4.7.2 WPF DLL. You have two ways to get one:

### Option 1 — Let GitHub Actions build it

Every push to `main` triggers a Windows build on GitHub's CI. The DLL appears as a downloadable artifact:

- Go to the Actions tab of this repo
- Click the latest successful run
- Scroll to **Artifacts** → download `HsConquestPlugin.zip`
- Unzip → `HsConquestPlugin.dll` is inside

### Option 2 — Build locally

Requires Visual Studio Community (free) on Windows. After installing HDT to its default location:

```powershell
msbuild HsConquestPlugin.csproj /p:Configuration=Release
```

DLL lands at `bin\Release\HsConquestPlugin.dll`.

If your HDT install is elsewhere, set the path explicitly:

```powershell
msbuild HsConquestPlugin.csproj /p:Configuration=Release /p:HdtPath="C:\path\to\HDT"
```

## Troubleshooting

**"No matrix data" in the overlay** — the matrix didn't fetch. Open settings, hit Reload, check that the sync URL is current.

**Overlay doesn't appear at all** — check HDT's log at `%AppData%\HearthstoneDeckTracker\hdt_log.txt` for `[HsConquest]` lines. Common causes: HDT version mismatch (plugin was built against a newer HDT API), or the plugin DLL is in the wrong folder.

**Dropdown is empty after the placeholder** — that class isn't represented in your matrix slice. Sync a broader matrix on hsconquest.netlify.app (different rank/timeframe filters) and Reload in the plugin settings.

## Privacy

The plugin only makes HTTP GET requests to the sync URL you provided. No analytics, no other network traffic. The matchup data is public — anyone with the URL can read it, so don't share the URL anywhere you wouldn't want others to see your synced matrix.
