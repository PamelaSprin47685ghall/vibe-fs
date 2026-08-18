#!/usr/bin/env node
// requirement-trace.mjs — REQUIREMENT-SYSTEM-018 closure gate (TASK.md trace roadmap).
//
// Modes:
//   node scripts/checks/requirement-trace.mjs              strict: all findings fail
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

import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

import { buildTraceGraph, packageOf, scanTestSource } from '../lib/requirement-trace.mjs'

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
const strictFilter = argValue('--strict')
const strictPackages = (packageFilter ?? strictFilter)?.split(',').filter(Boolean) ?? null

const inScope = (fileOrPackage) => {
  if (!strictPackages) return true
  const packageName = typeof fileOrPackage === 'string' && fileOrPackage.includes('/') ? packageOf(fileOrPackage) : fileOrPackage
  return strictPackages.includes(packageName)
}

const rel = (p) => relative(ROOT, p).replace(/\\/g, '/')

const graph = buildTraceGraph(REQUIREMENTS)

const print = (msg) => console.log(msg)

if (explain) {
  const separator = explain.lastIndexOf(':')
  const filePart = separator >= 0 ? explain.slice(0, separator) : explain
  const linePart = separator >= 0 ? explain.slice(separator + 1) : ''
  const file = resolve(ROOT, filePart)
  const targetLine = Number(linePart)
  const hits = scanTestSource(file).filter((t) => t.line === targetLine)
  const test = hits[0]
  if (!test) {
    print(`test\n  ${rel(file)}:${targetLine}\n\nnot a test call site`)
    process.exit(1)
  }
  print(`test\n  ${rel(file)}:${test.line}\n  ${test.title ?? '(dynamic or missing title)'}\n  state: ${test.state}`)
  if (test.whatIds.length === 0) {
    print('\nproves\n  (no WHAT[<ID>] owner declared)')
    process.exit(1)
  }
  let failed = false
  for (const id of test.whatIds) {
    const w = graph.whats.get(id)
    if (!w) {
      print(`\nproves\n  WHAT[${id}]  — UNKNOWN proposition`)
      failed = true
      continue
    }
    print(`\nproves\n  WHAT[${id}]\n\nnormative source\n  ${rel(w.file)}:${w.line}\n  ${w.heading}`)
    const edges = graph.proofEdges.filter((edge) => edge.file === test.file && edge.line === test.line && edge.whatId === id)
    if (edges.length === 0) {
      print(`\nproof edges\n  (none — HOW.md has no exact anchor for this test)`)
      failed = true
    } else {
      print('\nproof edges')
      for (const edge of edges) {
        print(`  ${rel(edge.proofFile)}:${edge.proofLine} → ${rel(edge.file)}:${edge.line} ${edge.title ?? ''}${edge.reason ? ` [${edge.reason}]` : ''}`)
        if (edge.reason || edge.state !== 'active') failed = true
      }
    }
  }
  process.exit(failed ? 1 : 0)
}

// ── findings ─────────────────────────────────────────────────────────────────

const failures = []
const add = (file, line, code, msg) => failures.push({ file, line, code, msg })

// Every actual call site is a proof declaration, including skip/todo calls.
// Their state changes whether they can prove a WHAT, not whether they need an
// owner tag.
for (const test of graph.tests) {
  if (!inScope(test.file)) continue
  if (test.whatIds.length === 0) {
    add(rel(test.file), test.line, 'TRACE_ORPHAN_TEST', `"${test.title ?? '(missing title)'}" has no WHAT[<ID>] owner`)
  }
}

// unknown WHAT
for (const id of graph.unknownWhat) {
  const refs = graph.tests.filter((test) => inScope(test.file) && test.whatIds.includes(id))
  for (const test of refs) {
    add(rel(test.file), test.line, 'TRACE_UNKNOWN_WHAT', `references WHAT[${id}], but that proposition does not exist`)
  }
}

// A title carrying two tags is ambiguous even when both tags happen to be the
// same ID. Duplicate declarations are not a second proof edge.
for (const { test, whats } of graph.multiPrimary) {
  if (!inScope(test.file)) continue
  add(rel(test.file), test.line, 'TRACE_MULTI_PRIMARY', `declares more than one primary WHAT: ${whats.join(', ')}`)
}

