// dual compute: old host ProviderProjection (wt/CUT-17-base dist) vs new (library-owned bytes) through ProjectionSurface
const [oldDir, newDir, seedArg, roundsArg, mutate] = process.argv.slice(2)
const S = 'dist/Participant/Provider/Projection/Surface.js'
const oldS = await import(`${oldDir}/${S}`)
const newS = await import(`${newDir}/${S}`)
function mulberry32(a) { return () => { a |= 0; a = (a + 0x6d2b79f5) | 0; let t = Math.imul(a ^ (a >>> 15), 1 | a); t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t; return ((t ^ (t >>> 14)) >>> 0) / 4294967296 } }
const rnd = mulberry32(Number(seedArg))
const pick = (xs) => xs[Math.floor(rnd() * xs.length)]
const atoms = ['', 'a', 'b', '\\', '"', '\n', '\r', '\t', '\u0001', '\u001f', '\u007f', '\u2028', 'é', '😀', '\ud800', '{"x":1}', ',', ':', '[', ']', ' ']
const str = () => { let s = ''; const n = Math.floor(rnd() * 6); for (let i = 0; i < n; i++) s += pick(atoms); return s }
const opt = () => (rnd() < 0.4 ? null : str())
const id = () => pick(['c1', 'c2', 'call_' + Math.floor(rnd() * 5), str()])
const part = () => {
  switch (Math.floor(rnd() * 5)) {
    case 0: return { kind: 'text', text: str() }
    case 1: return { kind: 'reasoning', text: str() }
    case 2: return { kind: 'tool-call', callId: id(), name: str(), args: str() }
    case 3: return { kind: 'tool-result', callId: id(), result: str() }
    default: return { kind: 'media', mediaType: opt(), contentDigest: str() }
  }
}
const message = () => ({ role: pick(['user', 'assistant', 'tool', 'system', str()]), parts: Array.from({ length: Math.floor(rnd() * 4) }, part) })
const projection = () => ({
  providerId: opt(), modelId: opt(), variant: opt(),
  tools: Array.from({ length: Math.floor(rnd() * 3) }, str),
  system: Array.from({ length: Math.floor(rnd() * 3) }, str),
  messages: Array.from({ length: Math.floor(rnd() * 5) }, message),
})
const ident = (x) => x
const run = (f) => { try { return 'ok:' + JSON.stringify(f()) } catch (e) { return 'err:' + e.message } }
let compared = 0, diverged = 0, okBoth = 0, sample = null
const cmp = (label, f) => {
  const a = run(() => f(oldS)), b = run(() => f(newS))
  compared++
  if (a.startsWith('ok:') && b.startsWith('ok:')) okBoth++
  if (a !== b) { diverged++; if (!sample) sample = { label, a: a.slice(0, 300), b: b.slice(0, 300) } }
}
const rounds = Number(roundsArg)
for (let i = 0; i < rounds; i++) {
  const p = projection()
  const q = rnd() < 0.5 ? { ...p, messages: [...p.messages, ...Array.from({ length: Math.floor(rnd() * 2) }, message)] } : rnd() < 0.5 ? projection() : { ...p, tools: [...p.tools].reverse() }
  cmp('seal', (s) => s.sealDigest(ident, p))
  cmp('renderWire', (s) => s.renderWire(p.messages))
  cmp('renderSemantic', (s) => s.renderSemantic(p))
  cmp('semanticallyEqual', (s) => s.semanticallyEqual(p, q))
  cmp('append', (s) => s.isAppendOnlyPrefix(p, q))
  const snap = { currentProjection: { ...oldS.semanticProjection(p.messages), providerId: p.providerId, modelId: p.modelId, variant: p.variant, tools: p.tools, system: p.system } }
  const cut = Math.floor(rnd() * (p.messages.length + 2))
  cmp('cutoff', (s) => s.cutoffDigest(ident, snap, cut))
}
console.log(JSON.stringify({ seed: seedArg, rounds, compared, okBoth, diverged, sample }))
