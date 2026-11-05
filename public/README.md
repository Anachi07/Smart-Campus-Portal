# Campus app assets

The gateway serves only the named public assets in `scripts/gateway.mjs`.
The manifest provides install icons and shortcuts. Installation requires HTTPS
(or localhost) and a supported browser. iOS users use Share → Add to Home Screen.

The service worker caches only the generic offline page and vector app icon.
Private pages, API responses, documents, and submitted forms are never cached.
No transactions can be submitted offline. Browser alerts require explicit user
permission and an open, visible portal tab; there is no background push service.
OS notifications use generic text without student names or transaction details.

The original vector icon is `pwa/icon.svg`; the 192px and 512px PNGs are rendered
from it. Update both PNG assets and the cache version if changing the offline shell.
