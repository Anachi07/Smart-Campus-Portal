# Browser accessibility preferences

Use **Accessibility** at the top of the public campus site, sign-in pages, academic
workspace, queue pages, document verification page, or offline screen.

- **Text size:** Normal, Large (115%), or Extra large (130%). HTML text and form
  controls scale from their original theme sizes, including dynamically inserted
  content. Images, PDFs, and text painted inside canvas charts are not rewritten.
  Wide data tables without an existing responsive container gain a keyboard-scrollable
  region instead of making the entire mobile page wider.
- **Contrast:** device setting, Standard, or High contrast. High contrast uses dark
  text on white, distinct links, strong control borders, and visible focus outlines.
  Images and QR codes retain their colors. Native OS forced colors remain available.
- **Motion:** device setting or Reduce motion. The device's reduced-motion preference
  is always respected. Page transitions, queue button ripples, and chart animations
  respect reduced motion; live queue updates and form submissions continue normally.

Preferences apply immediately, without a page reload or clearing form input. The
nonmodal preference panel supports native keyboard navigation, Escape, a close
button, and focus restoration to its summary. It does not trap keyboard focus.
**Reset display preferences** restores device defaults and removes only this feature's
storage key. Browser zoom and native accessibility tools remain available.

Settings are stored under `icas-accessibility-v1` in local storage. Only the version,
text-size choice, contrast choice, and motion choice are saved. They are browser-wide,
including other accounts using that browser, and synchronize across open tabs of the
same site. No account IDs, records, form values, or authentication tokens are stored
in this preference record. Malformed preferences fall back safely. If storage is
blocked, controls still apply to the current page and explain that persistence may
be unavailable.

Preferences work independently of PWA installation, service-worker availability, or
notification permission. The public offline shell caches the shared preference CSS
and JavaScript so saved settings also work offline. Private pages and API responses
remain uncached. The service-worker public-shell cache is versioned to replace the
older shell. Display preferences are an enhancement, not a claim that all existing
charts, documents, content, and workflows have completed an accessibility audit.

## Implementation and verification

Shared public assets are `public/accessibility/preferences.js` and `preferences.css`,
served through the gateway allowlist. No new database tables, runtime dependencies,
cloud services, or credentials are required. Header/sidebar offsets account for the
preference and PWA toolbars. Controls are hidden when printing.

Browser check, against a running disposable demo:

```sh
CAMPUS_TEST_URL=http://127.0.0.1:8080 CAMPUS_CHROMIUM=/usr/bin/chromium node scripts/accessibility-browser.cjs
```

It checks actual font enlargement without compounding, desktop/mobile overflow,
high-contrast colors, keyboard open/Escape/focus, form-value preservation, dynamic
text, reload/navigation and cross-tab persistence, reset, offline settings, device
reduced motion, malformed saved values, and blocked browser storage. Existing normal
UI, live/PWA, and account/academic/queue regression suites remain applicable. The
application regression suite restores its initial archive: use disposable data only.
