#!/usr/bin/env node
// migration-ledger.mjs — DAG migration ledger architectural gate (AGENTS.md Chapter 57).
//
// Invariants enforced:
// 1. JSON parsing integrity: fail-closed if ledger or semantic-owners cannot be parsed.
// 2. Schema compliance: all 57.1 node fields present and legal.
// 3. Dependency referential integrity: every depends_on.id exists in nodes.
// 4. Edge categorization: every edge must have kind in {contract, ownership, compile, closure}.
// 5. READY state semantic validity: state=READY => all dependencies must have state=DONE.
// 6. Acyclic invariant: Kahn topological sort with cycle path reporting on cycles.
// 7. Full coverage invariant: nodes.files U coverage_backlog exactly matches semantic-owners.json.
//
// Usage:
//   node scripts/checks/migration-ledger.mjs              # Validate active ledger
//   node scripts/checks/migration-ledger.mjs --self-test  # Run built-in known-bad test suite

import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..')

export const LEGAL_CLASSIFICATIONS = new Set([
  'KEEP', 'MOVE', 'SPLIT', 'DELETE', 'COMPOSITION-ROOT', 'ADAPTER',
])

export const LEGAL_STATES = new Set([
  'PENDING', 'READY', 'RUNNING', 'DONE',
])

export const LEGAL_RESULTS = new Set([
  'PENDING', 'CUTOVER', 'DELETED', 'PROVEN-KEEP',
])

export const LEGAL_EDGE_KINDS = new Set([
  'contract', 'ownership', 'compile', 'closure',
])

export const REQUIRED_NODE_FIELDS = [
  'id',
  'primary_owner',
  'intent',
  'files',
  'classification',
  'publishes',
  'consumes',
  'depends_on',
  'production_callers_to_migrate',
  'proofs',
  'architecture_gates',
  'touched_paths',
  'coverage_tags',
  'state',
  'result',
]

