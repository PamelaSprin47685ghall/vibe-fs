import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { default: fc } = await import("fast-check");
const { capabilityFactIdV1, enumerateJavaScriptAstNodesV1, extractObservedCapabilityFactsV1, validateCapabilityPartitionV1, validateJavaScriptTraversalV1, visitJavaScriptNodeV1 } = await import("../../../scripts/lib/capability-observations-v1.mjs");

const SEED = 0x43415041
const site = {
  locality_id: 'fixture-contract',
  source_path: 'src/Fixture.fs',
  semantic_declaration_anchor: 'Fixture.value',
  same_anchor_occurrence_ordinal: 0,
}
const observation = (ordinal) => ({
  case: 'fable-import',
  payload: {
    module_specifier: 'node:path/posix',
    selector: 'join',
    generated_artifact_id: null,
    site: {
      locality_id: 'fixture-contract',
      source_path: 'src/Fixture.fs',
      semantic_declaration_anchor: 'Fixture.value',
      same_anchor_occurrence_ordinal: ordinal,
    },
  },
})

test('WHAT[STRUCTURED-WORKFLOW-014] one capability or traversal mutation yields its exact violation', () => {
  fc.assert(fc.property(fc.integer({ min: 1, max: 40 }), fc.integer({ min: 0, max: 39 }), (count, rawIndex) => {
    const observations = Array.from({ length: count }, (_, index) => observation(index))
    const legal = extractObservedCapabilityFactsV1(observations)
    assert.deepEqual(legal.violations, [])
    const dispositions = legal.facts.map(({ observation_id: observationId, disposition }) => ({ observation_id: observationId, disposition }))
    const index = rawIndex % count

    const missing = validateCapabilityPartitionV1({ observations, dispositions: dispositions.filter((_, rowIndex) => rowIndex !== index) })
    assert.deepEqual(missing.violations.map(({ code }) => code), ['capability-observation-missing'])
    assert.equal(missing.violations[0].observation_id, dispositions[index].observation_id)

    const duplicate = validateCapabilityPartitionV1({ observations, dispositions: [...dispositions, dispositions[index]] })
    assert.deepEqual(duplicate.violations.map(({ code }) => code), ['capability-observation-duplicate'])
    assert.equal(duplicate.violations[0].observation_id, dispositions[index].observation_id)

    const forgedFacts = structuredClone(legal.facts)
    forgedFacts[index].disposition = {
      case: 'classified',
      payload: {
        runtimes: ['node'],
        authorities: ['file-system'],
        mutable_resources: [],
        semantic_classes: ['capability-value'],
      },
    }
    forgedFacts[index].fact_id = capabilityFactIdV1(forgedFacts[index].observation_id, forgedFacts[index].disposition)
    const forged = validateCapabilityPartitionV1({ observations, facts: forgedFacts })
    assert.deepEqual(forged.violations, [{ code: 'capability-extraction-incomplete', observation_id: forgedFacts[index].observation_id }])

    const ast = {
      type: 'Program',
      body: Array.from({ length: count }, (_, value) => ({
        type: 'ExpressionStatement',
        expression: { type: 'Literal', value },
      })),
    }
    const resolver = () => 'local'
    const nodes = enumerateJavaScriptAstNodesV1(ast, 'generated-artifact/v1:property', resolver)
    const visits = nodes.map(visitJavaScriptNodeV1)
    const nodeIndex = rawIndex % nodes.length
    const legalTraversal = validateJavaScriptTraversalV1({
      source_kind: 'generated-artifact',
      source_id: 'generated-artifact/v1:property',
      observation_site: site,
      ast,
      binding_provenance_for_node: resolver,
      visits,
      capability_facts: [],
    })
    assert.deepEqual(legalTraversal.violations, [])

    const unvisited = validateJavaScriptTraversalV1({
      source_kind: 'generated-artifact',
      source_id: 'generated-artifact/v1:property',
      observation_site: site,
      ast,
      binding_provenance_for_node: resolver,
      visits: visits.filter((_, visitIndex) => visitIndex !== nodeIndex),
      capability_facts: [],
    })
    assert.deepEqual(unvisited.violations, [{ code: 'javascript-ast-node-unvisited', node_id: nodes[nodeIndex].node_id }])

    const duplicateVisit = validateJavaScriptTraversalV1({
      source_kind: 'generated-artifact',
      source_id: 'generated-artifact/v1:property',
      observation_site: site,
      ast,
      binding_provenance_for_node: resolver,
      visits: [...visits, visits[nodeIndex]],
      capability_facts: [],
    })
    assert.deepEqual(duplicateVisit.violations, [{ code: 'javascript-ast-node-duplicate-visit', node_id: nodes[nodeIndex].node_id }])
  }), { seed: SEED, numRuns: 100 })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { capabilityFactIdV1, classifyCapabilityObservationV1, enumerateJavaScriptAstNodesV1, extractObservedCapabilityFactsV1, projectJavaScriptCapabilityObservationsV1, validateCapabilityPartitionV1, validateCapabilityDispositionV1, validateJavaScriptTraversalV1, visitJavaScriptNodeV1 } = await import("../../../scripts/lib/capability-observations-v1.mjs");

const site = (ordinal = 0) => ({
  locality_id: 'fixture-contract',
  source_path: 'src/Fixture.fs',
  semantic_declaration_anchor: 'Fixture.value',
  same_anchor_occurrence_ordinal: ordinal,
})
const pureImport = (ordinal = 0) => ({
  case: 'fable-import',
  payload: { module_specifier: 'node:path/posix', selector: 'join', generated_artifact_id: null, site: site(ordinal) },
})
const fileSystemImport = (ordinal = 1) => ({
  case: 'fable-import',
  payload: { module_specifier: 'node:fs', selector: 'readFileSync', generated_artifact_id: null, site: site(ordinal) },
})
const unknownImport = (ordinal = 2) => ({
  case: 'fable-import',
  payload: { module_specifier: 'node:unclassified.dynamic', selector: 'dynamic', generated_artifact_id: null, site: site(ordinal) },
})
const codes = (result) => result.violations.map(({ code }) => code)
const bindingRoot = (node) => {
  if (node?.type === 'Identifier') return node.name
  if (node?.type === 'MemberExpression') return bindingRoot(node.object)
  if (node?.type === 'CallExpression' || node?.type === 'NewExpression') return bindingRoot(node.callee)
  return null
}
const fixtureBindingProvenance = ({ node }) => {
  if (['ImportDeclaration', 'ImportExpression'].includes(node.type)) return 'imported'
  const root = bindingRoot(node)
  if (['console', 'Date', 'process', 'require'].includes(root)) return 'free'
  if (root === 'factory' || (['CallExpression', 'NewExpression', 'MemberExpression'].includes(node.type) && root === null)) return 'unresolved'
  return 'local'
}
const generated = (artifactId, javascriptObservation, ordinal = 0) => ({
  case: 'javascript-capability',
  payload: {
    source_kind: 'generated-artifact',
    source_id: artifactId,
    generated_artifact_id: artifactId,
    javascript_observation: javascriptObservation,
    site: site(ordinal),
  },
})

test('WHAT[STRUCTURED-WORKFLOW-014] capability observations and JavaScript traversal fail closed with exact codes', () => {
  assert.deepEqual(codes(validateCapabilityPartitionV1({ observations: [{ case: 'future-node' }] })), ['capability-extraction-incomplete'])
  for (const retired of [
    { case: 'fsharp-node', payload: { node_kind: 'application', semantic_identity: 'Fixture.value', site: site(90) } },
    { case: 'fcs-external-symbol-use', payload: { assembly: 'node', fully_qualified_symbol: 'node:fs.readFileSync', site: site(91) } },
    { case: 'public-signature-export', payload: { export_kind: 'pure-function', declaration_identity: 'Fixture.map', site: site(92) } },
  ]) {
    assert.deepEqual(codes(validateCapabilityPartitionV1({ observations: [retired] })), ['capability-extraction-incomplete'])
  }
  const pureNode = pureImport()
  const pureDisposition = classifyCapabilityObservationV1(pureNode)
  assert.deepEqual(pureDisposition, {
    case: 'classified',
    payload: {
      runtimes: ['node'],
      authorities: [],
      mutable_resources: [],
      semantic_classes: ['pure-representation'],
    },
  })

  const fileSystemNode = fileSystemImport(1)
  assert.deepEqual(classifyCapabilityObservationV1(fileSystemNode).payload.authorities, ['file-system'])
  assert.deepEqual(classifyCapabilityObservationV1(unknownImport(2)), {
    case: 'unknown',
    payload: {
      unknown_class: 'dynamic-target',
      syntax_kind: 'fable-import',
      raw_identity: 'node:unclassified.dynamic:dynamic',
    },
  })
  assert.equal(validateCapabilityDispositionV1({
    case: 'classified',
    payload: { runtimes: ['node'], authorities: ['invented'], mutable_resources: [], semantic_classes: ['pure-representation'] },
  }), false)
  const parsedEmit = {
    case: 'fable-emit',
    payload: { expression: 'console.error($0)', javascript_traversal_id: 'traversal', site: site(7) },
  }
  assert.deepEqual(classifyCapabilityObservationV1(parsedEmit), {
    case: 'irrelevant',
    payload: { closed_rule_id: 'javascript-traversal-owned' },
  })
  parsedEmit.payload.javascript_traversal_id = null
  assert.equal(classifyCapabilityObservationV1(parsedEmit).case, 'unknown')
  const observations = [pureNode, fileSystemNode]
  const extracted = extractObservedCapabilityFactsV1(observations)
  assert.deepEqual(extracted.violations, [])
  assert.equal(extracted.facts.length, 2)
  assert.deepEqual(
    validateCapabilityPartitionV1({ observations, facts: extracted.facts }),
    extracted,
    'derived and caller-supplied fact paths must produce the same canonical result',
  )

  const dispositionRows = extracted.facts.map(({ observation_id: observationId, disposition }) => ({ observation_id: observationId, disposition }))
  assert.deepEqual(codes(validateCapabilityPartitionV1({ observations, dispositions: dispositionRows.slice(1) })), ['capability-observation-missing'])
  assert.deepEqual(codes(validateCapabilityPartitionV1({ observations, dispositions: [...dispositionRows, dispositionRows[0]] })), ['capability-observation-duplicate'])

  const collidingFacts = structuredClone(extracted.facts)
  collidingFacts[1].fact_id = collidingFacts[0].fact_id
  assert.deepEqual(codes(validateCapabilityPartitionV1({ observations, facts: collidingFacts })), ['capability-fact-id-collision'])

  const forgedFacts = structuredClone(extracted.facts)
  const forgedFact = forgedFacts.find(({ observation }) => observation.payload.module_specifier === 'node:fs')
  forgedFact.disposition = structuredClone(pureDisposition)
  forgedFact.fact_id = capabilityFactIdV1(forgedFact.observation_id, forgedFact.disposition)
  assert.deepEqual(codes(validateCapabilityPartitionV1({ observations, facts: forgedFacts })), ['capability-extraction-incomplete'])

  const unknown = extractObservedCapabilityFactsV1([unknownImport(2)])
  assert.deepEqual(codes(unknown), ['unknown-capability-classification'])

  const ast = {
    type: 'Program',
    body: [{
      type: 'ExpressionStatement',
      expression: {
        type: 'CallExpression',
        callee: {
          type: 'MemberExpression',
          computed: false,
          object: { type: 'Identifier', name: 'console' },
          property: { type: 'Identifier', name: 'error' },
        },
        arguments: [{ type: 'Literal', value: 'boom' }],
      },
    }],
  }
  const nodes = enumerateJavaScriptAstNodesV1(ast, 'generated-artifact/v1:fixture', fixtureBindingProvenance)
  const visits = nodes.map(visitJavaScriptNodeV1)
  const capabilityFacts = extractObservedCapabilityFactsV1([
    generated('generated-artifact/v1:fixture', { kind: 'call', root: 'console', member_path: ['error'], binding_provenance: 'free' }),
    generated('generated-artifact/v1:fixture', { kind: 'member-read', root: 'console', member_path: ['error'], binding_provenance: 'free' }),
  ]).facts
  const legalTraversal = validateJavaScriptTraversalV1({
    source_kind: 'generated-artifact',
    source_id: 'generated-artifact/v1:fixture',
    observation_site: site(),
    ast,
    binding_provenance_for_node: fixtureBindingProvenance,
    visits,
    capability_facts: capabilityFacts,
  })
  assert.deepEqual(legalTraversal.violations, [])
  assert.equal(legalTraversal.coverage.ast_node_count, nodes.length)
  assert.deepEqual(
    legalTraversal.emitted_observation_ids,
    capabilityFacts.map(({ observation_id: observationId }) => observationId).sort(),
  )

  assert.deepEqual(codes(validateJavaScriptTraversalV1({ source_kind: 'generated-artifact', source_id: 'generated-artifact/v1:fixture', observation_site: site(), ast, binding_provenance_for_node: fixtureBindingProvenance, visits: visits.slice(1), capability_facts: capabilityFacts })), ['javascript-ast-node-unvisited'])
  assert.deepEqual(codes(validateJavaScriptTraversalV1({ source_kind: 'generated-artifact', source_id: 'generated-artifact/v1:fixture', observation_site: site(), ast, binding_provenance_for_node: fixtureBindingProvenance, visits: [...visits, visits[0]], capability_facts: capabilityFacts })), ['javascript-ast-node-duplicate-visit'])

  const unknownAst = structuredClone(ast)
  unknownAst.type = 'FutureSyntax'
  const unknownNodes = enumerateJavaScriptAstNodesV1(unknownAst, 'generated-artifact/v1:fixture', fixtureBindingProvenance)
  const unknownVisits = unknownNodes.map(visitJavaScriptNodeV1)
  assert.deepEqual(codes(validateJavaScriptTraversalV1({ source_kind: 'generated-artifact', source_id: 'generated-artifact/v1:fixture', observation_site: site(), ast: unknownAst, binding_provenance_for_node: fixtureBindingProvenance, visits: unknownVisits, capability_facts: capabilityFacts })), ['javascript-ast-node-unknown'])

  const mismatchedFacts = extractObservedCapabilityFactsV1([
    ...capabilityFacts.map(({ observation }) => observation),
    generated('generated-artifact/v1:fixture', { kind: 'call', root: 'decoy', member_path: [], binding_provenance: 'unresolved' }, 9),
  ]).facts
  assert.deepEqual(codes(validateJavaScriptTraversalV1({ source_kind: 'generated-artifact', source_id: 'generated-artifact/v1:fixture', observation_site: site(), ast, binding_provenance_for_node: fixtureBindingProvenance, visits, capability_facts: mismatchedFacts })), ['javascript-traversal-source-mismatch'])
})
test('WHAT[STRUCTURED-WORKFLOW-014] JavaScript visitor closes dynamic computed CommonJS and parameterless Date capabilities', () => {
  const sourceId = 'generated-artifact/v1:dangerous'
  const ast = {
    type: 'Program',
    body: [
      { type: 'ExpressionStatement', expression: { type: 'NewExpression', callee: { type: 'Identifier', name: 'Date' }, arguments: [] } },
      {
        type: 'ExpressionStatement',
        expression: {
          type: 'MemberExpression',
          computed: true,
          object: { type: 'Identifier', name: 'process' },
          property: { type: 'Literal', value: 'env' },
        },
      },
      {
        type: 'ExpressionStatement',
        expression: {
          type: 'CallExpression',
          callee: { type: 'Identifier', name: 'require' },
          arguments: [{ type: 'Literal', value: 'node:fs' }],
        },
      },
      {
        type: 'ExpressionStatement',
        expression: {
          type: 'CallExpression',
          callee: { type: 'CallExpression', callee: { type: 'Identifier', name: 'factory' }, arguments: [] },
          arguments: [],
        },
      },
      { type: 'ExpressionStatement', expression: { type: 'Identifier', name: 'process' } },
    ],
  }
  const nodes = enumerateJavaScriptAstNodesV1(ast, sourceId, fixtureBindingProvenance)
  const visits = nodes.map(visitJavaScriptNodeV1)
  const emitted = visits.flatMap(({ result }) => result.case === 'emitted-capability-observations' ? result.payload.observations : [])
  const expectedJavaScriptObservations = [
    { kind: 'construct', root: 'Date', member_path: [], binding_provenance: 'free' },
    { kind: 'member-read', root: 'process', member_path: ['env'], binding_provenance: 'free' },
    { kind: 'static-import', root: 'node:fs', member_path: [], binding_provenance: 'imported' },
    { kind: 'call', root: '<dynamic>', member_path: [], binding_provenance: 'unresolved' },
    { kind: 'call', root: 'factory', member_path: [], binding_provenance: 'unresolved' },
    { kind: 'free-global', root: 'process', member_path: [], binding_provenance: 'free' },
  ]
  assert.deepEqual(
    emitted.map(JSON.stringify).sort(),
    expectedJavaScriptObservations.map(JSON.stringify).sort(),
  )
  assert.ok(emitted.some((value) => value.kind === 'construct' && value.root === 'Date'))
  assert.ok(emitted.some((value) => value.kind === 'member-read' && value.root === 'process' && value.member_path[0] === 'env'))
  assert.ok(emitted.some((value) => value.kind === 'static-import' && value.root === 'node:fs'))
  assert.ok(emitted.some((value) => value.kind === 'call' && value.root === '<dynamic>'))
  assert.ok(emitted.some((value) => value.kind === 'free-global' && value.root === 'process'))

  const expected = expectedJavaScriptObservations.map((javascriptObservation) => generated(sourceId, javascriptObservation))
  const projected = projectJavaScriptCapabilityObservationsV1({
    source_kind: 'generated-artifact',
    source_id: sourceId,
    observation_site: site(),
    visits,
  })
  assert.deepEqual(projected.violations, [])
  assert.deepEqual(projected.observations.map(JSON.stringify).sort(), expected.map(JSON.stringify).sort())
  const facts = extractObservedCapabilityFactsV1(expected).facts
  const traversal = validateJavaScriptTraversalV1({ source_kind: 'generated-artifact', source_id: sourceId, observation_site: site(), ast, binding_provenance_for_node: fixtureBindingProvenance, visits, capability_facts: facts })
  assert.deepEqual(traversal.violations, [])
  assert.ok(facts.some(({ disposition }) => disposition.case === 'unknown'))
  assert.ok(facts.some(({ disposition }) => disposition.payload?.authorities?.includes('clock')))
  assert.ok(facts.some(({ disposition }) => disposition.payload?.authorities?.includes('environment')))
  assert.ok(facts.some(({ disposition }) => disposition.payload?.authorities?.includes('file-system')))
})
test('WHAT[STRUCTURED-WORKFLOW-014] JavaScript visitor uses resolved binding provenance and never guesses from a root name', () => {
  const sourceId = 'generated-artifact/v1:provenance'
  const shadowedProcessAst = {
    type: 'Program',
    body: [{
      type: 'ExpressionStatement',
      expression: {
        type: 'MemberExpression',
        computed: false,
        object: { type: 'Identifier', name: 'process' },
        property: { type: 'Identifier', name: 'env' },
      },
    }],
  }
  const localNodes = enumerateJavaScriptAstNodesV1(shadowedProcessAst, sourceId, () => 'local')
  assert.deepEqual(localNodes.map(visitJavaScriptNodeV1).flatMap(({ result }) =>
    result.case === 'emitted-capability-observations' ? result.payload.observations : []), [])

  const unresolvedCall = visitJavaScriptNodeV1({
    node_id: `${sourceId}#root`,
    node_type: 'CallExpression',
    node: { type: 'CallExpression', callee: { type: 'Identifier', name: 'process' }, arguments: [] },
    binding_provenance: 'unresolved',
  })
  assert.deepEqual(unresolvedCall.result.payload.observations, [{
    kind: 'call',
    root: 'process',
    member_path: [],
    binding_provenance: 'unresolved',
  }])
  assert.equal(extractObservedCapabilityFactsV1([
    generated(sourceId, unresolvedCall.result.payload.observations[0]),
  ]).facts[0].disposition.case, 'unknown')

  const missingProvenanceCall = visitJavaScriptNodeV1({
    node_id: `${sourceId}#missing`,
    node_type: 'CallExpression',
    node: { type: 'CallExpression', callee: { type: 'Identifier', name: 'console' }, arguments: [] },
  })
  assert.equal(missingProvenanceCall.result.payload.observations[0].binding_provenance, 'unresolved')
  assert.equal(extractObservedCapabilityFactsV1([
    generated(sourceId, missingProvenanceCall.result.payload.observations[0]),
  ]).facts[0].disposition.case, 'unknown')
})
test('WHAT[STRUCTURED-WORKFLOW-014] JavaScript traversal rejects empty AST and every open visit-result shape', () => {
  const sourceId = 'generated-artifact/v1:closed-results'
  assert.deepEqual(codes(validateJavaScriptTraversalV1({ source_kind: 'generated-artifact', source_id: sourceId, observation_site: site(), ast: {}, binding_provenance_for_node: () => 'local', visits: [], capability_facts: [] })), ['capability-extraction-incomplete'])

  const ast = { type: 'Program', body: [] }
  const nodes = enumerateJavaScriptAstNodesV1(ast, sourceId, () => 'local')
  const visits = nodes.map(visitJavaScriptNodeV1)
  for (const result of [
    { case: 'future-result', payload: {} },
    { case: 'no-capability-observation', payload: { decoy: true } },
    { case: 'emitted-capability-observations', payload: { observations: [] } },
    { case: 'emitted-capability-observations', payload: { observations: [{ kind: 'call', root: 7, member_path: [], binding_provenance: 'free' }] } },
    { case: 'emitted-capability-observations', payload: { observations: [{ kind: 'call', root: 'console', member_path: [], binding_provenance: 'future' }] } },
    { case: 'unknown-node-type', payload: {} },
  ]) {
    const malformed = structuredClone(visits)
    malformed[0].result = result
    assert.deepEqual(codes(validateJavaScriptTraversalV1({ source_kind: 'generated-artifact', source_id: sourceId, observation_site: site(), ast, binding_provenance_for_node: () => 'local', visits: malformed, capability_facts: [] })), ['capability-extraction-incomplete'])
  }
})
test('WHAT[STRUCTURED-WORKFLOW-014] traversal derives the complete node universe from AST and every array boundary is total', () => {
  const sourceId = 'generated-artifact/v1:atomic-universe'
  const ast = { type: 'Program', body: [{ type: 'ExpressionStatement', expression: { type: 'Literal', value: 1 } }] }
  const resolver = () => 'local'
  const nodes = enumerateJavaScriptAstNodesV1(ast, sourceId, resolver)
  const visits = nodes.map(visitJavaScriptNodeV1)
  const truncated = validateJavaScriptTraversalV1({
    source_kind: 'generated-artifact',
    source_id: sourceId,
    observation_site: site(),
    ast,
    binding_provenance_for_node: resolver,
    visits: visits.slice(1),
    capability_facts: [],
  })
  assert.deepEqual(truncated.violations, [{ code: 'javascript-ast-node-unvisited', node_id: `${sourceId}#root` }])

  for (const input of [
    { observations: {} },
    { observations: [], dispositions: {} },
    { observations: [], facts: {} },
    { observations: [], extraction_diagnostics: {} },
  ]) assert.deepEqual(codes(validateCapabilityPartitionV1(input)), ['capability-extraction-incomplete'])
  assert.deepEqual(codes(extractObservedCapabilityFactsV1({})), ['capability-extraction-incomplete'])
  assert.deepEqual(codes(extractObservedCapabilityFactsV1([], {})), ['capability-extraction-incomplete'])
  assert.deepEqual(codes(validateJavaScriptTraversalV1({
    source_kind: 'generated-artifact',
    source_id: sourceId,
    observation_site: site(),
    ast,
    binding_provenance_for_node: resolver,
    visits: {},
    capability_facts: [],
  })), ['capability-extraction-incomplete'])
})
}

{
const { default: assert } = await import("node:assert/strict");
const { EventEmitter } = await import("node:events");
const { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join, relative, resolve } = await import("node:path");
const { default: test } = await import("node:test");
const { checkSubsystems } = await import("../../../scripts/checks/subsystems.mjs");
const { planOwnerCompile, materializeOwnerCompile, compileOwnerProject } = await import("../../../scripts/lib/owner-compile.mjs");

const ROOT = resolve(import.meta.dirname, '../../..')
const SRC = join(ROOT, 'src/Wanxiangshu')
const FIXTURE = join(ROOT, 'requirements/structured-workflow/tests/fixtures/owner-project-boundary')
const inventory = checkSubsystems()
assert.ok(inventory.ok, inventory.violations.join('\n'))
function productionProject(projectName) {
  const project = inventory.projects.get(join(SRC, projectName))
  assert.ok(project, `missing production compile shard ${projectName}`)
  return project
}
function compileItems(projectName) {
  const project = productionProject(projectName)
  return [...project.signatureFiles, ...project.implementationFiles].map((path) => relative(SRC, path))
}
function references(projectName) {
  return productionProject(projectName).references.map((path) => relative(SRC, path))
}

test('WHAT[STRUCTURED-WORKFLOW-014] NodeFs physical port and tool contracts have isolated compiler boundaries', () => {
  const managedProject = 'Wanxiangshu.Owner.action-affordance.opencode-tools-managedagent.fsproj'
  const staticProject = 'Wanxiangshu.Owner.action-affordance.opencode-tools-statictools.fsproj'
  const nodeFsProject = 'Wanxiangshu.Owner.action-affordance.opencode-tools-nodefs.fsproj'
  assert.deepEqual(compileItems(managedProject), ['OpenCode/Tools/ManagedAgent.fsi', 'OpenCode/Tools/ManagedAgent.fs'])
  assert.deepEqual(compileItems(staticProject), ['OpenCode/Tools/StaticTools.fsi', 'OpenCode/Tools/StaticTools.fs'])
  assert.deepEqual(compileItems(nodeFsProject), ['OpenCode/Tools/NodeFs.fsi', 'OpenCode/Tools/NodeFs.fs'])

  const staticSignature = readFileSync(join(SRC, 'OpenCode/Tools/StaticTools.fsi'), 'utf8')
  const staticImplementation = readFileSync(join(SRC, 'OpenCode/Tools/StaticTools.fs'), 'utf8')
  const fileMutationImplementation = readFileSync(join(SRC, 'OpenCode/Tools/FileMutationTools.fs'), 'utf8')
  assert.doesNotMatch(staticSignature, /\bmodule NodeFs\b/)
  assert.doesNotMatch(staticImplementation, /\bmodule NodeFs\b|\[<Import\([^\n]+, "fs"\)>\]/)
  assert.doesNotMatch(fileMutationImplementation, /\bmodule private NodeFs\b|\[<Import\([^\n]+, "fs"\)>\]/)

  const nodeFsSignature = readFileSync(join(SRC, 'OpenCode/Tools/NodeFs.fsi'), 'utf8')
  assert.deepEqual(
    [...nodeFsSignature.matchAll(/^\s*val ([A-Za-z0-9_]+):/gm)].map(([, name]) => name),
    ['readFileSync', 'writeFileSync', 'existsSync', 'statSync', 'readdirSync', 'renameSync', 'rmSync', 'cpSync'],
  )

  const capabilityRefs = references('Wanxiangshu.Owner.capability-enforcement.opencode-host-managedagentconfig.fsproj')
  assert.ok(capabilityRefs.includes(managedProject))
  assert.ok(capabilityRefs.includes(staticProject))
  assert.ok(capabilityRefs.includes('Wanxiangshu.Owner.host-boundary.sphinx-host-adapter.fsproj'))

  const routingRefs = references('Wanxiangshu.Owner.execution-model-routing.opencode-host-modelroutingsurface.fsproj')
  assert.ok(routingRefs.includes(managedProject))
  assert.ok(!routingRefs.includes(staticProject))

  const actionRuntimeRefs = references('Wanxiangshu.Owner.action-affordance.runtime.fsproj')
  assert.ok(actionRuntimeRefs.includes(staticProject))
  assert.ok(actionRuntimeRefs.includes(nodeFsProject))
  assert.ok(!actionRuntimeRefs.includes(managedProject))

  // The mv/rm file-mutation tool face and the JS tool bindings are separate compile shards:
  // the interaction-side tool shard consumes the narrow NodeFs port, while the JS bindings shard
  // compiles only Repository/Programming/Js sources and does not inherit the tool-face port.
  const fileMutationProject = 'Wanxiangshu.Owner.interaction.opencode-tools-filemutationtools.fsproj'
  const jsToolBindingsProject = 'Wanxiangshu.Owner.repository-programming.repository-programming-js-toolbindings.fsproj'
  // The mv/rm tool face and its registered JS boundary travel together (same OpenCode/Tools
  // knowledge), while the JS programming surface bundle is a repository-programming shard.
  assert.deepEqual(compileItems(fileMutationProject), [
    'OpenCode/Tools/FileMutationTools.fsi',
    'OpenCode/Tools/FileMutationSurface.fsi',
    'OpenCode/Tools/FileMutationTools.fs',
    'OpenCode/Tools/FileMutationSurface.fs',
  ])
  assert.ok(
    !compileItems(fileMutationProject).some((path) => path.startsWith('Repository/Programming/Js/')),
    'the interaction tool face shard must not compile repository-programming sources',
  )
  const jsRuntimeSurfaceProject = 'Wanxiangshu.Owner.repository-programming.runtime.fsproj'
  // RuntimeSurface composes tool bindings (repository-programming) with TransactionSurface and
  // OpenCode/ToolHostSurface (cross-domain JS/runtime boundary). application-composition
  // reflects that fusion — the shard still owns only Surface files, not repository-programming runtime.
  assert.equal(productionProject(jsRuntimeSurfaceProject).subsystem, 'application-composition')
  assert.deepEqual(compileItems(jsRuntimeSurfaceProject), [
    'Repository/Programming/Js/RuntimeSurface.fsi',
    'Repository/Programming/Js/FilesystemSurface.fsi',
    'Repository/Programming/Js/TransactionSurface.fsi',
    'Repository/Programming/Js/WorkflowSurface.fsi',
    'Repository/Programming/Js/OpenCode/ToolHostSurface.fsi',
    'Repository/Programming/Js/RuntimeSurface.fs',
    'Repository/Programming/Js/FilesystemSurface.fs',
    'Repository/Programming/Js/TransactionSurface.fs',
    'Repository/Programming/Js/WorkflowSurface.fs',
    'Repository/Programming/Js/OpenCode/ToolHostSurface.fs',
  ])
  // ToolsOwnership moved TransactionStore to the js-capability shard alongside Session
  // lifecycle port work; js-toolbindings retains bindings/workflow/tool-host/generator only.
  assert.deepEqual(compileItems(jsToolBindingsProject), [
    'Repository/Programming/Js/ToolsBindings.fsi',
    'Repository/Programming/Js/OpenCode/ToolWorkflow.fsi',
    'Repository/Programming/Js/OpenCode/ToolHost.fsi',
    'Repository/Programming/Js/GeneratorSurface.fsi',
    'Repository/Programming/Js/ToolsBindings.fs',
    'Repository/Programming/Js/OpenCode/ToolWorkflow.fs',
    'Repository/Programming/Js/OpenCode/ToolHost.fs',
    'Repository/Programming/Js/GeneratorSurface.fs',
  ])

  const fileMutationRefs = references(fileMutationProject)
  assert.ok(fileMutationRefs.includes(nodeFsProject))
  assert.ok(!fileMutationRefs.includes(jsToolBindingsProject))

  const jsToolBindingsRefs = references(jsToolBindingsProject)
  assert.ok(!jsToolBindingsRefs.includes('Wanxiangshu.Owner.repository-programming.opencode-tools-filemutationtools.fsproj'))
  assert.ok(!jsToolBindingsRefs.includes(nodeFsProject))

  const ptyToolRefs = references('Wanxiangshu.Owner.process-execution.opencode-tools-ptytool.fsproj')
  assert.ok(ptyToolRefs.includes('Wanxiangshu.Owner.delegation.delegation-pty-adapter.fsproj'))

  const joinToolRefs = references('Wanxiangshu.Owner.delegation.execution-delegation-hostturnobservedsurface.fsproj')
  assert.ok(joinToolRefs.includes('Wanxiangshu.Owner.delegation.delegation-pty-adapter.fsproj'))
})
}
