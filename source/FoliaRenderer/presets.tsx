// SPDX-License-Identifier: AGPL-3.0-only
// Keep Folia's settings/renderer graph out of the bridge bootstrap. A preset's module,
// render or shader failure is caught by the mounted boundary and cannot kill the bridge.
import React, { Suspense, useEffect, useMemo, useRef } from 'react';
import { applyVisualizerTuning } from './vendor/folia-major/src/components/visualizer/tuningRegistry';
import { getVisualizerRegistryEntry } from './vendor/folia-major/src/components/visualizer/registry';
import { VisualizerBackdropContext } from './vendor/folia-major/src/components/visualizer/VisualizerBackdrop';
import { FirstFrameContext } from './vendor/folia-major/src/components/visualizer/styleTransition/firstFrame';
import type { VisualizerSharedProps } from './vendor/folia-major/src/components/visualizer/definition';
import './vendor/folia-major/src/stores/useMotionSettingsStore';

function Committed({drawn,claimed}:{drawn:()=>void;claimed:React.RefObject<boolean>}){
  useEffect(()=>{
    let second=0;
    const first=requestAnimationFrame(()=>{second=requestAnimationFrame(()=>{if(!claimed.current)drawn()})});
    return()=>{cancelAnimationFrame(first);cancelAnimationFrame(second)};
  },[drawn,claimed]);
  return null;
}
function Render({mode,props}:{mode:string;props:VisualizerSharedProps}){
  // Evaluate the entry inside the error boundary, including synchronous tuning reads.
  return getVisualizerRegistryEntry(mode).render(applyVisualizerTuning(mode,props));
}
export default function Preset({mode,revision,props}:{mode:string;revision:number;props:VisualizerSharedProps}){
  const claimed=useRef(false),sent=useRef(false);
  const report=useMemo(()=>({
    claim:()=>{claimed.current=true},
    drawn:()=>{if(!sent.current){sent.current=true;window.peakFolia.presented(revision)}},
  }),[revision]);
  return <FirstFrameContext.Provider value={report}><VisualizerBackdropContext.Provider value={{pointerArea:null}}>
    <Suspense fallback={null}><Render mode={mode} props={props}/><Committed drawn={report.drawn} claimed={claimed}/></Suspense>
  </VisualizerBackdropContext.Provider></FirstFrameContext.Provider>;
}
