#!/usr/bin/env node
// ledger-retirement-gate.mjs — REQUIREMENT-SYSTEM-020: the migration ledger is
// permanently retired. Architecture truth lives in production code, per-package
// WHY/WHAT/HOW/GAP, executable proofs and architecture gates — never in a DONE
// ledger. This gate mechanically forbids resurrection.
//
import { readFileSync, existsSync, readdirSync, statSync, realpathSync } from 'node:fs'
import { basename, join, dirname } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

// Checks:
//   1. Retired paths must not exist:
//      scripts/checks/migration-ledger.json, scripts/checks/migration-ledger.mjs,
//      requirements/migration-ledger/
//   2. scripts/check.mjs must not reference the retired gate.
//   3. No `migration-ledger` reference may appear in src/, scripts/, or formal
//      requirements docs (WHY/WHAT/HOW/GAP/APPLIES-TO/INDEX/README).
//   4. REQUIREMENT-SYSTEM-019 must not be re-declared as a live WHAT clause.


const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..')

export const RETIRED_PATHS = [
  'scripts/checks/migration-ledger.json',
  'scripts/checks/migration-ledger.mjs',
  'requirements/migration-ledger',
]

const DOC_EXTENSIONS = new Set(['.md'])

function walk(dir, out) {
  for (const entry of readdirSync(dir)) {
    const p = join(dir, entry)
    const s = statSync(p)
    if (s.isDirectory()) walk(p, out)
    else out.push(p)
  }
  return out
}

/// Pure checker over injected facts so tests can drive it without the tree.
export function analyzeRetirement({ retiredPathsExisting, checkWiring, offendingReferences, whatIds }) {
  const errors = []
  for (const p of retiredPathsExisting) {
    errors.push(`retired path reappeared: ${p}`)
  }
  if (checkWiring) {
    errors.push('scripts/check.mjs still references the retired migration-ledger gate')
  }
  for (const ref of offendingReferences) {
    errors.push(`formal surface references retired ledger: ${ref}`)
  }
  if (whatIds.includes('REQUIREMENT-SYSTEM-019')) {
    errors.push('REQUIREMENT-SYSTEM-019 is retired and must not be re-declared (number never reused)')
  }
  return errors
}

function scanRepository() {
  const retiredPathsExisting = RETIRED_PATHS.filter((p) => existsSync(join(ROOT, p)))

  const checkSource = readFileSync(join(ROOT, 'scripts/check.mjs'), 'utf8')
  const checkWiring = checkSource.includes('migration-ledger')

  const offendingReferences = []
  for (const rel of ['src', 'scripts']) {
    for (const file of walk(join(ROOT, rel), [])) {
      if (!/\.(fs|fsi|mjs|cjs|json)$/.test(file)) continue
      if (basename(file) === 'ledger-retirement-gate.mjs') continue
      let content
      try {
        content = readFileSync(file, 'utf8')
      } catch {
        continue
      }
      if (content.includes('migration-ledger')) {
        offendingReferences.push(file.slice(ROOT.length + 1))
      }
    }
  }

  const reqDocs = []
  const reqDir = join(ROOT, 'requirements')
  for (const pkg of readdirSync(reqDir)) {
    const p = join(reqDir, pkg)
    if (!statSync(p).isDirectory()) continue
    // The retirement-law owner (requirement-system) must be able to name the
    // retired artifact it forbids; every other package stays under the ban.
    const isOwner = pkg === 'requirement-system'
    for (const entry of readdirSync(p)) {
      const isDoc = DOC_EXTENSIONS.has(entry.split('.').pop() ?? '')
      const isAppliesTo = entry === 'APPLIES-TO'
      if (isOwner ? isAppliesTo : isDoc || isAppliesTo) reqDocs.push(join(p, entry))
    }
    if (!isOwner) {
      const testsDir = join(p, 'tests')
      if (existsSync(testsDir) && statSync(testsDir).isDirectory()) {
        for (const f of walk(testsDir, [])) reqDocs.push(f)
      }
    }
  }
  for (const file of reqDocs) {
    let content
    try {
      content = readFileSync(file, 'utf8')
    } catch {
      continue
    }
    if (content.includes('migration-ledger')) {
      offendingReferences.push(file.slice(ROOT.length + 1))
    }
  }

  const whatText = readFileSync(join(ROOT, 'requirements/requirement-system/WHAT.md'), 'utf8')
  const whatIds = [...whatText.matchAll(/##\s+(REQUIREMENT-SYSTEM-\d+)/g)].map((m) => m[1])

  return { retiredPathsExisting, checkWiring, offendingReferences, whatIds }
}

const isMainModule = (() => {
  try {
    return import.meta.url === pathToFileURL(realpathSync(process.argv[1])).href
  } catch {
    return false
  }
})()

if (!isMainModule) process.exit(0)

const facts = scanRepository()

const errors = analyzeRetirement({
  retiredPathsExisting: facts.retiredPathsExisting,
  checkWiring: facts.checkWiring,
  offendingReferences: facts.offendingReferences,
  whatIds: facts.whatIds,
})

if (errors.length > 0) {
  console.error(`ledger-retirement-gate: ${errors.length} violation(s)`)
  for (const e of errors) console.error(`  ${e}`)
  process.exit(1)
}
console.log('ledger-retirement-gate: OK — migration ledger stays retired; no resurrection paths')
