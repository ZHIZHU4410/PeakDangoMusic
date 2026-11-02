// SPDX-License-Identifier: AGPL-3.0-only
// Offscreen only: no visible window, account/player control or arbitrary remote content.
const {app,BrowserWindow,ipcMain,session,protocol}=require('electron');
const fs=require('node:fs'),path=require('node:path'),readline=require('node:readline'),net=require('node:net');
// No loopback HTTP server, proxy, port or network permission is needed for bundled files.
protocol.registerSchemesAsPrivileged([{scheme:'peak-folia',privileges:{standard:true,secure:true,supportFetchAPI:true,corsEnabled:true}}]);
const params=Object.fromEntries(process.argv.filter(a=>/^--[a-z]+=/.test(a)).map(a=>{const at=a.indexOf('=');return[a.slice(2,at),a.slice(at+1)]}));
const parent=Number(params.parent),profile=params.profile;
if(!Number.isSafeInteger(parent)||parent<=0||!profile||!/^PeakFolia-[a-f0-9-]+$/.test(params.pipe||'')){app.exit(2);return}
fs.mkdirSync(profile,{recursive:true});fs.mkdirSync(path.join(profile,'session'),{recursive:true});
app.setPath('userData',profile);app.setPath('sessionData',path.join(profile,'session'));
app.commandLine.appendSwitch('force-device-scale-factor','1');
if(params.software==='1'){
  app.commandLine.appendSwitch('use-angle','swiftshader');
  app.commandLine.appendSwitch('enable-unsafe-swiftshader');
}
app.commandLine.appendSwitch('disable-background-timer-throttling');
app.commandLine.appendSwitch('disable-renderer-backgrounding');
app.commandLine.appendSwitch('autoplay-policy','user-gesture-required');
let win,latest=null,cachedLines,ready=false,frameBusy=false,presentedRevision=0,width=960,height=540,quitting=false;
const styles=new Set(['sonnet','tempera','lumiere','hanabi','classic','cadenza','partita','fume','tilt','claddagh','monet','pendolo','cappella','diorama','still']);
const logFile=path.resolve(profile,'..','..','..','PeakDangoMusic-Folia.log');
const log=text=>{
  const line=String(text).slice(0,2048);
  try{if(fs.existsSync(logFile)&&fs.statSync(logFile).size>131072)fs.writeFileSync(logFile,'');fs.appendFileSync(logFile,new Date().toISOString()+' '+line+'\n')}catch{}
  process.stderr.write(line+'\n');
};
const failure=text=>{presentedRevision=0;log('PEAK_FOLIA_ERROR '+text)};
const quit=()=>{if(quitting)return;quitting=true;try{win?.destroy();transport.destroy()}catch{}app.quit()};
process.on('uncaughtException',error=>{failure(error.stack||error.message);quit()});
process.on('unhandledRejection',error=>{failure(error?.stack||String(error));quit()});
log('PEAK Folia adapter 1.3.0 starting');
// Windows GUI applications may expose an already-ended stdin even when redirected.
// A private duplex named pipe keeps our transport independent of console handles.
const transport=net.createConnection('\\\\.\\pipe\\'+params.pipe);
transport.on('connect',()=>log('Named pipe connected'));
transport.on('error',error=>{failure('IPC: '+error.message);quit()});
transport.on('close',quit);
const input=readline.createInterface({input:transport,crlfDelay:Infinity});
input.on('line',line=>{
  if(line.length>8*1024*1024)return;
  try{
    const data=JSON.parse(line);
    if(data.type==='quit'){quit();return}
    if(!styles.has(data.mode)||typeof data.key!=='string'||!Number.isFinite(data.positionMs)||!Number.isSafeInteger(data.revision))return;
    if(data.lines!==undefined&&(!Array.isArray(data.lines)||data.lines.length>12000))return;
    // Keep the most recent full timeline until the renderer is ready.
    if(latest?.key!==data.key)cachedLines=undefined;
    if(data.lines!==undefined)cachedLines=data.lines;
    if(latest?.revision!==data.revision)presentedRevision=0;
    latest=data;
    if(win&&!win.isDestroyed()){
      const dimensions=[[640,360],[960,540],[1280,720]][Math.min(2,Math.max(0,data.quality|0))];
      if(width!==dimensions[0]||height!==dimensions[1]){[width,height]=dimensions;win.setContentSize(width,height)}
      win.webContents.setFrameRate([15,24,30,60].includes(data.fps)?data.fps:24);
      if(ready){win.webContents.send('state',data);if(!win.webContents.isPainting())win.webContents.startPainting()}
    }
  }catch(error){failure(error.message)}
});
input.on('close',quit);
const watch=setInterval(()=>{try{process.kill(parent,0)}catch{quit()}},1500);watch.unref();
app.on('window-all-closed',quit);
app.whenReady().then(async()=>{
  const root=path.resolve(__dirname,'dist');
  const mime={'.html':'text/html','.js':'text/javascript','.css':'text/css','.svg':'image/svg+xml','.png':'image/png','.webp':'image/webp','.jpg':'image/jpeg','.woff2':'font/woff2','.wasm':'application/wasm'};
  const origin='peak-folia://app';
  protocol.handle('peak-folia',async request=>{
    try{
      if(request.method!=='GET')return new Response(null,{status:405});
      const url=new URL(request.url);
      if(url.host!=='app')return new Response(null,{status:403});
      if(url.pathname==='/selected-font'){
        const font=latest?.fontPath;
        if(typeof font!=='string'||! /\.(ttf|otf|ttc)$/i.test(font))return new Response(null,{status:404});
        const stat=await fs.promises.stat(font);if(!stat.isFile()||stat.size>64*1024*1024)return new Response(null,{status:404});
        return new Response(await fs.promises.readFile(font),{headers:{'Content-Type':/\.otf$/i.test(font)?'font/otf':'font/ttf','Cache-Control':'no-store'}});
      }
      const name=url.pathname==='/'?'index.html':decodeURIComponent(url.pathname.slice(1));
      const file=path.resolve(root,name);
      if(!file.startsWith(root+path.sep))return new Response(null,{status:403});
      const bytes=await fs.promises.readFile(file);
      return new Response(bytes,{headers:{'Content-Type':mime[path.extname(file)]||'application/octet-stream','Cache-Control':'no-store'}});
    }catch(error){log('Bundled resource unavailable: '+error.message);return new Response(null,{status:404})}
  });
  session.defaultSession.setPermissionRequestHandler((_contents,_permission,callback)=>callback(false));
  // Keep the bundled protocol out of Chromium's blocking network interceptor.
  session.defaultSession.webRequest.onBeforeRequest({urls:['http://*/*','https://*/*','ws://*/*','wss://*/*']},(_details,callback)=>callback({cancel:true}));
  win=new BrowserWindow({width,height,useContentSize:true,show:false,paintWhenInitiallyHidden:true,transparent:true,backgroundColor:'#00000000',frame:false,skipTaskbar:true,focusable:false,
    webPreferences:{offscreen:true,backgroundThrottling:false,nodeIntegration:false,contextIsolation:true,sandbox:true,preload:path.join(__dirname,'preload.cjs')}});
  win.setMenu(null);win.webContents.setFrameRate(24);
  win.webContents.setWindowOpenHandler(()=>({action:'deny'}));
  win.webContents.on('will-navigate',(event,url)=>{if(!url.startsWith(origin+'/'))event.preventDefault()});
  win.webContents.on('console-message',(...args)=>{const details=args[1];log(typeof details==='object'?details.message:args[2]||details)});
  win.webContents.on('preload-error',(_event,_path,error)=>failure('preload: '+error.message));
  win.webContents.on('did-fail-load',(_event,code,description,_url,main)=>{if(main)failure('load '+code+': '+description)});
  win.on('unresponsive',()=>{failure('Renderer exited: unresponsive');quit()});
  win.webContents.on('render-process-gone',(_event,details)=>{failure('Renderer exited: '+details.reason);quit()});
  win.webContents.on('paint',(_event,_dirty,image)=>{
    if(frameBusy||!ready||!presentedRevision||presentedRevision!==latest?.revision)return;
    try{
      let bitmap=image.toBitmap({scaleFactor:1});
      if(bitmap.length!==width*height*4)bitmap=image.resize({width,height}).toBitmap({scaleFactor:1});
      if(bitmap.length!==width*height*4)return;
      const header=Buffer.alloc(24);header.write('DANGOF02',0,'ascii');header.writeUInt32LE(width,8);header.writeUInt32LE(height,12);header.writeUInt32LE(bitmap.length,16);header.writeUInt32LE(presentedRevision,20);
      frameBusy=true;transport.write(header);transport.write(bitmap,()=>{frameBusy=false});
    }catch(error){failure('paint: '+error.message)}
  });
  ipcMain.on('ready',event=>{if(event.sender!==win.webContents)return;ready=true;log('Frontend subscribed');if(latest)win.webContents.send('state',{...latest,lines:cachedLines});win.webContents.startPainting();win.webContents.invalidate()});
  ipcMain.on('presented',(event,revision)=>{if(event.sender!==win.webContents||revision!==latest?.revision)return;presentedRevision=revision;log('Style ready: '+latest.mode);win.webContents.invalidate()});
  ipcMain.on('render-error',(event,text)=>{if(event.sender===win.webContents)failure(text)});
  // A static or paused page does not otherwise emit paint events after its first frame.
  const refresh=setInterval(()=>{if(ready&&presentedRevision&&!win.isDestroyed())win.webContents.invalidate()},1000);refresh.unref();
  await win.loadURL(origin+'/?obs=1');
}).catch(error=>{failure(error.stack||error.message);app.exit(1)});