export function validateLedger(ledger, ownersManifest) {
  const errors = []

  if (!ledger || typeof ledger !== 'object') {
    return { ok: false, errors: ['Ledger is not a valid JSON object'] }
  }

  if (!ledger.protocol || typeof ledger.protocol !== 'object') {
    errors.push('Missing or invalid top-level "protocol" header')
  }

  if (!Array.isArray(ledger.nodes)) {
    return { ok: false, errors: ['Missing or invalid top-level "nodes" array'] }
  }

  if (!ledger.coverage_backlog || typeof ledger.coverage_backlog !== 'object') {
    return { ok: false, errors: ['Missing or invalid top-level "coverage_backlog" object'] }
  }

  const nodeMap = new Map()
  for (let i = 0; i < ledger.nodes.length; i++) {
    const node = ledger.nodes[i]
    const prefix = `node[${i}] (${node?.id || '???'})`

    if (!node || typeof node !== 'object') {
      errors.push(`${prefix}: node entry must be an object`)
      continue
    }

    if (!node.id || typeof node.id !== 'string') {
      errors.push(`${prefix}: missing or invalid "id" string`)
      continue
    }

    if (nodeMap.has(node.id)) {
      errors.push(`Duplicate node id: "${node.id}"`)
    }
    nodeMap.set(node.id, node)

    for (const field of REQUIRED_NODE_FIELDS) {
      if (!(field in node)) {
        errors.push(`${prefix}: missing required field "${field}"`)
      }
    }

    if (node.classification && !LEGAL_CLASSIFICATIONS.has(node.classification)) {
      errors.push(`${prefix}: illegal classification "${node.classification}"`)
    }

    if (node.state && !LEGAL_STATES.has(node.state)) {
      errors.push(`${prefix}: illegal state "${node.state}"`)
    }

    if (node.result && !LEGAL_RESULTS.has(node.result)) {
      errors.push(`${prefix}: illegal result "${node.result}"`)
    }

    if (node.files && !Array.isArray(node.files)) {
      errors.push(`${prefix}: "files" must be an array`)
    }

    if (node.publishes && !Array.isArray(node.publishes)) {
      errors.push(`${prefix}: "publishes" must be an array`)
    }

    if (node.consumes && !Array.isArray(node.consumes)) {
      errors.push(`${prefix}: "consumes" must be an array`)
    }

    if (node.depends_on && !Array.isArray(node.depends_on)) {
      errors.push(`${prefix}: "depends_on" must be an array`)
    }
  }

  // 1. Dependency referential integrity & edge kind validation & READY state validation
  for (const node of ledger.nodes) {
    if (!Array.isArray(node?.depends_on)) continue

    for (let j = 0; j < node.depends_on.length; j++) {
      const dep = node.depends_on[j]
      const depPrefix = `node "${node.id}" depends_on[${j}]`

      if (!dep || typeof dep !== 'object' || !dep.id || typeof dep.id !== 'string') {
        errors.push(`${depPrefix}: invalid dependency edge structure (must be object with "id" and "kind")`)
        continue
      }

      if (!nodeMap.has(dep.id)) {
        errors.push(`node "${node.id}" references unknown dependency "${dep.id}"`)
        continue
      }

      if (!dep.kind || !LEGAL_EDGE_KINDS.has(dep.kind)) {
        errors.push(
          `node "${node.id}" -> "${dep.id}" edge missing or illegal kind "${dep.kind}" (must be one of: contract, ownership, compile, closure)`,
        )
      }

      if (node.state === 'READY') {
        const target = nodeMap.get(dep.id)
        if (target && target.state !== 'DONE') {
          errors.push(
            `node "${node.id}" has state=READY but dependency "${dep.id}" is in state="${target.state}" (must be DONE)`,
          )
        }
      }
    }
  }

  // 2. Kahn Topological Sort (Acyclic check with cycle tracing)
  const inDegree = new Map()
  const dependents = new Map()

  for (const id of nodeMap.keys()) {
    inDegree.set(id, 0)
    dependents.set(id, [])
  }

  for (const node of ledger.nodes) {
    if (!Array.isArray(node?.depends_on)) continue
    for (const dep of node.depends_on) {
      if (nodeMap.has(dep?.id)) {
        inDegree.set(node.id, (inDegree.get(node.id) || 0) + 1)
        dependents.get(dep.id).push(node.id)
      }
    }
  }

  const queue = []
  for (const [id, deg] of inDegree.entries()) {
    if (deg === 0) queue.push(id)
  }

  let visitedCount = 0
  while (queue.length > 0) {
    const u = queue.shift()
    visitedCount++
    for (const v of dependents.get(u)) {
      const newDeg = inDegree.get(v) - 1
      inDegree.set(v, newDeg)
      if (newDeg === 0) queue.push(v)
    }
  }

  if (visitedCount < nodeMap.size) {
    const remaining = [...inDegree.entries()].filter(([, d]) => d > 0).map(([id]) => id)
    const path = []
    const visited = new Set()
    let curr = remaining[0]
    while (!visited.has(curr)) {
      visited.add(curr)
      path.push(curr)
      const node = nodeMap.get(curr)
      const nextDep = node?.depends_on?.find((d) => remaining.includes(d?.id))
      if (nextDep) {
        curr = nextDep.id
      } else {
        break
      }
    }
    const cycleStartIndex = path.indexOf(curr)
    const cyclePath =
      cycleStartIndex >= 0
        ? path.slice(cycleStartIndex).concat(curr).join(' -> ')
        : remaining.join(', ')
    errors.push(`Kahn topological sort failed: cycle detected in DAG (${cyclePath})`)
  }

  // 3. Coverage completeness against semantic-owners manifest
  if (ownersManifest && Array.isArray(ownersManifest.ownership)) {
    const expectedFiles = new Set(ownersManifest.ownership.map((e) => e.path))
    const ledgerFileToLocation = new Map()

    for (const node of ledger.nodes) {
      if (Array.isArray(node?.files)) {
        for (const f of node.files) {
          if (ledgerFileToLocation.has(f)) {
            errors.push(
              `File "${f}" appears in multiple locations: "${ledgerFileToLocation.get(f)}" and node "${node.id}"`,
            )
          }
          ledgerFileToLocation.set(f, `node:${node.id}`)
        }
      }
    }

    for (const [group, files] of Object.entries(ledger.coverage_backlog || {})) {
      if (Array.isArray(files)) {
        for (const f of files) {
          if (ledgerFileToLocation.has(f)) {
            errors.push(
              `File "${f}" appears in multiple locations: "${ledgerFileToLocation.get(f)}" and backlog "${group}"`,
            )
          }
          ledgerFileToLocation.set(f, `backlog:${group}`)
        }
      }
    }

    const missingFromLedger = []
    for (const f of expectedFiles) {
      if (!ledgerFileToLocation.has(f)) {
        missingFromLedger.push(f)
      }
    }

    const extraInLedger = []
    for (const f of ledgerFileToLocation.keys()) {
      if (!expectedFiles.has(f)) {
        extraInLedger.push(f)
      }
    }

    if (missingFromLedger.length > 0) {
      errors.push(
        `Coverage mismatch: ${missingFromLedger.length} file(s) in semantic-owners.json missing from ledger:\n  ${missingFromLedger.slice(0, 10).join('\n  ')}${missingFromLedger.length > 10 ? '\n  ...' : ''}`,
      )
    }

    if (extraInLedger.length > 0) {
      errors.push(
        `Coverage mismatch: ${extraInLedger.length} file(s) in ledger not in semantic-owners.json:\n  ${extraInLedger.slice(0, 10).join('\n  ')}${extraInLedger.length > 10 ? '\n  ...' : ''}`,
      )
    }
  }

  return {
    ok: errors.length === 0,
    errors,
  }
}

