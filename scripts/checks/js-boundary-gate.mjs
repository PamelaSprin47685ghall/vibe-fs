#!/usr/bin/env node
// JS-semantic-surface boundary ratchet (P2): semantic-test debt can only
// shrink. The scan covers every requirements/**/tests/**/*.mjs file; moving
// Fable knowledge into support or fixtures does not change its debt class.
//
//   node scripts/checks/js-boundary-gate.mjs
//       fail on new debt, a per-file increase, or a new package-local
//       *-contract.mjs adapter
//   node scripts/checks/js-boundary-gate.mjs --generate [--out=<file>]
//       write a smaller baseline only after proving it contains no new debt
//
// A missing baseline is a valid terminal state only when the scan is clean.
// The baseline is a migration ledger, never an exemption or a regeneration
// button that can bless newly introduced debt.

import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, join, relative } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
import { walk } from '../lib/walk.mjs'
import { scanAll } from '../lib/test-surface-scan.mjs'

export const DEFAULT_OUT = join(dirname(fileURLToPath(import.meta.url)), 'js-boundary-baseline.json')
export const FROZEN_CONTRACTS = join(dirname(fileURLToPath(import.meta.url)), 'js-boundary-frozen-contracts.json')

const normalize = (path) => path.replace(/\\/g, '/')
const relFrom = (root, path) => normalize(relative(root, path))

/** { file: { rule: count } } from raw scan hits. */
export const countsByFile = (all) => {
  const out = {}
  for (const file of Object.keys(all).sort()) {
    const byRule = {}
    for (const hit of all[file]) byRule[hit.rule] = (byRule[hit.rule] ?? 0) + 1
    out[file] = byRule
  }
  return out
}

/** Parse and validate the migration ledger before it participates in a gate. */
export const validateBaseline = (baseline) => {
  if (baseline === null || typeof baseline !== 'object' || Array.isArray(baseline)) {
    throw new Error('js-boundary-gate: baseline must be an object mapping files to rule counts')
  }
  for (const [file, rules] of Object.entries(baseline)) {
    if (rules === null || typeof rules !== 'object' || Array.isArray(rules)) {
      throw new Error(`js-boundary-gate: baseline entry ${file} must map rules to counts`)
    }
    for (const [rule, count] of Object.entries(rules)) {
      if (!Number.isInteger(count) || count < 0) {
        throw new Error(`js-boundary-gate: baseline count ${file}/${rule} must be a non-negative integer`)
      }
    }
  }
  return baseline
}

/** Find package-local adapters; verification-system is the only quarantine. */
export const packageLocalContracts = (root = process.cwd()) =>
  walk(join(root, 'requirements'), ['-contract.mjs'])
    .map((path) => relFrom(root, path))
    .filter((path) => {
      const parts = path.split('/')
      return (
        parts[0] === 'requirements' &&
        parts[1] !== 'verification-system' &&
        parts[2] === 'tests' &&
        parts.length >= 4
      )
    })

/** Add an explicit freeze violation without granting the file scan authority. */
export const withFrozenContractViolations = (actual, contracts, frozen) => {
  const next = { ...actual }
  for (const file of contracts) {
    if (frozen.has(file)) continue
    next[file] = {
      ...(next[file] ?? {}),
      'frozen-contract-added': (next[file]?.['frozen-contract-added'] ?? 0) + 1,
    }
  }
  return next
}

/** Compare current debt with a baseline; every returned string is a RED fact. */
export const compareBaseline = (baseline, actual) => {
  const failures = []
  const baselineFiles = new Set(Object.keys(baseline))
  for (const file of Object.keys(actual).sort()) {
    if (!baselineFiles.has(file)) {
      const total = Object.values(actual[file]).reduce((sum, count) => sum + count, 0)
      failures.push(`${file}: NEW debt (${total} violating line(s)) — baseline can only shrink`)
      continue
    }
    for (const rule of Object.keys(actual[file]).sort()) {
      const count = actual[file][rule]
      const allowed = baseline[file][rule] ?? 0
      if (count > allowed) failures.push(`${file}: ${rule} ${allowed} -> ${count} (baseline can only shrink)`)
    }
  }
  return failures
}

