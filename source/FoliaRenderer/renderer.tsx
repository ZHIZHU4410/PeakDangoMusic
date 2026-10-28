// SPDX-License-Identifier: AGPL-3.0-only
// PEAK adapter: the original Folia renderers receive the existing read-only player timeline.
import React, { Suspense, useEffect, useMemo, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { motionValue, useMotionValue } from 'framer-motion';
import './vendor/folia-major/src/i18n/config';
import { getVisualizerRegistryEntry } from './vendor/folia-major/src/components/visualizer/registry';
import { applyVisualizerTuning } from './vendor/folia-major/src/components/visualizer/tuningRegistry';
import { VisualizerBackdropContext } from './vendor/folia-major/src/components/visualizer/VisualizerBackdrop';
import { DEFAULT_THEME } from './vendor/folia-major/src/services/baseThemes';
import type { Line, Theme } from './vendor/folia-major/src/types';
import './vendor/folia-major/src/stores/useMotionSettingsStore';
import './stage.css';

type Packet={mode:string;key:string;positionMs:number;durationMs:number;playing:boolean;lines?:Line[];title:string;artist:string;translation:boolean;fontScale:number;intensity:string;primary:string;accent:string;background:boolean;visible:boolean;generation:number};
declare global {interface Window {peakFolia:{subscribe:(callback:(packet:Packet)=>void)=>()=>void;ready:()=>void;report:(text:string)=>void}}}
const empty:Packet={mode:'classic',key:'',positionMs:0,durationMs:0,playing:false,title:'',artist:'',translation:false,fontScale:1,intensity:'normal',primary:'#F7E1D7',accent:'#69D7D1',background:false,visible:false,generation:0};
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
  useEffect(()=>{
    const unsubscribe=window.peakFolia.subscribe(next=>{
      if(next.key!==clock.current.key&&next.lines===undefined){timeline.current=[];setLines([])}
      if(Array.isArray(next.lines)){timeline.current=next.lines;setLines(next.lines)}
      clock.current={position:next.positionMs,tick:performance.now(),playing:next.playing,valid:next.visible,duration:next.durationMs,key:next.key};
      setPacket(previous=>({...next,lines:undefined}));
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
  const theme:Theme=useMemo(()=>({...DEFAULT_THEME,backgroundColor:'#10171f',primaryColor:packet.primary,accentColor:packet.accent,secondaryColor:'#9AAEBE',fontFamily:'Microsoft YaHei',animationIntensity:packet.intensity as Theme['animationIntensity']}),[packet.primary,packet.accent,packet.intensity]);
  const entry=getVisualizerRegistryEntry(packet.mode);
  const props=applyVisualizerTuning(packet.mode,{
    currentTime,duration:packet.durationMs/1000,currentLineIndex:index,lines,theme,subtitleTheme:theme,
    audioPower,audioBands:bands,seed:packet.key,songTitle:packet.title,songArtist:packet.artist,
    lyricsFontScale:packet.fontScale,subtitleFontScale:1,showText:true,isDaylight:false,
    isPlayerChromeHidden:true,showSubtitleTranslation:packet.translation,hideTranslationSubtitle:!packet.translation,
    subtitleContentMode:packet.translation?'translation':'none',showHarmonySubtitle:false,
    paused:!packet.playing,isPreviewMode:true,background:{mode:'common'},
  },undefined);
  return <div className="peak-stage" style={{visibility:packet.visible&&lines.length?'visible':'hidden',background:packet.background?'#10171f':'transparent'}}>
    {packet.visible&&lines.length>0&&<Boundary reset={packet.mode+'|'+packet.key}><VisualizerBackdropContext.Provider value={{pointerArea:null}}>
      <Suspense fallback={null}>{entry.render(props)}</Suspense>
    </VisualizerBackdropContext.Provider></Boundary>}
  </div>;
}
createRoot(document.getElementById('root')!).render(<Stage/>);