// unproved WHAT: only one current, active executable declaration proves it.
for (const what of graph.unproved) {
  if (what.deleted || !inScope(what.package)) continue
  add(rel(what.file), what.line, 'TRACE_UNPROVED_WHAT', `${what.id} has zero active executable tests`)
}

for (const what of graph.proofMissing) {
  if (what.deleted || !inScope(what.package)) continue
  add(rel(what.file), what.line, 'TRACE_PROOF_MISSING', `${what.id} has no HOW.md row in ${what.package}`)
}

// An explicit PROOF anchor is an executable edge, not a file existence claim.
// A stale, ambiguous, state-ineligible, or WHAT-mismatched anchor is a hard
// failure. Bare file references remain structural evidence for meta-verifier;
// they do not create an invented test edge.
for (const edge of graph.danglingProof) {
  const packageName = edge.whatId ? graph.whats.get(edge.whatId)?.package : packageOf(edge.file)
  if (!inScope(packageName)) continue
  add(
    rel(edge.proofFile),
    edge.proofLine,
    'TRACE_DANGLING_PROOF',
    `${edge.whatId ? `${edge.whatId} ` : ''}PROOF anchor ${edge.anchor ?? '(unnamed)'} does not resolve to an active matching test (${edge.reason})`,
  )
}

// A PROOF row that mentions a law ID but references no executable .test.mjs
// path is prose, not proof. It must carry at least one test path with a
// matching WHAT-tagged anchor to count as executable evidence.
for (const row of graph.proseOnlyProof) {
  const packageName = row.proofFile.split('/').slice(-2)[0]
  if (!inScope(packageName)) continue
  add(
    rel(row.proofFile),
    row.proofLine,
    'TRACE_PROSE_ONLY_PROOF',
    `${row.whatIds.join(', ')} PROOF row has no executable .test.mjs anchor: ${row.rowText.slice(0, 80)}`,
  )
}

// ── output ───────────────────────────────────────────────────────────────────

const sorted = [...failures].sort((a, b) => a.file.localeCompare(b.file) || a.line - b.line)

if (report) {
  const perPackage = new Map()
  for (const test of graph.tests) {
    const pkg = packageOf(test.file)
    if (!inScope(pkg)) continue
    if (!perPackage.has(pkg)) perPackage.set(pkg, { active: 0, skipped: 0, todo: 0, tagged: 0, orphan: 0, unproved: 0 })
    const row = perPackage.get(pkg)
    if (test.state === 'active') row.active++
    else if (test.state === 'skip') row.skipped++
    else row.todo++
    if (test.whatIds.length === 1) row.tagged++
    if (test.whatIds.length === 0) row.orphan++
  }
  for (const what of graph.unproved) {
    if (what.deleted || !inScope(what.package)) continue
    const row = perPackage.get(what.package) ?? { active: 0, skipped: 0, todo: 0, tagged: 0, orphan: 0, unproved: 0 }
    row.unproved++
    perPackage.set(what.package, row)
  }
  print('package                   WHAT   active tests   tagged   skipped   todo   orphan calls   unproved WHAT')
  for (const [pkg, row] of [...perPackage.entries()].sort()) {
    const whats = [...graph.whats.values()].filter((what) => what.package === pkg && !what.deleted).length
    print(
      `${pkg.padEnd(25)} ${String(whats).padStart(4)} ${String(row.active).padStart(12)} ${String(row.tagged).padStart(7)} ${String(row.skipped).padStart(9)} ${String(row.todo).padStart(6)} ${String(row.orphan).padStart(14)} ${String(row.unproved).padStart(13)}`,
    )
  }
  print('')
  print(`totals: ${graph.whats.size} WHAT / ${graph.tests.length} test calls / ${graph.proofEdges.length} exact proof edges / ${sorted.length} findings`)
  for (const failure of sorted) print(`  ${failure.code} ${failure.file}:${failure.line} ${failure.msg}`)
  process.exit(0)
}

if (sorted.length === 0) {
  print(`requirement-trace: OK — ${graph.whats.size} WHAT, ${graph.tests.length} tests, closure complete`)
  process.exit(0)
}
print(`requirement-trace: ${sorted.length} finding(s)`)
for (const f of sorted) print(`  ${f.code} ${f.file}:${f.line} ${f.msg}`)
process.exit(1)
