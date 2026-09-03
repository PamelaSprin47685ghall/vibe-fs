import {
  assertRepositoryPathV1,
  canonicalDigestV1,
  compareCanonicalTextV1,
  encodeCanonicalJsonV1,
} from './canonical-json-v1.mjs'

const RAW_CASE_KEYS = Object.freeze({
  'fsharp-node': ['node_kind', 'semantic_identity', 'site'],
  'fcs-external-symbol-use': ['assembly', 'fully_qualified_symbol', 'site'],
  'fable-import': ['module_specifier', 'selector', 'generated_artifact_id', 'site'],
  'fable-emit': ['expression', 'javascript_traversal_id', 'site'],
  'emit-js-expr': ['expression', 'javascript_traversal_id', 'site'],
  'public-signature-export': ['export_kind', 'declaration_identity', 'site'],
  'generated-javascript': ['generated_artifact_id', 'javascript_observation', 'site'],
})

const JS_KINDS = new Set(['static-import', 'dynamic-import', 'free-global', 'member-read', 'member-write', 'call', 'construct', 'mutable-binding', 'update'])
const LABEL_KEYS = ['runtimes', 'authorities', 'mutable_resources', 'semantic_classes']
const LABEL_VALUES = Object.freeze({
  runtimes: new Set(['fsharp', 'node', 'bun', 'browser', 'generated-javascript', 'external-package']),
  authorities: new Set(['console', 'process-control', 'environment', 'file-system', 'network', 'clock', 'randomness', 'timer', 'git', 'provider', 'host']),
  mutable_resources: new Set(['top-level-mutable', 'registry', 'waiter', 'task-completion-source', 'runtime-cell']),
  semantic_classes: new Set(['pure-representation', 'capability-type-only', 'capability-value', 'capability-factory', 'effect-constructor']),
})
const UNKNOWN_CLASSES = new Set([
  'unsupported-ast',
  'unparsed-interop',
  'dynamic-target',
  'unclassified-external-symbol',
  'unclassified-capability',
  'incomplete-generated-linkage',
])
const KNOWN_NODE_TYPES = new Set([
  'ArrayExpression', 'ArrowFunctionExpression', 'AssignmentExpression', 'AwaitExpression',
  'BinaryExpression', 'BlockStatement', 'BreakStatement', 'CallExpression', 'CatchClause',
  'ChainExpression', 'ClassBody', 'ClassDeclaration', 'ClassExpression', 'ConditionalExpression',
  'ContinueStatement', 'DebuggerStatement', 'DoWhileStatement', 'EmptyStatement',
  'ExportAllDeclaration', 'ExportDefaultDeclaration', 'ExportNamedDeclaration', 'ExpressionStatement',
  'ForInStatement', 'ForOfStatement', 'ForStatement', 'FunctionDeclaration', 'FunctionExpression',
  'Identifier', 'IfStatement', 'ImportDeclaration', 'ImportDefaultSpecifier', 'ImportExpression',
  'ImportNamespaceSpecifier', 'ImportSpecifier', 'LabeledStatement', 'Literal', 'LogicalExpression',
  'MemberExpression', 'MetaProperty', 'MethodDefinition', 'NewExpression', 'ObjectExpression',
  'PrivateIdentifier', 'Program', 'Property', 'PropertyDefinition', 'RestElement', 'ReturnStatement',
  'SequenceExpression', 'SpreadElement', 'StaticBlock', 'Super', 'SwitchCase', 'SwitchStatement',
  'TaggedTemplateExpression', 'TemplateElement', 'TemplateLiteral', 'ThisExpression', 'ThrowStatement',
  'TryStatement', 'UnaryExpression', 'UpdateExpression', 'VariableDeclaration', 'VariableDeclarator',
  'WhileStatement', 'WithStatement', 'YieldExpression',
])

const exactKeys = (value, keys) => {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) return false
  const actual = Object.keys(value).sort(compareCanonicalTextV1)
  const expected = [...keys].sort(compareCanonicalTextV1)
  return actual.length === expected.length && actual.every((key, index) => key === expected[index])
}

