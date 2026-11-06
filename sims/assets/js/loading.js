(() => {
  'use strict';
  let overlay, observer, finished = false;
  const reduced = matchMedia('(prefers-reduced-motion: reduce)');
  function mount() {
    if (!document.body || overlay || finished) return;
    overlay = document.createElement('div'); overlay.className = 'campus-loader';
    overlay.setAttribute('role', 'status'); overlay.setAttribute('aria-label', 'Loading ICAS SUCAT');
    overlay.innerHTML = '<div class="campus-loader-inner"><img src="/pwa/icon-192.png?v=icas-sucat-2" width="110" height="110" alt=""><p>ICAS SUCAT</p></div>';
    document.body.prepend(overlay); observer?.disconnect();
    reduced.addEventListener('change', () => {if (overlay && reduced.matches) overlay.querySelector('img').style.animation = 'none';});
  }
  function finish() {
    if (finished) return; finished = true; observer?.disconnect(); clearTimeout(fallback);
    if (overlay) {overlay.classList.add('is-ready'); setTimeout(() => {overlay?.remove(); overlay = null;}, reduced.matches ? 0 : 180);}
  }
  const fallback = setTimeout(finish, 5000);
  if (document.body) mount(); else {observer = new MutationObserver(mount); observer.observe(document.documentElement, {childList: true});}
  window.addEventListener('load', finish, {once: true});
  window.addEventListener('pageshow', event => {if (event.persisted) finish();});
  document.addEventListener('DOMContentLoaded', () => {mount(); if (document.readyState === 'complete') finish();}, {once: true});
  if (document.readyState === 'complete') finish();
})();
