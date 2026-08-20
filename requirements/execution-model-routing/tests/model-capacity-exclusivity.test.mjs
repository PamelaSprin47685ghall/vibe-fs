import assert from 'node:assert/strict'
import { readdir, readFile } from 'node:fs/promises'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const repoRoot = fileURLToPath(new URL('../../../', import.meta.url))
const srcRoot = join(repoRoot, 'src/Wanxiangshu')

/**
 * Normalizes absolute path to unix-style relative path from repo root.
 * @param {string} absPath
 * @returns {string}
 */
const toRelative = (absPath) => {
  const rel = absPath.startsWith(repoRoot) ? absPath.slice(repoRoot.length) : absPath
  return rel.replace(/\\/g, '/')
}

/**
 * Recursively collects all production .fs files under src/Wanxiangshu.
 * @param {string} dir
 * @returns {Promise<string[]>}
 */
async function getAllProductionFsFiles(dir) {
  const entries = await readdir(dir, { withFileTypes: true })
  const nested = await Promise.all(
    entries.map(async (entry) => {
      const fullPath = join(dir, entry.name)
      if (entry.isDirectory()) {
        return getAllProductionFsFiles(fullPath)
      } else if (entry.isFile() && entry.name.endsWith('.fs')) {
        return [fullPath]
      }
      return []
    })
  )
  return nested.flat()
}

/**
 * ModelCapacity exclusive private knowledge identifiers.
 * These private types, state dictionaries, and algorithm helpers must exist
 * only in src/Wanxiangshu/OpenCode/Host/ModelCapacity.fs and nowhere else.
 */
const EXCLUSIVE_PRIVATE_KNOWLEDGE = Object.freeze([
  // Private types & DU states
  'CapacityStep',
  'CapacityTokenState',
  'CapacityToken',
  'CapacityStepDemand',
  'CapacityCreditSource',

  // Private mutable state / dictionary resources
  'ownedTokenByExecution',
  'creditSourceByExecution',
  'companionSessionByOwner',
  'companionOwnerBySession',
  'nextDemand',

  // Private borrowing, recall, credit distance & reservation algorithm helpers
  'ancestorDistance',
  'currentCreditSource',
  'clearCreditSource',
  'clearCreditSourcesForToken',
  'clearCreditSourcesForSession',
  'rememberCreditSource',
  'moveCreditSource',
  'nextAncestor',
  'companionLender',
  'matchingLenderDistance',
  'creditDistance',
  'isRetiring',
  'releaseToken',
  'retireToken',
  'retireTokenId',
  'retireExecution',
  'creditTokens',
  'withoutTokens',
  'schedulingView',
  'ordinaryDecision',
  'attributedDecision',
  'matchingCreditDecision',
  'routeDecision',
  'acquireOwnedToken',
  'moveOwnedToken',
  'finishStep',
  'reconcileFence',
  'rememberGrantedCredit',
  'idleBorrowPairs',
  'tryGrantBorrowed',
  'demandOwnsToken',
  'tryGrantOrdinary',
  'acquireForRoute',
  'requiredCreditDistance',
  'recordRoutedCredit',
  'applyRoutedToken',
  'commitRoutedTarget',
  'ensureReservationToken',
  'recordReservationCredit',
  'adoptOwnedToken',
  'dropOwnedCompanion',
  'clearOwnedCompanionIfMatches',
  'dropCompanionOwner',
])

/**
 * Controlled public types.
 * Allowed ONLY in ModelCapacity.fs (owner/producer) and ModelRouting.fs (permitted consumer).
 */
const CONTROLLED_PUBLIC_TYPES = Object.freeze([
  'CapacityLedger',
  'BorrowingCapacity',
])

const OWNER_FILE = 'src/Wanxiangshu/OpenCode/Host/ModelCapacity.fs'
const PERMITTED_CONSUMER_FILES = Object.freeze([
  OWNER_FILE,
  'src/Wanxiangshu/OpenCode/Host/ModelRouting.fs',
])

