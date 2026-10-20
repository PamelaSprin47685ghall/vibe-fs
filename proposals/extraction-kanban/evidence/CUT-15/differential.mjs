// CUT-15 differential: the same inputs through base and candidate dist.
// usage: node differential.mjs <base-root> <cand-root> [seed]
import path from 'node:path'
import { pathToFileURL } from 'node:url'

const [baseRoot, candRoot, seedArg] = process.argv.slice(2)
const load = (root, rel) => import(pathToFileURL(path.join(path.resolve(root), 'dist', rel)).href)
const sides = await Promise.all([baseRoot, candRoot].map(async root => ({
  wire: await load(root, 'Participant/Provider/Projection/Surface.js'),
  policy: await load(root, 'Host/Contract/CompactionPolicySurface.js'),
})))

let seed = Number(seedArg ?? 15)
const rnd = () => ((seed = (seed * 1103515245 + 12345) % 2147483648) / 2147483648)
const pick = xs => xs[Math.floor(rnd() * xs.length)]
const small = ['a', 'b', 'é', '', 'x y', '{"k":1}', '\u0000', 'b ']
const part = () => pick([
  () => ({ kind: 'text', text: pick(small) }),
  () => ({ kind: 'reasoning', text: pick(small) }),
  () => ({ kind: 'tool-call', callId: pick(['c1', 'c2']), name: pick(['read', 'edit']), args: pick(small) }),
  () => ({ kind: 'tool-result', callId: pick(['c1', 'c2']), result: pick(small) }),
  () => ({ kind: 'media', mediaType: pick([null, 'image/png']), contentDigest: pick(['d1', 'd2']) }),
])()
const message = () => ({ role: pick(['user', 'assistant', 'system']), parts: Array.from({ length: Math.floor(rnd() * 3) }, part) })
const identity = () => ({
  providerId: pick([null, 'p1', 'p2']), modelId: pick([null, 'm1']), variant: pick([null, 'high', 'low']),
  tools: pick([[], ['read'], ['read', 'edit'], ['edit', 'read']]), system: pick([[], ['s'], ['s', 't']]),
})
const mutate = w => {
  const next = structuredClone(w)
  const r = rnd()
  if (r < 0.35) next.messages.push(...Array.from({ length: 1 + Math.floor(rnd() * 3) }, message))
  else if (r < 0.45) next.messages.pop()
  else if (r < 0.6 && next.messages.length) next.messages[Math.floor(rnd() * next.messages.length)] = message()
  else if (r < 0.7) Object.assign(next, { [pick(['providerId', 'modelId', 'variant'])]: pick([null, 'p1', 'm1', 'high']) })
  else if (r < 0.8) next.tools = pick([[], ['read'], ['read', 'edit'], ['edit', 'read']])
  else if (r < 0.85) next.system = pick([[], ['s'], ['s', 't']])
  return next
}

let compared = 0, differing = 0, appendTrue = 0
for (let i = 0; i < 20000; i += 1) {
  const a = { ...identity(), messages: Array.from({ length: Math.floor(rnd() * 5) }, message) }
  const b = mutate(a)
  for (const [x, y] of [[a, b], [b, a], [a, a]]) {
    const [l, r] = sides.map(s => s.wire.isAppendOnlyPrefix(x, y))
    compared += 1
    if (l) appendTrue += 1
    if (l !== r) { differing += 1; if (differing < 5) console.log('wire diff', JSON.stringify({ x, y, base: l, cand: r })) }
  }
}
const runs = ['r1', 'r2', 'r3', 'r4', 'r5']
for (let i = 0; i < 5000; i += 1) {
  const observed = Array.from({ length: Math.floor(rnd() * 5) }, () => pick(runs))
  const handled = new Set(runs.filter(() => rnd() < 0.4))
  const [l, r] = sides.map(s => s.policy.nextReanchor(observed, run => handled.has(run)))
  compared += 1
  if (l !== r) { differing += 1; if (differing < 5) console.log('reanchor diff', JSON.stringify({ observed, handled: [...handled], base: l, cand: r })) }
}
console.log(`compared ${compared}; differing ${differing}; wire append-true ${appendTrue}`)
process.exit(differing === 0 ? 0 : 1)
