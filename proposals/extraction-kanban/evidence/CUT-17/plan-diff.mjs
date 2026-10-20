// Differential: old host ProjectionPlanner (master dist before this cut, wt/CUT-17-base) vs candidate
// (ProjectionPlanner backed by wanxiangli/projection plan), through the public ProjectionSurface plan/render.
const OLD = process.env.OLD_DIST ?? '/workspace/vfs-cut/wt/CUT-17-base/dist'
const NEW = process.env.NEW_DIST ?? '/workspace/vfs-cut/wt/CUT-06-main/dist'
const oldS = await import(OLD + '/Participant/Provider/Projection/Surface.js')
const newS = await import(NEW + '/Participant/Provider/Projection/Surface.js')
let seed = Number(process.argv[2] ?? 1)
const rnd = (n) => { seed = (seed + 0x6d2b79f5) | 0; let t = Math.imul(seed ^ (seed >>> 15), 1 | seed); t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t; return (((t ^ (t >>> 14)) >>> 0) % n) }
const pick = (a) => a[rnd(a.length)]
const parts = [
  { kind: 'text', text: 'a' }, { kind: 'text', text: 'b' }, { kind: 'reasoning', text: 'a' },
  { kind: 'tool-call', callId: 'c1', name: 'read', args: '{}' }, { kind: 'tool-call', callId: 'c2', name: 'read', args: '{}' },
  { kind: 'tool-result', callId: 'c1', result: 'ok' }, { kind: 'media', mediaType: null, contentDigest: 'd' },
  { kind: 'media', mediaType: 'image/png', contentDigest: 'd' }, { kind: 'text', text: '"],[' },
]
const rowPool = []
for (let i = 0; i < 6; i++) rowPool.push({ message: { role: pick(['user', 'assistant', 'tool']), parts: Array.from({ length: rnd(3) }, () => pick(parts)) }, hostMessageId: rnd(2) ? `h${rnd(3)}` : null, hostIsPhysical: rnd(2) === 0 })
const rowsOf = () => Array.from({ length: rnd(3) }, () => pick(rowPool))
const snap = { currentProjection: { providerId: null, modelId: null, variant: null, tools: [], system: [], messages: [] } }
const run = (f) => { try { return JSON.stringify(f()) } catch (e) { return 'THROW ' + String(e.message).replace(/\d+/g, 'N').slice(0, 60) } }
const rounds = Number(process.argv[3] ?? 3000)
let cmp = 0, diverge = 0, conflicts = 0, renders = 0
for (let r = 0; r < rounds; r++) {
  const intents = [], baseRows = rowsOf()
  const nb = rnd(4) === 0 ? 2 : rnd(2)
  for (let i = 0; i < nb; i++) intents.push({ kind: 'ReplaceMessageBase', key: pick(['b', 'b', 'c']), rows: rnd(3) ? baseRows : rowsOf() })
  const ni = rnd(6)
  for (let i = 0; i < ni; i++) {
    const anchor = rnd(3) === 0 ? { kind: 'Append' } : { kind: 'BeforeMessageIndex', index: rnd(9) - 2 }
    const ins = { kind: 'InsertMessageRows', key: pick(['k1', 'k2', 'k3', 'K', 'k10']), anchor, rows: rowsOf() }
    intents.push(ins)
    if (rnd(3) === 0) intents.push(rnd(2) ? { ...ins } : { ...ins, anchor: rnd(2) ? anchor : { kind: 'Append' } })
  }
  for (let i = intents.length - 1; i > 0; i--) { const j = rnd(i + 1); [intents[i], intents[j]] = [intents[j], intents[i]] }
  const base = Array.from({ length: rnd(5) }, () => pick(rowPool).message)
  for (const S of [0]) {
    const a = run(() => oldS.plan(intents)), b = run(() => newS.plan(intents))
    cmp++; if (a !== b) { diverge++; if (diverge < 5) console.log('DIVERGE plan', a, b, JSON.stringify(intents)) }
    if (a.includes('"ok":false')) conflicts++
    const c = run(() => oldS.renderMessagesWithHostIds(snap, base, intents)), d = run(() => newS.renderMessagesWithHostIds(snap, base, intents))
    cmp++; if (c !== d) { diverge++; if (diverge < 5) console.log('DIVERGE render', c, d, JSON.stringify(intents)) }
    if (!c.startsWith('THROW')) renders++
  }
}
console.log(JSON.stringify({ seed: process.argv[2] ?? 1, comparisons: cmp, divergences: diverge, conflicts, renders, renderThrows: rounds - renders }))