const siteValid = (site) => {
  if (!exactKeys(site, ['locality_id', 'source_path', 'semantic_declaration_anchor', 'same_anchor_occurrence_ordinal'])
    || [site.locality_id, site.semantic_declaration_anchor].some((value) => typeof value !== 'string' || value.length === 0)
    || !Number.isSafeInteger(site.same_anchor_occurrence_ordinal)
    || site.same_anchor_occurrence_ordinal < 0) return false
  try {
    assertRepositoryPathV1(site.source_path, '$.observation.site.source_path')
    return true
  } catch {
    return false
  }
}

const nonEmptyText = (value) => typeof value === 'string' && value.length > 0

const javascriptObservationValid = (value) => exactKeys(value, ['kind', 'root', 'member_path'])
  && JS_KINDS.has(value.kind)
  && typeof value.root === 'string'
  && Array.isArray(value.member_path)
  && value.member_path.every((member) => typeof member === 'string')

export const validateRawCapabilityObservationV1 = (observation) => {
  if (!exactKeys(observation, ['case', 'payload'])) return false
  const keys = RAW_CASE_KEYS[observation.case]
  if (!keys || !exactKeys(observation.payload, keys) || !siteValid(observation.payload.site)) return false
  const payload = observation.payload
  if (observation.case === 'fsharp-node' && ![payload.node_kind, payload.semantic_identity].every(nonEmptyText)) return false
  if (observation.case === 'fcs-external-symbol-use' && ![payload.assembly, payload.fully_qualified_symbol].every(nonEmptyText)) return false
  if (observation.case === 'fable-import' && ![payload.module_specifier, payload.selector].every(nonEmptyText)) return false
  if (['fable-emit', 'emit-js-expr'].includes(observation.case) && !nonEmptyText(payload.expression)) return false
  if (observation.case === 'public-signature-export' && ![payload.export_kind, payload.declaration_identity].every(nonEmptyText)) return false
  if (observation.case === 'generated-javascript' && !javascriptObservationValid(observation.payload.javascript_observation)) return false
  if (observation.case === 'generated-javascript' && !nonEmptyText(payload.generated_artifact_id)) return false
  if (observation.case === 'fable-import' && !(payload.generated_artifact_id === null || nonEmptyText(payload.generated_artifact_id))) return false
  if (['fable-emit', 'emit-js-expr'].includes(observation.case) && !(payload.javascript_traversal_id === null || nonEmptyText(payload.javascript_traversal_id))) return false
  return true
}

export const capabilityObservationIdV1 = (observation, site) => canonicalDigestV1('capability-observation/v1\0',
  observation?.case === undefined
    ? { case: 'generated-javascript', payload: { generated_artifact_id: 'fixture', javascript_observation: observation, site } }
    : observation)

export const capabilityFactIdV1 = (observationId, disposition) => canonicalDigestV1('capability-fact/v1\0', {
  observation_id: observationId,
  disposition,
})

const uniqueLabels = (values) => [...new Set(values)].sort(compareCanonicalTextV1)

const classified = ({ runtimes = [], authorities = [], mutableResources = [], semanticClasses = [] }) => ({
  case: 'classified',
  payload: {
    runtimes: uniqueLabels(runtimes),
    authorities: uniqueLabels(authorities),
    mutable_resources: uniqueLabels(mutableResources),
    semantic_classes: uniqueLabels(semanticClasses),
  },
})

const unknown = (unknownClass, syntaxKind, rawIdentity) => ({
  case: 'unknown',
  payload: {
    unknown_class: unknownClass,
    syntax_kind: syntaxKind,
    raw_identity: rawIdentity,
  },
})

const identityIsOrExtends = (identity, owner, separators = '.') => identity === owner
  || (identity.length > owner.length
    && identity.slice(0, owner.length) === owner
    && separators.includes(identity[owner.length]))

