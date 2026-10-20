// Differential: old host PlanningSurface.assembleTenureMessages (CUT-08-main dist, same TenureIsolation source as
// master before CUT-17) vs candidate (CUT-06-main dist, TenureIsolation backed by wanxiangli/projection).
import * as oldS from '/workspace/vfs-cut/wt/CUT-08-main/dist/Mission/Planning/Surface.js'
import * as newS from '/workspace/vfs-cut/wt/CUT-06-main/dist/Mission/Planning/Surface.js'
const H = (id, role, parts, extra = {}) => ({ info: { ...(id === undefined ? {} : { id }), role, ...extra }, parts })
let seed = Number(process.argv[2] ?? 1)
const rnd = (n) => { seed = (seed + 0x6d2b79f5) | 0; let t = Math.imul(seed ^ (seed >>> 15), 1 | seed); t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t; return (((t ^ (t >>> 14)) >>> 0) % n) }
const roles = ['user', 'assistant', 'tool', 'system']
const run = (S, msgs, tenure, cur) => {
  try { const r = S.PlanningSurface.assembleTenureMessages(msgs, tenure, cur); return JSON.stringify({ ids: r.messages.map((m) => msgs.indexOf(m)), re: r.reanchorRequested }) }
  catch (e) { return 'THROW ' + (/no stable XTrace message identity/.test(e.message) ? 'identity' : e.message) }
}
const rounds = Number(process.argv[3] ?? 3000)
let cmp = 0, diverge = 0, throws = 0, kept = 0
for (let r = 0; r < rounds; r++) {
  const n = rnd(9), msgs = [], cur = {}
  for (let i = 0; i < n; i++) {
    const hasId = rnd(5) !== 0, id = hasId ? `m${i}` : undefined
    const parts = rnd(4) === 0 ? [] : [{ type: 'text', text: `t${i}` }]
    const extra = rnd(6) === 0 ? { synthetic: true } : {}
    msgs.push(H(id, roles[rnd(4)], parts, extra))
    if (hasId && rnd(3) !== 0) cur[id] = BigInt(rnd(10))
  }
  const tenure = { workId: 'w', incumbencyId: 'i', stage: 'S1', openingCursor: BigInt(rnd(10)), isFreshHandover: rnd(2) === 0 }
  const a = run(oldS, msgs, tenure, cur), b = run(newS, msgs, tenure, cur)
  cmp++; if (a !== b) { diverge++; if (diverge < 5) console.log('DIVERGE', a, b, JSON.stringify(msgs), tenure.openingCursor, cur) }
  if (a.startsWith('THROW')) throws++; else kept += JSON.parse(a).ids.length
}
console.log(JSON.stringify({ seed: process.argv[2] ?? 1, comparisons: cmp, divergences: diverge, oldThrows: throws, keptRows: kept }))
