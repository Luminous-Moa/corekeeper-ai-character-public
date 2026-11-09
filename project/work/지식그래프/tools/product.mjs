#!/usr/bin/env node
// 제작 지침 A~D로 만든 제품 그래프의 읽기 전용 진입점.
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {spawnSync} from 'node:child_process';
const here=path.dirname(fileURLToPath(import.meta.url));
const target=path.resolve(here,'../../2026-11/2026-11-05-지식그래프-실화면/D/query.mjs');
const result=spawnSync(process.execPath,[target,...process.argv.slice(2)],{stdio:'inherit'});
if(result.error){console.error(result.error.message);process.exit(1);}
process.exit(result.status??1);
