#!/usr/bin/env node

import { existsSync, readFileSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

import { buildTraceGraph } from '../lib/requirement-trace.mjs'
import { validateSemanticEvidenceProof } from '../lib/semantic-evidence.mjs'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..')

const FSPROJ = join(ROOT, 'src/Wanxiangshu/Wanxiangshu.fsproj')
const PRODUCTION_ROOT = join(ROOT, 'src/Wanxiangshu')
const OWNERS = join(ROOT, 'scripts/checks/semantic-owners.json')
const CONTRACTS = join(ROOT, 'scripts/checks/published-contracts.json')
const RELEASE_CLOSURE_NODES = join(ROOT, 'scripts/checks/release-closure-nodes.json')

const PATH_GLOB = /[*?\[\]]/

const norm = (path) => path.replace(/\\/g, '/')
const meaningful = (value) => typeof value === 'string' && value.trim().length >= 16
const semanticSourcePath = (path) => path.endsWith('.fsi') ? path.slice(0, -1) : path
function repositoryPath(path, label) {
  const normalized = norm(relative(ROOT, resolve(path)))
  if (normalized === '..' || normalized.startsWith('../')) throw new Error(`${label} is outside the repository: ${path}`)
  return normalized
}

function readCompilePaths(projectFile, productionRoot) {
  const project = resolve(projectFile)
  const root = resolve(productionRoot)
  const prefix = `${norm(root).replace(/\/$/, '')}/`
  const text = readFileSync(project, 'utf8')
  const paths = [...text.matchAll(/<Compile\s+Include="([^"]+\.fs)"\s*\/>/g)]
    .map((match) => resolve(dirname(project), match[1]))
    .filter((path) => `${norm(path)}/`.startsWith(prefix))
    .map((path) => repositoryPath(path, 'compile source'))

  if (paths.length === 0) throw new Error(`${repositoryPath(project, 'project file')}: no production Compile entries found`)
  if (new Set(paths).size !== paths.length) throw new Error(`${repositoryPath(project, 'project file')}: duplicate Compile entry`)
  return paths
}

function authorizationOf(value, label, fail) {
  const symbols = value?.symbols ?? []
  const symbolRoots = value?.symbol_roots ?? []
  if (!Array.isArray(symbols) || !Array.isArray(symbolRoots)) {
    fail('invalid-symbol-authorization', `${label}: symbols and symbol_roots must be arrays`)
    return null
  }
  const invalid = [...symbols, ...symbolRoots].filter(
    (symbol) => typeof symbol !== 'string' || symbol.trim().length === 0 || PATH_GLOB.test(symbol),
  )
  if (invalid.length > 0 || symbols.length + symbolRoots.length === 0) {
    fail('invalid-symbol-authorization', `${label}: declare at least one exact symbol or symbol root without globs`)
    return null
  }
  const normalizedSymbols = symbols.map((symbol) => symbol.trim())
  const normalizedRoots = symbolRoots.map((symbol) => symbol.trim().replace(/\.$/, ''))
  if (new Set(normalizedSymbols).size !== normalizedSymbols.length || new Set(normalizedRoots).size !== normalizedRoots.length) {
    fail('duplicate-symbol-authorization', `${label}: duplicate symbol authorization`)
    return null
  }
  return { symbols: normalizedSymbols, symbolRoots: normalizedRoots }
}

const authorizes = (authorization, symbol) =>
  authorization.symbols.includes(symbol) ||
  authorization.symbolRoots.some((root) => symbol === root || symbol.startsWith(`${root}.`))

export function analyzeOwnerContracts({
  compilePaths,
  semanticOwners,
  publishedContracts,
  migrationState,
  requirementTrace,
  repositoryRoot = ROOT,
}) {
  const violations = []
  const fail = (code, message, details = {}) => violations.push({ code, message, ...details })
  const compiled = compilePaths.map(norm)
  const compiledSet = new Set(compiled)
  if (compiledSet.size !== compiled.length) fail('duplicate-compile-entry', 'production compile set contains duplicate paths')

  const ownerClaims = new Map()
  for (const entry of semanticOwners?.ownership ?? []) {
    const path = norm(entry.path)
    const claims = ownerClaims.get(path) ?? []
    claims.push(entry.owner)
    ownerClaims.set(path, claims)
  }
  for (const path of compiled) {
    const claims = ownerClaims.get(path) ?? []
    if (claims.length === 0) fail('unowned-production-module', `${path}: production module has no primary owner`, { path })
    if (claims.length > 1)
      fail('duplicate-primary-owner', `${path}: primary owner declared ${claims.length} times (${claims.join(', ')})`, { path })
  }
  for (const [path, claims] of ownerClaims) {
    if (!compiledSet.has(path)) fail('stale-owner-entry', `${path}: semantic owner entry is outside the compile set`, { path })
    if (claims.length > 1 && !compiledSet.has(path))
      fail('duplicate-primary-owner', `${path}: primary owner declared ${claims.length} times (${claims.join(', ')})`, { path })
  }

  const ownerOf = new Map([...ownerClaims].filter(([, claims]) => claims.length === 1).map(([path, claims]) => [path, claims[0]]))
  const declaredOwners = new Set(ownerOf.values())

  const closedPaths = migrationState ? new Set(migrationState.closedPaths ?? []) : null
  const migrationNodeByPath = new Map(migrationState?.nodeByPath ?? [])
  const migrationNodes = new Map((migrationState?.nodes ?? []).map((node) => [node.id, node]))
  const registry = publishedContracts ?? {}
  const contractsByPath = new Map()
  const contractEntries = []

  const validateOwnedPath = (entry, kind, publication) => {
    const path = norm(entry?.path ?? '')
    if (!path || PATH_GLOB.test(path) || !compiledSet.has(path)) {
      fail('invalid-contract-declaration', `${kind}: '${path}' must be one exact compiled path`, { path })
      return null
    }
    if (ownerOf.get(path) !== entry.owner) {
      fail('contract-owner-mismatch', `${path}: registry owner '${entry.owner}' does not match '${ownerOf.get(path) ?? 'unowned'}'`, {
        path,
      })
      return null
    }
    if (!meaningful(entry.justification)) {
      fail('missing-architectural-justification', `${path}: ${kind} needs a written architectural justification`, { path })
      return null
    }
    if (closedPaths) {
      if (!closedPaths.has(path)) {
        fail('contract-before-cutover', `${path}: ${kind} cannot be declared before its migration node is DONE`, { path })
        return null
      }
      const nodeId = migrationNodeByPath.get(path)
      const node = nodeId ? migrationNodes.get(nodeId) : null
      if (!entry.node || entry.node !== nodeId || !node || node.state !== 'DONE') {
        fail('contract-node-mismatch', `${path}: ${kind} must reference its exact DONE migration node '${nodeId ?? 'none'}'`, {
          path,
        })
        return null
      }
      if (publication && (!entry.contract || !node.publishes?.includes(entry.contract))) {
        fail('contract-vocabulary-mismatch', `${path}: '${entry.contract ?? ''}' is not published by migration node '${nodeId}'`, {
          path,
        })
        return null
      }
    }
    return path
  }

  const contractKeys = new Set()
  for (const entry of registry.contracts ?? []) {
    if (!['published-contract', 'physical-port', 'semantic-evidence'].includes(entry?.kind)) {
      fail('invalid-contract-kind', `${entry?.path ?? ''}: illegal contract kind '${entry?.kind ?? ''}'`, { path: entry?.path })
      continue
    }
    const path = validateOwnedPath(entry, 'contract', true)
    const authorization = authorizationOf(entry, `contract ${entry?.path ?? ''}`, fail)
    const semanticEvidenceFinding = validateSemanticEvidenceProof(entry, requirementTrace, repositoryRoot)
    if (semanticEvidenceFinding) fail(
      semanticEvidenceFinding.code,
      semanticEvidenceFinding.message,
      { path: semanticEvidenceFinding.path },
    )
    const semanticEvidence = !semanticEvidenceFinding
    if (
      entry.kind === 'semantic-evidence' &&
      authorization &&
      (authorization.symbols.length === 0 || authorization.symbolRoots.length > 0)
    ) {
      fail(
        'invalid-semantic-evidence-authorization',
        `${entry?.path ?? ''}: semantic-evidence must authorize exact symbols and forbids symbol roots`,
        { path: entry?.path },
      )
    }
    const consumers = [...new Set(entry?.consumers ?? [])]
    if (
      consumers.length === 0 ||
      consumers.some((owner) => typeof owner !== 'string' || owner === entry.owner || !declaredOwners.has(owner))
    ) {
      fail('invalid-contract-consumers', `${entry?.path ?? ''}: contract consumers must be exact, foreign, existing owners`, {
        path: entry?.path,
      })
      continue
    }
    if (
      !path ||
      !authorization ||
      !semanticEvidence ||
      (entry.kind === 'semantic-evidence' && (authorization.symbols.length === 0 || authorization.symbolRoots.length > 0))
    ) continue
    const key = `${path}\0${entry.kind}\0${[...consumers].sort().join('\0')}\0${authorization.symbols.join('\0')}\0${authorization.symbolRoots.join('\0')}`
    if (contractKeys.has(key)) {
      fail('duplicate-contract-declaration', `${path}: exact contract declaration is duplicated`, { path })
      continue
    }
    contractKeys.add(key)
    const normalized = { ...entry, path, consumers: new Set(consumers), authorization }
    contractEntries.push(normalized)
    const entries = contractsByPath.get(path) ?? []
    entries.push(normalized)
    contractsByPath.set(path, entries)
  }

  const validateTargets = (entry, field, kind, code) => {
    const values = entry?.[field]
    if (!Array.isArray(values) || values.length === 0) {
      fail(code, `${entry?.path ?? ''}: ${kind} must declare exact symbol-bearing targets`, { path: entry?.path })
      return null
    }
    const targets = []
    for (const value of values) {
      if (!value || typeof value !== 'object' || Array.isArray(value)) {
        fail(code, `${entry?.path ?? ''}: ${kind} targets must be objects, not bare paths`, { path: entry?.path })
        continue
      }
      const path = norm(value.path ?? '')
      const authorization = authorizationOf(value, `${kind} target ${path}`, fail)
      if (!path || PATH_GLOB.test(path) || !compiledSet.has(path) || !authorization) {
        fail(code, `${entry?.path ?? ''}: ${kind} target '${path}' must be one exact compiled path with exact symbols`, {
          path: entry?.path,
          targetPath: path,
        })
        continue
      }
      targets.push({ path, authorization })
    }
    return targets.length === values.length ? targets : null
  }

  for (const entry of registry.physical_adapters ?? []) {
    const path = validateOwnedPath(entry, 'physical adapter', false)
    const targets = validateTargets(entry, 'ports', 'physical adapter', 'invalid-physical-adapter')
    if (!path || !targets) continue
    const undeclaredTargets = targets.filter(
      (target) =>
        !(contractsByPath.get(target.path) ?? []).some(
          (contract) =>
            contract.kind === 'physical-port' &&
            contract.consumers.has(entry.owner) &&
            target.authorization.symbols.every((symbol) => authorizes(contract.authorization, symbol)) &&
            target.authorization.symbolRoots.every((root) =>
              contract.authorization.symbolRoots.some(
                (contractRoot) => root === contractRoot || root.startsWith(`${contractRoot}.`),
              ),
            ),
        ),
    )
    for (const target of undeclaredTargets)
      fail(
        'undeclared-physical-port',
        `${path} → ${target.path}: physical adapter target must be a declared physical port consumed by '${entry.owner}'`,
        { path, targetPath: target.path },
      )
  }
  for (const entry of registry.composition_roots ?? []) {
    validateOwnedPath(entry, 'composition root', false)
    validateTargets(entry, 'wires', 'composition root', 'invalid-composition-root')
  }

  const requirementOwnerEdges = []
  for (const edge of registry.requirement_dependencies ?? []) {
    if (!edge?.consumer || !edge?.provider || edge.consumer === edge.provider || !meaningful(edge.justification)) {
      fail(
        'invalid-requirement-dependency',
        `requirement dependency '${edge?.consumer ?? ''}' → '${edge?.provider ?? ''}' needs distinct packages and written justification`,
        { consumer: edge?.consumer, provider: edge?.provider },
      )
      continue
    }
    requirementOwnerEdges.push({ consumer: edge.consumer, provider: edge.provider, justification: edge.justification.trim() })
  }

  const cycleJustifications = new Map()
  for (const entry of registry.owner_cycle_justifications ?? []) {
    const owners = [...new Set(entry?.owners ?? [])].sort()
    const key = owners.join('\0')
    if (owners.length < 2 || !meaningful(entry?.justification)) {
      fail('invalid-cycle-justification', `owner cycle '${owners.join(' → ')}' needs exact members and written justification`, {
        owners,
      })
      continue
    }
    if (cycleJustifications.has(key)) {
      fail('duplicate-cycle-justification', `owner cycle justification is duplicated: ${owners.join(' → ')}`, { owners })
      continue
    }
    cycleJustifications.set(key, entry.justification.trim())
  }

  violations.sort((left, right) => `${left.code}/${left.message}`.localeCompare(`${right.code}/${right.message}`))
  return {
    ok: violations.length === 0,
    violations,
    requirementOwnerEdges,
    contracts: contractEntries.length,
  }
}

function readMigrationState(semanticOwners) {
  if (!existsSync(RELEASE_CLOSURE_NODES)) return undefined
  const closure = JSON.parse(readFileSync(RELEASE_CLOSURE_NODES, 'utf8'))
  const nodes = closure.nodes ?? []
  const nodeByPath = []
  const closedPaths = []
  for (const node of nodes)
    for (const path of node.files ?? []) {
      nodeByPath.push([norm(path), node.id])
      if (node.state === 'DONE') closedPaths.push(norm(path))
    }
  const ownerByPath = new Map((semanticOwners?.ownership ?? []).map((entry) => [norm(entry.path), entry.owner]))
  const pendingOwners = new Set(
    Object.values(closure.coverage_backlog ?? {})
      .flat()
      .map(norm)
      .map((path) => ownerByPath.get(path))
      .filter(Boolean),
  )
  const closedOwners = [...new Set(ownerByPath.values())].filter((owner) => !pendingOwners.has(owner)).sort()
  return { nodes, nodeByPath, closedPaths, closedOwners }
}

function readProductionInput() {
  const compilePaths = readCompilePaths(FSPROJ, PRODUCTION_ROOT)
  const semanticOwners = JSON.parse(readFileSync(OWNERS, 'utf8'))
  return {
    compilePaths,
    semanticOwners,
    publishedContracts: JSON.parse(readFileSync(CONTRACTS, 'utf8')),
    migrationState: readMigrationState(semanticOwners),
    requirementTrace: buildTraceGraph(join(ROOT, 'requirements')),
    repositoryRoot: ROOT,
  }
}

function runCli() {
  try {
    const result = analyzeOwnerContracts(readProductionInput())
    if (process.argv.includes('--json')) console.log(JSON.stringify(result, null, 2))
    else if (result.ok)
      console.log(
        `owner-contracts: OK — ${result.contracts} contracts, ${result.requirementOwnerEdges.length} requirement dependencies`,
      )
    else {
      console.error(`owner-contracts: ${result.violations.length} violation(s)`)
      for (const violation of result.violations) console.error(`  ${violation.code}: ${violation.message}`)
    }
    process.exitCode = result.ok ? 0 : 1
  } catch (error) {
    console.error(`owner-contracts: ${error.message}`)
    process.exitCode = 1
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) runCli()
