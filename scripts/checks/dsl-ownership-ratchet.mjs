#!/usr/bin/env node
// Per-file DSL ownership ratchet (M0). Freezes per-file/per-gate violation counts.
//
// Modes:
//   node scripts/checks/dsl-ownership-ratchet.mjs \
//     --baseline=<json-string|path> [--root=<dir>]
//       exit 1 when any file/gate exceeds its baseline (0 when absent)
//   node scripts/checks/dsl-ownership-ratchet.mjs --generate [--out=<file>] [--root=<dir>]
//       write a baseline of current violation counts (violating files only)
//
// Baseline keys are POSIX-normalized paths relative to --root (default src/Wanxiangshu).
// Reuses gate definitions and scanners from dsl-ownership.mjs.

import { readFileSync, writeFileSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'
import { PRODUCTION_ROOT, scanFiles } from './dsl-ownership.mjs'

const norm = (p) => p.replace(/\\/g, '/')

export const DEFAULT_OUT = join(
  dirname(fileURLToPath(import.meta.url)),
  'dsl-ownership-ratchet-baseline.json',
)

/** Parse --baseline: inline JSON first, then treat the value as a JSON file path. */
export const parseBaseline = (arg) => {
  try {
    return JSON.parse(arg)
  } catch {
    // Not inline JSON — read it as a file path.
  }
  return JSON.parse(readFileSync(arg, 'utf8'))
}

/** Count violations per file per gate. Returns Map<file, Map<gate, count>>. */
export const countByFileGate = (violations) => {
  const counts = new Map()
  for (const v of violations) {
    let gates = counts.get(v.file)
    if (!gates) {
      gates = new Map()
      counts.set(v.file, gates)
    }
    gates.set(v.gate, (gates.get(v.gate) ?? 0) + 1)
  }
  return counts
}

const scanRoot = (root) => {
  const files = walk(root, ['.fs'])
  // scanFiles path predicates (isMutableDeclarationAllowed, isProcessPhysicalPath,
  // isProcessCommandPath) match '/Process/' etc., so entry.file must carry the
  // src/Wanxiangshu/ prefix used by dsl-ownership.mjs. Baseline keys stay
  // root-relative: remap violation.file after scanning.
  const prefix = `${PRODUCTION_ROOT}/`
  const entries = files
    .map((abs) => {
      const rel = norm(relative(root, abs))
      return {
        rel,
        file: norm(`${PRODUCTION_ROOT}/${rel}`),
        text: readFileSync(abs, 'utf8'),
      }
    })
  const violations = scanFiles(entries).map((v) => {
    const n = norm(v.file)
    return { ...v, file: n.startsWith(prefix) ? n.slice(prefix.length) : n }
  })
  return countByFileGate(violations)
}

const runCli = () => {
  const argv = process.argv.slice(2)
  const value = (name) => {
    const hit = argv.find((arg) => arg.startsWith(`--${name}=`))
    return hit ? hit.slice(`--${name}=`.length) : undefined
  }
  const root = value('root') ?? PRODUCTION_ROOT
  const counts = scanRoot(root)

  if (argv.includes('--generate')) {
    const outPath = value('out') ?? DEFAULT_OUT
    const payload = {}
    for (const [file, gates] of counts) payload[file] = Object.fromEntries(gates)
    writeFileSync(outPath, JSON.stringify(payload, null, 2) + '\n')
    console.log(`dsl-ownership-ratchet: baseline written to ${outPath}`)
    process.exit(0)
  }

  const baselineArg = value('baseline')
  if (baselineArg === undefined) {
    console.error('dsl-ownership-ratchet: --baseline=<json-string|path> is required')
    process.exit(1)
  }

  let baseline
  try {
    baseline = parseBaseline(baselineArg)
  } catch {
    console.error(`dsl-ownership-ratchet: baseline missing or unreadable: ${baselineArg}`)
    console.error('Generate it first:')
    console.error('  node scripts/checks/dsl-ownership-ratchet.mjs --generate')
    process.exit(1)
  }

  const failures = []
  for (const [file, gates] of counts) {
    const fileBaseline = baseline[file] ?? {}
    for (const [gate, actual] of gates) {
      const old = fileBaseline[gate] ?? 0
      if (actual > old) failures.push(`${file} ${gate} ${old} -> ${actual}`)
    }
  }

  if (failures.length > 0) {
    for (const line of failures) console.error(line)
    console.error(
      `dsl-ownership-ratchet: ${failures.length} file/gate regression(s) against baseline`,
    )
    process.exit(1)
  }
  console.log('dsl-ownership-ratchet: OK — no regression against baseline')
  process.exit(0)
}

const isMain =
  process.argv[1] !== undefined &&
  resolve(fileURLToPath(import.meta.url)) === resolve(process.argv[1])

if (isMain) runCli()