test('WHAT[EMR-010] EMR_010_model_capacity_owner_defines_all_private_borrowing_knowledge', async () => {
  const ownerAbs = join(repoRoot, OWNER_FILE)
  const ownerContent = await readFile(ownerAbs, 'utf8')

  for (const identifier of EXCLUSIVE_PRIVATE_KNOWLEDGE) {
    const pattern = new RegExp(`\\b${identifier}\\b`)
    assert.match(
      ownerContent,
      pattern,
      `ModelCapacity owner file must define knowledge identifier: ${identifier}`
    )
  }

  for (const publicType of CONTROLLED_PUBLIC_TYPES) {
    const pattern = new RegExp(`\\b${publicType}\\b`)
    assert.match(
      ownerContent,
      pattern,
      `ModelCapacity owner file must define controlled public type: ${publicType}`
    )
  }
})

test('WHAT[EMR-010] EMR_010_model_capacity_private_knowledge_is_exclusive_to_owner', async () => {
  const allFsFiles = await getAllProductionFsFiles(srcRoot)
  assert.ok(allFsFiles.length >= 600, `Expected at least 600 production files, got ${allFsFiles.length}`)

  const violations = []

  for (const fileAbs of allFsFiles) {
    const relPath = toRelative(fileAbs)
    if (relPath === OWNER_FILE) {
      continue
    }

    const content = await readFile(fileAbs, 'utf8')
    for (const identifier of EXCLUSIVE_PRIVATE_KNOWLEDGE) {
      const pattern = new RegExp(`\\b${identifier}\\b`)
      if (pattern.test(content)) {
        violations.push({
          file: relPath,
          leakedIdentifier: identifier,
        })
      }
    }
  }

  assert.deepEqual(
    violations,
    [],
    `Private ModelCapacity borrowing/lineage knowledge leaked outside owner file (${OWNER_FILE}). Violations: ${JSON.stringify(violations, null, 2)}`
  )
})

test('WHAT[EMR-010] EMR_010_capacity_ledger_and_borrowing_capacity_types_are_restricted_to_permitted_zones', async () => {
  const allFsFiles = await getAllProductionFsFiles(srcRoot)
  const violations = []

  for (const fileAbs of allFsFiles) {
    const relPath = toRelative(fileAbs)
    if (PERMITTED_CONSUMER_FILES.includes(relPath)) {
      continue
    }

    const content = await readFile(fileAbs, 'utf8')
    for (const publicType of CONTROLLED_PUBLIC_TYPES) {
      const pattern = new RegExp(`\\b${publicType}\\b`)
      if (pattern.test(content)) {
        violations.push({
          file: relPath,
          leakedType: publicType,
        })
      }
    }
  }

  assert.deepEqual(
    violations,
    [],
    `Controlled ModelCapacity types appeared outside permitted zones (${PERMITTED_CONSUMER_FILES.join(', ')}). Violations: ${JSON.stringify(violations, null, 2)}`
  )
})

test('WHAT[EMR-010] EMR_010_exclusivity_test_is_refutable_and_fails_closed_on_violation', () => {
  // Test refutability: simulate a leaked knowledge identifier in non-owner content
  const simulatedLeakedContent = `
    namespace Wanxiangshu.SomeModule
    let checkDistance = ancestorDistance "parent" "child"
  `

  const checkContent = (content, identifiers) => {
    const matches = []
    for (const id of identifiers) {
      if (new RegExp(`\\b${id}\\b`).test(content)) {
        matches.push(id)
      }
    }
    return matches
  }

  const detected = checkContent(simulatedLeakedContent, EXCLUSIVE_PRIVATE_KNOWLEDGE)
  assert.deepEqual(
    detected,
    ['ancestorDistance'],
    'Exclusivity detector must reliably catch leaked identifiers'
  )

  const cleanContent = `
    namespace Wanxiangshu.SomeModule
    let doSomething () = ()
  `
  assert.deepEqual(
    checkContent(cleanContent, EXCLUSIVE_PRIVATE_KNOWLEDGE),
    [],
    'Clean content must produce zero violations'
  )
})
