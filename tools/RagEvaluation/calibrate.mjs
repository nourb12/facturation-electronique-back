import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const report=JSON.parse(fs.readFileSync(path.join(root,'.rag-evaluation/after-initial.json'),'utf8'));
const rows=report.results.filter(q=>q.split==='calibration');
const grid=[];
for(let step=200;step<=850;step++){
 const threshold=step/1000;let tp=0,fn=0,fp=0,tn=0;
 for(const q of rows){const blocked=['Source fiscale manquante','Protection des informations'].includes(q.mode);const scenario=q.question.normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().match(/\b(optimiste|realiste|prudent)\b/)?.[1];
 const focused=scenario?q.results.filter(r=>r.heading.normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase().includes(scenario)):q.results;
 const accepted=!blocked&&focused.length>0&&focused[0].score/1000>=threshold;
  if(q.expectedDocument){if(accepted)tp++;else fn++;}else{if(accepted)fp++;else tn++;}}
 grid.push({threshold,tp,fn,fp,tn,cost:fp*3+fn});
}
grid.sort((a,b)=>a.cost-b.cost||b.threshold-a.threshold);
const best=grid[0];
fs.writeFileSync(path.join(root,'.rag-evaluation/calibration.json'),JSON.stringify({method:'Calibration only; false acceptance costs 3, false refusal costs 1. Highest threshold on a tie. Scores rounded to 0.001; final threshold uses a 0.002 numerical margin.',calibrationQuestionIds:rows.map(q=>q.id),selectedThreshold:Math.max(0,best.threshold-0.002),operatingPoint:best,grid},null,2));
console.log(JSON.stringify({selectedThreshold:Math.max(0,best.threshold-0.002),...best}));
