export function initialState(){return {version:1,node:'reply',line:0,flags:{},done:[]}}
export function reviewCheckpoint(data,target){
 if(!data.nodes[target])throw Error('Unknown review checkpoint');
 let state=initialState();
 for(let i=0;i<200;i++){
  if(state.node===target)return state;
  const n=data.nodes[state.node];
  state=step(data,state,state.line<n.lines.length?null:0);
 }
 throw Error('Review checkpoint not on default path');
}
export function resolve(data,state){
 const s=structuredClone(state);
 if(s.node==='prepare'&&s.flags.water_packed&&s.flags.blanket_packed){s.node='prepared';s.line=0}
 if(s.node==='inspect'&&s.flags.switch_checked&&s.flags.panel_checked){s.node='small_light';s.line=0}
 if(!data.nodes[s.node])throw Error('Unknown story node: '+s.node);
 return s;
}
export function availableChoices(data,state){return data.nodes[state.node].choices.filter(c=>c.requires.every(f=>state.flags[f])&&c.unless.every(f=>!state.flags[f]))}
function finish(data,state){const n=data.nodes[state.node];if(!state.done.includes(n.id)){Object.assign(state.flags,n.effects);state.done.push(n.id)}}
export function step(data,state,choiceIndex=null){
 const s=structuredClone(state),n=data.nodes[s.node];
 if(s.line<n.lines.length){s.line++;if(s.line<n.lines.length)return s;finish(data,s);if(n.choices.length)return s;if(n.next){s.node=n.next;s.line=0}return resolve(data,s)}
 if(choiceIndex!==null){const c=availableChoices(data,s)[choiceIndex];if(!c)throw Error('Choice not available');finish(data,s);Object.assign(s.flags,c.effects);s.node=c.target;s.line=0;return resolve(data,s)}
 return s;
}
export function sceneFor(data,state){
 let name=data.nodes[state.node].scene;
 if(name==='preparation')name=state.flags.water_packed?(state.flags.blanket_packed?'packed':'water_only'):(state.flags.blanket_packed?'blanket_only':'current');
 return data.scenes[name];
}
export function restore(data,raw){
 const s=JSON.parse(raw);if(s.version!==1||!s.flags||typeof s.flags!=='object'||!Array.isArray(s.done)||!data.nodes[s.node]||!Number.isInteger(s.line)||s.line<0||s.line>data.nodes[s.node].lines.length)throw Error('Invalid review bookmark');
 if(!s.done.every(id=>typeof id==='string'&&data.nodes[id]))throw Error('Invalid completed nodes');
 return resolve(data,s);
}
