# Automatic responsive layout

The site automatically adapts to phone, tablet and desktop viewport widths. There is no Accessibility menu or manual text-size/contrast/motion selector. Native browser zoom and operating-system accessibility tools remain available; the site does not disable zoom or shrink the entire page with a transform.

The shared layout assets retain their existing `public/accessibility/preferences.js` and `preferences.css` paths for older pages and public offline compatibility. They now:

- Remove the retired `icas-accessibility-v1` local-storage setting when possible, and ignore it even if storage is blocked. No replacement preference data is saved.
- Use the device's reduced-motion, increased-contrast and forced-color preferences automatically, including chart animation updates.
- Keep images, inputs and grid children inside their available width, apply fluid heading sizes, and use readable mobile form input sizes.
- Wrap wide tables in a keyboard-scrollable region so all columns remain available without stretching the whole page. Wide tables scroll within their container; charts and PDFs are not rewritten into mobile cards.
- Recalculate sticky toolbar/header offsets and table overflow after resizing or dynamic content changes. Footer app controls are excluded from sticky-header height.

Private pages, submitted values and account records remain outside the offline cache. Public-shell cache v4 updates the existing four public offline assets. No database tables, dependencies or credentials are added. Responsive behavior does not constitute a complete content/PDF/chart accessibility audit.

## Verification

Against a running disposable demo:

```sh
CAMPUS_TEST_URL=http://127.0.0.1:8080 CAMPUS_CHROMIUM=/usr/bin/chromium node scripts/accessibility-browser.cjs
```

The existing command now verifies automatic layout at 320, 390, 768 and 1440 CSS pixels, landscape resizing, login/academic/admin/queue/forms pages, absence of manual controls and saved scaling, wide dynamic tables, device reduced motion/forced colors and blocked storage. Existing normal browser and live/PWA checks remain applicable. A finite set of viewport checks cannot establish that every future page, third-party document or device is free of clipping.
