// SPDX-License-Identifier: AGPL-3.0-only
const {contextBridge,ipcRenderer}=require('electron');
window.addEventListener('error',event=>ipcRenderer.send('render-error',String(event.error?.stack||event.message||'Resource load failed').slice(0,2048)));
window.addEventListener('unhandledrejection',event=>ipcRenderer.send('render-error',String(event.reason?.stack||event.reason).slice(0,2048)));
contextBridge.exposeInMainWorld('peakFolia',{
  subscribe(callback){const listener=(_event,packet)=>callback(packet);ipcRenderer.on('state',listener);return()=>ipcRenderer.removeListener('state',listener)},
  ready(){ipcRenderer.send('ready')},
  presented(revision){ipcRenderer.send('presented',revision)},
  report(text){ipcRenderer.send('render-error',String(text).slice(0,2048))},
});