export function runSelfTest(validLedger, ownersManifest) {
  console.log('migration-ledger: running self-test suite...')
  const testResults = []

  // Helper deep copy
  const clone = (obj) => JSON.parse(JSON.stringify(obj))

  // 1. Fixture: Cycle Detection
  {
    const cycleLedger = clone(validLedger)
    const nodeA = cycleLedger.nodes[0]
    const nodeB = cycleLedger.nodes[1]
    nodeA.depends_on = [{ id: nodeB.id, kind: 'contract' }]
    nodeB.depends_on = [{ id: nodeA.id, kind: 'contract' }]
    const result = validateLedger(cycleLedger, ownersManifest)
    const hasCycleError = result.errors.some((e) => e.includes('cycle detected'))
    if (!result.ok && hasCycleError) {
      testResults.push({ name: '1. Cycle detection (Kahn topological sort)', ok: true })
    } else {
      testResults.push({
        name: '1. Cycle detection (Kahn topological sort)',
        ok: false,
        detail: `Expected cycle error, got: ${result.errors.join('; ')}`,
      })
    }
  }

  // 2. Fixture: Missing / Invalid Edge Kind
  {
    const missingKindLedger = clone(validLedger)
    const readyNode = missingKindLedger.nodes.find((n) => n.depends_on && n.depends_on.length > 0)
    readyNode.depends_on[0] = { id: readyNode.depends_on[0].id } // missing kind
    const result = validateLedger(missingKindLedger, ownersManifest)
    const hasKindError = result.errors.some((e) => e.includes('missing or illegal kind'))
    if (!result.ok && hasKindError) {
      testResults.push({ name: '2. Missing/illegal edge kind rejection', ok: true })
    } else {
      testResults.push({
        name: '2. Missing/illegal edge kind rejection',
        ok: false,
        detail: `Expected kind error, got: ${result.errors.join('; ')}`,
      })
    }
  }

  // 3. Fixture: READY state with non-DONE dependency
  {
    const readyNotDoneLedger = clone(validLedger)
    const readyNode = readyNotDoneLedger.nodes.find((n) => n.depends_on && n.depends_on.length > 0)
    readyNode.state = 'READY'
    const depNodeId = readyNode.depends_on[0].id
    const depNode = readyNotDoneLedger.nodes.find((n) => n.id === depNodeId)
    depNode.state = 'PENDING'
    const result = validateLedger(readyNotDoneLedger, ownersManifest)
    const hasStateError = result.errors.some((e) => e.includes('has state=READY but dependency'))
    if (!result.ok && hasStateError) {
      testResults.push({ name: '3. READY node with non-DONE dependency rejection', ok: true })
    } else {
      testResults.push({
        name: '3. READY node with non-DONE dependency rejection',
        ok: false,
        detail: `Expected READY dependency error, got: ${result.errors.join('; ')}`,
      })
    }
  }

  // 4. Fixture: Coverage incompleteness (missing file)
  {
    const missingFileLedger = clone(validLedger)
    const firstGroup = Object.keys(missingFileLedger.coverage_backlog)[0]
    missingFileLedger.coverage_backlog[firstGroup].pop() // drop one file
    const result = validateLedger(missingFileLedger, ownersManifest)
    const hasCoverageError = result.errors.some((e) => e.includes('Coverage mismatch'))
    if (!result.ok && hasCoverageError) {
      testResults.push({ name: '4. Coverage mismatch (missing file) rejection', ok: true })
    } else {
      testResults.push({
        name: '4. Coverage mismatch (missing file) rejection',
        ok: false,
        detail: `Expected coverage error, got: ${result.errors.join('; ')}`,
      })
    }
  }

  // 5. Valid baseline check
  {
    const result = validateLedger(validLedger, ownersManifest)
    if (result.ok) {
      testResults.push({ name: '5. Valid ledger baseline acceptance', ok: true })
    } else {
      testResults.push({
        name: '5. Valid ledger baseline acceptance',
        ok: false,
        detail: `Expected pass, got errors: ${result.errors.join('; ')}`,
      })
    }
  }

  let allPassed = true
  for (const tr of testResults) {
    if (tr.ok) {
      console.log(`  ✓ ${tr.name}`)
    } else {
      console.error(`  ✗ ${tr.name}: ${tr.detail}`)
      allPassed = false
    }
  }

  if (!allPassed) {
    console.error('migration-ledger: self-test failed')
    process.exit(1)
  }

  console.log('migration-ledger: self-test passed (4 known-bad fixtures rejected, baseline accepted)')
}

