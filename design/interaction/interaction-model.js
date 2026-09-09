function createLive49InteractionModel() {
  const definitions = {
    drawer: { name: '걸린 서랍', item: '성냥', game: 'search' },
    food: { name: '식품 선반', item: '통조림', game: null },
    medicine: { name: '구급 물품장', item: '진통제', game: null },
    pencils: { name: '문구 선반', item: '색연필', game: 'search' },
    stove: { name: '작은 주방', item: null, game: 'cook' },
    sketchbook: { name: '소이의 스케치북', item: null, game: null }
  };
  const state = { selected: null, phase: 'idle', inventory: [], completed: [], found: [], noise: 0, minutes: 0, result: '', game: null };
  function select(id) {
    if (!definitions[id] || state.phase === 'running') return false;
    state.selected=id; state.game=definitions[id].game; state.result='';
    state.phase=state.completed.includes(id)?'completed':state.found.includes(id)?'found':'inspect';
    return true;
  }
  function start() {
    const d=definitions[state.selected];
    if (!d || !d.game || !['inspect','failed'].includes(state.phase)) return false;
    if(d.game==='cook'&&!state.inventory.includes('통조림')) return false;
    state.phase='running';state.result='';return true;
  }
  function resolve(position) {
    if(state.phase!=='running'||!Number.isFinite(position)||position<0||position>100) return false;
    state.minutes++;
    const success=position>=39&&position<=65;
    const perfect=position>=49&&position<=55;
    if(state.game==='cook') {
      const at=state.inventory.indexOf('통조림');
      if(at<0) {state.phase='inspect';return false;}
      state.inventory.splice(at,1,success?'따뜻한 수프':'조금 탄 수프');
      state.completed.push('stove'); state.phase='completed';
      state.result=success?(perfect?'알맞게 데워졌다. 소이와 나눠 먹을 수 있겠다.':'따뜻한 수프가 완성됐다.'):'조금 눌어붙었지만 먹을 수는 있다. 다음엔 불을 조금 일찍 줄이자.';
    } else if(success) {
      if(!state.found.includes(state.selected))state.found.push(state.selected);
      state.phase='found';state.result=perfect?'소리 없이 꺼냈다.':'물건을 무사히 꺼냈다.';
    } else {
      state.noise++;state.phase='failed';state.result='덜컹. 소리가 났다. 다시 시도하거나 멈출 수 있다.';
    }
    return true;
  }
  function take() {
    const id=state.selected,d=definitions[id];
    if(!d||!d.item||state.completed.includes(id)||state.inventory.length>=4)return false;
    if(d.game&&state.phase!=='found')return false;
    if(!d.game&&state.phase!=='inspect')return false;
    state.inventory.push(d.item);state.completed.push(id);
    if(!state.found.includes(id))state.found.push(id);
    state.phase='completed';state.result=d.item+'을 가방에 넣었다.';return true;
  }
  function cancel() {
    if(state.phase==='running') {state.minutes++; if(state.game==='search')state.noise++;}
    state.selected=null;state.phase='idle';state.game=null;state.result='';
  }
  return { state, definitions, select, start, resolve, take, cancel };
}
if(typeof module!=='undefined')module.exports={createLive49InteractionModel};
