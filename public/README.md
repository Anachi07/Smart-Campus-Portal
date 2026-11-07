# Campus app assets

The gateway serves only the named public assets in `scripts/gateway.mjs`.
The manifest provides install icons and shortcuts. Installation requires HTTPS
(or localhost) and a supported browser. iPhone/iPad users get an explicit install-guide button for Share → Add to Home Screen; iOS does not provide Android’s one-click install API. See [IPHONE_INSTALL.md](../IPHONE_INSTALL.md).

The service worker caches only the generic offline page, app icon, and shared responsive-layout CSS/JavaScript.
Private pages, API responses, documents, and submitted forms are never cached.
No transactions can be submitted offline. Browser alerts require explicit user
permission and an open, visible portal tab; there is no background push service.
OS notifications use generic text without student names or transaction details.

The installed app name and short name are **ICAS SUCAT**. The 192px and 512px PNG icons render the existing `sims/assets/images/icas-logo.png` with transparent backgrounds and safe padding. The SVG wrapper embeds the same transparent logo; the school logo has its white/off-white background removed. Manifest icon URLs and Apple touch icons include a branding version, and offline cache v5 refreshes the icon while keeping the same public-only allowlist. Existing installed apps may require a browser-managed update or removal and reinstallation to refresh their OS name/icon. Update all icon formats and cache versions together for future branding changes.

Apple layouts reference `/pwa/apple-touch-icon.png?v=ios-1` (180×180) and include standalone-capable/name metadata. Website/Android icon assets are unchanged. The in-page guide remains available even if service-worker registration fails.
