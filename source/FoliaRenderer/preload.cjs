// SPDX-License-Identifier: AGPL-3.0-only
const {contextBridge,ipcRenderer}=require('electron');
contextBridge.exposeInMainWorld('peakFolia',{
  subscribe(callback){const listener=(_event,packet)=>callback(packet);ipcRenderer.on('state',listener);return()=>ipcRenderer.removeListener('state',listener)},
  ready(){ipcRenderer.send('ready')},
  report(text){ipcRenderer.send('render-error',String(text).slice(0,2048))},
});