const labelsForIdentity = (identity, runtime = 'fsharp') => {
  const lower = identity.toLowerCase()
  if (lower === 'date.parse' || lower.includes('date.parse(') || lower.includes('new date(epoch)')) {
    return classified({ runtimes: [runtime], semanticClasses: ['pure-representation'] })
  }
  if (identityIsOrExtends(lower, 'gpt-tokenizer/encoding/o200k_base')) {
    return classified({ runtimes: ['external-package'], semanticClasses: ['pure-representation'] })
  }
  if (identityIsOrExtends(lower, 'node:path/posix')) {
    return classified({ runtimes: ['node'], semanticClasses: ['pure-representation'] })
  }
  if (identityIsOrExtends(lower, 'node:path', './')) {
    return classified({ runtimes: ['node'], authorities: ['environment'], semanticClasses: ['capability-value'] })
  }
  if (lower.includes('node:fs') || lower.startsWith('fs.') || lower.includes('system.io')) {
    return classified({ runtimes: [runtime === 'fsharp' ? 'fsharp' : 'node'], authorities: ['file-system'], semanticClasses: ['capability-value'] })
  }
  if (lower.includes('child_process') || lower.includes('process.kill') || lower.includes('process.exit') || lower.includes('process.pid')) {
    return classified({ runtimes: ['node'], authorities: ['process-control'], semanticClasses: ['capability-value'] })
  }
  if (lower.includes('process.env') || lower.includes('getenvironmentvariable') || lower.includes('process.cwd') || lower.includes('process.platform')) {
    return classified({ runtimes: [runtime], authorities: ['environment'], semanticClasses: ['capability-value'] })
  }
  if (lower.startsWith('console.') || lower.includes('system.console')) {
    return classified({ runtimes: [runtime], authorities: ['console'], semanticClasses: ['capability-value'] })
  }
  if (lower.includes('date.now') || lower.includes('datetime.now') || lower.includes('datetime.utcnow') || lower.includes('datetimeoffset.utcnow') || lower.includes('performance.now')) {
    return classified({ runtimes: [runtime], authorities: ['clock'], semanticClasses: ['capability-value'] })
  }
  if (lower.includes('math.random') || lower.includes('guid.newguid') || lower.includes('system.random') || lower.includes('randomuuid')) {
    return classified({ runtimes: [runtime], authorities: ['randomness'], semanticClasses: ['capability-value'] })
  }
  if (lower.includes('settimeout') || lower.includes('cleartimeout') || lower.includes('setinterval') || lower.includes('clearinterval') || lower.includes('task.delay')) {
    return classified({ runtimes: [runtime], authorities: ['timer'], semanticClasses: ['capability-value'] })
  }
  if (lower === 'fetch' || lower.startsWith('node:http') || lower.startsWith('node:https') || lower.startsWith('node:net')) {
    return classified({ runtimes: [runtime], authorities: ['network'], semanticClasses: ['capability-value'] })
  }
  if (lower === 'host' || lower.startsWith('host.')) {
    return classified({ runtimes: [runtime], authorities: ['host'], semanticClasses: ['capability-value'] })
  }
  return null
}

export const classifyCapabilityObservationV1 = (observation) => {
  if (!validateRawCapabilityObservationV1(observation)) {
    let rawIdentity
    try {
      rawIdentity = encodeCanonicalJsonV1(observation)
    } catch {
      rawIdentity = Object.prototype.toString.call(observation)
    }
    return unknown('unsupported-ast', 'invalid-observation', rawIdentity)
  }
  const payload = observation.payload
  if (observation.case === 'fcs-external-symbol-use') {
    return labelsForIdentity(payload.fully_qualified_symbol, payload.assembly === 'node' ? 'node' : 'external-package')
      ?? unknown('unclassified-external-symbol', observation.case, `${payload.assembly}:${payload.fully_qualified_symbol}`)
  }
  if (observation.case === 'fable-import') {
    if (payload.generated_artifact_id !== null) return classified({ runtimes: ['generated-javascript'], semanticClasses: ['pure-representation'] })
    return labelsForIdentity(payload.module_specifier, 'node')
      ?? unknown('dynamic-target', observation.case, `${payload.module_specifier}:${payload.selector}`)
  }
  if (observation.case === 'public-signature-export') {
    const semanticClasses = payload.export_kind === 'capability-type'
      ? ['capability-type-only']
      : ['pure-representation']
    return classified({ runtimes: ['fsharp'], semanticClasses })
  }
  if (observation.case === 'fsharp-node') {
    const known = labelsForIdentity(payload.semantic_identity, 'fsharp')
    if (known) return known
    if (['constant', 'record', 'union-case', 'pure-call', 'binding'].includes(payload.node_kind)) {
      return classified({ runtimes: ['fsharp'], semanticClasses: ['pure-representation'] })
    }
    return unknown('unsupported-ast', payload.node_kind, payload.semantic_identity)
  }
  if (observation.case === 'generated-javascript') {
    const { javascript_observation: javascriptObservation } = payload
    const identity = [javascriptObservation.root, ...javascriptObservation.member_path].join('.')
    const known = labelsForIdentity(identity, 'generated-javascript')
    if (known) {
      return classified({
        runtimes: [...known.payload.runtimes, 'generated-javascript'],
        authorities: known.payload.authorities,
        mutableResources: javascriptObservation.kind === 'mutable-binding' ? ['top-level-mutable'] : known.payload.mutable_resources,
        semanticClasses: known.payload.semantic_classes,
      })
    }
    if (javascriptObservation.kind === 'mutable-binding' || javascriptObservation.kind === 'update' || javascriptObservation.kind === 'member-write') {
      return classified({ runtimes: ['generated-javascript'], mutableResources: ['top-level-mutable'], semanticClasses: ['capability-value'] })
    }
    return unknown('unclassified-capability', javascriptObservation.kind, identity)
  }
  if (['fable-emit', 'emit-js-expr'].includes(observation.case)) {
    return payload.javascript_traversal_id === null
      ? unknown('unparsed-interop', observation.case, payload.expression)
      : { case: 'irrelevant', payload: { closed_rule_id: 'javascript-traversal-owned' } }
  }
  return unknown('unsupported-ast', observation.case, encodeCanonicalJsonV1(observation))
}

