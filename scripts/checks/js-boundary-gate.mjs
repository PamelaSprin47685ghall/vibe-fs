#!/usr/bin/env node
// JS-semantic-surface boundary ratchet (P2, TASK.md §2): only-shrink debt
// baseline for Fable knowledge in semantic tests.
//
//   node scripts/checks/js-boundary-gate.mjs
//       exit 1 when any semantic test carries NEW debt or an EXISTING file's
//       debt exceeds its baseline; existing debt at/below baseline is tolerated
//   node scripts/checks/js-boundary-gate.mjs --generate [--out=<file>]
//       write the current violation set as the baseline (only to shrink)
//
// Baseline shape: { "<rel-file>": { "<rule>": <count> } }. Rules:
//   deep-dist-import / export-discovery / du-shape / fsharp-type /
//   fable-modules / interop-helper  (see lib/test-surface-scan.mjs)
//
// Contract: baseline can be deleted (file removed or count lowered), never
// added. A NEW rule firing in a NEW file is RED. The final architecture gate
// (P11) is absolute prohibition; this ratchet exists only to make the debt
// monotonic while it is paid down.
//
// Package-local *-contract.mjs adapters are FROZEN (PR 3): the four existing
// product-package support contracts are grandfathered and must only shrink;
// any NEW product-package *-contract.mjs is RED because it would recreate the
// "second quarantine" anti-pattern.

import { readFileSync, writeFileSync, existsSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'
import { scanAll } from '../lib/test-surface-scan.mjs'

const DEFAULT_OUT = join(dirname(fileURLToPath(import.meta.url)), 'js-boundary-baseline.json')
const FROZEN_CONTRACTS = join(dirname(fileURLToPath(import.meta.url)), 'js-boundary-frozen-contracts.json')

const args = process.argv.slice(2)
const argValue = (flag) => {
  const inline = args.find((a) => a.startsWith(`${flag}=`))
  if (inline) return inline.slice(flag.length + 1)
  const index = args.indexOf(flag)
  return index >= 0 ? args[index + 1] : undefined
}

const generate = args.includes('--generate')
const out = argValue('--out') ?? DEFAULT_OUT

/** { file: { rule: count } } from raw scan hits. */
const countsByFile = (all) => {
  const out = {}
  for (const [file, hits] of Object.entries(all)) {
    const byRule = {}
    for (const hit of hits) byRule[hit.rule] = (byRule[hit.rule] ?? 0) + 1
    out[file] = byRule
  }
  return out
}

if (generate) {
  const baseline = countsByFile(scanAll())
  writeFileSync(out, JSON.stringify(baseline, null, 2) + '\n')
  console.log(`js-boundary-gate: baseline written to ${out} (${Object.keys(baseline).length} files)`)
  process.exit(0)
}

if (!existsSync(out)) {
  console.error(`js-boundary-gate: baseline missing — run --generate first: ${out}`)
  process.exit(1)
}

const baseline = JSON.parse(readFileSync(out, 'utf8'))
const actual = countsByFile(scanAll())

// PR 3 freeze: product-package *-contract.mjs adapters must not be added.
const frozenSet = new Set(JSON.parse(readFileSync(FROZEN_CONTRACTS, 'utf8')))
const contracts = walk(join(process.cwd(), 'requirements'), ['-contract.mjs'])
  .map((p) => p.replace(process.cwd() + '/', '').replace(/\\/g, '/'))
  .filter((rel) => rel.includes('/tests/support/') && !rel.startsWith('requirements/verification-system/'))
for (const c of contracts) {
  if (!frozenSet.has(c)) {
    actual[c] = { ...(actual[c] ?? {}), 'frozen-contract-added': (actual[c]?.['frozen-contract-added'] ?? 0) + 1 }
  }
}

const failures = []
const baselineFiles = new Set(Object.keys(baseline))
const actualFiles = new Set(Object.keys(actual))

// New debt in a file the baseline does not know: RED.
for (const file of actualFiles) {
  if (!baselineFiles.has(file)) {
    const total = Object.values(actual[file]).reduce((a, b) => a + b, 0)
    failures.push(`${file}: NEW debt (${total} violating line(s)) — baseline can only shrink`)
    continue
  }
  for (const [rule, count] of Object.entries(actual[file])) {
    const allowed = baseline[file][rule] ?? 0
    if (count > allowed) {
      failures.push(`${file}: ${rule} ${allowed} -> ${count} (baseline can only shrink)`)
    }
  }
}

if (failures.length === 0) {
  const debt = [...actualFiles].reduce((sum, f) => sum + Object.values(actual[f]).reduce((a, b) => a + b, 0), 0)
  const frozen = contracts.filter((c) => frozenSet.has(c)).length
  console.log(`js-boundary-gate: OK — ${debt} debt line(s) across ${actualFiles.size} file(s), at/below baseline; ${frozen} product-package *-contract.mjs frozen`)
  process.exit(0)
}

console.error(`js-boundary-gate: ${failures.length} violation(s)`)
for (const f of failures) console.error(`  ${f}`)
process.exit(1)
