(() => {
  'use strict';
  // Retire manual display settings so old saved zoom choices do not affect the layout.
  try { localStorage.removeItem('icas-accessibility-v1'); } catch {}
  const root = document.documentElement;
  const reduced = matchMedia('(prefers-reduced-motion: reduce)');
  const contrast = matchMedia('(prefers-contrast: more)');
  const forced = matchMedia('(forced-colors: active)');
  const observed = new WeakSet();
  let observer, frame;
  root.dataset.campusSize = 'normal';
  function devicePreferences() {
    root.dataset.campusContrast = contrast.matches || forced.matches ? 'high' : 'standard';
    root.dataset.campusMotion = reduced.matches ? 'reduce' : 'standard';
    for (const chart of Object.values(window.Chart?.instances || {})) {
      chart.stop(); chart.options.animation = reduced.matches ? false : undefined; chart.update('none');
    }
  }
  devicePreferences();
  function refresh() {
    frame = null;
    const bars = [...document.querySelectorAll('.campus-app-tools')].filter(el => getComputedStyle(el).position === 'sticky');
    const height = bars.reduce((sum, bar) => sum + bar.getBoundingClientRect().height, 0);
    root.style.setProperty('--campus-tools-height', height + 'px');
    root.style.setProperty('--campus-a11y-height', '0px');
    const header = document.querySelector('body.app-body > .topbar');
    if (header) root.style.setProperty('--campus-header-height', header.getBoundingClientRect().height + 'px');
    for (const table of document.querySelectorAll('table')) {
      if (!table.closest('.table-responsive,.campus-table-scroll')) {
        const wrapper = document.createElement('div'); wrapper.className = 'campus-table-scroll'; table.before(wrapper); wrapper.append(table);
      }
    }
    for (const wrapper of document.querySelectorAll('.campus-table-scroll,.table-responsive')) {
      if (wrapper.scrollWidth > wrapper.clientWidth + 1) {
        wrapper.tabIndex = 0; wrapper.setAttribute('role', 'region'); wrapper.setAttribute('aria-label', 'Scrollable data table');
      } else {
        wrapper.removeAttribute('tabindex');
        if (wrapper.getAttribute('aria-label') === 'Scrollable data table') { wrapper.removeAttribute('role'); wrapper.removeAttribute('aria-label'); }
      }
      if (observer && !observed.has(wrapper)) { observed.add(wrapper); observer.observe(wrapper); }
    }
    for (const element of [...bars, ...(header ? [header] : [])]) {
      if (observer && !observed.has(element)) { observed.add(element); observer.observe(element); }
    }
  }
  function schedule() { if (!frame) frame = requestAnimationFrame(refresh); }
  function ready() {
    if ('ResizeObserver' in window) observer = new ResizeObserver(schedule);
    new MutationObserver(schedule).observe(document.body, {childList: true, subtree: true});
    window.addEventListener('resize', schedule); document.fonts?.ready.then(schedule); refresh();
  }
  for (const media of [reduced, contrast, forced]) media.addEventListener('change', devicePreferences);
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', ready, {once: true}); else ready();
})();
