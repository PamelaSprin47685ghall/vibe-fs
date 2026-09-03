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
  'javascript-capability': ['source_kind', 'source_id', 'generated_artifact_id', 'javascript_observation', 'site'],
})

const JS_KINDS = new Set(['static-import', 'dynamic-import', 'free-global', 'member-read', 'member-write', 'call', 'construct', 'mutable-binding', 'update'])
const JAVASCRIPT_BINDING_PROVENANCES = new Set(['local', 'imported', 'free', 'unresolved'])
const PUBLIC_SIGNATURE_EXPORT_KINDS = new Set([
  'pure-type',
  'pure-value',
  'pure-function',
  'capability-type',
])
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
  'ArrayExpression', 'ArrayPattern', 'ArrowFunctionExpression', 'AssignmentExpression', 'AssignmentPattern', 'AwaitExpression',
  'BinaryExpression', 'BlockStatement', 'BreakStatement', 'CallExpression', 'CatchClause',
  'ChainExpression', 'ClassBody', 'ClassDeclaration', 'ClassExpression', 'ConditionalExpression',
  'ContinueStatement', 'DebuggerStatement', 'DoWhileStatement', 'EmptyStatement',
  'ExportAllDeclaration', 'ExportDefaultDeclaration', 'ExportNamedDeclaration', 'ExportSpecifier', 'ExpressionStatement',
  'ForInStatement', 'ForOfStatement', 'ForStatement', 'FunctionDeclaration', 'FunctionExpression',
  'Identifier', 'IfStatement', 'ImportDeclaration', 'ImportDefaultSpecifier', 'ImportExpression',
  'ImportNamespaceSpecifier', 'ImportSpecifier', 'LabeledStatement', 'Literal', 'LogicalExpression',
  'MemberExpression', 'MetaProperty', 'MethodDefinition', 'NewExpression', 'ObjectExpression', 'ObjectPattern',
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

const JAVASCRIPT_SOURCE_KINDS = new Set(['fable-emit', 'emit-js-expr', 'generated-artifact'])

const javascriptObservationValid = (value) => exactKeys(value, ['kind', 'root', 'member_path', 'binding_provenance'])
  && JS_KINDS.has(value.kind)
  && nonEmptyText(value.root)
  && Array.isArray(value.member_path)
  && value.member_path.every(nonEmptyText)
  && JAVASCRIPT_BINDING_PROVENANCES.has(value.binding_provenance)

export const validateRawCapabilityObservationV1 = (observation) => {
  if (!exactKeys(observation, ['case', 'payload'])) return false
  const keys = RAW_CASE_KEYS[observation.case]
  if (!keys || !exactKeys(observation.payload, keys) || !siteValid(observation.payload.site)) return false
  const payload = observation.payload
  if (observation.case === 'fsharp-node' && ![payload.node_kind, payload.semantic_identity].every(nonEmptyText)) return false
  if (observation.case === 'fcs-external-symbol-use' && ![payload.assembly, payload.fully_qualified_symbol].every(nonEmptyText)) return false
  if (observation.case === 'fable-import' && ![payload.module_specifier, payload.selector].every(nonEmptyText)) return false
  if (['fable-emit', 'emit-js-expr'].includes(observation.case) && !nonEmptyText(payload.expression)) return false
  if (observation.case === 'public-signature-export'
    && (!PUBLIC_SIGNATURE_EXPORT_KINDS.has(payload.export_kind) || !nonEmptyText(payload.declaration_identity))) return false
  if (observation.case === 'javascript-capability') {
    if (!JAVASCRIPT_SOURCE_KINDS.has(payload.source_kind)
      || !nonEmptyText(payload.source_id)
      || !javascriptObservationValid(payload.javascript_observation)) return false
    if (payload.source_kind === 'generated-artifact') {
      if (payload.generated_artifact_id !== payload.source_id) return false
    } else if (payload.generated_artifact_id !== null) return false
  }
  if (observation.case === 'fable-import' && !(payload.generated_artifact_id === null || nonEmptyText(payload.generated_artifact_id))) return false
  if (['fable-emit', 'emit-js-expr'].includes(observation.case) && !(payload.javascript_traversal_id === null || nonEmptyText(payload.javascript_traversal_id))) return false
  return true
}

