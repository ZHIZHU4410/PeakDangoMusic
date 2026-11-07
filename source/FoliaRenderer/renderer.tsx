// SPDX-License-Identifier: AGPL-3.0-only
// PEAK adapter: the original Folia renderers receive the existing read-only player timeline.
import React, { Suspense, useEffect, useMemo, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { motionValue, useMotionValue } from 'framer-motion';
import './vendor/folia-major/src/i18n/config';
import { DEFAULT_THEME } from './vendor/folia-major/src/services/baseThemes';
import type { Line, Theme } from './vendor/folia-major/src/types';
import './stage.css';

type Packet={fontPath?:string;revision:number;mode:string;key:string;positionMs:number;durationMs:number;playing:boolean;lines?:Line[];title:string;artist:string;translation:boolean;fontScale:number;intensity:string;primary:string;accent:string;background:boolean;visible:boolean;generation:number};
declare global {interface Window {peakFolia:{subscribe:(callback:(packet:Packet)=>void)=>()=>void;ready:()=>void;presented:(revision:number)=>void;report:(text:string)=>void}}}
const Preset=React.lazy(()=>import('./presets'));
const empty:Packet={revision:0,mode:'classic',key:'',positionMs:0,durationMs:0,playing:false,title:'',artist:'',translation:false,fontScale:1,intensity:'normal',primary:'#F7E1D7',accent:'#69D7D1',background:false,visible:false,generation:0};
class Boundary extends React.Component<React.PropsWithChildren<{reset:string}>,{failed:boolean}> {
  state={failed:false};static getDerivedStateFromError(){return{failed:true}}
  componentDidCatch(error:Error){window.peakFolia.report('样式渲染错误：'+error.message)}
  componentDidUpdate(previous:{reset:string}){if(previous.reset!==this.props.reset&&this.state.failed)this.setState({failed:false})}
  render(){return this.state.failed?null:this.props.children}
}
function Stage(){
  const [packet,setPacket]=useState(empty),[lines,setLines]=useState<Line[]>([]),[index,setIndex]=useState(-1);
  const clock=useRef({position:0,tick:performance.now(),playing:false,valid:false,duration:0,key:''});
  const timeline=useRef<Line[]>([]),indexRef=useRef(-1);
  const currentTime=useMotionValue(0);
  const bands=useMemo(()=>({bass:motionValue(0),lowMid:motionValue(0),mid:motionValue(0),vocal:motionValue(0),treble:motionValue(0),spectrum:motionValue(new Uint8Array(0))}),[]);
  const audioPower=useMotionValue(0);
  const [fontFamily,setFontFamily]=useState('Microsoft YaHei');
  useEffect(()=>{
    let cancelled=false,face:FontFace|undefined;
    setFontFamily('Microsoft YaHei');
    if(packet.fontPath){
      const family='PeakLocalFont'+packet.revision;
      face=new FontFace(family,'url("/selected-font?v='+packet.revision+'")');
      face.load().then(loaded=>{if(!cancelled){document.fonts.add(loaded);setFontFamily(family)}})
        .catch(error=>console.warn('Local font fallback: '+error.message));
    }
    return()=>{cancelled=true;if(face)document.fonts.delete(face)};
  },[packet.fontPath,packet.revision]);
  useEffect(()=>{
    const unsubscribe=window.peakFolia.subscribe(next=>{
      if(next.key!==clock.current.key&&next.lines===undefined){timeline.current=[];setLines([])}
      if(Array.isArray(next.lines)){timeline.current=next.lines;setLines(next.lines)}
      clock.current={position:next.positionMs,tick:performance.now(),playing:next.playing,valid:next.visible,duration:next.durationMs,key:next.key};
      currentTime.set(Math.max(0,next.positionMs)/1000);
      setPacket({...next,lines:undefined});
    });
    let animation=0;
    const frame=()=>{
      const c=clock.current,age=performance.now()-c.tick;
      // Never invent progress across a disconnected player; the game sends an explicit invalid clock.
      const position=Math.min(c.duration,Math.max(0,c.position+(c.playing?Math.min(age,1500):0)))/1000;
      currentTime.set(position);
      let lo=0,hi=timeline.current.length-1,found=-1;
      while(lo<=hi){const mid=(lo+hi)>>1;if(timeline.current[mid].startTime<=position){found=mid;lo=mid+1}else hi=mid-1}
      if(found!==indexRef.current){indexRef.current=found;setIndex(found)}
      animation=requestAnimationFrame(frame);
    };
    animation=requestAnimationFrame(frame);window.peakFolia.ready();
    return()=>{unsubscribe();cancelAnimationFrame(animation)};
  },[currentTime]);
  const theme:Theme=useMemo(()=>({...DEFAULT_THEME,backgroundColor:'#10171f',primaryColor:packet.primary,accentColor:packet.accent,secondaryColor:parseInt(packet.primary.slice(1,3),16)<128?'#263442':'#D8E2EB',fontFamily,fontFamilyStack:[fontFamily,'Microsoft YaHei','Arial'],animationIntensity:packet.intensity as Theme['animationIntensity']}),[packet.primary,packet.accent,packet.intensity,fontFamily]);
  const props={
    currentTime,duration:packet.durationMs/1000,currentLineIndex:index,lines,theme,subtitleTheme:theme,
    audioPower,audioBands:bands,seed:packet.key,songTitle:packet.title,songArtist:packet.artist,
    lyricsFontScale:packet.fontScale,subtitleFontScale:1,showText:true,isDaylight:parseInt(packet.primary.slice(1,3),16)<128,
    isPlayerChromeHidden:true,showSubtitleTranslation:packet.translation,hideTranslationSubtitle:!packet.translation,
    subtitleContentMode:packet.translation?'translation':'none',showHarmonySubtitle:false,
    paused:!packet.playing,isPreviewMode:true,background:{mode:'common',transparent:!packet.background},
  };
  return <div className="peak-stage" style={{visibility:packet.visible&&lines.length?'visible':'hidden',background:packet.background?'#10171f':'transparent',textShadow:parseInt(packet.primary.slice(1,3),16)<128?'1px 1px 1px #fff,-1px -1px 1px #fff':'1px 1px 2px #07101a,-1px -1px 1px #07101a'}}>
    {packet.visible&&lines.length>0&&<Boundary key={packet.revision} reset={String(packet.revision)}>
      <Suspense fallback={null}><Preset mode={packet.mode} revision={packet.revision} props={props}/></Suspense>
    </Boundary>}
  </div>;
}
createRoot(document.getElementById('root')!).render(<Stage/>);
