// CUT-27 ② golden vectors: drive the host's own computeMaintenanceDiff (a built dist) over fixed-seed
// random B/T file pairs in temp workspaces (never the real .git/wanxiangshu) and record
// [path, before|null, after|null, diffSummary]. null = Missing. Used as wanxiangli casebook-maintenance fixtures.
// usage: node golden-diffs.mjs <dist> <out.json> [count]
import { mkdtempSync, rmSync, writeFileSync, unlinkSync, mkdirSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'

const [dist, out, countArg] = process.argv.slice(2)
const store = await import(join(resolve(dist), 'Persistence/EventStore/Surface.js'))
const casebook = await import(join(resolve(dist), 'Repository/Knowledge/Casebook/Surface.js'))
let seed = 2702
const random = () => { seed = (seed * 1103515245 + 12345) % 2147483648; return seed / 2147483648 }
const pick = list => list[Math.floor(random() * list.length)]
const vocabulary = ['a\n', 'b\n', 'c\n', 'd\n', 'shared line\n', '\n', '😀 emoji\n', '缓存\n', 'x', 'tail-no-lf', '\r\n', 'a', ' \n']
const body = () => Array.from({ length: Math.floor(random() * 14) }, () => pick(vocabulary)).join('')
const mutate = text => {
  const lines = text.split(/(?<=\n)/).filter(Boolean)
  for (let k = Math.floor(random() * 4); k >= 0; k -= 1) {
    const at = Math.floor(random() * (lines.length + 1)); const op = random()
    if (op < 0.35) lines.splice(at, 0, pick(vocabulary)); else if (op < 0.7) lines.splice(at, 1); else lines[at] = pick(vocabulary)
  }
  return lines.join('')
}
const state = () => { const r = random(); return r < 0.1 ? null : body() }
const vectors = []
const count = Number(countArg ?? 400)
for (let i = 0; i < count; i += 1) {
  const dir = mkdtempSync(join(tmpdir(), 'cut27-golden-'))
  const handle = store.create(join(dir, '.store'), 'golden')
  try {
    const paths = random() < 0.2 ? ['b.txt', 'a.txt', 'sub/c.txt'].slice(0, 1 + Math.floor(random() * 3)) : ['subject.txt']
    mkdirSync(join(dir, 'sub'), { recursive: true })
    const pairs = paths.map(path => { const before = state(); const after = random() < 0.15 ? before : random() < 0.1 ? null : mutate(before ?? body()); return { path, before, after } })
    for (const { path, before } of pairs) if (before !== null) writeFileSync(join(dir, path), before)
    const baseline = await casebook.freezeCompletionState(handle, dir, paths)
    if (typeof baseline !== 'string') throw new Error('freeze failed: ' + JSON.stringify(baseline))
    for (const { path, before, after } of pairs) {
      if (after === null) { if (before !== null) unlinkSync(join(dir, path)) } else writeFileSync(join(dir, path), after)
    }
    const captured = await casebook.computeMaintenanceDiff(handle, dir, paths, baseline)
    if (typeof captured.diffSummary !== 'string') throw new Error('capture failed: ' + JSON.stringify(captured))
    vectors.push({ files: pairs.sort((l, r) => (l.path < r.path ? -1 : l.path > r.path ? 1 : 0)).map(p => [p.path, p.before, p.after]), diffSummary: captured.diffSummary })
  } finally { store.dispose(handle); rmSync(dir, { recursive: true, force: true }) }
}
writeFileSync(out, JSON.stringify(vectors) + '\n')
console.log(JSON.stringify({ vectors: vectors.length, withDiff: vectors.filter(v => v.diffSummary !== '').length, multiFile: vectors.filter(v => v.files.length > 1).length }))