export const capabilityObservationIdV1 = (observation) => canonicalDigestV1('capability-observation/v1\0', observation)

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
    const semanticClasses = [payload.export_kind === 'capability-type' ? 'capability-type-only' : 'pure-representation']
    return classified({ runtimes: ['fsharp'], semanticClasses })
  }
  if (observation.case === 'fsharp-node') {
    const known = labelsForIdentity(payload.semantic_identity, 'fsharp')
    if (known) return known
    if (payload.node_kind === 'const') {
      return classified({ runtimes: ['fsharp'], semanticClasses: ['pure-representation'] })
    }
    return unknown('unsupported-ast', payload.node_kind, payload.semantic_identity)
  }
  if (observation.case === 'javascript-capability') {
    const { javascript_observation: javascriptObservation } = payload
    const identity = [javascriptObservation.root, ...javascriptObservation.member_path].join('.')
    if (['mutable-binding', 'update', 'member-write'].includes(javascriptObservation.kind)) {
      return classified({ runtimes: ['generated-javascript'], mutableResources: ['top-level-mutable'], semanticClasses: ['capability-value'] })
    }
    if (javascriptObservation.binding_provenance === 'unresolved') {
      return unknown('dynamic-target', javascriptObservation.kind, identity)
    }
    if (javascriptObservation.binding_provenance === 'local') {
      return { case: 'irrelevant', payload: { closed_rule_id: 'javascript-local-binding' } }
    }
    if (javascriptObservation.binding_provenance === 'free'
      && ['call', 'construct'].includes(javascriptObservation.kind)
      && identity === 'Date') {
      return classified({ runtimes: ['generated-javascript'], authorities: ['clock'], semanticClasses: ['capability-value'] })
    }
    const known = labelsForIdentity(identity, 'generated-javascript')
    if (known) {
      return classified({
        runtimes: [...known.payload.runtimes, 'generated-javascript'],
        authorities: known.payload.authorities,
        mutableResources: javascriptObservation.kind === 'mutable-binding' ? ['top-level-mutable'] : known.payload.mutable_resources,
        semanticClasses: known.payload.semantic_classes,
      })
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

const sortedViolations = (violations) => violations
  .map((row) => ({ row, encoded: encodeCanonicalJsonV1(row) }))
  .sort((left, right) => {
    const byCode = compareCanonicalTextV1(left.row.code, right.row.code)
    return byCode || compareCanonicalTextV1(left.encoded, right.encoded)
  })
  .map(({ row }) => row)

export const validateCapabilityPartitionV1 = (input = {}, {
  collectUnknownViolations = true,
  deriveDispositions = false,
} = {}) => {
  const observations = input?.observations
  const dispositions = input?.dispositions
  const facts = input?.facts
  const diagnostics = input?.extraction_diagnostics ?? []
  const violations = []
  const explicitFacts = facts !== undefined
  if (deriveDispositions && (dispositions !== undefined || explicitFacts)) {
    return { facts: [], coverage: null, violations: [violation('capability-extraction-incomplete')] }
  }
  if (!Array.isArray(observations)
    || !Array.isArray(diagnostics)
    || (dispositions !== undefined && !Array.isArray(dispositions))
    || (facts !== undefined && !Array.isArray(facts))) {
    return { facts: [], coverage: null, violations: [violation('capability-extraction-incomplete')] }
  }
  const validObservations = observations.filter(validateRawCapabilityObservationV1)
  if (diagnostics.length > 0 || validObservations.length !== observations.length) violations.push(violation('capability-extraction-incomplete'))
  const observationRows = validObservations.map((observation) => {
    const encodedObservation = encodeCanonicalJsonV1(observation)
    const expectedDisposition = classifyCapabilityObservationV1(observation)
    return {
      observation,
      observation_id: capabilityObservationIdV1(observation),
      encodedObservation,
      expectedDisposition,
      encodedExpectedDisposition: encodeCanonicalJsonV1(expectedDisposition),
    }
  })
  const observationsById = new Map()
  for (const row of observationRows) {
    const prior = observationsById.get(row.observation_id)
    if (prior && prior.encodedObservation !== row.encodedObservation) {
      violations.push(violation('capability-observation-duplicate', { observation_id: row.observation_id }))
    } else observationsById.set(row.observation_id, row)
  }

  let factRows = facts
  if (factRows === undefined) {
    const byObservation = new Map()
    const dispositionRows = deriveDispositions
      ? observationRows.map(({ observation_id: observationId, expectedDisposition: disposition }) => ({ observation_id: observationId, disposition }))
      : dispositions ?? []
    for (const row of dispositionRows) {
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
  const payloadByFact = new WeakMap()
  const expectedFactIds = new Map()
  const collidingFactIds = new Set()
  const structurallyValidFacts = []
  for (const fact of factRows ?? []) {
    const observationRow = observationsById.get(fact?.observation_id)
    const factObservation = validateRawCapabilityObservationV1(fact?.observation)
      ? encodeCanonicalJsonV1(fact.observation)
      : null
    const encodedDisposition = validateCapabilityDispositionV1(fact?.disposition)
      ? encodeCanonicalJsonV1(fact.disposition)
      : null
    if (!exactKeys(fact, ['observation_id', 'fact_id', 'observation', 'disposition'])
      || factObservation === null
      || encodedDisposition === null
      || !observationRow
      || observationRow.encodedObservation !== factObservation) {
      violations.push(violation('capability-extraction-incomplete'))
      continue
    }
    const payload = `${factObservation}\0${encodedDisposition}`
    payloadByFact.set(fact, payload)
    if (factIdPayload.has(fact.fact_id) && factIdPayload.get(fact.fact_id) !== payload) {
      violations.push(violation('capability-fact-id-collision', { fact_id: fact.fact_id }))
      collidingFactIds.add(fact.fact_id)
    } else factIdPayload.set(fact.fact_id, payload)
    const expectedFactKey = `${fact.observation_id}\0${encodedDisposition}`
    if (!expectedFactIds.has(expectedFactKey)) {
      expectedFactIds.set(expectedFactKey, capabilityFactIdV1(fact.observation_id, fact.disposition))
    }
    structurallyValidFacts.push(fact)
  }
  for (const fact of structurallyValidFacts) {
    const encodedDisposition = encodeCanonicalJsonV1(fact.disposition)
    const expectedFactId = expectedFactIds.get(`${fact.observation_id}\0${encodedDisposition}`)
    if (fact.fact_id !== expectedFactId && !collidingFactIds.has(fact.fact_id)) {
      violations.push(violation('capability-extraction-incomplete'))
    }
    if (encodedDisposition !== observationsById.get(fact.observation_id).encodedExpectedDisposition) {
      violations.push(violation('capability-extraction-incomplete', { observation_id: fact.observation_id }))
    }
  }

  const uniqueFacts = [...new Map(structurallyValidFacts.map((fact) => [
    `${fact.fact_id}\0${payloadByFact.get(fact)}`,
    fact,
  ])).values()]
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
  let unknownCount = 0
  for (const [observationId, rows] of factsByObservation) {
    if (!observationsById.has(observationId)) violations.push(violation('capability-extraction-incomplete'))
    for (const fact of rows) {
      if (fact.disposition.case === 'unknown') {
        unknownCount += 1
        if (collectUnknownViolations) violations.push(violation('unknown-capability-classification', { observation_id: fact.observation_id }))
      }
    }
  }
  if (!collectUnknownViolations && unknownCount > 0) {
    violations.push(violation('unknown-capability-classification', { count: unknownCount }))
  }
  const counts = { irrelevant: 0, classified: 0, unknown: 0 }
  for (const fact of uniqueFacts) if (Object.hasOwn(counts, fact.disposition.case)) counts[fact.disposition.case] += 1
  const coverage = {
    capability_observation_count: observationsById.size,
    irrelevant_count: counts.irrelevant,
    classified_count: counts.classified,
    unknown_count: counts.unknown,
    capability_observation_digest: canonicalDigestV1(
      'capability-observations/v1\0',
      [...observationsById.entries()]
        .sort(([left], [right]) => compareCanonicalTextV1(left, right))
        .map(([, { observation }]) => observation),
    ),
    disposition_digest: canonicalDigestV1('capability-dispositions/v1\0', uniqueFacts),
  }
  return { facts: uniqueFacts, coverage, violations: sortedViolations(violations) }
}

export const extractObservedCapabilityFactsV1 = (observations, extractionDiagnostics = [], options = {}) => {
  if (!Array.isArray(observations) || !Array.isArray(extractionDiagnostics)) {
    return { facts: [], coverage: null, violations: [violation('capability-extraction-incomplete')] }
  }
  return validateCapabilityPartitionV1(
    { observations, extraction_diagnostics: extractionDiagnostics },
    { ...options, deriveDispositions: true },
  )
}

const childNodes = (node) => Object.keys(node)
  .filter((key) => !['type', 'start', 'end', 'loc', 'range', 'raw'].includes(key))
  .sort(compareCanonicalTextV1)
  .flatMap((key) => {
    const value = node[key]
    if (Array.isArray(value)) return value.filter((item) => item?.type).map((item, index) => ({ node: item, segment: `${key}[${index}]` }))
    return value?.type ? [{ node: value, segment: key }] : []
  })

const closedBindingProvenance = (value) => JAVASCRIPT_BINDING_PROVENANCES.has(value) ? value : 'unresolved'

export const enumerateJavaScriptAstNodesV1 = (ast, sourceId, bindingProvenanceForNode) => {
  if (typeof sourceId !== 'string' || sourceId.length === 0 || !ast?.type) return []
  const rows = []
  const visit = (node, path) => {
    const nodeId = `${sourceId}#${path}`
    const scope = typeof bindingProvenanceForNode === 'function'
      ? bindingProvenanceForNode({ node_id: nodeId, node_type: node.type, node })
      : 'unresolved'
    const bindingProvenance = typeof scope === 'string' ? scope : scope?.binding_provenance
    const programScope = typeof scope === 'object' && scope?.program_scope === true
    rows.push({
      node_id: nodeId,
      node_type: node.type,
      node: structuredClone(node),
      binding_provenance: closedBindingProvenance(bindingProvenance),
      program_scope: programScope,
    })
    for (const child of childNodes(node)) visit(child.node, `${path}/${child.segment}`)
  }
  visit(ast, 'root')
  return rows
}

export const javascriptTraversalIdV1 = (sourceKind, sourceId) =>
  canonicalDigestV1('javascript-traversal/v1\0', { source_kind: sourceKind, source_id: sourceId })

export const javascriptSourceIdV1 = (expression, site) =>
  canonicalDigestV1('javascript-source/v1\0', { expression, site })

const memberIdentity = (node) => {
  if (node?.type === 'Identifier') return { root: node.name, member_path: [] }
  if (node?.type !== 'MemberExpression') return null
  const parent = memberIdentity(node.object)
  const member = node.computed
    ? node.property?.type === 'Literal' && typeof node.property.value === 'string' && node.property.value.length > 0
      ? node.property.value
      : '<computed>'
    : node.property?.type === 'Identifier' ? node.property.name : null
  return parent && member ? { root: parent.root, member_path: [...parent.member_path, member] } : null
}

const emittedResult = (row, observations) => ({
  node_id: row.node_id,
  result: {
    case: 'emitted-capability-observations',
    payload: {
      observations: [...observations].sort((left, right) => compareCanonicalTextV1(encodeCanonicalJsonV1(left), encodeCanonicalJsonV1(right))),
    },
  },
})

const noCapabilityResult = (row) => ({ node_id: row.node_id, result: { case: 'no-capability-observation', payload: {} } })

const dynamicIdentity = { root: '<dynamic>', member_path: [] }
const identifierOwnedByParent = (nodeId) => /\/(callee|exported|id|imported|key|label|local|object|param|params\[\d+\])$/.test(nodeId)

export const visitJavaScriptNodeV1 = (row) => {
  const node = row.node
  const bindingProvenance = closedBindingProvenance(row.binding_provenance)
  const observed = (kind, identity, provenance = bindingProvenance) => ({
    kind,
    ...identity,
    binding_provenance: provenance,
  })
  if (!KNOWN_NODE_TYPES.has(node.type)) return { node_id: row.node_id, result: { case: 'unknown-node-type', payload: { node_type: node.type } } }
  if (node.type === 'ImportDeclaration') {
    const root = typeof node.source?.value === 'string' && node.source.value.length > 0 ? node.source.value : '<dynamic>'
    return emittedResult(row, [observed('static-import', { root, member_path: [] }, root === '<dynamic>' ? 'unresolved' : 'imported')])
  }
  if (node.type === 'ImportExpression') {
    const root = typeof node.source?.value === 'string' && node.source.value.length > 0 ? node.source.value : '<dynamic>'
    return emittedResult(row, [observed('dynamic-import', { root, member_path: [] }, root === '<dynamic>' ? 'unresolved' : 'imported')])
  }
  if ((node.type === 'ExportAllDeclaration' || node.type === 'ExportNamedDeclaration') && node.source !== null) {
    const root = typeof node.source?.value === 'string' && node.source.value.length > 0 ? node.source.value : '<dynamic>'
    return emittedResult(row, [observed('static-import', { root, member_path: [] }, root === '<dynamic>' ? 'unresolved' : 'imported')])
  }
  if (node.type === 'Identifier' && !identifierOwnedByParent(row.node_id) && bindingProvenance !== 'local') {
    return emittedResult(row, [observed(bindingProvenance === 'free' ? 'free-global' : 'member-read', { root: node.name, member_path: [] })])
  }
  if (node.type === 'VariableDeclaration' && node.kind !== 'const' && row.program_scope === true) {
    const observations = (node.declarations ?? []).map((declaration) =>
      observed('mutable-binding', { root: declaration.id?.name ?? '<dynamic>', member_path: [] }, 'local'))
    return observations.length > 0 ? emittedResult(row, observations) : noCapabilityResult(row)
  }
  if (node.type === 'UpdateExpression') {
    if (bindingProvenance === 'local' && row.program_scope !== true) return noCapabilityResult(row)
    const identity = memberIdentity(node.argument) ?? dynamicIdentity
    return emittedResult(row, [observed('update', identity)])
  }
  if (node.type === 'AssignmentExpression') {
    if (bindingProvenance === 'local' && row.program_scope !== true) return noCapabilityResult(row)
    return emittedResult(row, [observed('member-write', memberIdentity(node.left) ?? dynamicIdentity)])
  }
  if (node.type === 'MemberExpression') {
    const identity = memberIdentity(node)
    return bindingProvenance === 'local'
      ? noCapabilityResult(row)
      : emittedResult(row, [observed('member-read', identity ?? dynamicIdentity)])
  }
  if (node.type === 'CallExpression' || node.type === 'NewExpression') {
    const identity = memberIdentity(node.callee)
    if (bindingProvenance === 'local') return noCapabilityResult(row)
    if (node.type === 'CallExpression'
      && bindingProvenance === 'free'
      && identity?.root === 'require'
      && identity.member_path.length === 0) {
      const specifier = node.arguments?.length === 1 && node.arguments[0]?.type === 'Literal' && typeof node.arguments[0].value === 'string'
        ? node.arguments[0].value
        : '<dynamic>'
      return emittedResult(row, [observed(
        specifier === '<dynamic>' ? 'dynamic-import' : 'static-import',
        { root: specifier, member_path: [] },
        specifier === '<dynamic>' ? 'unresolved' : 'imported',
      )])
    }
    if (node.type === 'NewExpression'
      && bindingProvenance === 'free'
      && identity?.root === 'Date'
      && identity.member_path.length === 0
      && (node.arguments?.length ?? 0) > 0) {
      return emittedResult(row, [observed('construct', { root: 'new Date(epoch)', member_path: [] })])
    }
    return emittedResult(row, [observed(node.type === 'CallExpression' ? 'call' : 'construct', identity ?? dynamicIdentity)])
  }
  return noCapabilityResult(row)
}

const visitResultValid = (result) => {
  if (!exactKeys(result, ['case', 'payload'])) return false
  if (result.case === 'no-capability-observation') return exactKeys(result.payload, [])
  if (result.case === 'unknown-node-type') return exactKeys(result.payload, ['node_type']) && nonEmptyText(result.payload.node_type)
  return result.case === 'emitted-capability-observations'
    && exactKeys(result.payload, ['observations'])
    && Array.isArray(result.payload.observations)
    && result.payload.observations.length > 0
    && result.payload.observations.every(javascriptObservationValid)
    && result.payload.observations.every((observation, index) => index === 0
      || compareCanonicalTextV1(encodeCanonicalJsonV1(result.payload.observations[index - 1]), encodeCanonicalJsonV1(observation)) < 0)
}

const compareAstNodeIdV1 = (left, right) => {
  const leftSegments = left.slice(left.indexOf('#') + 1).split('/')
  const rightSegments = right.slice(right.indexOf('#') + 1).split('/')
  for (let index = 0; index < Math.min(leftSegments.length, rightSegments.length); index += 1) {
    const leftMatch = /^(.*)\[(\d+)\]$/.exec(leftSegments[index])
    const rightMatch = /^(.*)\[(\d+)\]$/.exec(rightSegments[index])
    const byKey = compareCanonicalTextV1(leftMatch?.[1] ?? leftSegments[index], rightMatch?.[1] ?? rightSegments[index])
    if (byKey !== 0) return byKey
    if (leftMatch && rightMatch && Number(leftMatch[2]) !== Number(rightMatch[2])) return Number(leftMatch[2]) - Number(rightMatch[2])
    if (Boolean(leftMatch) !== Boolean(rightMatch)) return leftMatch ? 1 : -1
  }
  return leftSegments.length - rightSegments.length
}

const canonicalJavaScriptObservationsV1 = (sourceKind, sourceId, observationSite, visits) => {
  const occurrences = new Map()
  return [...visits]
    .sort((left, right) => compareAstNodeIdV1(left.node_id, right.node_id))
    .flatMap(({ result }) => result.case === 'emitted-capability-observations' ? result.payload.observations : [])
    .map((javascriptObservation) => {
      const identity = encodeCanonicalJsonV1(javascriptObservation)
      const ordinal = occurrences.get(identity) ?? 0
      occurrences.set(identity, ordinal + 1)
      return {
        case: 'javascript-capability',
        payload: {
          source_kind: sourceKind,
          source_id: sourceId,
          generated_artifact_id: sourceKind === 'generated-artifact' ? sourceId : null,
          javascript_observation: javascriptObservation,
          site: { ...observationSite, same_anchor_occurrence_ordinal: observationSite.same_anchor_occurrence_ordinal + ordinal },
        },
      }
    })
}

export const projectJavaScriptCapabilityObservationsV1 = ({ source_kind: sourceKind, source_id: sourceId, observation_site: observationSite, visits }) => {
  if (!JAVASCRIPT_SOURCE_KINDS.has(sourceKind)
    || !nonEmptyText(sourceId)
    || !siteValid(observationSite)
    || !Array.isArray(visits)
    || !visits.every((row) => exactKeys(row, ['node_id', 'result']) && nonEmptyText(row.node_id) && visitResultValid(row.result))) {
    return { observations: [], violations: [violation('capability-extraction-incomplete', { source_id: sourceId })] }
  }
  return { observations: canonicalJavaScriptObservationsV1(sourceKind, sourceId, observationSite, visits), violations: [] }
}

export const validateJavaScriptTraversalV1 = (input = {}) => {
  const sourceKind = input?.source_kind
  const sourceId = input?.source_id
  const observationSite = input?.observation_site
  const ast = input?.ast
  const bindingProvenanceForNode = input?.binding_provenance_for_node
  const visits = input?.visits
  const capabilityFacts = input?.capability_facts
  const inputShapeValid = exactKeys(input, [
    'source_kind',
    'source_id',
    'observation_site',
    'ast',
    'binding_provenance_for_node',
    'visits',
    'capability_facts',
  ])
  const inputBoundaryValid = inputShapeValid
    && JAVASCRIPT_SOURCE_KINDS.has(sourceKind)
    && nonEmptyText(sourceId)
    && siteValid(observationSite)
    && ast !== null
    && typeof ast === 'object'
    && !Array.isArray(ast)
    && nonEmptyText(ast.type)
    && typeof bindingProvenanceForNode === 'function'
    && Array.isArray(visits)
    && Array.isArray(capabilityFacts)
  if (!inputBoundaryValid) {
    return { coverage: null, emitted_observation_ids: [], violations: [violation('capability-extraction-incomplete', { source_id: sourceId })] }
  }
  let nodes
  try {
    nodes = enumerateJavaScriptAstNodesV1(ast, sourceId, bindingProvenanceForNode)
  } catch {
    return { coverage: null, emitted_observation_ids: [], violations: [violation('capability-extraction-incomplete', { source_id: sourceId })] }
  }
  const violations = []
  let incomplete = false
  const visitRows = visits
  const factRows = capabilityFacts
  const nodeCounts = new Map()
  for (const row of nodes) {
    const valid = exactKeys(row, ['node_id', 'node_type', 'node', 'binding_provenance', 'program_scope'])
      && nonEmptyText(row.node_id)
      && nonEmptyText(row.node_type)
      && row.node !== null
      && typeof row.node === 'object'
      && !Array.isArray(row.node)
      && row.node.type === row.node_type
      && JAVASCRIPT_BINDING_PROVENANCES.has(row.binding_provenance)
      && typeof row.program_scope === 'boolean'
      && row.node_id.startsWith(`${sourceId}#`)
    if (!valid) incomplete = true
    if (nonEmptyText(row?.node_id)) nodeCounts.set(row.node_id, (nodeCounts.get(row.node_id) ?? 0) + 1)
  }
  const visitCounts = new Map()
  for (const row of visitRows) {
    const valid = exactKeys(row, ['node_id', 'result']) && nonEmptyText(row.node_id) && visitResultValid(row.result)
    if (!valid) incomplete = true
    if (nonEmptyText(row?.node_id)) visitCounts.set(row.node_id, (visitCounts.get(row.node_id) ?? 0) + 1)
  }
  if (nodeCounts.size === 0) incomplete = true
  for (const [nodeId, count] of nodeCounts) if (count > 1) violations.push(violation('javascript-ast-node-duplicate-visit', { node_id: nodeId }))
  for (const [nodeId, count] of visitCounts) if (count > 1) violations.push(violation('javascript-ast-node-duplicate-visit', { node_id: nodeId }))
  for (const nodeId of nodeCounts.keys()) if (!visitCounts.has(nodeId)) violations.push(violation('javascript-ast-node-unvisited', { node_id: nodeId }))
  for (const nodeId of visitCounts.keys()) if (!nodeCounts.has(nodeId)) violations.push(violation('javascript-traversal-stale', { node_id: nodeId }))

  const uniqueVisits = [...new Map(visitRows.filter((row) => nonEmptyText(row?.node_id)).map((row) => [row.node_id, row])).values()]
  const validVisits = uniqueVisits.filter((row) => visitResultValid(row.result))
  for (const row of validVisits) if (row.result.case === 'unknown-node-type') violations.push(violation('javascript-ast-node-unknown', { node_id: row.node_id, node_type: row.result.payload.node_type }))

  let sourceMismatch = false
  const nodesById = new Map(nodes.filter((row) => nonEmptyText(row?.node_id)).map((row) => [row.node_id, row]))
  for (const row of validVisits) {
    const node = nodesById.get(row.node_id)
    if (node && encodeCanonicalJsonV1(row) !== encodeCanonicalJsonV1(visitJavaScriptNodeV1(node))) sourceMismatch = true
  }
  const validFacts = factRows.filter(validateCanonicalCapabilityFactV1)
  if (validFacts.length !== factRows.length) incomplete = true
  let emittedObservationIds = []
  if (!incomplete) {
    emittedObservationIds = projectJavaScriptCapabilityObservationsV1({
      source_kind: sourceKind,
      source_id: sourceId,
      observation_site: observationSite,
      visits: validVisits,
    }).observations
      .map(capabilityObservationIdV1)
      .sort(compareCanonicalTextV1)
    const expected = validFacts
      .filter(({ observation }) => observation.case === 'javascript-capability'
        && observation.payload.source_kind === sourceKind
        && observation.payload.source_id === sourceId)
      .map(({ observation_id: observationId }) => observationId)
      .sort(compareCanonicalTextV1)
    if (encodeCanonicalJsonV1(emittedObservationIds) !== encodeCanonicalJsonV1(expected)) sourceMismatch = true
  }
  if (sourceMismatch && !incomplete) violations.push(violation('javascript-traversal-source-mismatch', { source_id: sourceId }))
  if (incomplete) violations.push(violation('capability-extraction-incomplete', { source_id: sourceId }))

  const counts = {
    noCapability: validVisits.filter((row) => row.result.case === 'no-capability-observation').length,
    capability: validVisits.filter((row) => row.result.case === 'emitted-capability-observations').length,
    unknown: validVisits.filter((row) => row.result.case === 'unknown-node-type').length,
  }
  const coverage = {
    id: javascriptTraversalIdV1(sourceKind, sourceId),
    source_kind: sourceKind,
    source_id: sourceId,
    ast_node_count: nodeCounts.size,
    visited_node_count: validVisits.length,
    no_capability_node_count: counts.noCapability,
    capability_emitting_node_count: counts.capability,
    unknown_node_count: counts.unknown,
    ast_node_set_digest: canonicalDigestV1('javascript-ast-nodes/v1\0', [...nodeCounts.keys()].sort(compareCanonicalTextV1)),
    visit_partition_digest: canonicalDigestV1('javascript-visit-partition/v1\0', [...validVisits].sort((left, right) => compareCanonicalTextV1(left.node_id, right.node_id)).map(({ node_id: nodeId, result }) => ({ node_id: nodeId, result }))),
  }
  return { coverage, emitted_observation_ids: emittedObservationIds, violations: sortedViolations(violations) }
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
  && encodeCanonicalJsonV1(fact.disposition) === encodeCanonicalJsonV1(classifyCapabilityObservationV1(fact.observation))
