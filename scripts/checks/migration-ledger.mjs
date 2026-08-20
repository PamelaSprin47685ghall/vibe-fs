#!/usr/bin/env node
// migration-ledger.mjs — Temporary Wave 0 validation script.
// Not wired into scripts/check.mjs. Delete after Wave 11 release closure.
//
// Validates migration-ledger.json against semantic-owners.json:
// - entry count matches ownership count
// - every entry has path/owner/classification/wave/status
// - classification values are legal
// - wave values are legal (1-7)
// - status values are legal
// - no UNKNOWN classification
// - no missing or extra files vs semantic-owners.json

import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..')

const VALID_CLASSIFICATIONS = new Set([
  'KEEP', 'MOVE', 'SPLIT', 'DELETE', 'COMPOSITION-ROOT', 'ADAPTER',
])

const VALID_WAVES = new Set([1, 2, 3, 4, 5, 6, 7])

const VALID_STATUSES = new Set([
  'PENDING', 'CUTOVER', 'DELETED', 'PROVEN-KEEP',
])

const REQUIRED_FIELDS = ['path', 'owner', 'classification', 'wave', 'status']

const main = () => {
  const ledgerPath = join(ROOT, 'scripts/checks/migration-ledger.json')
  const ownersPath = join(ROOT, 'scripts/checks/semantic-owners.json')

  let ledger, owners
  try {
    ledger = JSON.parse(readFileSync(ledgerPath, 'utf8'))
  } catch (e) {
    console.error(`migration-ledger: cannot read ${ledgerPath}: ${e.message}`)
    process.exit(1)
  }
  try {
    owners = JSON.parse(readFileSync(ownersPath, 'utf8'))
  } catch (e) {
    console.error(`migration-ledger: cannot read ${ownersPath}: ${e.message}`)
    process.exit(1)
  }

  const errors = []

  // 1. version field
  if (ledger.version !== 1) {
    errors.push(`version must be 1, got ${ledger.version}`)
  }

  // 2. total field matches entries length
  if (typeof ledger.total !== 'number') {
    errors.push(`total must be a number, got ${typeof ledger.total}`)
  } else if (ledger.total !== ledger.entries.length) {
    errors.push(`total (${ledger.total}) does not match entries.length (${ledger.entries.length})`)
  }

  // 3. entry count matches semantic-owners ownership count
  const ownersCount = owners.ownership.length
  if (ledger.entries.length !== ownersCount) {
    errors.push(
      `entries count (${ledger.entries.length}) does not match semantic-owners ownership count (${ownersCount})`,
    )
  }

  // 4. build owner path set from semantic-owners for cross-check
  const ownerPaths = new Set(owners.ownership.map((e) => e.path.replace('src/Wanxiangshu/', '')))
  const ledgerPaths = new Set()

  // 5. validate each entry
  for (let i = 0; i < ledger.entries.length; i++) {
    const entry = ledger.entries[i]
    const prefix = `entry[${i}] (${entry.path || '???'})`

    // required fields
    for (const field of REQUIRED_FIELDS) {
      if (!(field in entry)) {
        errors.push(`${prefix}: missing required field '${field}'`)
      }
    }

    // classification legal
    if (entry.classification && !VALID_CLASSIFICATIONS.has(entry.classification)) {
      errors.push(`${prefix}: illegal classification '${entry.classification}'`)
    }

    // no UNKNOWN
    if (entry.classification === 'UNKNOWN') {
      errors.push(`${prefix}: classification must not be UNKNOWN`)
    }

    // wave legal
    if (entry.wave !== undefined && !VALID_WAVES.has(entry.wave)) {
      errors.push(`${prefix}: illegal wave ${entry.wave}`)
    }

    // status legal
    if (entry.status && !VALID_STATUSES.has(entry.status)) {
      errors.push(`${prefix}: illegal status '${entry.status}'`)
    }

    // track paths
    if (entry.path) {
      ledgerPaths.add(entry.path)
    }
  }

  // 6. no missing files (in semantic-owners but not in ledger)
  for (const p of ownerPaths) {
    if (!ledgerPaths.has(p)) {
      errors.push(`missing from ledger: ${p}`)
    }
  }

  // 7. no extra files (in ledger but not in semantic-owners)
  for (const p of ledgerPaths) {
    if (!ownerPaths.has(p)) {
      errors.push(`extra in ledger (not in semantic-owners): ${p}`)
    }
  }

  // report
  if (errors.length > 0) {
    console.error(`migration-ledger: ${errors.length} error(s)`)
    for (const err of errors) {
      console.error(`  ${err}`)
    }
    process.exit(1)
  }

  // summary
  const classCounts = {}
  const waveCounts = {}
  const statusCounts = {}
  for (const e of ledger.entries) {
    classCounts[e.classification] = (classCounts[e.classification] || 0) + 1
    waveCounts[e.wave] = (waveCounts[e.wave] || 0) + 1
    statusCounts[e.status] = (statusCounts[e.status] || 0) + 1
  }

  console.log('migration-ledger: OK')
  console.log(`  entries: ${ledger.entries.length}`)
  console.log(`  classifications: ${JSON.stringify(classCounts)}`)
  console.log(`  waves: ${JSON.stringify(waveCounts)}`)
  console.log(`  statuses: ${JSON.stringify(statusCounts)}`)
}

main()
