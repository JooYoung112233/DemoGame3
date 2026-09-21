export const clamp01=value=>Math.max(0,Math.min(1,value));
export const smoothstep=value=>{const t=clamp01(value);return t*t*(3-2*t);};
export function durationOf(config){
  const call=config.entry_tracks.find(t=>t.id==='call-text');
  return Math.max(call.start+(graphemes(call.text).length-1)*call.seconds_per_grapheme,
    config.camera.start_seconds+config.camera.duration_seconds,
    ...config.entry_tracks.filter(t=>['start-feedback','menu-exit'].includes(t.id)).map(t=>t.start+t.duration));
}
export const graphemes=text=>Array.from(new Intl.Segmenter('ko',{granularity:'grapheme'}).segment(text),s=>s.segment);
export const transitionDuration=config=>{const t=config.memory_transition;return t.fade_out_seconds+t.black_hold_seconds+t.fade_in_seconds;};
export function transitionFrame(seconds,config){
 const t=config.memory_transition;
 const swap=seconds>=t.fade_out_seconds;
 const fadeInStart=t.fade_out_seconds+t.black_hold_seconds;
 const blackOpacity=swap?1-smoothstep((seconds-fadeInStart)/t.fade_in_seconds):smoothstep(seconds/t.fade_out_seconds);
 return {blackOpacity,showMemory:swap,complete:seconds>=transitionDuration(config)};
}
export function sampleFrame(seconds,config){
  const camera=config.camera;
  const progress=smoothstep((seconds-camera.start_seconds)/camera.duration_seconds);
  const scale=camera.scale_from+(camera.scale_to-camera.scale_from)*progress;
  const menu=config.entry_tracks.find(t=>t.id==='menu-exit');
  const feedback=config.entry_tracks.find(t=>t.id==='start-feedback');
  const dim=config.entry_tracks.find(t=>t.id==='bottom-dim');
  const call=config.entry_tracks.find(t=>t.id==='call-text');
  const count=seconds<call.start?0:Math.min(graphemes(call.text).length,1+Math.floor((seconds-call.start+1e-9)/call.seconds_per_grapheme));
  return {seconds,scale,
    dimOpacity:smoothstep((seconds-dim.start)/dim.duration),visibleGraphemes:count,
    x:config.resolution[0]*camera.focus_normalized[0]*(1-scale),
    y:config.resolution[1]*camera.focus_normalized[1]*(1-scale),
    menuOpacity:1-smoothstep((seconds-menu.start)/menu.duration),
    pressed:seconds>=feedback.start&&seconds<feedback.start+feedback.duration,
    complete:seconds>=durationOf(config)};
}
export function configured(base,preset){
  const copy=structuredClone(base);
  if(preset==='gentle')Object.assign(copy.camera,{duration_seconds:1.5,scale_to:1.02});
  if(preset==='clear')Object.assign(copy.camera,{duration_seconds:1.0,scale_to:1.05});
  return copy;
}
