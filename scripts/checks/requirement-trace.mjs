#!/usr/bin/env node
// requirement-trace.mjs — REQUIREMENT-SYSTEM-018 closure gate (TASK.md trace roadmap).
//
// Modes:
//   node scripts/checks/requirement-trace.mjs              strict: all six counts zero
//   node scripts/checks/requirement-trace.mjs --report     report-only inventory table
//   node scripts/checks/requirement-trace.mjs --package=<pkg>   trace one package
//   node scripts/checks/requirement-trace.mjs --explain=<file:line>  explain one test
//   node scripts/checks/requirement-trace.mjs --strict=<pkg>[,<pkg>]  hard mode per package
//
// Exit codes: 0 clean; 1 findings (report mode also exits 0, prints the table).
//
// Machine contract: a test declares exactly one primary WHAT via `WHAT[<ID>]`
// in its title. Historic IDs, path-implicit ownership and comment prose do not
// count. skip/todo may carry a tag but never prove a WHAT.

import { readFileSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

import { buildTraceGraph, packageOf, scanTestSource } from '../lib/requirement-trace.mjs'
import { walk } from '../lib/walk.mjs'

const walkProofFiles = (root) => walk(root, ['.md']).filter((f) => f.endsWith('/PROOF.md'))

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..')
const REQUIREMENTS = join(ROOT, 'requirements')

const args = process.argv.slice(2)
const argValue = (flag) => {
  const inline = args.find((a) => a.startsWith(`${flag}=`))
  if (inline) return inline.slice(flag.length + 1)
  const index = args.indexOf(flag)
  return index >= 0 ? args[index + 1] : undefined
}

const report = args.includes('--report')
const explain = argValue('--explain')
const packageFilter = argValue('--package')

const rel = (p) => relative(ROOT, p).replace(/\\/g, '/')

const graph = buildTraceGraph(REQUIREMENTS)

const print = (msg) => console.log(msg)
const byPackage = (nodes) => {
  const out = new Map()
  for (const n of nodes) {
    const pkg = packageOf(n.file)
    if (packageFilter && pkg !== packageFilter) continue
    if (!out.has(pkg)) out.set(pkg, [])
    out.get(pkg).push(n)
  }
  return out
}

if (explain) {
  const [filePart, linePart] = explain.split(':')
  const file = resolve(ROOT, filePart)
  const targetLine = Number(linePart)
  const hits = scanTestSource(file).filter((t) => t.line === targetLine)
  const test = hits[0]
  if (!test) {
    print(`test\n  ${rel(file)}:${targetLine}\n\nnot a test call site`)
    process.exit(1)
  }
  print(`test\n  ${rel(file)}:${test.line}\n  ${test.title}`)
  if (test.whatIds.length === 0) {
    print('\nproves\n  (no WHAT[<ID>] owner declared)')
    process.exit(1)
  }
  for (const id of test.whatIds) {
    const w = graph.whats.get(id)
    if (!w) {
      print(`\nproves\n  WHAT[${id}]  — UNKNOWN proposition`)
      process.exit(1)
    }
    print(`\nproves\n  WHAT[${id}]\n\nnormative source\n  ${rel(w.file)}:${w.line}\n  ${w.heading}`)
    print(`\nproof index\n  ${rel(join(dirname(w.file), 'PROOF.md'))}`)
  }
  process.exit(0)
}

// ── findings ─────────────────────────────────────────────────────────────────

const failures = []
const add = (file, line, code, msg) => failures.push({ file, line, code, msg })

const activeTests = graph.tests.filter((t) => t.state === 'active')

// orphan: active test with no WHAT tag
for (const t of graph.tests) {
  if (packageFilter && packageOf(t.file) !== packageFilter) continue
  if (t.whatIds.length === 0 && t.state === 'active') {
    add(rel(t.file), t.line, 'TRACE_ORPHAN_TEST', `"${t.title}" has no WHAT[<ID>] owner`)
  }
}

// unknown WHAT
for (const id of graph.unknownWhat) {
  const refs = graph.tests.filter((t) => t.whatIds.includes(id))
  for (const t of refs) {
    add(rel(t.file), t.line, 'TRACE_UNKNOWN_WHAT', `references WHAT[${id}], but that proposition does not exist`)
  }
}

// multi primary
for (const { test: t, whats } of graph.multiPrimary) {
  add(rel(t.file), t.line, 'TRACE_MULTI_PRIMARY', `declares more than one primary WHAT: ${whats.join(', ')}`)
}

// unproved WHAT
for (const w of graph.unproved) {
  if (w.deleted) continue
  if (packageFilter && w.package !== packageFilter) continue
  add(rel(w.file), w.line, 'TRACE_UNPROVED_WHAT', `${w.id} has zero active executable tests`)
}

// PROOF exact-anchor closure: the PROOF row naming a file must name a live test.
// (structural — meta-verifier already owns the WHAT→file existence direction)
// ID matching mirrors meta-verifier: full ID in cell 1 or 2 (tokenized for
// `A-002/003` merges), bare number in cell 1 only (`| 011 |`).
const proofIds = new Set()
for (const file of walkProofFiles(REQUIREMENTS)) {
  const text = readFileSync(file, 'utf8')
  const pkg = file.split('/').slice(-2)[0]
  // Bare numbers in cell 1 refer to propositions of this package; resolve
  // them against the package's own WHAT.md ids so `| 011 |` and `| 006/007 |`
  // match WORK-RECORD-011 / COGNITIVE-ENVIRONMENT-006/007. Cell 2 carries
  // full IDs plus merged tails (`DISPATCH-PROTOCOL-002/003/004`).
  const pkgIds = [...readFileSync(join(dirname(file), 'WHAT.md'), 'utf8').matchAll(/^#{1,6}\s+([A-Z][A-Z0-9-]*-\d{3})\b/gm)].map((m) => m[1])
  const byTail = new Map()
  for (const id of pkgIds) byTail.set(id.slice(-3), id)
  for (const line of text.split('\n')) {
    if (!line.startsWith('|')) continue
    const cells = line.split('|')
    // Strip code spans so `CONTEXT-COMPRESSION-001（…）` and `` `KNOWLEDGE-REUSE-001` `` parse.
    const cell1 = (cells[1] ?? '').replace(/`/g, '')
    const cell2 = (cells[2] ?? '').replace(/`/g, '')
    for (const t of cell1.split(/[\s,、/–—]+/).filter(Boolean)) {
      const full = /^([A-Z][A-Z0-9-]*-\d{3})/.exec(t)
      if (full) proofIds.add(`${pkg}:${full[1]}`)
      else if (/^\d{3}$/.test(t) && byTail.has(t)) proofIds.add(`${pkg}:${byTail.get(t)}`)
    }
    for (const m of cell2.matchAll(/\b([A-Z][A-Z0-9-]*-\d{3})((?:\/\d{3})+)?\b/g)) {
      proofIds.add(`${pkg}:${m[1]}`)
      for (const tail of (m[2] ?? '').split('/').filter(Boolean)) {
        const id = byTail.get(tail)
        if (id) proofIds.add(`${pkg}:${id}`)
      }
    }
  }
}
for (const w of graph.whats.values()) {
  if (w.deleted) continue
  if (!proofIds.has(`${w.package}:${w.id}`)) {
    if (packageFilter && w.package !== packageFilter) continue
    add(rel(w.file), w.line, 'TRACE_PROOF_MISSING', `${w.id} has no PROOF.md row in ${w.package}`)
  }
}

// ── output ───────────────────────────────────────────────────────────────────

const sorted = [...failures].sort((a, b) => a.file.localeCompare(b.file) || a.line - b.line)

if (report) {
  const perPackage = new Map()
  for (const t of graph.tests) {
    const pkg = packageOf(t.file)
    if (!perPackage.has(pkg)) perPackage.set(pkg, { active: 0, tagged: 0, orphan: 0, unproved: 0 })
    const row = perPackage.get(pkg)
    row.active++
    if (t.whatIds.length > 0) row.tagged++
    if (t.whatIds.length === 0 && t.state === 'active') row.orphan++
  }
  for (const w of graph.unproved) {
    if (w.deleted) continue
    const row = perPackage.get(w.package) ?? { active: 0, tagged: 0, orphan: 0, unproved: 0 }
    row.unproved++
    perPackage.set(w.package, row)
  }
  print('package                   WHAT   active tests   tagged   orphan tests   unproved WHAT')
  for (const [pkg, row] of [...perPackage.entries()].sort()) {
    const whats = [...graph.whats.values()].filter((w) => w.package === pkg).length
    print(
      `${pkg.padEnd(25)} ${String(whats).padStart(4)} ${String(row.active).padStart(12)} ${String(row.tagged).padStart(7)} ${String(row.orphan).padStart(13)} ${String(row.unproved).padStart(13)}`,
    )
  }
  print('')
  print(`totals: ${graph.whats.size} WHAT / ${graph.tests.length} tests / ${sorted.length} findings`)
  for (const f of sorted) print(`  ${f.code} ${f.file}:${f.line} ${f.msg}`)
  process.exit(sorted.length === 0 ? 0 : 0)
}

if (sorted.length === 0) {
  print(`requirement-trace: OK — ${graph.whats.size} WHAT, ${graph.tests.length} tests, closure complete`)
  process.exit(0)
}
print(`requirement-trace: ${sorted.length} finding(s)`)
for (const f of sorted) print(`  ${f.code} ${f.file}:${f.line} ${f.msg}`)
process.exit(1)
