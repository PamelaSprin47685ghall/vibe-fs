import { setTimeout as delay } from 'node:timers/promises'
import { readFileSync, writeFileSync, existsSync } from 'node:fs'
import { join } from 'node:path'
export { fixture, assert, data, caseName, failureCode, parkedPayload, render, WAIT, source } from './named-program.fixture.mjs'
export { readFileSync, writeFileSync, existsSync, delay }
export const quote = text => "'" + text.replaceAll("'", "'\\''") + "'"
export function command(h, name, body) {
  const file = h.file(name + '.cjs')
  writeFileSync(file, "const fs=require('node:fs'); const path=require('node:path'); const root=" + JSON.stringify(h.root) + ";const file=name=>path.join(root,name);\n" + body)
  return 'exec ' + quote(process.execPath) + ' ' + quote(file)
}
export function barrier(h, name, extra = '') {
  return command(h,name, "fs.appendFileSync(file("+JSON.stringify(name+'.starts')+"),'start\\n');fs.writeFileSync(file("+JSON.stringify(name+'.pid')+"),String(process.pid));"+extra+";const gate=setInterval(()=>{if(!fs.existsSync(file("+JSON.stringify(name+'.release')+")))return;clearInterval(gate);},10);")
}
export async function until(observe, message = 'physical observation was not reached') {
  const deadline = Date.now() + 4000
  while (!observe()) { if (Date.now() >= deadline) throw new Error(message); await delay(5) }
}
export async function stopped(h,name) {
  await until(()=>existsSync(h.file(name+'.pid')))
  const pid=Number(readFileSync(h.file(name+'.pid'),'utf8'))
  await until(()=>{
    try {
      const stat=readFileSync('/proc/'+pid+'/stat','utf8')
      return ['Z','X','x'].includes(stat.slice(stat.lastIndexOf(')')+1).trim().split(/\s+/)[0])
    } catch(error) { if(error.code==='ENOENT'||error.code==='ESRCH')return true;throw error }
  },'command did not exit')
}