const main = () => {
  const ledgerPath = join(ROOT, 'scripts/checks/migration-ledger.json')
  const ownersPath = join(ROOT, 'scripts/checks/semantic-owners.json')

  let ledger, owners
  try {
    ledger = JSON.parse(readFileSync(ledgerPath, 'utf8'))
  } catch (e) {
    console.error(`migration-ledger: cannot read or parse ${ledgerPath}: ${e.message}`)
    process.exit(1)
  }

  try {
    owners = JSON.parse(readFileSync(ownersPath, 'utf8'))
  } catch (e) {
    console.error(`migration-ledger: cannot read or parse ${ownersPath}: ${e.message}`)
    process.exit(1)
  }

  if (process.argv.includes('--self-test')) {
    runSelfTest(ledger, owners)
    process.exit(0)
  }

  const { ok, errors } = validateLedger(ledger, owners)

  if (!ok) {
    console.error(`migration-ledger: ${errors.length} error(s) detected`)
    for (const err of errors) {
      console.error(`  ${err}`)
    }
    process.exit(1)
  }

  const stateCounts = {}
  const classCounts = {}
  let totalFiles = 0

  for (const n of ledger.nodes) {
    stateCounts[n.state] = (stateCounts[n.state] || 0) + 1
    classCounts[n.classification] = (classCounts[n.classification] || 0) + 1
    totalFiles += (n.files || []).length
  }

  const backlogCounts = {}
  let backlogTotal = 0
  for (const [group, files] of Object.entries(ledger.coverage_backlog || {})) {
    backlogCounts[group] = files.length
    backlogTotal += files.length
  }

  console.log('migration-ledger: OK (DAG integrity & coverage verified)')
  console.log(`  nodes: ${ledger.nodes.length} (${JSON.stringify(stateCounts)})`)
  console.log(`  node files: ${totalFiles}, coverage backlog files: ${backlogTotal}, total: ${totalFiles + backlogTotal}`)
  console.log(`  classifications: ${JSON.stringify(classCounts)}`)
  console.log(`  coverage backlog by group: ${JSON.stringify(backlogCounts)}`)
}

main()
