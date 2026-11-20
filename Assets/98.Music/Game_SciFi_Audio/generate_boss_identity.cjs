'use strict';
// 보스별 음색과 소환·등장음만 갱신하는 재현 가능한 오디오 생성기다.
const fs=require('fs'),path=require('path'),SR=44100,TAU=Math.PI*2;
let seed=19112027;const report=[];
/** 동일한 시드로 거친 호흡과 금속 마찰에 사용할 잡음을 만든다. */
function rand(){seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/2147483648-1;}
/** 신호를 부드럽게 제한하고 시작·끝 처리를 거쳐 PCM WAV를 저장한다. */
function save(file,seconds,sample,loop=false){
 const n=Math.round(seconds*SR),a=new Float64Array(n);let mean=0;
 for(let i=0;i<n;i++){a[i]=Math.tanh(sample(i/SR,i)*1.5);mean+=a[i];}mean/=n;
 let max=0;for(let i=0;i<n;i++){a[i]-=mean;max=Math.max(max,Math.abs(a[i]));}
 const data=Buffer.alloc(n*2);let sum=0,maxDelta=0;
 for(let i=0;i<n;i++){
  // 단발의 경계 클릭을 없애고 최고 음량에 여유를 둔다.
  a[i]*=.85/max;if(!loop)a[i]*=Math.min(1,i/(SR*.008),(n-1-i)/(SR*.04));
  if(!Number.isFinite(a[i])||Math.abs(a[i])>=1)throw Error(file+' invalid sample');
  sum+=a[i]*a[i];if(i)maxDelta=Math.max(maxDelta,Math.abs(a[i]-a[i-1]));data.writeInt16LE(Math.round(a[i]*32767),i*2);
 }
 const h=Buffer.alloc(44);h.write('RIFF');h.writeUInt32LE(data.length+36,4);h.write('WAVEfmt ',8);h.writeUInt32LE(16,16);
 h.writeUInt16LE(1,20);h.writeUInt16LE(1,22);h.writeUInt32LE(SR,24);h.writeUInt32LE(SR*2,28);h.writeUInt16LE(2,32);h.writeUInt16LE(16,34);h.write('data',36);h.writeUInt32LE(data.length,40);
 const out=path.join(__dirname,file);fs.mkdirSync(path.dirname(out),{recursive:true});fs.writeFileSync(out,Buffer.concat([h,data]));
 report.push({file,seconds,loop,rms:Math.sqrt(sum/n),boundaryJump:Math.abs(a[0]-a[n-1]),maxAdjacentDelta:maxDelta});
}
/** 성문 고조파와 공명 대역으로 으르렁거림 또는 열린 모음의 비명을 만든다. */
function voice(file,scream){
 const duration=scream?2.6:1.7;let phase=0,air=0;
 save(file,duration,(t)=>{
  // 비명은 높은 음으로 솟았다가 꺼지고, 도그는 낮고 거칠게 가라앉는다.
  const f=scream?235+100*Math.sin(Math.PI*t/duration)-105*t/duration:84-28*t/duration;
  phase+=TAU*f/SR;air+=.22*(rand()-air);let v=0;
  const f1=scream?850:380,f2=scream?1450:920;
  for(let k=1;k<=32;k++){const hz=k*f;const form=.08+Math.exp(-Math.pow((hz-f1)/210,2))+.65*Math.exp(-Math.pow((hz-f2)/320,2));v+=Math.sin(phase*k)*form/Math.sqrt(k);}
  const envelope=Math.pow(Math.sin(Math.PI*t/duration),.55);
  return envelope*(v*(.75+.25*Math.sin(TAU*(scream?31:24)*t))+.25*air);
 });
}
/** 빠르게 반복되는 높고 낮은 전자 경보음을 만든다. */
function alarm(){
 save('07_Bosses/Head/head_death.wav',2.1,t=>{
  const period=.14,p=t%period;const env=Math.min(1,p/.008)*Math.max(0,Math.min(1,(.105-p)/.015));
  const f=Math.floor(t/period)%2?1450:1080;
  return env*(Math.sin(TAU*f*t)+.22*Math.sin(TAU*f*2*t));
 });
}
/** 함체가 찢어지는 저음과 금속 비명, 물속으로 가라앉는 잔향을 합성한다. */
function sinking(){
 let water=0;
 save('07_Bosses/Cruiser/cruiser_death_explosion.wav',3.4,t=>{
  water+=.035*(rand()-water);const env=Math.pow(Math.sin(Math.PI*t/3.4),.65);
  const metal=Math.sin(TAU*(310*t-24*t*t))*.32+Math.sin(TAU*(457*t-33*t*t))*.22;
  const creak=(.5+.5*Math.sin(TAU*2.5*t))*(.45*Math.sin(TAU*(76*t-6*t*t))+metal);
  const bubble=Math.sin(TAU*(135*t-12*t*t))*.18*(.5+.5*Math.sin(TAU*9*t));
  return env*(creak+water*2.5+bubble)+rand()*.3*Math.exp(-t/.08);
 });
}
/** 주기적인 대역 잡음으로 바람이 빨려드는 4초 무봉합 루프를 만든다. */
function suction(){
 const partials=Array.from({length:160},(_,k)=>({f:(160+k*19.25),p:rand()*Math.PI,a:1/Math.sqrt(1+k*.2)}));
 save('07_Bosses/Head/head_blackhole_loop.wav',4,t=>{
  let wind=0;for(const p of partials)wind+=p.a*Math.sin(TAU*p.f*t+p.p);
  // 모든 발진·변조 주파수는 0.25Hz 배수로 반복 경계를 일치시킨다.
  return wind*.15*(.7+.3*Math.sin(TAU*.5*t))+.17*Math.sin(TAU*80*t+Math.sin(TAU*.25*t));
 },true);
}
/** 자폭 적이 배출되는 순간의 짧은 공기 분출음을 만든다. */
function launch(){let air=0;save('07_Bosses/Cruiser/cruiser_minion_launch.wav',.22,t=>{
 air+=.45*(rand()-air);const env=Math.pow(Math.sin(Math.PI*t/.22),1.2);
 return env*(air*2+.3*Math.sin(TAU*(900*t-1400*t*t)));
});}
/** 헤드·크루저·로봇의 서로 다른 등장 동기를 만든다. */
function intros(){
 save('07_Bosses/Head/head_intro.wav',1.6,t=>{
  const gate=.45+.55*Math.pow(Math.sin(TAU*5*t),2),env=Math.pow(Math.sin(Math.PI*t/1.6),.6);
  return env*gate*(Math.sin(TAU*(260*t+220*t*t))+.25*Math.sin(TAU*1170*t));
 });
 let air=0;save('07_Bosses/Cruiser/cruiser_intro.wav',2.2,t=>{
  air+=.04*(rand()-air);const env=Math.pow(Math.sin(Math.PI*t/2.2),.6);
  return env*(.6*Math.sin(TAU*98*t)+.4*Math.sin(TAU*147*t)+.25*Math.sin(TAU*196*t)+air);
 });
 let hiss=0;save('07_Bosses/Alien/alien_robot_intro.wav',2,t=>{
  hiss+=.2*(rand()-hiss);const env=Math.pow(Math.sin(Math.PI*t/2),.7),pulse=Math.exp(-Math.pow((t-.3)/.045,2))+Math.exp(-Math.pow((t-1.05)/.05,2));
  return env*(.35*Math.sin(TAU*(65*t+55*t*t))+.2*Math.sin(TAU*540*t)+hiss*.5)+pulse*(Math.sin(TAU*120*t)+hiss);
 });
}
voice('07_Bosses/Dog/dog_death.wav',false);
voice('07_Bosses/Alien/alien_death.wav',true);
alarm();sinking();suction();launch();intros();
fs.writeFileSync(path.join(__dirname,'boss-identity-report.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));
