# HYTCG local frame catalog

The built-in templates include Classic, Petal, Arcane, Circuit, Royal, Moonlace, Starlit, Sakura Scroll, Obsidian Gate and Gilded Crest. The original Classic appearance is free. Creating any inventory frame costs 2,000 gems + 10 Frame fiber + 5 Sakura petals + 5 Arcane crystal. Each created instance can bind to one card; it is not a permanent design unlock shared across cards.

To add an artist frame, put `catalog.json` and optional transparent PNG overlays in this folder, including the installed `dist/assets/hytcg/frames/` folder, then restart the launcher. The catalog is a JSON array, maximum 54 entries / 256 KB. IDs are lowercase letters, digits, `_` or `-`, 2–32 characters, beginning with a letter. Names are at most 64 characters and descriptions at most 240.

`Design` selects procedural fallback geometry: `classic`, `petal`, `arcane`, `circuit`, `royal`, `moonlace`, `starlit`, `sakura`, `obsidian` or `gilded`. `Portrait` and `Landscape` separately place the artwork and native identity fields. Coordinates are fractions of card width/height from 0 to 1; each box must remain inside the card and have width/height at least 0.01. Keep name, series, rarity and number boxes readable and separate. Native renders the authenticated identity into these boxes. Optional `CardDescription` provides a display description up to 240 characters; line breaks are allowed.

```json
[
  {
    "Id": "my_artist_frame",
    "Name": "My artist frame",
    "Description": "An illustrated arch and name plaque.",
    "Design": "moonlace",
    "PortraitOverlay": "my_frame_portrait.png",
    "LandscapeOverlay": "my_frame_landscape.png",
    "Portrait": {
      "ArtworkX":0.075,"ArtworkY":0.09,"ArtworkWidth":0.85,"ArtworkHeight":0.60,
      "NameX":0.10,"NameY":0.735,"NameWidth":0.80,"NameHeight":0.10,
      "SeriesX":0.10,"SeriesY":0.85,"SeriesWidth":0.80,"SeriesHeight":0.055,
      "RarityX":0.70,"RarityY":0.022,"RarityWidth":0.22,"RarityHeight":0.048,
      "NumberX":0.08,"NumberY":0.924,"NumberWidth":0.84,"NumberHeight":0.045,
      "DescriptionX":0,"DescriptionY":0,"DescriptionWidth":0,"DescriptionHeight":0
    },
    "Landscape": {
      "ArtworkX":0.055,"ArtworkY":0.105,"ArtworkWidth":0.58,"ArtworkHeight":0.79,
      "NameX":0.675,"NameY":0.29,"NameWidth":0.275,"NameHeight":0.20,
      "SeriesX":0.675,"SeriesY":0.535,"SeriesWidth":0.275,"SeriesHeight":0.15,
      "RarityX":0.73,"RarityY":0.14,"RarityWidth":0.20,"RarityHeight":0.075,
      "NumberX":0.67,"NumberY":0.855,"NumberWidth":0.29,"NumberHeight":0.07
    }
  }
]
```

Overlay filenames must be plain PNG filenames in this folder; no URL, absolute path or traversal is accepted. Leave overlay fields empty to use native geometry. PNG resources are capped at 4 MB and normalized to a longest edge of 1600 px. Make the artwork window transparent; use portrait 750×1050 and landscape 1050×750 proportions. Native identity fields remain authoritative and are redrawn after decoration. Missing/corrupt optional overlays use procedural geometry.

Catalog edits define appearance only. All templates retain the compiled native creation price/material recipe; JSON cannot reduce those prices or substitute items. Description coordinates are optional: all zero hides the label, otherwise provide a valid normalized box. Native card identity is redrawn after the overlay. Created frame instances snapshot their two layouts and custom display description in the protected vault; imported overlays reference protected local artwork. Keep catalog and bundled overlay files with the launcher when moving installations. This folder contains display assets; ownership, consumed instances and wallet data remain in the protected account vault.
