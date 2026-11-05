const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const expression=fs.readFileSync(path.join(__dirname,'../Desktop/CdpProgressReader.cs'),'utf8').match(/internal const string Expression = """\s*([\s\S]*?)\s*""";/)[1];
const states={Pause:1,Playing:2,Stop:0};
function fixture(){
  const playing={curPlaying:{track:{name:'Fixture',artists:[{name:'Artist'}]},resourceId:123},playId:456,playingState:2,resourceDuration:225};
  let q={playId:456,current:83};
  const getter=()=>q;
  let unsafeCalls=0;
  // Observed 3.1.43 layout: store moved to 7/a; 148/b is no longer a pure getter.
  // The actual clock is discovered by shape and playId rather than assuming its export letter.
  const cache={7:{exports:{a:{getStore:()=>({playing})}}},8:{exports:{k:states}},148:{exports:{b:()=>{unsafeCalls++;return q},c:getter}},149:{exports:{b:getter}}};
  const context=vm.createContext({location:{protocol:'orpheus:'},window:{__dangoReadOnlyRequire:{c:cache}},navigator:{mediaSession:{metadata:null,playbackState:'none'}},document:{title:'NetEase',querySelectorAll:()=>[]}});
  return {playing,cache,run:()=>vm.runInContext(expression,context,{timeout:1000}),unsafe:()=>unsafeCalls,setClock:x=>{q=x}};
}
let f=fixture(),result=f.run();
assert.equal(result.songId,123);assert.equal(result.positionMs,83000);assert.equal(result.playing,true);assert.equal(f.unsafe(),0);
f.setClock({playId:456,current:100});assert.equal(f.run().positionMs,100000,'cached bindings read fresh progress');
f.playing.playingState=1;f.playing.playId=null;f.setClock({playId:123,current:101});assert.equal(f.run().playing,false,'paused song-id fallback');
f=fixture();f.setClock({playId:999,current:83});assert.equal(f.run(),null,'reject unrelated song clock');
f=fixture();f.setClock({playId:456,current:226});assert.equal(f.run(),null,'reject out-of-range clock');
f=fixture();const second={playId:456,current:90};f.cache[999]={exports:{a:()=>second}};assert.equal(f.run(),null,'reject distinct matching clocks');
f=fixture();f.cache[8]={exports:{}};assert.equal(f.run(),null,'require recognized playback state enum');
f=fixture();f.cache[8]={exports:{a:{getStore:()=>{throw Error('unrelated')}},k:states}};assert.equal(f.run().songId,123,'unrelated legacy slot does not prevent discovery');
console.log('CDP binding discovery: moved modules, aliases, fresh progress, paused identity and rejection cases passed');