export const validateCapabilityDispositionV1 = (disposition) => {
  if (!exactKeys(disposition, ['case', 'payload'])) return false
  if (disposition.case === 'classified') {
    return exactKeys(disposition.payload, LABEL_KEYS)
      && LABEL_KEYS.every((key) => Array.isArray(disposition.payload[key])
        && disposition.payload[key].every((value) => typeof value === 'string' && LABEL_VALUES[key].has(value))
        && disposition.payload[key].every((value, index) => index === 0 || compareCanonicalTextV1(disposition.payload[key][index - 1], value) < 0))
      && LABEL_KEYS.some((key) => disposition.payload[key].length > 0)
  }
  if (disposition.case === 'irrelevant') return exactKeys(disposition.payload, ['closed_rule_id']) && nonEmptyText(disposition.payload.closed_rule_id)
  if (disposition.case === 'unknown') return exactKeys(disposition.payload, ['unknown_class', 'syntax_kind', 'raw_identity'])
    && UNKNOWN_CLASSES.has(disposition.payload.unknown_class)
    && [disposition.payload.syntax_kind, disposition.payload.raw_identity].every(nonEmptyText)
  return false
}

const violation = (code, coordinates = {}) => ({ code, ...coordinates })

const sortedViolations = (violations) => violations.sort((left, right) => {
  const byCode = compareCanonicalTextV1(left.code, right.code)
  return byCode || compareCanonicalTextV1(encodeCanonicalJsonV1(left), encodeCanonicalJsonV1(right))
})

