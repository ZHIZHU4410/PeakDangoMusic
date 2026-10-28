// SPDX-License-Identifier: AGPL-3.0-only
// Offscreen only: no visible window, account/player control or arbitrary remote content.
const {app,BrowserWindow,ipcMain,session}=require('electron');
const fs=require('node:fs'),path=require('node:path'),http=require('node:http'),readline=require('node:readline');
const params=Object.fromEntries(process.argv.filter(a=>/^--[a-z]+=/.test(a)).map(a=>{const at=a.indexOf('=');return[a.slice(2,at),a.slice(at+1)]}));
const parent=Number(params.parent),profile=params.profile;
if(!Number.isSafeInteger(parent)||parent<=0||!profile){app.exit(2);return}
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
let win,server,latest=null,cachedLines,ready=false,frameBusy=false,sequence=0,width=960,height=540;
const styles=new Set(['sonnet','tempera','lumiere','hanabi','classic','cadenza','partita','fume','tilt','claddagh','monet','pendolo','cappella','diorama','still']);
const log=text=>process.stderr.write(String(text).slice(0,2048)+'\n');
const quit=()=>{try{win?.destroy();server?.close()}catch{}app.quit()};
const input=readline.createInterface({input:process.stdin,crlfDelay:Infinity});
input.on('line',line=>{
  if(line.length>8*1024*1024)return;
  try{
    const data=JSON.parse(line);
    if(data.type==='quit'){quit();return}
    if(!styles.has(data.mode)||typeof data.key!=='string'||!Number.isFinite(data.positionMs))return;
    if(data.lines!==undefined&&(!Array.isArray(data.lines)||data.lines.length>12000))return;
    // Keep the most recent full timeline until the renderer is ready.
    if(latest?.key!==data.key)cachedLines=undefined;
    if(data.lines!==undefined)cachedLines=data.lines;
    latest=data;
    if(win&&!win.isDestroyed()){
      const dimensions=[[640,360],[960,540],[1280,720]][Math.min(2,Math.max(0,data.quality|0))];
      if(width!==dimensions[0]||height!==dimensions[1]){[width,height]=dimensions;win.setContentSize(width,height)}
      win.webContents.setFrameRate([15,24,30,60].includes(data.fps)?data.fps:24);
      if(ready){win.webContents.send('state',data);if(!win.webContents.isPainting())win.webContents.startPainting()}
    }
  }catch(error){log(error.message)}
});
input.on('close',quit);process.stdout.on('error',quit);
const watch=setInterval(()=>{try{process.kill(parent,0)}catch{quit()}},1500);watch.unref();
app.on('window-all-closed',quit);
app.whenReady().then(async()=>{
  const root=path.resolve(__dirname,'dist');
  const mime={'.html':'text/html','.js':'text/javascript','.css':'text/css','.svg':'image/svg+xml','.png':'image/png','.webp':'image/webp','.jpg':'image/jpeg','.woff2':'font/woff2','.wasm':'application/wasm'};
  server=http.createServer((request,response)=>{
    try{
      if(request.method!=='GET'){response.writeHead(405);response.end();return}
      const url=new URL(request.url,'http://localhost');
      const name=url.pathname==='/'?'index.html':decodeURIComponent(url.pathname.slice(1));
      const file=path.resolve(root,name);
      if(file!==root&&!file.startsWith(root+path.sep)){response.writeHead(403);response.end();return}
      const stat=fs.statSync(file);if(!stat.isFile())throw Error('not a file');
      response.setHeader('Content-Type',mime[path.extname(file)]||'application/octet-stream');
      response.setHeader('Cache-Control','no-store');fs.createReadStream(file).pipe(response);
    }catch{response.writeHead(404);response.end()}
  });
  await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
  const origin='http://127.0.0.1:'+server.address().port;
  session.defaultSession.setPermissionRequestHandler((_contents,_permission,callback)=>callback(false));
  session.defaultSession.webRequest.onBeforeRequest((details,callback)=>callback({cancel:!details.url.startsWith(origin+'/')&&!details.url.startsWith('data:')&&!details.url.startsWith('blob:')}));
  win=new BrowserWindow({width,height,useContentSize:true,show:false,transparent:true,frame:false,skipTaskbar:true,focusable:false,
    webPreferences:{offscreen:true,backgroundThrottling:false,nodeIntegration:false,contextIsolation:true,sandbox:true,preload:path.join(__dirname,'preload.cjs')}});
  win.setMenu(null);win.webContents.setFrameRate(24);
  win.webContents.setWindowOpenHandler(()=>({action:'deny'}));
  win.webContents.on('will-navigate',event=>event.preventDefault());
  win.webContents.on('console-message',(...args)=>{const details=args[1];log(typeof details==='object'?details.message:args[2]||details)});
  win.on('unresponsive',()=>{log('Renderer exited: unresponsive');quit()});
  win.webContents.on('render-process-gone',(_event,details)=>{log('Renderer exited: '+details.reason);quit()});
  win.webContents.on('paint',(_event,_dirty,image)=>{
    if(frameBusy||!ready)return;
    try{
      let bitmap=image.toBitmap({scaleFactor:1});
      if(bitmap.length!==width*height*4)bitmap=image.resize({width,height}).toBitmap({scaleFactor:1});
      if(bitmap.length!==width*height*4)return;
      const header=Buffer.alloc(24);header.write('DANGOF01',0,'ascii');header.writeUInt32LE(width,8);header.writeUInt32LE(height,12);header.writeUInt32LE(bitmap.length,16);header.writeUInt32LE(++sequence,20);
      frameBusy=true;process.stdout.write(header);process.stdout.write(bitmap,()=>{frameBusy=false});
    }catch(error){log(error.message)}
  });
  ipcMain.on('ready',event=>{if(event.sender!==win.webContents)return;ready=true;if(latest)win.webContents.send('state',{...latest,lines:cachedLines})});
  ipcMain.on('render-error',(event,text)=>{if(event.sender===win.webContents)log(text)});
  await win.loadURL(origin+'/?obs=1');
}).catch(error=>{log(error.stack||error.message);app.exit(1)});
