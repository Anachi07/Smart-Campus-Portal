function updateClock(){
  const el = document.getElementById('clock');
  if(el){
    el.textContent = new Date().toLocaleTimeString([], { hour:'2-digit', minute:'2-digit', second:'2-digit' });
  }
}
setInterval(updateClock, 1000);
updateClock();

document.addEventListener('input', function(e){
  const input = e.target;
  if(input && input.matches('input[inputmode="numeric"]')){
    input.value = input.value.replace(/\D/g, '').slice(0, 11);
  }
});

function ensureNotificationBadge(){
  const link = document.querySelector('.notification-link');
  if(!link) return null;
  let badge = link.querySelector('.notif-badge');
  if(!badge){
    badge = document.createElement('span');
    badge.className = 'notif-badge';
    badge.style.display = 'none';
    link.appendChild(badge);
  }
  return badge;
}

function setNotificationBadge(count){
  const badge = ensureNotificationBadge();
  if(!badge) return;
  if(count > 0){
    badge.textContent = count;
    badge.style.display = 'inline-grid';
  } else {
    badge.textContent = '';
    badge.style.display = 'none';
  }
}

function showSmartQueueToast(title, message){
  const toast = document.createElement('div');
  toast.className = 'smartqueue-toast';
  toast.innerHTML = '<strong>' + escapeHtml(title || 'New notification') + '</strong><span>' + escapeHtml(message || '') + '</span>';
  document.body.appendChild(toast);
  setTimeout(function(){ toast.classList.add('show'); }, 20);
  setTimeout(function(){
    toast.classList.remove('show');
    setTimeout(function(){ toast.remove(); }, 240);
  }, 5200);
}

function escapeHtml(value){
  return String(value || '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}

function startNotificationPolling(){
  const url = document.body.dataset.notificationSummaryUrl;
  if(!url) return;

  let lastId = Number(sessionStorage.getItem('smartqueue-last-notification-id') || '0');
  const initialUnread = Number(document.body.dataset.initialUnread || '0');
  setNotificationBadge(initialUnread);

  function poll(){
    fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
      .then(function(res){ return res.ok ? res.json() : null; })
      .then(function(data){
        if(!data) return;
        setNotificationBadge(Number(data.unreadCount || 0));
        const latestId = Number(data.latestId || 0);
        if(latestId > 0 && lastId > 0 && latestId !== lastId){
          showSmartQueueToast(data.latestTitle, data.latestMessage);
        }
        if(latestId > 0){
          lastId = latestId;
          sessionStorage.setItem('smartqueue-last-notification-id', String(latestId));
        }
      })
      .catch(function(){});
  }

  poll();
  setInterval(poll, 8000);
}

function setupNavToggle(){
  const btn = document.querySelector('[data-nav-toggle]');
  const nav = document.querySelector('[data-topnav]');
  if(!btn || !nav) return;
  btn.addEventListener('click', function(){ const open=nav.classList.toggle('open');btn.setAttribute('aria-expanded',String(open)); });
  document.addEventListener('keydown',e=>{if(e.key==='Escape'){nav.classList.remove('open');btn.setAttribute('aria-expanded','false');}});
}

function setupButtonRipple(){
  document.addEventListener('click', function(e){
    const btn = e.target.closest('.btn');
    if(!btn) return;
    btn.animate([{transform:'scale(.98)'},{transform:'scale(1)'}], {duration:160, easing:'ease-out'});
  });
}

document.addEventListener('DOMContentLoaded', function(){
  document.body.classList.add('page-ready');
  document.querySelectorAll('form').forEach(function(form){
    form.addEventListener('submit', function(){
      const btn = form.querySelector('button[type="submit"]');
      if(btn){ btn.classList.add('is-loading'); }
    });
  });
  setupNavToggle();
  setupButtonRipple();
  startNotificationPolling();
});
