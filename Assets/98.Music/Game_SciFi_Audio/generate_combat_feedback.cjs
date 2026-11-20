'use strict';
// 전투 피드백만 재생성하며 기존 음원 GUID와 카탈로그 연결은 유지한다.
const fs = require('fs'), path = require('path');
const SR = 44100, TAU = Math.PI * 2, report = [];
let seed = 19112026;
/** 재현 가능한 잡음을 생성한다. */
function noise() { seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0; return seed / 2147483648 - 1; }
/** 여유 음량을 가진 16비트 PCM으로 저장하고 반복 경계를 검사한다. */
function save(file, samples, peak, loop = false) {
  // DC 성분을 제거한 뒤 최대 피크를 제한한다.
  const mean = samples.reduce((a,b)=>a+b,0) / samples.length;
  let max = 0;
  for (let i=0;i<samples.length;i++) { samples[i]-=mean; max=Math.max(max,Math.abs(samples[i])); }
  if (!(max>0)) throw Error('Silent output: '+file);
  const pcm=Buffer.alloc(samples.length*2), scale=peak/max;
  let sum=0;
  for(let i=0;i<samples.length;i++) {
    let v=samples[i]*scale;
    // 단발의 시작과 끝은 짧게 감쇠하여 클릭을 방지한다.
    if(!loop) v*=Math.min(1,i/(SR*.004),(samples.length-1-i)/(SR*.015));
    if(!Number.isFinite(v)||Math.abs(v)>=1) throw Error('Invalid sample');
    samples[i]=v; sum+=v*v; pcm.writeInt16LE(Math.round(v*32767),i*2);
  }
  // PCM WAV 헤더를 구성한다.
  const h=Buffer.alloc(44); h.write('RIFF');h.writeUInt32LE(36+pcm.length,4);h.write('WAVEfmt ',8);
  h.writeUInt32LE(16,16);h.writeUInt16LE(1,20);h.writeUInt16LE(1,22);h.writeUInt32LE(SR,24);
  h.writeUInt32LE(SR*2,28);h.writeUInt16LE(2,32);h.writeUInt16LE(16,34);h.write('data',36);h.writeUInt32LE(pcm.length,40);
  const target=path.join(__dirname,file);fs.mkdirSync(path.dirname(target),{recursive:true});
  fs.writeFileSync(target,Buffer.concat([h,pcm]));
  report.push({file,seconds:samples.length/SR,peak,rms:Math.sqrt(sum/samples.length),loop,boundaryJump:Math.abs(samples[0]-samples.at(-1))});
}
/** 짧은 성문 진동과 거친 호흡으로 신음형 피격음을 합성한다. */
function hurt() {
  const out=new Float64Array(SR*.46);let phase=0,breath=0;
  for(let i=0;i<out.length;i++) {
    const t=i/SR, f=143-65*t/.46;phase+=TAU*f/SR;breath+=.28*(noise()-breath);
    // 두 짧은 음절과 감소하는 음높이로 충격 후 숨이 빠지는 질감을 만든다.
    const env=(1-Math.exp(-t/0.006))*(Math.exp(-t/0.13)+.45*Math.exp(-Math.pow((t-.19)/.055,2)));
    let voice=0;
    for(let k=1;k<=26;k++) {
      const hz=f*k, formant=Math.exp(-Math.pow((hz-620)/200,2))+.5*Math.exp(-Math.pow((hz-1200)/280,2));
      voice+=Math.sin(phase*k)*(0.08+formant)/k;
    }
    out[i]=env*(voice*(.85+.15*Math.sin(TAU*37*t))+.35*breath);
  }
  save('06_Combat/player_hurt.wav',out,.7);
}
/** 저음 압력, 파열음, 잔해음을 층으로 쌓아 개별 사망음을 만든다. */
function explosion(file,duration,freq,metal,peak) {
  const out=new Float64Array(Math.round(SR*duration));let low=0;
  for(let i=0;i<out.length;i++) {
    const t=i/SR,n=noise();low+=.055*(n-low);
    const attack=1-Math.exp(-t/.002);
    // 저음은 빠르게 내려가고 금속 잔향은 더 오래 남는다.
    const body=Math.sin(TAU*(freq*t-0.22*freq*t*t/duration))*Math.exp(-t/(duration*.15));
    const debris=(Math.sin(TAU*metal*t)+.4*Math.sin(TAU*metal*1.47*t))*Math.exp(-t/(duration*.3));
    out[i]=attack*(body*.7+low*2*Math.exp(-t/(duration*.25))+n*.25*Math.exp(-t/.025)+debris*.14);
  }
  save(file,out,peak);
}
/** 정수 주기의 발진기와 잡음 대역을 합성하여 끊김 없는 지속음을 만든다. */
function loop(file,blackhole) {
  const duration=4,n=SR*duration,out=new Float64Array(n),partials=[];
  // 모든 주파수는 4초의 정수 주기로 선택하여 시작과 끝의 위상을 맞춘다.
  for(let k=0;k<80;k++) partials.push({f:Math.round((blackhole?70+k*21:650+k*23)*duration)/duration,p:noise()*Math.PI,a:1/Math.sqrt(k+1)});
  for(let i=0;i<n;i++) {
    const t=i/SR;let air=0;
    for(const p of partials) air+=p.a*Math.sin(TAU*p.f*t+p.p);
    // 레이저는 고정 고주파, 블랙홀은 느리게 맥동하는 공기 흐름을 중심으로 한다.
    out[i]=blackhole
      ? air*.12*(.75+.25*Math.sin(TAU*.5*t))+.45*Math.sin(TAU*55*t+1.2*Math.sin(TAU*.25*t))
      : .6*Math.sin(TAU*220*t+2*Math.sin(TAU*440*t))+.22*Math.sin(TAU*1760*t)+air*.035;
  }
  save(file,out,blackhole?.38:.42,true);
}
hurt();
explosion('06_Combat/enemy_death.wav',.32,150,410,.65);
explosion('07_Bosses/Dog/dog_death.wav',1.1,110,230,.75);
explosion('07_Bosses/Head/head_death.wav',1.35,85,740,.75);
explosion('07_Bosses/Cruiser/cruiser_death_explosion.wav',1.8,55,160,.8);
explosion('07_Bosses/Alien/alien_death.wav',1.6,74,330,.78);
loop('07_Bosses/Head/head_laser_loop.wav',false);
loop('07_Bosses/Head/head_blackhole_loop.wav',true);
// 재생성 결과를 수치로 남겨 무음과 클리핑 여부를 검토할 수 있게 한다.
fs.writeFileSync(path.join(__dirname,'combat-feedback-report.json'),JSON.stringify(report,null,2));
console.log(JSON.stringify(report,null,2));
