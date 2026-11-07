# iPhone / iPad installation

ICAS SUCAT is a web app. Android Chromium can provide a native install prompt through `beforeinstallprompt`. Safari and Chrome on iPhone do not expose an equivalent one-click website installation API. A website cannot add itself to the iOS Home Screen or press the final Add button for the user.

The **Install on iPhone / iPad** button opens a keyboard-accessible on-page guide:

1. Open the public HTTPS site in Safari. From Facebook/Messenger/Instagram, open the link in Safari first or copy it into Safari.
2. Tap Share (square with upward arrow); some Safari layouts place this inside More (…).
3. Choose **Add to Home Screen**. Scroll the actions if necessary.
4. Keep **ICAS SUCAT**, enable **Open as Web App** if offered, then tap **Add**.

The install guide is available independently of service-worker support/registration. It does not imitate a native iOS install prompt or use undocumented installation links. It does not automatically open Share, install a profile, redirect to an app store or request permissions. The installed standalone app hides the install button. iPad desktop-mode detection uses MacIntel plus touch support. In-app browsers get an explicit Safari handoff instruction. The existing Android native prompt remains user-triggered, with browser-menu instructions when no prompt is available.

Public, academic, sign-in and queue layouts declare Apple standalone capability, the ICAS SUCAT title and a dedicated 180×180 Apple touch icon. That icon uses the original transparent logo over the site's cream icon canvas for iOS rendering; existing transparent Android/website logos are unchanged. The app ID, origin, start URL and private-page/offline-cache policy remain unchanged. iPhone browser-alert controls are suppressed outside standalone mode; no background push service is added.

## Verification and limits

```sh
CAMPUS_TEST_URL=http://127.0.0.1:8080 node scripts/pwa-install-browser.cjs
```

The Chromium tests emulate iPhone Safari/Chrome, an in-app browser, desktop-mode iPad and standalone iOS; they check instructions, Escape/focus, layout, metadata/icon dimensions, failed service-worker registration and preservation of the Android install event. This is not a physical iPhone installation test.

The test also supports `CAMPUS_BROWSER=webkit` when Playwright WebKit is installed. In this environment WebKit was initially absent; the official download attempt was blocked by network policy (403 Domain forbidden). No Safari-engine or physical iOS installation pass is claimed. After Render deployment, test Share → Add to Home Screen and launch/sign-in on an actual iPhone. iOS version, browser UI and device restrictions can affect available menu actions.
