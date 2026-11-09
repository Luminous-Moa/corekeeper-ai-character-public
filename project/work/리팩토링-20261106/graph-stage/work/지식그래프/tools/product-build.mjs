#!/usr/bin/env node
// A/B/C 산출물을 다시 합친다. 코드 원본 재추출 절차는 제품그래프-사용법.md 참조.
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {spawnSync} from 'node:child_process';
const here=path.dirname(fileURLToPath(import.meta.url));
const target=path.resolve(here,'../../2026-11/2026-11-05-지식그래프-실화면/D/integrate.mjs');
const result=spawnSync(process.execPath,[target,...process.argv.slice(2)],{stdio:'inherit'});
if(result.error){console.error(result.error.message);process.exit(1);}
process.exit(result.status??1);
