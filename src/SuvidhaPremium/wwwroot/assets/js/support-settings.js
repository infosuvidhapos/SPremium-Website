(function(){
  'use strict';
  const q=s=>document.querySelector(s);
  let loaded=false;

  function ensureCard(){
    const grid=q('#view-settings .settings-grid');
    if(!grid||q('#supportWhatsAppCard'))return;
    const card=document.createElement('section');
    card.className='panel setting-card';
    card.id='supportWhatsAppCard';
    card.innerHTML='<div style="width:100%"><span class="panel-kicker">CUSTOMER SUPPORT</span><h3>WhatsApp Support Number</h3><p>This number is used by POS Contact Support / subscription renewal buttons.</p><div class="form-group" style="margin-top:14px"><label>WhatsApp Number with Country Code</label><input id="supportWhatsAppInput" class="input-control" inputmode="tel" placeholder="918271718844" autocomplete="off"><small>Example: 918271718844. POS message stays fixed and includes the activated Outlet Code automatically.</small></div><button class="primary-btn" type="button" id="saveSupportWhatsAppBtn" style="margin-top:12px">Save Support Number</button><div id="supportWhatsAppStatus" style="margin-top:10px"></div></div>';
    grid.appendChild(card);
    q('#saveSupportWhatsAppBtn').addEventListener('click',save);
  }

  async function load(){
    ensureCard();
    const input=q('#supportWhatsAppInput'),status=q('#supportWhatsAppStatus');
    if(!input)return;
    try{
      const r=await fetch('/api/admin/settings/support',{cache:'no-store',headers:{'Accept':'application/json'}});
      const x=await r.json().catch(()=>({}));
      if(!r.ok)throw new Error(x.message||('HTTP '+r.status));
      input.value=x.supportWhatsApp||'';
      if(status)status.textContent='Current central support number loaded.';
      loaded=true;
    }catch(e){if(status)status.textContent='Support setting load failed: '+e.message;}
  }

  async function save(){
    const input=q('#supportWhatsAppInput'),btn=q('#saveSupportWhatsAppBtn'),status=q('#supportWhatsAppStatus');
    if(!input||!btn)return;
    btn.disabled=true;btn.textContent='Saving...';
    try{
      const r=await fetch('/api/admin/settings/support',{method:'PUT',headers:{'Content-Type':'application/json','Accept':'application/json'},body:JSON.stringify({supportWhatsApp:input.value})});
      const x=await r.json().catch(()=>({}));
      if(!r.ok)throw new Error(x.message||('HTTP '+r.status));
      input.value=x.supportWhatsApp||input.value;
      if(status)status.textContent='Saved. All POS Contact Support buttons now use '+input.value+'.';
    }catch(e){if(status)status.textContent='Save failed: '+e.message;}
    finally{btn.disabled=false;btn.textContent='Save Support Number';}
  }

  ensureCard();
  document.addEventListener('click',function(e){
    const b=e.target&&e.target.closest?e.target.closest('[data-view="settings"]'):null;
    if(b)setTimeout(load,0);
  },true);
  if(q('#view-settings')&&q('#view-settings').classList.contains('active'))load();
  window.loadSupportWhatsAppSetting=load;
})();
