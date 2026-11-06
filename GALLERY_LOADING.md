# Campus photos and loading logo

Admin/Super Admin opens **School settings → Campus photos** to upload the original reception, entrance and campus grounds photos. Choose JPG/PNG files under 750 KB each, up to 4000 × 4000 pixels. Captions are editable. Saving publishes the selected photos; leaving a file field blank keeps its current image, while Remove deletes that photo. The public hero uses the entrance photo when available, then the first uploaded photo. A gallery displays all available photos. Image containers use `object-fit: contain`, with a single-column phone layout so the original images are not cropped.

No campus photographs are fabricated or seeded. In this session the three original photographs were visible as chat previews but were not exposed as downloadable files or local attachments. They still need to be supplied as downloadable originals (a ZIP works) or uploaded through School settings. Tests use temporary logo fixtures only on disposable data and restore the original backup afterward.

Uploads require existing Admin/Super Admin permissions and CSRF validation. Images are checked by content type/dimensions and stored as bounded JPEG/PNG data URLs in the existing settings table, not as executable files or external image URLs. Invalid uploads leave current settings intact. Image/caption values are escaped before rendering. Gallery images are intentionally public once saved. Complete paired backups include uploaded images and captions in the academic database; no new tables, services, file volume or credentials are needed. The three-image cap bounds additional database and backup size. Keep downloading backups when using ephemeral free Render storage.

The website shows a transparent ICAS logo with a gentle 1.25-second pulse while the page loads, then removes it after the load event. A five-second fallback removes it even when a resource hangs. It does not intercept navigation, delay submissions, block pointer input, or run for background queue polling. Device reduced motion uses a static logo. With JavaScript disabled there is no overlay. Browser/OS-controlled installed-app splash screens remain outside the website's animation control.

The loader uses `/sims/assets/css/loading.css` and `/sims/assets/js/loading.js` on the public homepage, academic/auth layouts and .NET queue layout. Its icon is the existing public app icon. New page assets and campus photos are not added to the public offline cache; private pages remain uncached.

## Verification

Run sequentially against disposable data; both tests restore their initial archive:

```sh
CAMPUS_TEST_URL=http://127.0.0.1:8080 CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1 python3 scripts/gallery-test.py
CAMPUS_TEST_URL=http://127.0.0.1:8080 CAMPUS_ALLOW_DESTRUCTIVE_TESTS=1 node scripts/gallery-loading-browser.cjs
```

Checks include upload/oversize/type validation, caption escaping, gallery/hero selection, backup/restore, removal, phone/tablet/desktop layouts, slow-load pulse behavior, reduced motion, timeout and no-JavaScript fallback. These do not claim native OS splash animation support.