export const validateCapabilityPartitionV1 = ({ observations, dispositions, facts, extraction_diagnostics: diagnostics = [] }) => {
  const violations = []
  const explicitFacts = facts !== undefined
  if (!Array.isArray(observations) || !Array.isArray(diagnostics)) return { facts: [], coverage: null, violations: [violation('capability-extraction-incomplete')] }
  const validObservations = observations.filter(validateRawCapabilityObservationV1)
  if (diagnostics.length > 0 || validObservations.length !== observations.length) violations.push(violation('capability-extraction-incomplete'))
  const observationRows = validObservations.map((observation) => ({ observation, observation_id: capabilityObservationIdV1(observation) }))
  const observationsById = new Map()
  for (const row of observationRows) {
    const prior = observationsById.get(row.observation_id)
    if (prior && encodeCanonicalJsonV1(prior.observation) !== encodeCanonicalJsonV1(row.observation)) {
      violations.push(violation('capability-observation-duplicate', { observation_id: row.observation_id }))
    } else observationsById.set(row.observation_id, row)
  }

  let factRows = facts
  if (factRows === undefined) {
    const byObservation = new Map()
    for (const row of dispositions ?? []) {
      if (!exactKeys(row, ['observation_id', 'disposition']) || !validateCapabilityDispositionV1(row.disposition)) {
        violations.push(violation('capability-extraction-incomplete'))
        continue
      }
      if (!byObservation.has(row.observation_id)) byObservation.set(row.observation_id, [])
      byObservation.get(row.observation_id).push(row.disposition)
    }
    for (const observationId of byObservation.keys()) {
      if (!observationsById.has(observationId)) violations.push(violation('capability-extraction-incomplete'))
    }
    factRows = []
    for (const [observationId, row] of [...observationsById].sort(([left], [right]) => compareCanonicalTextV1(left, right))) {
      const matches = byObservation.get(observationId) ?? []
      if (matches.length === 0) violations.push(violation('capability-observation-missing', { observation_id: observationId }))
      if (matches.length > 1) violations.push(violation('capability-observation-duplicate', { observation_id: observationId }))
      if (matches.length === 1) factRows.push({
        observation_id: observationId,
        fact_id: capabilityFactIdV1(observationId, matches[0]),
        observation: row.observation,
        disposition: matches[0],
      })
    }
  }

  const factIdPayload = new Map()
  const collidingFactIds = new Set()
  const structurallyValidFacts = []
  for (const fact of factRows ?? []) {
    if (!exactKeys(fact, ['observation_id', 'fact_id', 'observation', 'disposition'])
      || !validateRawCapabilityObservationV1(fact.observation)
      || !validateCapabilityDispositionV1(fact.disposition)
      || fact.observation_id !== capabilityObservationIdV1(fact.observation)) {
      violations.push(violation('capability-extraction-incomplete'))
      continue
    }
    const payload = encodeCanonicalJsonV1(fact)
    if (factIdPayload.has(fact.fact_id) && factIdPayload.get(fact.fact_id) !== payload) {
      violations.push(violation('capability-fact-id-collision', { fact_id: fact.fact_id }))
      collidingFactIds.add(fact.fact_id)
    } else factIdPayload.set(fact.fact_id, payload)
    structurallyValidFacts.push(fact)
  }
  for (const fact of structurallyValidFacts) {
    if (fact.fact_id !== capabilityFactIdV1(fact.observation_id, fact.disposition) && !collidingFactIds.has(fact.fact_id)) {
      violations.push(violation('capability-extraction-incomplete'))
    }
  }

  const uniqueFacts = [...new Map(structurallyValidFacts.map((fact) => [encodeCanonicalJsonV1(fact), fact])).values()]
    .sort((left, right) => compareCanonicalTextV1(`${left.observation_id}\0${left.fact_id}`, `${right.observation_id}\0${right.fact_id}`))
  const factsByObservation = new Map()
  for (const fact of uniqueFacts) {
    if (!factsByObservation.has(fact.observation_id)) factsByObservation.set(fact.observation_id, [])
    factsByObservation.get(fact.observation_id).push(fact)
  }
  if (explicitFacts) {
    for (const observationId of observationsById.keys()) {
      const matches = factsByObservation.get(observationId) ?? []
      if (matches.length === 0) violations.push(violation('capability-observation-missing', { observation_id: observationId }))
      if (matches.length > 1) violations.push(violation('capability-observation-duplicate', { observation_id: observationId }))
    }
  }
  for (const [observationId, rows] of factsByObservation) {
    if (!observationsById.has(observationId)) violations.push(violation('capability-extraction-incomplete'))
    for (const fact of rows) {
      if (fact.disposition.case === 'unknown') violations.push(violation('unknown-capability-classification', { observation_id: fact.observation_id }))
    }
  }
  const counts = { irrelevant: 0, classified: 0, unknown: 0 }
  for (const fact of uniqueFacts) if (Object.hasOwn(counts, fact.disposition.case)) counts[fact.disposition.case] += 1
  const coverage = {
    capability_observation_count: observationsById.size,
    irrelevant_count: counts.irrelevant,
    classified_count: counts.classified,
    unknown_count: counts.unknown,
    capability_observation_digest: canonicalDigestV1('capability-observations/v1\0', [...observationsById.values()].map(({ observation }) => observation).sort((left, right) => compareCanonicalTextV1(capabilityObservationIdV1(left), capabilityObservationIdV1(right)))),
    disposition_digest: canonicalDigestV1('capability-dispositions/v1\0', uniqueFacts),
  }
  return { facts: uniqueFacts, coverage, violations: sortedViolations(violations) }
}

