// CUT-27 ① shadow check: the old hand-written index projection (base dist) and wanxiangli/casebook
// (candidate dist) compute the same shelfmarks, LRU victims, snapshots, epochs and resolutions.
// Each side drives its own temp EventStore fixture with the same operations; no real .git/wanxiangshu.
// Known, intended difference: titles whose 71-unit cut splits a surrogate pair (base emits a lone
// surrogate); those are tallied separately and excluded from the equality count.
// usage: node shadow-index.mjs <base-dist> <candidate-dist> [rounds]
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'

const [baseDist, candDist, roundsArg] = process.argv.slice(2)
const load = async dist => ({
  store: await import(join(resolve(dist), 'Persistence/EventStore/Surface.js')),
  casebook: await import(join(resolve(dist), 'Repository/Knowledge/Casebook/Surface.js')),
  index: await import(join(resolve(dist), 'Repository/Knowledge/Casebook/IndexSurface.js')),
})
const base = await load(baseDist)
const cand = await load(candDist)
let seed = 2727
const random = () => { seed = (seed * 1103515245 + 12345) % 2147483648; return seed / 2147483648 }
const pick = list => list[Math.floor(random() * list.length)]
const pieces = ['Why', ' cache', '\n', '## ', '- ', '缓存', '😀', '\t', '  ', 'restart?', 'x'.repeat(30), 'é', '\r\n']
const text = () => Array.from({ length: Math.floor(random() * 8) }, () => pick(pieces)).join('')
const ids = Array.from({ length: 12 }, (_, i) => pick(['ses_', 'private-', '会话-', 'ses_🙂', '']) + i)
const tally = { rounds: Number(roundsArg ?? 300), shelfmarkEqual: 0, shelfmarkSplitPair: 0, stepsWithSplitPair: 0, evictEqual: 0, stepsEqual: 0, steps: 0, differ: 0, examples: [] }
const note = example => { tally.differ += 1; if (tally.examples.length < 5) tally.examples.push(example) }

for (let round = 0; round < tally.rounds; round += 1) {
  for (let k = 0; k < 20; k += 1) {
    const sessionId = pick(ids)
    const question = random() < 0.2 ? 'a'.repeat(60 + Math.floor(random() * 15)) + '😀😀 tail' : text()
    const left = base.index.shelfmarkFor(sessionId, question)
    const right = cand.index.shelfmarkFor(sessionId, question)
    if (left === right) tally.shelfmarkEqual += 1
    else if (!left.isWellFormed() && right.isWellFormed()) tally.shelfmarkSplitPair += 1
    else note({ kind: 'shelfmark', sessionId, question, left, right })
  }
  const cases = Array.from({ length: Math.floor(random() * 8) }, (_, i) => ({ sessionId: `e${i}`, q: 'Q', a: 'A', observations: [], lastAccessOrder: Math.floor(random() * 4) }))
  const capacity = Math.floor(random() * 9) - 1
  const ev = side => { const r = side.casebook.evict(capacity, cases); return { kept: r.kept.map(c => c.sessionId), victims: [...r.victims] } }
  JSON.stringify(ev(base)) === JSON.stringify(ev(cand)) ? (tally.evictEqual += 1) : note({ kind: 'evict', capacity, cases, base: ev(base), cand: ev(cand) })

  const sides = [base, cand].map(side => {
    const dir = mkdtempSync(join(tmpdir(), 'cut27-shadow-'))
    return { side, dir, handle: side.store.create(dir, 'shadow') }
  })
  const capacityIdx = 1 + Math.floor(random() * 5)
  for (let step = 0; step < 6; step += 1) {
    const op = random()
    const record = { sessionId: pick(ids), q: random() < 0.15 ? 'a'.repeat(70) + '😀 x' : text() || 'Q', a: 'A', observations: [], lastAccessOrder: 0 }
    const outputs = []
    for (const { side, handle } of sides) {
      if (op < 0.5) await side.casebook.archive(handle, record)
      else if (op < 0.65) side.index.invalidate()
      const snap = await side.index.refresh(handle, capacityIdx)
      const resolved = []
      for (const entry of snap.cases) resolved.push((await side.index.resolve(handle, capacityIdx, entry.shelfmark)).value?.sessionId ?? null)
      outputs.push({ epoch: String(snap.epoch), cases: snap.cases.map(e => [e.shelfmark, e.question]), resolved })
    }
    tally.steps += 1
    const [l, r] = outputs
    const splitPair = l.cases.some(([s]) => !s.isWellFormed())
    if (JSON.stringify(l) === JSON.stringify(r)) tally.stepsEqual += 1
    else if (splitPair) tally.stepsWithSplitPair += 1
    else note({ kind: 'step', round, step, base: l, cand: r })
  }
  for (const { side, handle, dir } of sides) { side.store.dispose(handle); rmSync(dir, { recursive: true, force: true }) }
}
console.log(JSON.stringify(tally, null, 2))
process.exitCode = tally.differ === 0 ? 0 : 1
