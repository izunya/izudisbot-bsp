namespace IzudisbotBSP
{
    /// <summary>
    /// 로컬 웹 UI 의 단일 HTML 페이지 (영어/한국어 토글 내장).
    /// C# verbatim 문자열이라 큰따옴표는 피하고 HTML 속성/JS 문자열은 작은따옴표·백틱으로만 작성.
    /// </summary>
    public static class WebUiPage
    {
        public const string Html = @"<!doctype html>
<html lang='en' data-bs-theme='dark'>
<head>
<meta charset='utf-8'>
<meta name='viewport' content='width=device-width, initial-scale=1'>
<title>izudisbot Discord Bridge</title>
<link href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css' rel='stylesheet'>
<style>
 body{padding-bottom:48px}
 .log{font-family:ui-monospace,Consolas,monospace;font-size:.84rem;max-height:380px;overflow-y:auto}
 .log .row-f{padding:1px 0}
 .log .muted{opacity:.4}
 code{color:#7aa2f7}
</style>
</head>
<body class='bg-dark'>
 <nav class='navbar bg-body-tertiary border-bottom'>
  <div class='container'>
   <span class='navbar-brand mb-0'>🟦 izudisbot Discord Bridge</span>
   <span>
    <button id='langbtn' class='btn btn-sm btn-outline-secondary me-2' onclick='toggleLang()'>한국어</button>
    <span id='conn' class='badge text-bg-secondary'>…</span>
   </span>
  </div>
 </nav>

 <div class='container mt-4'>
  <div class='row g-4'>

   <div class='col-lg-6'>
    <div class='card'>
     <div class='card-header' data-i18n='card.connection'></div>
     <div class='card-body'>
      <div class='mb-3'>
       <label class='form-label' data-i18n='label.token'></label>
       <div class='input-group'>
        <input id='token' type='password' class='form-control' placeholder='bsp_...'>
        <button class='btn btn-outline-secondary' type='button' id='showbtn' onclick='toggleTok()' data-i18n='btn.show'></button>
       </div>
      </div>
      <div class='row'>
       <div class='col-7 mb-3'>
        <div class='form-check'>
         <input class='form-check-input' type='checkbox' id='auto'>
         <label class='form-check-label' for='auto' data-i18n='label.auto'></label>
        </div>
        <div class='form-check'>
         <input class='form-check-input' type='checkbox' id='cmdonly'>
         <label class='form-check-label' for='cmdonly' data-i18n='label.cmdonly'></label>
        </div>
        <div class='form-check'>
         <input class='form-check-input' type='checkbox' id='launch'>
         <label class='form-check-label' for='launch' data-i18n='label.launch'></label>
        </div>
       </div>
       <div class='col-5 mb-3'>
        <label class='form-label' data-i18n='label.interval'></label>
        <input id='ivl' type='number' min='1' class='form-control'>
       </div>
      </div>
      <button class='btn btn-primary' onclick='save()' data-i18n='btn.save'></button>
      <span id='saved' class='ms-2 text-success small'></span>
     </div>
    </div>

    <div class='card mt-4'>
     <div class='card-header' data-i18n='card.status'></div>
     <div class='card-body' id='status'>…</div>
    </div>
   </div>

   <div class='col-lg-6'>
    <div class='card'>
     <div class='card-header d-flex justify-content-between align-items-center'>
      <span data-i18n='card.channels'></span><span class='text-secondary small' data-i18n='ch.toggle'></span>
     </div>
     <div class='card-body p-2'>
      <table class='table table-sm align-middle mb-0'><tbody id='channels'></tbody></table>
     </div>
    </div>
   </div>

  </div>

  <div class='card mt-4'>
   <div class='card-header d-flex justify-content-between align-items-center'>
    <span><span data-i18n='card.log'></span> <span class='text-secondary small' data-i18n='log.sub'></span></span>
    <button class='btn btn-sm btn-outline-secondary' onclick='clearLog()' data-i18n='btn.clear'></button>
   </div>
   <div class='card-body log' id='log'></div>
  </div>
 </div>

<script>
const $=id=>document.getElementById(id);
let inited=false;
const LANGS=['en','ko','ja'];
const LANGNAME={en:'English',ko:'한국어',ja:'日本語'};
let lang=localStorage.getItem('lang')||'en';
if(LANGS.indexOf(lang)<0)lang='en';

const S={
 en:{
  'conn.connected':'Connected','conn.disconnected':'Disconnected','conn.noresp':'No response',
  'card.connection':'Connection','card.status':'Status','card.channels':'Channels','card.log':'Message Log',
  'label.url':'WebSocket URL','label.token':'Token','btn.show':'Show',
  'label.auto':'Auto reconnect','label.cmdonly':'Forward only commands (!)','label.launch':'Open web on Beat Saber launch',
  'label.interval':'Reconnect interval (sec)','btn.save':'Save & Reconnect','msg.saved':'Saved ✓',
  'status.ws':'WebSocket','status.token':'Token','status.tokenSet':'set','status.tokenNone':'not set',
  'status.last':'Last message','btn.reconnect':'Reconnect now','time.now':'just now','time.sec':'s ago','time.min':'m ago','time.hour':'h ago',
  'ch.toggle':'forward on/off','ch.none':'No channels received yet','log.sub':'(newest first · filtered are dimmed)',
  'btn.clear':'Clear','log.filtered':'(filtered)','log.none':'No log'
 },
 ko:{
  'conn.connected':'연결됨','conn.disconnected':'끊김','conn.noresp':'웹 응답 없음',
  'card.connection':'연결 설정','card.status':'상태','card.channels':'채널','card.log':'메시지 로그',
  'label.url':'WebSocket URL','label.token':'토큰','btn.show':'표시',
  'label.auto':'자동 재접속','label.cmdonly':'명령어(!)만 게임으로 전달','label.launch':'비트세이버 실행 시 웹 열기',
  'label.interval':'재접속 간격(초)','btn.save':'저장 & 재접속','msg.saved':'저장됨 ✓',
  'status.ws':'WebSocket','status.token':'토큰','status.tokenSet':'설정됨','status.tokenNone':'없음',
  'status.last':'마지막 수신','btn.reconnect':'지금 재접속','time.now':'방금','time.sec':'초 전','time.min':'분 전','time.hour':'시간 전',
  'ch.toggle':'게임 전달 on/off','ch.none':'아직 수신된 채널이 없습니다','log.sub':'(신규순 · 필터된 메시지는 흐리게)',
  'btn.clear':'지우기','log.filtered':'(필터됨)','log.none':'로그 없음'
 },
 ja:{
  'conn.connected':'接続済み','conn.disconnected':'切断','conn.noresp':'応答なし',
  'card.connection':'接続設定','card.status':'ステータス','card.channels':'チャンネル','card.log':'メッセージログ',
  'label.url':'WebSocket URL','label.token':'トークン','btn.show':'表示',
  'label.auto':'自動再接続','label.cmdonly':'コマンド(!)のみ転送','label.launch':'起動時にWebを開く',
  'label.interval':'再接続間隔(秒)','btn.save':'保存して再接続','msg.saved':'保存しました ✓',
  'status.ws':'WebSocket','status.token':'トークン','status.tokenSet':'設定済み','status.tokenNone':'未設定',
  'status.last':'最終受信','btn.reconnect':'今すぐ再接続','time.now':'たった今','time.sec':'秒前','time.min':'分前','time.hour':'時間前',
  'ch.toggle':'転送 on/off','ch.none':'受信したチャンネルがありません','log.sub':'(新しい順・フィルタ済みは薄く)',
  'btn.clear':'クリア','log.filtered':'(フィルタ済み)','log.none':'ログなし'
 }
};
function t(k){return (S[lang]&&S[lang][k])||S.en[k]||k;}

function esc(s){return (s==null?'':String(s)).replace(/[&<>]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;'}[c]));}
function ago(iso){const d=(Date.now()-new Date(iso).getTime())/1000;if(d<5)return t('time.now');if(d<60)return Math.floor(d)+t('time.sec');if(d<3600)return Math.floor(d/60)+t('time.min');return Math.floor(d/3600)+t('time.hour');}
function toggleTok(){const x=$('token');x.type=x.type==='password'?'text':'password';}

function applyI18n(){
 document.querySelectorAll('[data-i18n]').forEach(e=>{e.textContent=t(e.dataset.i18n);});
 document.documentElement.lang=lang;
 const next=LANGS[(LANGS.indexOf(lang)+1)%LANGS.length];
 $('langbtn').textContent=LANGNAME[next];
 poll();
}
function toggleLang(){lang=LANGS[(LANGS.indexOf(lang)+1)%LANGS.length];localStorage.setItem('lang',lang);applyI18n();}

async function poll(){
 try{
  const s=await(await fetch('/api/state')).json();
  render(s);
 }catch(e){
  const c=$('conn');c.className='badge text-bg-danger';c.textContent=t('conn.noresp');
 }
}

function render(s){
 if(!inited){
  $('token').value=s.token||'';
  $('auto').checked=!!s.autoReconnect;
  $('ivl').value=s.reconnectIntervalSec||10;
  $('cmdonly').checked=!!s.forwardOnlyCommands;
  $('launch').checked=!!s.openWebOnLaunch;
  inited=true;
 }
 const c=$('conn');
 c.className='badge '+(s.connected?'text-bg-success':'text-bg-danger');
 c.textContent=s.connected?t('conn.connected'):t('conn.disconnected');

 $('status').innerHTML=
  `<div class='mb-1'>${t('status.ws')}: <code>${esc(s.url||'-')}</code></div>`+
  `<div class='mb-1'>${t('status.token')}: ${s.tokenSet?`<span class='text-success'>${t('status.tokenSet')}</span>`:`<span class='text-warning'>${t('status.tokenNone')}</span>`}</div>`+
  `<div class='mb-2'>${t('status.last')}: ${s.lastMessageUtc?ago(s.lastMessageUtc):'-'}</div>`+
  `<button class='btn btn-sm btn-outline-light' onclick='reconnect()'>${t('btn.reconnect')}</button>`;

 const ch=s.channels||[];
 $('channels').innerHTML= ch.length? ch.map(x=>
  `<tr><td>${esc(x.name)}</td>`+
  `<td class='text-secondary text-end' style='width:70px'>${x.count}</td>`+
  `<td class='text-end' style='width:60px'><div class='form-check form-switch d-inline-block'>`+
  `<input class='form-check-input chtoggle' type='checkbox' data-id='${x.id}' ${x.enabled?'checked':''}></div></td></tr>`
 ).join('') : `<tr><td class='text-secondary'>${t('ch.none')}</td></tr>`;
 document.querySelectorAll('.chtoggle').forEach(el=>{el.onchange=()=>toggle(el.dataset.id,el.checked);});

 const lg=s.log||[];
 $('log').innerHTML= lg.length? lg.map(m=>
  `<div class='row-f ${m.forwarded?'':'muted'}'>`+
  `<span class='text-secondary'>${m.time}</span> `+
  `<span class='text-info'>${esc(m.channel)}</span> `+
  `<b>${esc(m.user)}</b>: ${esc(m.content)}`+
  `${m.forwarded?'':` <span class='text-warning'>${t('log.filtered')}</span>`}</div>`
 ).join('') : `<div class='text-secondary'>${t('log.none')}</div>`;
}

async function save(){
 const body={token:$('token').value.trim(),autoReconnect:$('auto').checked,reconnectIntervalSec:parseInt($('ivl').value)||10,forwardOnlyCommands:$('cmdonly').checked,openWebOnLaunch:$('launch').checked};
 await fetch('/api/config',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});
 const sv=$('saved');sv.textContent=t('msg.saved');setTimeout(()=>sv.textContent='',2000);
 poll();
}
async function reconnect(){
 await fetch('/api/config',{method:'POST',headers:{'Content-Type':'application/json'},body:'{}'});
 poll();
}
async function toggle(id,en){
 await fetch('/api/channel',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({id:id,enabled:en})});
}
async function clearLog(){
 await fetch('/api/clearlog',{method:'POST'});
 poll();
}

applyI18n();
setInterval(poll,1500);
</script>
</body>
</html>";
    }
}
