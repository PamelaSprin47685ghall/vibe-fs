// CUT-16 differential: old (inline) vs new (wanxiangli/compression) ANCHOR view surface.
// Usage: node differential.mjs <dist Anchor dir> <base js dir> <candidate js dir> [seed] [rounds]
// Copies each build's Anchor*.js next to the real dist as *.base.js / *.cand.js (internal
// imports rewritten) so both variants share fable-library and node_modules, then drives the
// public plain-JS surface (applyView / captureView / anchors / parse / reportMaterial) with the
// same random sessions and compares every answer byte-for-byte as JSON.
import fs from 'node:fs'
import path from 'node:path'
import { pathToFileURL } from 'node:url'

const [dir, baseDir, candDir, seedArg = '20261020', roundsArg = '2000'] = process.argv.slice(2)
const files = ['AnchorLedger', 'AnchorModel', 'AnchorSurface', 'AnchorView']
function install(src, tag) {
  for (const name of files) {
    let text = fs.readFileSync(path.join(src, name + '.js'), 'utf8')
    for (const other of files) text = text.split(`"./${other}.js"`).join(`"./${other}.${tag}.js"`)
    fs.writeFileSync(path.join(dir, `${name}.${tag}.js`), text)
  }
}
install(baseDir, 'base'); install(candDir, 'cand')
const A = await import(pathToFileURL(path.join(dir, 'AnchorSurface.base.js')))
const B = await import(pathToFileURL(path.join(dir, 'AnchorSurface.cand.js')))

let s = Number(seedArg) >>> 0
const rnd = () => ((s = (s * 1664525 + 1013904223) >>> 0) / 2 ** 32)
const int = n => Math.floor(rnd() * n)
const pick = xs => xs[int(xs.length)]
const words = ['alpha', 'beta', '读取文件', 'tool ok', 'x'.repeat(40), '', 'fix the bug in src/a.ts', '总结：完成']
const text = () => Array.from({ length: 1 + int(4) }, () => pick(words)).join(' ')

function result(r) { return r && typeof r === 'object' && 'tag' in r ? { tag: r.tag, fields: r.fields } : r }
function view(out) {
  return JSON.stringify(out, (k, v) => (k === 'message' && v && typeof v === 'object') ? { id: v.id, text: v.text } : v)
}
function both(f) {
  let a, b
  try { a = view(result(f(A))) } catch (e) { a = 'THROW ' + e.message }
  try { b = view(result(f(B))) } catch (e) { b = 'THROW ' + e.message }
  return [a, b]
}
function summary() { return pick([null, null, text(), text(), '  ', 7, '', undefined]) }
function declaration(live, top) {
  const anchors = () => pick([...live, int(top + 3), -1, 0])
  const r = rnd()
  if (r < 0.04) return pick([null, undefined, 3, 'view', [], { prefixUntil: 0 }, { thenMap: {} }])
  if (r < 0.07) return { prefixUntil: 0, thenMap: {}, extra: 1 }
  if (r < 0.09) return { prefixUntil: pick([-1, 1.5, '2', null]), thenMap: {} }
  if (r < 0.11) return { prefixUntil: 0, thenMap: pick([[], null, 'x']) }
  const thenMap = {}
  for (let i = int(5); i > 0; i--) thenMap[String(anchors())] = summary()
  if (rnd() < 0.03) thenMap['01'] = null
  if (rnd() < 0.03) thenMap['abc'] = 'x'
  return { prefixUntil: pick([0, 0, ...live, int(top + 2)]), thenMap }
}

const rounds = Number(roundsArg)
let compared = 0, mismatches = 0, failures = 0, summarizedRows = 0
for (let round = 0; round < rounds; round++) {
  A.clearAll(); B.clearAll()
  const sessions = ['s1', 's2']
  const history = { s1: [], s2: [] }
  let next = 0
  for (let turn = 0; turn < 12; turn++) {
    const sid = pick(sessions)
    for (let i = int(4); i > 0; i--) history[sid].push(rnd() < 0.8 ? { id: 'm' + next++, text: text() } : { text: text() })
    const msgs = history[sid].map(m => ({ ...m }))
    const checks = [
      () => both(M => M.applyView(sid, msgs)),
      () => both(M => M.anchors(sid)),
    ]
    for (const c of checks) {
      const [a, b] = c(); compared++
      if (a.includes(`"kind":"summarized"`)) summarizedRows++
      if (a !== b) { mismatches++; if (mismatches < 5) console.error('MISMATCH', round, turn, a.slice(0, 300), '\n   vs', b.slice(0, 300)) }
    }
    const live = JSON.parse(both(M => M.anchors(sid))[0]).map(x => x.anchor)
    const decl = declaration(live, next)
    for (const f of [M => M.captureView(sid, decl), M => M.parse(decl), M => M.reportMaterial(sid, decl?.thenMap ?? decl)]) {
      const [a, b] = both(f); compared++
      if (a.includes('"ok":false') || a.includes('"tag":1')) failures++
      if (a !== b) { mismatches++; if (mismatches < 5) console.error('MISMATCH', round, turn, JSON.stringify(decl), a.slice(0, 300), '\n   vs', b.slice(0, 300)) }
    }
  }
}
for (const name of files) for (const tag of ['base', 'cand']) fs.rmSync(path.join(dir, `${name}.${tag}.js`))
console.log(`seed ${seedArg}: ${JSON.stringify({ rounds, compared, rejectionsSeen: failures, viewsWithSummaries: summarizedRows, mismatches })}`)
process.exit(mismatches ? 1 : 0)
