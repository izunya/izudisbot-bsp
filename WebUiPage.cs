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
      <div class='alert alert-info py-2 mb-3' id='pair-banner' style='display:none'>
       <div class='d-flex justify-content-between align-items-center'>
        <span data-i18n='pair.bannerHint'></span>
        <button class='btn btn-sm btn-light' onclick='startPair()' data-i18n='pair.startBtn'></button>
       </div>
      </div>
      <div class='alert alert-success py-2 mb-3' id='pair-status' style='display:none'>
       <div data-i18n='pair.codeLabel' class='small'></div>
       <div class='d-flex justify-content-between align-items-center mt-1'>
        <code id='pair-code' class='fs-4'></code>
        <a id='pair-link' target='_blank' rel='noopener' class='btn btn-sm btn-primary' data-i18n='pair.openBtn'></a>
       </div>
       <div class='small mt-2 text-muted' id='pair-msg'></div>
      </div>

      <div class='card border-success mb-3' id='pair-issue' style='display:none'>
       <div class='card-header bg-success-subtle py-2 small'>
        <span data-i18n='pair.issueHeader'></span>
       </div>
       <div class='card-body p-3'>
        <label class='form-label small' data-i18n='pair.tokenNameLabel'></label>
        <input id='pair-name' type='text' class='form-control form-control-sm mb-3' />
        <div class='small text-muted mb-1' data-i18n='pair.pickChannels'></div>
        <div id='pair-guilds' class='border rounded p-2 mb-3' style='max-height:240px;overflow-y:auto;font-size:.85rem'></div>
        <div class='d-flex align-items-center gap-2'>
         <button id='pair-issue-btn' class='btn btn-success btn-sm' onclick='doIssue()' data-i18n='pair.issueBtn'></button>
         <button class='btn btn-link btn-sm text-muted' onclick='cancelPair()' data-i18n='pair.cancelBtn'></button>
         <span id='pair-issue-msg' class='ms-2 small text-danger'></span>
        </div>
       </div>
      </div>
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
  'btn.clear':'Clear','log.filtered':'(filtered)','log.none':'No log',
  'pair.bannerHint':'No token yet?','pair.startBtn':'Get from bot',
  'pair.codeLabel':'Enter this 6-digit code on the bot dashboard, then approve',
  'pair.openBtn':'Open dashboard',
  'pair.waiting':'Waiting for you to approve on the dashboard...',
  'pair.received':'✓ Token received. Saving...',
  'pair.expired':'Timed out. Try again.',
  'pair.failed':'Pairing failed',
  'pair.issueHeader':'Approved — pick channels & issue the token here',
  'pair.tokenNameLabel':'Token name (e.g. main PC)',
  'pair.pickChannels':'Pick the Discord channels to forward into the game:',
  'pair.issueBtn':'Issue token','pair.cancelBtn':'Cancel',
  'pair.loadingGuilds':'Loading channels...','pair.noGuilds':'No eligible servers found.',
  'pair.issuing':'Issuing...',
  'pair.errNameRequired':'Token name required','pair.errNoChannels':'Pick at least one channel'
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
  'btn.clear':'지우기','log.filtered':'(필터됨)','log.none':'로그 없음',
  'pair.bannerHint':'아직 토큰이 없으신가요?','pair.startBtn':'봇에서 가져오기',
  'pair.codeLabel':'봇 대시보드에서 이 6자리 코드를 입력하고 승인하세요',
  'pair.openBtn':'대시보드 열기',
  'pair.waiting':'대시보드에서 승인하기를 기다리는 중...',
  'pair.received':'✓ 토큰을 받았습니다. 저장 중...',
  'pair.expired':'시간 초과. 다시 시도해주세요.',
  'pair.failed':'페어링 실패',
  'pair.issueHeader':'승인 완료 — 여기서 채널 선택 + 토큰 발급',
  'pair.tokenNameLabel':'토큰 이름 (예: 메인 PC)',
  'pair.pickChannels':'게임으로 전달할 디스코드 채널을 선택하세요:',
  'pair.issueBtn':'토큰 발급','pair.cancelBtn':'취소',
  'pair.loadingGuilds':'채널 목록 불러오는 중...','pair.noGuilds':'사용 가능한 서버가 없습니다.',
  'pair.issuing':'발급 중...',
  'pair.errNameRequired':'토큰 이름이 필요합니다','pair.errNoChannels':'채널을 1개 이상 선택하세요'
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
  'btn.clear':'クリア','log.filtered':'(フィルタ済み)','log.none':'ログなし',
  'pair.bannerHint':'まだトークンがありませんか?','pair.startBtn':'ボットから取得',
  'pair.codeLabel':'ボットダッシュボードでこの 6 桁コードを入力して承認してください',
  'pair.openBtn':'ダッシュボードを開く',
  'pair.waiting':'ダッシュボードでの承認を待っています...',
  'pair.received':'✓ トークンを受信しました。保存中...',
  'pair.expired':'タイムアウト。もう一度お試しください。',
  'pair.failed':'ペアリング失敗',
  'pair.issueHeader':'承認完了 — ここでチャンネル選択とトークン発行',
  'pair.tokenNameLabel':'トークン名 (例: メイン PC)',
  'pair.pickChannels':'ゲームに転送する Discord チャンネルを選択してください:',
  'pair.issueBtn':'トークン発行','pair.cancelBtn':'キャンセル',
  'pair.loadingGuilds':'チャンネル一覧読込中...','pair.noGuilds':'利用可能なサーバーがありません。',
  'pair.issuing':'発行中...',
  'pair.errNameRequired':'トークン名が必要です','pair.errNoChannels':'チャンネルを 1 つ以上選択してください'
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
 pairBotBase=s.botApiBase||pairBotBase;
 const issueOpen=$('pair-issue').style.display==='block';
 const statusOpen=$('pair-status').style.display==='block';
 if(!issueOpen && !statusOpen){
  $('pair-banner').style.display=s.tokenSet?'none':'block';
 }
 if(s.tokenSet){
  $('pair-banner').style.display='none';
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

// ---- 페어링 (모드 → 봇 → 모드) ----
// 흐름:
//  1) startPair() → 봇이 code + sessionId 발급 (pending)
//  2) pollPair() 가 1.5s 마다 GET → status 가 authenticated 되면 loadGuilds()
//  3) loadGuilds() 가 봇에서 사용자가 발급 가능한 길드+채널 받아와 picker 렌더
//  4) 사용자가 채널 선택 + 이름 입력 + doIssue() → POST /issue → raw 토큰 받아 자동 저장
let pairBotBase=null;
let pairPolling=null;
let pairSessionId=null;

function botFetch(path, opts){
 return fetch(pairBotBase.replace(/\/$/,'')+path, opts);
}

async function startPair(){
 if(!pairBotBase){ $('pair-msg').textContent=t('pair.failed')+': botApiBase'; return; }
 try{
  const r=await botFetch('/api/bsp-bridge/pair/start',{method:'POST'});
  const j=await r.json();
  if(!r.ok||!j.sessionId) throw new Error(j.error||('http '+r.status));
  pairSessionId=j.sessionId;
  $('pair-code').textContent=j.code;
  $('pair-link').href=j.claimUrl;
  $('pair-banner').style.display='none';
  $('pair-status').style.display='block';
  $('pair-issue').style.display='none';
  $('pair-msg').textContent=t('pair.waiting');
  pollPair(j.sessionId, j.expiresAt);
 }catch(e){
  $('pair-status').style.display='block';
  $('pair-msg').textContent=t('pair.failed')+': '+(e.message||e);
 }
}

function pollPair(sessionId, expiresAt){
 if(pairPolling) clearInterval(pairPolling);
 pairPolling=setInterval(async()=>{
  if(Date.now()>expiresAt){
   stopPolling();
   $('pair-msg').textContent=t('pair.expired');
   return;
  }
  try{
   const r=await botFetch('/api/bsp-bridge/pair/'+encodeURIComponent(sessionId));
   const j=await r.json();
   if(j.status==='authenticated'){
    stopPolling();
    showIssuePanel();
    loadGuilds(sessionId);
   }else if(j.status==='issued'){
    // 정상적으로는 위에서 doIssue() 가 직접 받지만 race 대비
    stopPolling();
    await sendTokenToMod(j.rawToken, j.wsUrl);
   }else if(j.status==='expired'){
    stopPolling();
    $('pair-msg').textContent=t('pair.expired');
   }
  }catch{}
 }, 1500);
}

function stopPolling(){
 if(pairPolling){ clearInterval(pairPolling); pairPolling=null; }
}

function showIssuePanel(){
 $('pair-msg').textContent='';
 $('pair-status').style.display='none';
 $('pair-issue').style.display='block';
 $('pair-guilds').textContent=t('pair.loadingGuilds');
}

async function loadGuilds(sessionId){
 try{
  const r=await botFetch('/api/bsp-bridge/pair/'+encodeURIComponent(sessionId)+'/guilds',{method:'POST'});
  const j=await r.json();
  if(!r.ok) throw new Error(j.error||('http '+r.status));
  renderGuilds(j.guilds||[]);
 }catch(e){
  $('pair-guilds').textContent=t('pair.failed')+': '+(e.message||e);
 }
}

function renderGuilds(guilds){
 if(!guilds.length){
  $('pair-guilds').textContent=t('pair.noGuilds'); return;
 }
 const html=guilds.map(g=>
  `<div class='mb-2'><div class='fw-semibold small text-secondary'>${esc(g.name)}</div>`+
  `<div class='d-flex flex-wrap gap-1 mt-1'>`+
   (g.channels||[]).map(c=>{
    const key=g.id+':'+c.id;
    return `<label class='btn btn-sm btn-outline-secondary'><input type='checkbox' class='form-check-input me-1 pair-ch' value='${esc(key)}'>#${esc(c.name)}</label>`;
   }).join('')+
  `</div></div>`
 ).join('');
 $('pair-guilds').innerHTML=html;
}

async function doIssue(){
 const name=($('pair-name').value||'').trim();
 const picks=Array.from(document.querySelectorAll('.pair-ch')).filter(el=>el.checked).map(el=>{
  const [guildId,channelId]=el.value.split(':');
  return {guildId, channelId};
 });
 $('pair-issue-msg').textContent='';
 if(!name){ $('pair-issue-msg').textContent=t('pair.errNameRequired'); return; }
 if(!picks.length){ $('pair-issue-msg').textContent=t('pair.errNoChannels'); return; }
 const btn=$('pair-issue-btn');
 const orig=btn.textContent; btn.disabled=true; btn.textContent=t('pair.issuing');
 try{
  const r=await botFetch('/api/bsp-bridge/pair/'+encodeURIComponent(pairSessionId)+'/issue',{
   method:'POST', headers:{'Content-Type':'application/json'},
   body:JSON.stringify({name, sources:picks})
  });
  const j=await r.json();
  if(!r.ok||!j.rawToken) throw new Error(j.error||('http '+r.status));
  await sendTokenToMod(j.rawToken, j.wsUrl);
 }catch(e){
  $('pair-issue-msg').textContent=t('pair.failed')+': '+(e.message||e);
  btn.disabled=false; btn.textContent=orig;
 }
}

async function sendTokenToMod(rawToken, wsUrl){
 await fetch('/api/pair-receive',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({token:rawToken, wsUrl:wsUrl})});
 $('pair-issue').style.display='none';
 $('pair-status').style.display='block';
 $('pair-msg').textContent=t('pair.received');
 setTimeout(()=>{ $('pair-status').style.display='none'; poll(); }, 1500);
}

function cancelPair(){
 stopPolling();
 pairSessionId=null;
 $('pair-issue').style.display='none';
 $('pair-status').style.display='none';
}

applyI18n();
setInterval(poll,1500);
</script>
</body>
</html>";
    }
}
