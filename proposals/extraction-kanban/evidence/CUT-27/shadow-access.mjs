// CUT-27 ③ shadow differential: drive the old host Capture (base dist) and the candidate host Capture
// (wanxiangli/casebook-access behind it) over the same fixed-seed random tool results, executor
// command lines and fission merges; compare every observable output. Pure: no files, no store.
// usage: node shadow-access.mjs <base-dist> <cand-dist> [rounds]
import { join, resolve } from 'node:path'
const [baseDist, candDist, roundsArg] = process.argv.slice(2)
const load = d => import(join(resolve(d), 'Repository/Knowledge/Casebook/Capture.js'))
const B = await load(baseDist), C = await load(candDist)
let seed = 2703
const random = () => { seed = (seed * 1103515245 + 12345) % 2147483648; return seed / 2147483648 }
const pick = l => l[Math.floor(random() * l.length)]
const paths = ['a.txt', 'src/b.fs', ' ', '', 'z', 'dir/', '缓存.md', '😀.txt', 'a.txt', "it's.txt", '-n', '10', 'x y']
const tools = ['create', 'rewrite', 'delete', 'move', 'read', 'edit', 'grep', 'glob', 'ls', 'Create', '']
const pathKeys = ['path', 'filePath', 'file', 'target']
const args = () => {
  const o = {}
  for (const k of pathKeys) if (random() < 0.35) o[k] = random() < 0.1 ? null : pick(paths)
  if (random() < 0.5) o.source = random() < 0.1 ? null : pick(paths)
  if (random() < 0.5) o.destination = random() < 0.1 ? null : pick(paths)
  return o
}
const words = ['cat', 'head', 'tail', 'sed', 'sh', 'bash', 'grep', '-n', '5', '-c', "'s/a/b/'", "'x y'", 'a.txt', 'f', '|', '$(ls)', '`x`', '-n5', '+5', '100', '\t', '']
const command = () => Array.from({ length: Math.floor(random() * 6) }, () => pick(words)).join(random() < 0.8 ? ' ' : '\t')
const obs = o => o === undefined || o === null ? null : JSON.stringify(o)
const rounds = Number(roundsArg ?? 2000)
let equal = 0, differ = 0, cases = 0; const examples = []
const same = (what, a, b, input) => { cases++; if (JSON.stringify(a) === JSON.stringify(b)) equal++; else { differ++; examples.push({ what, input, base: a, cand: b }) } }
for (let r = 0; r < rounds; r++) {
  const tb = B.CasebookCapture_createAccessTracker(), tc = C.CasebookCapture_createAccessTracker()
  const steps = []
  for (let s = Math.floor(random() * 6); s >= 0; s--) {
    const t = pick(tools), a = args(), committed = random() < 0.7
    steps.push([t, a, committed])
    B.CasebookCapture_recordSubstantiveAccess(tb, t, a, committed); C.CasebookCapture_recordSubstantiveAccess(tc, t, a, committed)
    same('isSubstantiveTool', B.CasebookCapture_isSubstantiveTool(t), C.CasebookCapture_isSubstantiveTool(t), t)
  }
  same('relatedPaths', B.AccessTracker__getRelatedPaths(tb), C.AccessTracker__getRelatedPaths(tc), steps)
  const cmd = command()
  same('ofExecCommand', obs(B.CasebookCapture_ofExecCommand(cmd)), obs(C.CasebookCapture_ofExecCommand(cmd)), cmd)
}
const probes = ['head -c 100 f', 'tail -n +5 f', 'tail -c 20 f', 'cat -n f', 'head -n5 f', 'sed -n 1p f', 'cat `x`', 'cat f | grep x', 'echo $(cat f)']
const probe = probes.map(c => ({ command: c, base: obs(B.CasebookCapture_ofExecCommand(c)), cand: obs(C.CasebookCapture_ofExecCommand(c)) }))
const kinds = {}; for (const e of examples) { const k = e.what + (typeof e.input === 'string' && e.input.includes('`') ? ':backtick' : ':other'); kinds[k] = (kinds[k] ?? 0) + 1 }
console.log(JSON.stringify({ rounds, cases, equal, differ, differByKind: kinds, probe, examples: examples.slice(0, 5) }, null, 1))
