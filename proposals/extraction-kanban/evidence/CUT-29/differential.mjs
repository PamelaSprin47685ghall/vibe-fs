// CUT-29 ① differential: the same seeded inputs through a base dist (hand-written warm-start
// law) and a candidate dist (wanxiangli/warm-start). Usage:
//   node differential.mjs <dist-root> <rounds> <seed>   → prints one sha256 per case family + JSONL digest
// Compare two runs' outputs line by line. Searches are fed in production shape (ordinal order,
// as RepositoryWarmStart produces them); unsorted direct calls are reported separately.
import { createHash } from 'node:crypto'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

const [distRoot, roundsArg = '2000', seedArg = '29'] = process.argv.slice(2)
const ws = await import(join(distRoot, 'Repository/Investigation/WarmStartSurface.js'))
const rounds = Number(roundsArg)
let state = Number(seedArg) >>> 0
const rnd = () => { state = (state + 0x6D2B79F5) >>> 0; let t = state; t = Math.imul(t ^ (t >>> 15), t | 1); t ^= t + Math.imul(t ^ (t >>> 7), t | 61); return ((t ^ (t >>> 14)) >>> 0) / 4294967296 }
const pick = (xs) => xs[Math.floor(rnd() * xs.length)]
const int = (n) => Math.floor(rnd() * n)
const words = ['alpha', 'Alpha', 'beta', 'gamma', 'Δelta', '日本', 'x y', '', ' ', '\t', 'alpha ', ' beta']
const seps = ['\n', '\r\n', '\r', '\n\n']
const keywordText = () => { const n = int(14); let s = ''; for (let i = 0; i < n; i++) s += pick(words) + pick(seps); return rnd() < 0.05 ? pick(['', '   ', '\r\n']) : s }
const contentOf = () => { const big = rnd() < 0.15; const n = big ? 2000 + int(9000) : int(80); let s = ''; for (let i = 0; i < n; i++) s += pick(['a', 'b', '\n', '"', '\\', 'é', '😀', ' ']); return s }
const hitPool = () => Array.from({ length: 1 + int(6) }, () => ({ filePath: pick(['a.fs', 'b.fs', 'src/c.ts', 'A.fs']), startLine: 1 + int(3), endLine: 3 + int(3), content: rnd() < 0.5 ? pick(['x', 'y', 'same']) : contentOf(), score: Math.round(rnd() * 1000) / 1000, totalLines: int(40) }))
const searchesOf = (sorted) => {
  const n = int(9); const pool = hitPool()
  const out = Array.from({ length: n }, (_, k) => ({ ordinal: k + 1, query: pick(words), hints: Array.from({ length: int(5) }, (_, i) => ({ ...pick(pool), keywordOrdinal: k + 1, localRank: i + 1 })) }))
  if (!sorted) out.sort(() => rnd() - 0.5)
  return out
}
const instructionsOf = () => Array.from({ length: int(3) }, () => rnd() < 0.03 ? 'H'.repeat(70000) : pick(['Inspect.', 'Read only.', '先核实。']))
const h = () => createHash('sha256')
const fam = { keywords: h(), render: h(), append: h(), prepare: h(), unsorted: h(), unsortedPreSorted: h() }
const n = { keywords: 0, render: 0, append: 0, prepare: 0, unsorted: 0, unsortedPreSorted: 0 }
const cover = { omitted: 0, fallback: 0, trimmedByBudget: 0 }
const dir = mkdtempSync(join(tmpdir(), 'cut29-diff-'))
try {
  for (let r = 0; r < rounds; r++) {
    const raw = keywordText()
    fam.keywords.update(JSON.stringify([raw, ws.normalizeKeywords(raw)]) + '\n'); n.keywords++
    const s1 = searchesOf(true), ins = instructionsOf(), charge = pick(['Do it.', 'C'.repeat(int(200))])
    const out1 = ws.render(ins, charge, s1)
    fam.render.update(out1 + '\n'); n.render++
    if (out1.includes('repository_hint_omitted')) cover.omitted++
    if (ins.some(i => i.length > 65536) && !out1.includes('HHHH')) cover.fallback++
    const m = /repository_hint_omitted = (\d+)/.exec(out1); const total = s1.reduce((a, s) => a + s.hints.length, 0)
    if (m && Number(m[1]) > Math.max(0, total - 24) && total <= 24) cover.trimmedByBudget++
    fam.append.update(ws.appendToProviderPrompt(ins, 'BASE ' + charge, s1) + '\n'); n.append++
    const s2 = searchesOf(false)
    fam.unsorted.update(ws.render(ins, charge, s2) + '\n'); n.unsorted++
    fam.unsortedPreSorted.update(ws.render(ins, charge, [...s2].sort((a, b) => a.ordinal - b.ordinal)) + '\n'); n.unsortedPreSorted++
    if (r % 4 === 0) {
      const pool = hitPool(); const fail = new Set([pick(words)])
      const search = async (q, repo, k) => { if (fail.has(q)) throw new Error('down'); return Array.from({ length: int(7) }, () => pick(pool)) }
      const role = pick(['Engineer', 'DevOps', 'Manager', 'Inspector'])
      const res = await (r % 8 === 0
        ? ws.prepareWithSearch(search, 'diff-session', role, pick([dir, null, '/nonexistent-cut29']), raw, charge)
        : ws.appendToBaseWithSearch(search, 'diff-session', role, pick([dir, null]), raw, 'BASE ' + charge))
      fam.prepare.update(JSON.stringify(res) + '\n'); n.prepare++
    }
  }
} finally { rmSync(dir, { recursive: true, force: true }) }
for (const k of Object.keys(fam)) console.log(`${k} cases=${n[k]} sha256=${fam[k].digest('hex')}`)
console.log(`coverage ${JSON.stringify(cover)}`)
