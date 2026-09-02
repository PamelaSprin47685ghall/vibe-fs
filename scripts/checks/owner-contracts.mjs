#!/usr/bin/env node

import { existsSync, readFileSync, statSync } from 'node:fs'
import { dirname, isAbsolute, join, relative, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..')

const FSPROJ = join(ROOT, 'src/Wanxiangshu/Wanxiangshu.fsproj')
const PRODUCTION_ROOT = join(ROOT, 'src/Wanxiangshu')
const OWNERS = join(ROOT, 'scripts/checks/semantic-owners.json')
const CONTRACTS = join(ROOT, 'scripts/checks/published-contracts.json')
const RELEASE_CLOSURE_NODES = join(ROOT, 'scripts/checks/release-closure-nodes.json')

const PATH_GLOB = /[*?\[\]]/
const PUBLICISH_PATH = /(?:Surface|Contract|Port|Api)\.fs$/
const EXECUTION_POSITION = /(?:^|[._/])(Stage|Step|Cursor|Registry|NextAction|ResumeAt)(?:$|[A-Z._/])/i

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

function stronglyConnectedComponents(nodes, edges) {
  const adjacency = new Map(nodes.map((node) => [node, []]))
  for (const { consumer, provider } of edges) adjacency.get(consumer)?.push(provider)
  const indexByNode = new Map()
  const lowLink = new Map()
  const stack = []
  const onStack = new Set()
  const components = []
  let nextIndex = 0

  const visit = (node) => {
    indexByNode.set(node, nextIndex)
    lowLink.set(node, nextIndex++)
    stack.push(node)
    onStack.add(node)

    for (const target of adjacency.get(node) ?? []) {
      if (!indexByNode.has(target)) {
        visit(target)
        lowLink.set(node, Math.min(lowLink.get(node), lowLink.get(target)))
      } else if (onStack.has(target)) lowLink.set(node, Math.min(lowLink.get(node), indexByNode.get(target)))
    }

    if (lowLink.get(node) !== indexByNode.get(node)) return
    const component = []
    let member
    do {
      member = stack.pop()
      onStack.delete(member)
      component.push(member)
    } while (member !== node)
    components.push(component.sort())
  }

  for (const node of nodes) if (!indexByNode.has(node)) visit(node)
  return components.filter((component) => component.length > 1)
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

const useKind = (use) =>
  use.isFromPattern ? 'pattern' : use.isFromType ? 'type' : use.isFromUse ? 'use' : 'symbol'

const isExecutionPosition = (edge) =>
  !(edge.symbolKind === 'FSharpUnionCase' && /(?:Rejection|Error)\.[^.]*Cursor/.test(edge.symbol)) &&
  (EXECUTION_POSITION.test(edge.providerPath) ||
    (edge.symbolKind !== 'FSharpField' && EXECUTION_POSITION.test(edge.symbol)))

const semanticEvidenceMetadata = (entry, fail) => {
  const lawMatch = /^WHAT\[([A-Z0-9]+(?:-[A-Z0-9]+)*)\]$/.exec(entry?.law ?? '')
  const proof = norm(entry?.proof ?? '')
  const proofMatch = /^requirements\/([^/]+)\/tests\/.+\.test\.mjs$/.exec(proof)
  const proofPath = resolve(ROOT, proof)
  const proofExists =
    proofMatch &&
    proof === entry.proof &&
    !isAbsolute(entry.proof) &&
    existsSync(proofPath) &&
    statSync(proofPath).isFile()
  const whatPath = proofMatch ? join(ROOT, 'requirements', proofMatch[1], 'WHAT.md') : ''
  const lawId = lawMatch?.[1]
  const normative =
    proofExists &&
    lawId &&
    existsSync(whatPath) &&
    new RegExp(`^##\\s+${lawId.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}:`, 'm').test(readFileSync(whatPath, 'utf8')) &&
    readFileSync(proofPath, 'utf8').includes(`WHAT[${lawId}]`)
  if (!normative) {
    fail(
      'invalid-semantic-evidence-metadata',
      `${entry?.path ?? ''}: semantic-evidence needs an exact normative WHAT law and existing proof that cites it`,
      { path: entry?.path },
    )
    return false
  }
  return true
}

export function analyzeOwnerContracts({
  compilePaths,
  semanticOwners,
  publishedContracts,
  symbolUses,
  migrationState,
}) {
  const violations = []
  const fail = (code, message, details = {}) => violations.push({ code, message, ...details })
  const compiled = compilePaths.map(norm)
  const compiledSet = new Set(compiled)
  if (compiledSet.size !== compiled.length) fail('duplicate-compile-entry', 'production compile set contains duplicate paths')
  if (!Array.isArray(symbolUses)) fail('missing-compiler-symbol-uses', 'owner dependency analysis requires FCS symbol-use evidence')

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
  const sourceEdgeMap = new Map()
  for (const use of Array.isArray(symbolUses) ? symbolUses : []) {
    const consumerPath = norm(use.consumerPath ?? '')
    if (!compiledSet.has(consumerPath)) {
      fail('invalid-symbol-consumer', `${consumerPath || '<missing>'}: FCS symbol consumer is outside the compile set`, { consumerPath })
      continue
    }
    if (use.isFromOpenStatement || use.isNamespace || use.isModule) continue
    if (use.missingDeclaration) {
      fail(
        'missing-symbol-declaration',
        `${consumerPath}:${use.line ?? 0}:${use.column ?? 0}: project symbol '${use.symbol ?? ''}' has no declaration location`,
        { consumerPath, symbol: use.symbol },
      )
      continue
    }
    const providers = [...new Set((use.providerPaths ?? []).map(norm))]
    const invalidProviders = providers.filter((path) => !compiledSet.has(path))
    if (invalidProviders.length > 0) {
      fail(
        'invalid-symbol-provider',
        `${consumerPath}: symbol '${use.symbol ?? ''}' resolves outside the production compile set (${invalidProviders.join(', ')})`,
        { consumerPath, providerPaths: invalidProviders },
      )
      continue
    }
    if (providers.length > 1) {
      fail(
        'ambiguous-symbol-declaration',
        `${consumerPath}: symbol '${use.symbol ?? ''}' resolves to multiple production files (${providers.join(', ')})`,
        { consumerPath, providerPaths: providers, symbol: use.symbol },
      )
      continue
    }
    if (providers.length === 0 || providers[0] === consumerPath) continue
    const providerPath = providers[0]
    const consumerOwner = ownerOf.get(consumerPath)
    const providerOwner = ownerOf.get(providerPath)
    if (!consumerOwner || !providerOwner || consumerOwner === providerOwner) continue
    const edge = {
      consumerPath,
      providerPath,
      consumerOwner,
      providerOwner,
      symbol: use.symbol ?? '',
      symbolKind: use.symbolKind ?? 'Unknown',
      line: use.line ?? 0,
      column: use.column ?? 0,
      useKind: useKind(use),
      isFromPattern: use.isFromPattern === true,
    }
    const key = `${edge.consumerPath}\0${edge.providerPath}\0${edge.symbol}\0${edge.line}\0${edge.column}\0${edge.useKind}`
    sourceEdgeMap.set(key, edge)
  }
  const sourceEdges = [...sourceEdgeMap.values()].sort((left, right) =>
    `${left.consumerPath}/${left.line}/${left.column}/${left.providerPath}/${left.symbol}`.localeCompare(
      `${right.consumerPath}/${right.line}/${right.column}/${right.providerPath}/${right.symbol}`,
    ),
  )

  const hasSymbolEvidence = Array.isArray(symbolUses) && symbolUses.length > 0

  const closedPaths = migrationState ? new Set(migrationState.closedPaths ?? []) : null
  const migrationNodeByPath = new Map(migrationState?.nodeByPath ?? [])
  const migrationNodes = new Map((migrationState?.nodes ?? []).map((node) => [node.id, node]))
  const registry = publishedContracts ?? {}
  const hasCycleEvidence = Array.isArray(registry.owner_cycle_justifications) && registry.owner_cycle_justifications.length > 0
  const contractsByPath = new Map()
  const contractEntries = []
  const adapterEntries = []
  const rootEntries = []

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
      const proofs = node.proofs
      const invalidProofs = Array.isArray(proofs)
        ? proofs.filter((proof) => {
            if (typeof proof !== 'string' || proof.length === 0 || proof !== norm(proof) || isAbsolute(proof)) return true
            if (!/^requirements\/[^/]+\/tests\/.+\.test\.mjs$/.test(proof)) return true
            const resolved = resolve(ROOT, proof)
            const repositoryRelative = norm(relative(ROOT, resolved))
            return (
              repositoryRelative === '..' ||
              repositoryRelative.startsWith('../') ||
              repositoryRelative !== proof ||
              !existsSync(resolved) ||
              !statSync(resolved).isFile()
            )
          })
        : []
      if (!Array.isArray(proofs) || proofs.length === 0 || invalidProofs.length > 0) {
        fail(
          'contract-without-proof',
          `${path}: migration node '${nodeId}' must have existing executable proofs under requirements/<package>/tests/*.test.mjs`,
          { path, invalidProofs },
        )
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
    const semanticEvidence = entry.kind !== 'semantic-evidence' || semanticEvidenceMetadata(entry, fail)
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
    if (undeclaredTargets.length === 0) adapterEntries.push({ ...entry, path, targets })
  }
  for (const entry of registry.composition_roots ?? []) {
    const path = validateOwnedPath(entry, 'composition root', false)
    const targets = validateTargets(entry, 'wires', 'composition root', 'invalid-composition-root')
    if (path && targets) rootEntries.push({ ...entry, path, targets })
  }

  const targetAllows = (entries, consumerPath, providerPath, symbol) =>
    entries.some(
      (entry) =>
        entry.path === consumerPath &&
        entry.targets.some((target) => target.path === providerPath && authorizes(target.authorization, symbol)),
    )

  const pendingEdges = []
  const strictEdges = []
  const allowedEdges = []
  for (const edge of sourceEdges) {
    if (closedPaths && !closedPaths.has(edge.providerPath)) {
      pendingEdges.push(edge)
      continue
    }
    strictEdges.push(edge)
    const entries = contractsByPath.get(edge.providerPath) ?? []
    const symbolContracts = entries.filter((entry) => authorizes(entry.authorization, edge.symbol))
    const contractEdge = symbolContracts.some((entry) => entry.consumers.has(edge.consumerOwner))
    const semanticEvidenceEdge = symbolContracts.some(
      (entry) => entry.kind === 'semantic-evidence' && entry.consumers.has(edge.consumerOwner),
    )
    const physicalPortEdge = symbolContracts.some(
      (entry) => entry.kind === 'physical-port' && entry.consumers.has(edge.consumerOwner),
    )
    const adapterEdge = targetAllows(adapterEntries, edge.consumerPath, edge.providerPath, edge.symbol)
    const rootEdge = targetAllows(rootEntries, edge.consumerPath, edge.providerPath, edge.symbol)

    if (isExecutionPosition(edge) && !semanticEvidenceEdge && !physicalPortEdge && !adapterEdge) {
      fail(
        'foreign-execution-position',
        `${edge.consumerPath}:${edge.line}:${edge.column} → ${edge.providerPath}: foreign execution-position '${edge.symbol}' is forbidden`,
        edge,
      )
      continue
    }
    if (edge.isFromPattern && rootEdge && !contractEdge) {
      fail(
        'composition-root-foreign-policy',
        `${edge.consumerPath}:${edge.line}:${edge.column} → ${edge.providerPath}: composition root matches uncontracted foreign symbol '${edge.symbol}'`,
        edge,
      )
      continue
    }
    if (!contractEdge && !adapterEdge && !rootEdge) {
      const code =
        entries.length === 0
          ? PUBLICISH_PATH.test(edge.providerPath)
            ? 'undeclared-published-contract'
            : 'cross-owner-private-import'
          : symbolContracts.length === 0
            ? 'unauthorized-contract-symbol'
            : 'unauthorized-contract-consumer'
      fail(
        code,
        `${edge.consumerPath}:${edge.line}:${edge.column} → ${edge.providerPath}: ${edge.consumerOwner} may not consume ${edge.providerOwner} symbol '${edge.symbol}'`,
        edge,
      )
      continue
    }
    allowedEdges.push({
      ...edge,
      authorizationKind: adapterEdge
        ? 'physical-adapter'
        : rootEdge
          ? 'composition-root'
          : physicalPortEdge
            ? 'physical-port'
            : 'contract',
    })
  }

  const assertAuthorizationIsLive = (authorization, edges, label, details) => {
    for (const symbol of authorization.symbols)
      if (!edges.some((edge) => edge.symbol === symbol))
        fail('stale-symbol-authorization', `${label}: exact symbol '${symbol}' has no matching compiler-resolved edge`, details)
    for (const root of authorization.symbolRoots)
      if (!edges.some((edge) => edge.symbol === root || edge.symbol.startsWith(`${root}.`)))
        fail('stale-symbol-authorization', `${label}: symbol root '${root}' has no matching compiler-resolved edge`, details)
  }

  if (hasSymbolEvidence) {
    for (const entry of contractEntries) {
      const live = strictEdges.filter(
        (edge) => edge.providerPath === entry.path && authorizes(entry.authorization, edge.symbol),
      )
      assertAuthorizationIsLive(entry.authorization, live, entry.path, { path: entry.path })
      for (const consumer of entry.consumers)
        if (!live.some((edge) => edge.consumerOwner === consumer))
          fail('stale-contract-consumer', `${entry.path}: declared consumer '${consumer}' has no matching compiler-resolved edge`, {
            path: entry.path,
            consumer,
          })
    }
  }

  if (hasSymbolEvidence) {
    for (const entry of adapterEntries)
      for (const target of entry.targets) {
        const live = strictEdges.filter(
          (edge) =>
            edge.consumerPath === entry.path && edge.providerPath === target.path && authorizes(target.authorization, edge.symbol),
        )
        assertAuthorizationIsLive(target.authorization, live, `${entry.path} → ${target.path}`, {
          path: entry.path,
          targetPath: target.path,
        })
      }
  }

  if (hasSymbolEvidence) {
    for (const entry of rootEntries)
      for (const target of entry.targets) {
        const live = strictEdges.filter(
          (edge) =>
            edge.consumerPath === entry.path && edge.providerPath === target.path && authorizes(target.authorization, edge.symbol),
        )
        assertAuthorizationIsLive(target.authorization, live, `${entry.path} → ${target.path}`, {
          path: entry.path,
          targetPath: target.path,
        })
      }

  }

  const projectOwnerEdges = (edges) => {
    const ownerEdgeMap = new Map()
    for (const edge of edges) {
      const key = `${edge.consumerOwner}\0${edge.providerOwner}`
      if (!ownerEdgeMap.has(key)) ownerEdgeMap.set(key, { consumer: edge.consumerOwner, provider: edge.providerOwner, uses: [] })
      ownerEdgeMap.get(key).uses.push({
        consumerPath: edge.consumerPath,
        providerPath: edge.providerPath,
        symbol: edge.symbol,
        line: edge.line,
        column: edge.column,
        useKind: edge.useKind,
      })
    }
    return [...ownerEdgeMap.values()].sort((left, right) =>
      `${left.consumer}/${left.provider}`.localeCompare(`${right.consumer}/${right.provider}`),
    )
  }

  const allSourceOwnerEdges = projectOwnerEdges(sourceEdges)
  const sourceOwnerEdges = projectOwnerEdges(strictEdges)

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
  const semanticContractEdges = strictEdges.filter((edge) =>
    (contractsByPath.get(edge.providerPath) ?? []).some(
      (entry) =>
        ['published-contract', 'semantic-evidence'].includes(entry.kind) &&
        entry.consumers.has(edge.consumerOwner) &&
        authorizes(entry.authorization, edge.symbol),
    ),
  )
  const cycleOwnerEdges = projectOwnerEdges(semanticContractEdges)
  const cycleOwners = [...declaredOwners]
  const cycles = stronglyConnectedComponents(cycleOwners, cycleOwnerEdges)
  if (hasSymbolEvidence || hasCycleEvidence) {
    const liveCycleKeys = new Set()
    for (const owners of cycles) {
      const key = owners.join('\0')
      liveCycleKeys.add(key)
      if (!cycleJustifications.has(key))
        fail('unjustified-owner-cycle', `owner dependency cycle lacks exact justification: ${owners.join(' → ')}`, { owners })
    }
    for (const [key] of cycleJustifications)
      if (!liveCycleKeys.has(key))
        fail('stale-cycle-justification', `cycle justification has no matching live SCC: ${key.split('\0').join(' → ')}`, {
          owners: key.split('\0'),
        })

  }

  violations.sort((left, right) => `${left.code}/${left.message}`.localeCompare(`${right.code}/${right.message}`))
  return {
    ok: violations.length === 0,
    violations,
    sourceEdges,
    pendingEdges,
    strictEdges,
    allowedEdges,
    semanticContractEdges,
    allSourceOwnerEdges,
    sourceOwnerEdges,
    cycleOwnerEdges,
    requirementOwnerEdges,
    cycles,
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
    symbolUses: [],
    migrationState: readMigrationState(semanticOwners),
  }
}

export { analyzeOwnerContracts as analyzeOwnerDependencies }

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