const totalDebt = (actual) =>
  Object.values(actual).reduce(
    (fileTotal, rules) => fileTotal + Object.values(rules).reduce((sum, count) => sum + count, 0),
    0,
  )

const parseArgs = (args) => {
  const argValue = (flag) => {
    const inline = args.find((arg) => arg.startsWith(`${flag}=`))
    if (inline) return inline.slice(flag.length + 1)
    const index = args.indexOf(flag)
    return index >= 0 ? args[index + 1] : undefined
  }
  return { generate: args.includes('--generate'), out: argValue('--out') ?? DEFAULT_OUT }
}

const readJson = (path, label) => {
  try {
    return validateBaseline(JSON.parse(readFileSync(path, 'utf8')))
  } catch (error) {
    if (error?.code === 'ENOENT') throw new Error(`js-boundary-gate: ${label} missing: ${path}`)
    if (error instanceof SyntaxError) throw new Error(`js-boundary-gate: ${label} is not valid JSON: ${path}`)
    throw error
  }
}

const readFrozenContracts = (path) => {
  try {
    const frozen = JSON.parse(readFileSync(path, 'utf8'))
    if (!Array.isArray(frozen) || frozen.some((file) => typeof file !== 'string')) {
      throw new Error(`js-boundary-gate: frozen-contract list must be an array of paths: ${path}`)
    }
    return frozen
  } catch (error) {
    if (error?.code === 'ENOENT') throw new Error(`js-boundary-gate: frozen-contract list missing: ${path}`)
    if (error instanceof SyntaxError) throw new Error(`js-boundary-gate: frozen-contract list is not valid JSON: ${path}`)
    throw error
  }
}

const currentDebt = (root) => {
  const scanned = countsByFile(scanAll(join(root, 'requirements')))
  const frozen = new Set(readFrozenContracts(FROZEN_CONTRACTS))
  const contracts = packageLocalContracts(root)
  return {
    actual: withFrozenContractViolations(scanned, contracts, frozen),
    contracts,
    frozen,
  }
}

/** Execute the gate; returns a process status for the CLI and tests. */
export const run = ({ args = process.argv.slice(2), root = process.cwd() } = {}) => {
  const { generate, out } = parseArgs(args)
  let current
  try {
    current = currentDebt(root)
  } catch (error) {
    console.error(error.message)
    return 1
  }

  const baselineExists = existsSync(out)
  let baseline
  if (baselineExists) {
    try {
      baseline = readJson(out, 'baseline')
    } catch (error) {
      console.error(error.message)
      return 1
    }
  }

  const failures = baseline
    ? compareBaseline(baseline, current.actual)
    : Object.keys(current.actual).length === 0
      ? []
      : ['baseline missing while semantic-boundary debt remains — remove debt before deleting the ledger']

  if (generate) {
    if (failures.length > 0) {
      console.error(`js-boundary-gate: refusing baseline regeneration (${failures.length} violation(s))`)
      for (const failure of failures) console.error(`  ${failure}`)
      return 1
    }
    writeFileSync(out, `${JSON.stringify(current.actual, null, 2)}\n`)
    console.log(`js-boundary-gate: baseline written to ${out} (${Object.keys(current.actual).length} files)`)
    return 0
  }

  if (failures.length > 0) {
    console.error(`js-boundary-gate: ${failures.length} violation(s)`)
    for (const failure of failures) console.error(`  ${failure}`)
    return 1
  }

  const frozen = current.contracts.filter((file) => current.frozen.has(file)).length
  const ledger = baseline ? 'at/below baseline' : 'zero debt; baseline absent'
  console.log(
    `js-boundary-gate: OK — ${totalDebt(current.actual)} debt line(s) across ${Object.keys(current.actual).length} file(s), ${ledger}; ${frozen} frozen package-local *-contract.mjs`,
  )
  return 0
}

const isMain = process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href
if (isMain) process.exit(run())
