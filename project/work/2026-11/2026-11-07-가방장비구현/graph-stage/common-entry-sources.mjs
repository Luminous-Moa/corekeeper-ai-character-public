import fs from 'node:fs';import path from 'node:path';import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url)),p=path.join(stage,'work/2026-11/2026-11-05-지식그래프-실화면/D/bag-source.mjs');let s=fs.readFileSync(p,'utf8');
s=s.replace("'AIInventory.CheckGoal','AIInventory.ReportJson'","'AIInventory.CheckGoal','AIInventory.ReportJson','AIGoals.FinishChosen','AIPlanner.RequestReplan','AISkill.ObserveOnce'");
s=s.replace("  if(code.length===1)add('P.bag-agency',code[0].id,'역할 경계의 구현 위치(정적)',planEvidence);","  if(code.length!==1)throw Error('Role/common control anchor missing or ambiguous: '+symbol);\n  add('P.bag-agency',code[0].id,'역할·공통 제어 경계의 구현 위치(정적)',planEvidence);");
fs.writeFileSync(p,s);console.log('Attached shared finish_goal/request_plan/ObserveOnce boundaries to final code identities.');