export const extractObservedCapabilityFactsV1 = (observations, extractionDiagnostics = []) => {
  const dispositions = observations.map((observation) => ({
    observation_id: capabilityObservationIdV1(observation),
    disposition: classifyCapabilityObservationV1(observation),
  }))
  return validateCapabilityPartitionV1({ observations, dispositions, extraction_diagnostics: extractionDiagnostics })
}

const childNodes = (node) => Object.keys(node)
  .filter((key) => !['type', 'start', 'end', 'loc', 'range', 'raw'].includes(key))
  .sort(compareCanonicalTextV1)
  .flatMap((key) => {
    const value = node[key]
    if (Array.isArray(value)) return value.filter((item) => item?.type).map((item, index) => ({ node: item, segment: `${key}[${index}]` }))
    return value?.type ? [{ node: value, segment: key }] : []
  })

export const enumerateJavaScriptAstNodesV1 = (ast, sourceId) => {
  if (typeof sourceId !== 'string' || sourceId.length === 0 || !ast?.type) return []
  const rows = []
  const visit = (node, path) => {
    const nodeId = `${sourceId}#${path}`
    rows.push({ node_id: nodeId, node_type: node.type, node: structuredClone(node) })
    for (const child of childNodes(node)) visit(child.node, `${path}/${child.segment}`)
  }
  visit(ast, 'root')
  return rows
}

export const javascriptTraversalIdV1 = (sourceKind, sourceId) =>
  canonicalDigestV1('javascript-traversal/v1\0', { source_kind: sourceKind, source_id: sourceId })

const memberIdentity = (node) => {
  if (node?.type === 'Identifier') return { root: node.name, member_path: [] }
  if (node?.type !== 'MemberExpression' || node.computed) return null
  const parent = memberIdentity(node.object)
  const member = node.property?.type === 'Identifier' ? node.property.name : null
  return parent && member ? { root: parent.root, member_path: [...parent.member_path, member] } : null
}

const jsObservationId = (row, observation) => canonicalDigestV1('javascript-capability-observation/v1\0', {
  node_id: row.node_id,
  observation,
})

const emittedResult = (row, observations) => ({
  node_id: row.node_id,
  result: {
    case: 'emitted-capability-observations',
    payload: { observation_ids: observations.map((observation) => jsObservationId(row, observation)).sort(compareCanonicalTextV1) },
  },
})

export const visitJavaScriptNodeV1 = (row) => {
  const node = row.node
  if (!KNOWN_NODE_TYPES.has(node.type)) return { node_id: row.node_id, result: { case: 'unknown-node-type', payload: { node_type: node.type } } }
  if (node.type === 'ImportDeclaration') {
    return emittedResult(row, [{ kind: 'static-import', root: String(node.source?.value ?? ''), member_path: [] }])
  }
  if (node.type === 'ImportExpression') {
    return emittedResult(row, [{ kind: 'dynamic-import', root: String(node.source?.value ?? ''), member_path: [] }])
  }
  if (node.type === 'VariableDeclaration' && node.kind !== 'const') {
    return emittedResult(row, (node.declarations ?? []).map((declaration) => ({
      kind: 'mutable-binding',
      root: declaration.id?.name ?? '',
      member_path: [],
    })))
  }
  if (node.type === 'UpdateExpression') {
    const identity = memberIdentity(node.argument) ?? { root: '', member_path: [] }
    return emittedResult(row, [{ kind: 'update', ...identity }])
  }
  if (node.type === 'AssignmentExpression') {
    const identity = memberIdentity(node.left)
    if (identity) return emittedResult(row, [{ kind: 'member-write', ...identity }])
  }
  if (node.type === 'MemberExpression') {
    const identity = memberIdentity(node)
    if (identity && labelsForIdentity([identity.root, ...identity.member_path].join('.'), 'generated-javascript')) {
      return emittedResult(row, [{ kind: 'member-read', ...identity }])
    }
  }
  if (node.type === 'CallExpression' || node.type === 'NewExpression') {
    const identity = memberIdentity(node.callee)
    if (identity && labelsForIdentity([identity.root, ...identity.member_path].join('.'), 'generated-javascript')) {
      return emittedResult(row, [{ kind: node.type === 'CallExpression' ? 'call' : 'construct', ...identity }])
    }
  }
  return { node_id: row.node_id, result: { case: 'no-capability-observation', payload: {} } }
}

