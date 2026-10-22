// Differential: old host ProjectionRenderer (wt/CUT-17-base dist, master before CUT-17 ②/③; Projection/ byte-identical
// to master before this cut) vs candidate (ProjectionRenderer backed by wanxiangli/projection layout), through the public
// ProjectionSurface renderMessagesWithHostIds/renderMessages. Successful renders must match byte for byte (messages, host
// ids, physical flags); when both sides refuse, the refusal is counted as agreeing and the message pair is tallied
// separately (the old messages came from List.splitAt internals, the new ones from the library's anchor check).
const OLD = process.env.OLD_DIST ?? '/workspace/vfs-cut/wt/CUT-17-base/dist'
const NEW = process.env.NEW_DIST ?? '/workspace/vfs-cut/wt/CUT-06-main/dist'
const oldS = await import(OLD + '/Participant/Provider/Projection/Surface.js')
const newS = await import(NEW + '/Participant/Provider/Projection/Surface.js')
let seed = Number(process.argv[2] ?? 1)
const rnd = (n) => { seed = (seed + 0x6d2b79f5) | 0; let t = Math.imul(seed ^ (seed >>> 15), 1 | seed); t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t; return (((t ^ (t >>> 14)) >>> 0) % n) }
const pick = (a) => a[rnd(a.length)]
const parts = [
  { kind: 'text', text: 'a' }, { kind: 'text', text: 'b' }, { kind: 'reasoning', text: 'a' },
  { kind: 'tool-call', callId: 'c1', name: 'read', args: '{}' }, { kind: 'tool-result', callId: 'c1', result: 'ok' },
  { kind: 'media', mediaType: null, contentDigest: 'd' }, { kind: 'text', text: '"],[' },
]
const rowPool = []
for (let i = 0; i < 8; i++) rowPool.push({ message: { role: pick(['user', 'assistant', 'tool']), parts: Array.from({ length: 1 + rnd(2) }, () => pick(parts)) }, hostMessageId: rnd(2) ? `h${i}` : null, hostIsPhysical: rnd(2) === 0 })
const rowsOf = () => Array.from({ length: rnd(4) }, () => pick(rowPool))
const snap = { currentProjection: { providerId: null, modelId: null, variant: null, tools: [], system: [], messages: [] } }
const run = (f) => { try { return { ok: JSON.stringify(f()) } } catch (e) { return { err: String(e.message).replace(/\d+/g, 'N').slice(0, 50) } } }
const rounds = Number(process.argv[3] ?? 3000)
let cmp = 0, diverge = 0, renders = 0, bothRefuse = 0
const refusalPairs = new Map()
for (let r = 0; r < rounds; r++) {
  const base = Array.from({ length: rnd(5) }, () => pick(rowPool).message)
  const intents = []
  let space = base.length
  if (rnd(3) === 0) { const rows = rowsOf(); space = rows.length; intents.push({ kind: 'ReplaceMessageBase', key: 'b', rows }); if (rnd(4) === 0) intents.push({ kind: 'ReplaceMessageBase', key: 'b', rows }) }
  const ni = rnd(6)
  for (let i = 0; i < ni; i++) {
    const anchor = rnd(3) === 0 ? { kind: 'Append' } : { kind: 'BeforeMessageIndex', index: rnd(space + 3) - (rnd(8) === 0 ? 1 : 0) - (rnd(6) === 0 ? 0 : 1) }
    const ins = { kind: 'InsertMessageRows', key: pick(['k1', 'k2', 'k3', 'K', 'k10', 'a']), anchor, rows: rowsOf() }
    intents.push(ins)
    if (rnd(4) === 0) intents.push({ ...ins })
  }
  for (let i = intents.length - 1; i > 0; i--) { const j = rnd(i + 1); [intents[i], intents[j]] = [intents[j], intents[i]] }
  for (const f of ['renderMessagesWithHostIds', 'renderMessages']) {
    const a = run(() => oldS[f](snap, base, intents)), b = run(() => newS[f](snap, base, intents))
    cmp++
    if (a.ok !== undefined || b.ok !== undefined) {
      if (a.ok !== b.ok) { diverge++; if (diverge < 5) console.log('DIVERGE', f, a, b, JSON.stringify({ base, intents })) } else renders++
    } else {
      bothRefuse++
      const k = a.err + ' | ' + b.err; refusalPairs.set(k, (refusalPairs.get(k) ?? 0) + 1)
      // a conflict refusal must stay the same message (it is the host's own invalidOp)
      if ((a.err.includes('conflict-free') || b.err.includes('conflict-free')) && a.err !== b.err) { diverge++; console.log('DIVERGE refusal', a.err, b.err) }
    }
  }
}
console.log(JSON.stringify({ seed: process.argv[2] ?? 1, comparisons: cmp, divergences: diverge, identicalRenders: renders, bothRefuse }))
if (process.env.PAIRS) for (const [k, v] of refusalPairs) console.log('  refusal', v, k)
