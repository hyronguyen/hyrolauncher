# Third-party notices

## Microsoft Edge WebView2

HYROLauncher uses the Microsoft WebView2 SDK (`Microsoft.Web.WebView2`) and the Evergreen WebView2 Runtime. These are Microsoft components governed by their own license terms. The release includes the SDK license file supplied in the restored NuGet package; the runtime installer is supplied by Microsoft.

- [Official SDK package](https://www.nuget.org/packages/Microsoft.Web.WebView2)
- [Official distribution guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution)

## Lucide and Feather icons

SVG icons in `assets/icons` and `assets/builtin-plugins/hyrowser/plugin asset` include Lucide icons. Lucide is distributed under the ISC license. Some Lucide icons derive from Feather and retain Feather's MIT license attribution.

- Lucide: Copyright (c) 2026 Lucide Icons and Contributors. [Upstream license](https://github.com/lucide-icons/lucide/blob/main/LICENSE).
- Feather: Copyright (c) 2013–2023 Cole Bemis. [Upstream license](https://github.com/feathericons/feather/blob/master/LICENSE).

The unmodified upstream icon license texts accompany distributions in `licenses`.

## UI references

The bookshelf interaction was inspired by [mawise/bookshelf](https://github.com/mawise/bookshelf), and holographic card interaction by [pokemon-cards-css](https://github.com/simeydotme/pokemon-cards-css). HYRO's implementations use local CSS, JavaScript and Canvas, without bundling those projects as dependencies.

## External data and artwork

HYTCG and the game library query external services such as Anime Characters Database, Zerochan, AniList, IGDB and Steam. Downloaded character artwork, game covers, names and other third-party data remain subject to their original owners' rights and the applicable service terms. They are user data and are not included in the source repository or fresh installer.

HYRO branding, bundled illustration/audio assets and character-inspired pet assets are not covered by the third-party software licenses above. A software license does not grant rights to third-party characters or trademarks.