export const validateJavaScriptTraversalV1 = ({ source_kind: sourceKind, source_id: sourceId, nodes, visits, capability_observation_ids: capabilityObservationIds }) => {
  const violations = []
  const nodeCounts = new Map()
  for (const row of nodes ?? []) nodeCounts.set(row.node_id, (nodeCounts.get(row.node_id) ?? 0) + 1)
  const visitCounts = new Map()
  for (const row of visits ?? []) visitCounts.set(row.node_id, (visitCounts.get(row.node_id) ?? 0) + 1)
  for (const [nodeId, count] of nodeCounts) if (count > 1) violations.push(violation('javascript-ast-node-duplicate-visit', { node_id: nodeId }))
  for (const [nodeId, count] of visitCounts) if (count > 1) violations.push(violation('javascript-ast-node-duplicate-visit', { node_id: nodeId }))
  for (const nodeId of nodeCounts.keys()) if (!visitCounts.has(nodeId)) violations.push(violation('javascript-ast-node-unvisited', { node_id: nodeId }))
  for (const nodeId of visitCounts.keys()) if (!nodeCounts.has(nodeId)) violations.push(violation('javascript-traversal-stale', { node_id: nodeId }))

  const uniqueVisits = [...new Map((visits ?? []).map((row) => [row.node_id, row])).values()]
  for (const row of uniqueVisits) if (row.result?.case === 'unknown-node-type') violations.push(violation('javascript-ast-node-unknown', { node_id: row.node_id, node_type: row.result.payload?.node_type }))
  const emitted = uniqueVisits.flatMap((row) => row.result?.case === 'emitted-capability-observations' ? row.result.payload.observation_ids : []).sort(compareCanonicalTextV1)
  const expected = [...(capabilityObservationIds ?? [])].sort(compareCanonicalTextV1)
  if (encodeCanonicalJsonV1(emitted) !== encodeCanonicalJsonV1(expected)) violations.push(violation('javascript-traversal-source-mismatch', { source_id: sourceId }))

  const counts = {
    noCapability: uniqueVisits.filter((row) => row.result?.case === 'no-capability-observation').length,
    capability: uniqueVisits.filter((row) => row.result?.case === 'emitted-capability-observations').length,
    unknown: uniqueVisits.filter((row) => row.result?.case === 'unknown-node-type').length,
  }
  const coverage = {
    id: javascriptTraversalIdV1(sourceKind, sourceId),
    source_kind: sourceKind,
    source_id: sourceId,
    ast_node_count: nodeCounts.size,
    visited_node_count: uniqueVisits.length,
    no_capability_node_count: counts.noCapability,
    capability_emitting_node_count: counts.capability,
    unknown_node_count: counts.unknown,
    ast_node_set_digest: canonicalDigestV1('javascript-ast-nodes/v1\0', [...nodeCounts.keys()].sort(compareCanonicalTextV1)),
    visit_partition_digest: canonicalDigestV1('javascript-visit-partition/v1\0', uniqueVisits.sort((left, right) => compareCanonicalTextV1(left.node_id, right.node_id)).map(({ node_id: nodeId, result }) => ({ node_id: nodeId, result }))),
  }
  return { coverage, violations: sortedViolations(violations) }
}

export const capabilityDispositionViolatesContractV1 = (disposition) => disposition.case === 'unknown'
  || (disposition.case === 'classified' && (
    disposition.payload.authorities.length > 0
    || disposition.payload.mutable_resources.length > 0
    || disposition.payload.semantic_classes.some((semanticClass) => ['capability-value', 'capability-factory', 'effect-constructor'].includes(semanticClass))
  ))

export const validateCanonicalCapabilityFactV1 = (fact) => exactKeys(fact, ['observation_id', 'fact_id', 'observation', 'disposition'])
  && validateRawCapabilityObservationV1(fact.observation)
  && validateCapabilityDispositionV1(fact.disposition)
  && fact.observation_id === capabilityObservationIdV1(fact.observation)
  && fact.fact_id === capabilityFactIdV1(fact.observation_id, fact.disposition)
