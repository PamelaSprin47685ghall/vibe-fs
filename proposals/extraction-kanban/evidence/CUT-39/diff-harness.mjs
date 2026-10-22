// usage: node diff-harness.mjs <distDir> <out.json> [libDistAdvisorLaw.js]
// Seeded differential corpus over Advisor Surface.plan + the record/delivered cursor,
// and (optionally) AnchorView.materialKey vs wanxiangli/advisor materialKey.
import { writeFileSync } from 'node:fs'
import { pathToFileURL } from 'node:url'
const [dist, out, lib] = process.argv.slice(2)
const S = await import(pathToFileURL(`${dist}/Execution/Delegation/Advisor/Surface.js`))
const AV = await import(pathToFileURL(`${dist}/Repository/Programming/Js/Anchor/AnchorView.js`))
let seed = 0x39c0ffee
const rnd = () => ((seed = (seed * 1103515245 + 12345) >>> 0) / 2 ** 32)
const int = (n) => Math.floor(rnd() * n)
const pick = (xs) => xs[int(xs.length)]
const alphabet = ['a', 'b', ' ', '\n', ':', '1', '名', '😀', '"', '\\', '\t', 'z']
const str = (n = 1 + int(8)) => Array.from({ length: n }, () => pick(alphabet)).join('')
const results = []
const run = (f) => { try { return { ok: f() } } catch (e) { return { err: String(e.message).slice(0, 200) } } }
for (let c = 0; c < 3000; c++) {
  S.clearAll()
  const caller = 'caller-' + int(3)
  const steps = []
  let anchors = Array.from({ length: 1 + int(6) }, (_, i) => i + 1)
  let history = []
  const texts = {}
  for (let s = 0; s < 1 + int(4); s++) {
    if (rnd() < 0.4) anchors = [...anchors, anchors.length + 1]
    const messages = anchors.map((a) => { if (!(a in texts) || rnd() < 0.25) texts[a] = rnd() < 0.1 ? '' : 'm' + a + '-' + str(); return { anchor: a, text: texts[a] } })
    const thenMap = {}
    for (const a of [0, ...anchors]) if (rnd() < 0.2) thenMap[a] = rnd() < 0.3 ? null : 's-' + str()
    if (rnd() < 0.05) thenMap[1] = rnd() < 0.5 ? false : '   '
    const declaration = rnd() < 0.03 ? { prefixUntil: int(4) } : { prefixUntil: pick([0, 1, 2, 99, int(anchors.length + 1)]), thenMap }
    if (rnd() < 0.3 && anchors.length > 1) { const a = pick(anchors); history = [...history, { anchor: a, until: Math.min(anchors.at(-1), a + int(2)), summary: 'h-' + str() }] }
    const request = { messages, declaration, history }
    const planned = run(() => JSON.parse(JSON.stringify(S.plan(pick(['en', 'zh-CN']), S.delivered(caller), request))))
    const succeeded = rnd() < 0.7
    if (planned.ok && succeeded) S.record(caller, planned.ok.deliveredMaterial)
    steps.push({ planned, succeeded, delivered: S.delivered(caller) })
  }
  results.push(steps)
}
const keys = []
let libLaw = null
if (lib) { const { AdvisorLaw } = await import(pathToFileURL(lib)); libLaw = new AdvisorLaw() }
let keyMismatch = 0
for (let i = 0; i < 5000; i++) {
  const a = int(50), t = rnd() < 0.05 ? '' : str(int(20))
  const k = AV.materialKey(a, t)
  if (libLaw && libLaw.materialKey(a, t) !== k) keyMismatch++
  if (i < 500) keys.push(k)
}
writeFileSync(out, JSON.stringify({ results, keys }))
const errs = results.flat().filter((s) => s.planned.err).length
console.log(JSON.stringify({ cases: results.length, steps: results.flat().length, errors: errs, keyChecks: libLaw ? 5000 : 0, keyMismatch }))
process.exit(0)
