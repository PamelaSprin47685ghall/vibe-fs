import assert from 'node:assert/strict'
import test from 'node:test'

import {
  capabilityFactIdV1,
  classifyCapabilityObservationV1,
  enumerateJavaScriptAstNodesV1,
  extractObservedCapabilityFactsV1,
  projectJavaScriptCapabilityObservationsV1,
  validateCapabilityPartitionV1,
  validateCapabilityDispositionV1,
  validateJavaScriptTraversalV1,
  visitJavaScriptNodeV1,
} from '../../../scripts/lib/capability-observations-v1.mjs'

const site = (ordinal = 0) => ({
  locality_id: 'fixture-contract',
  source_path: 'src/Fixture.fs',
  semantic_declaration_anchor: 'Fixture.value',
  same_anchor_occurrence_ordinal: ordinal,
})

const external = (symbol, ordinal = 0) => ({
  case: 'fcs-external-symbol-use',
  payload: { assembly: 'node', fully_qualified_symbol: symbol, site: site(ordinal) },
})

const externalFsharp = (assembly, symbol, ordinal = 0) => ({
  case: 'fcs-external-symbol-use',
  payload: { assembly, fully_qualified_symbol: symbol, site: site(ordinal) },
})

const fsharpNode = (nodeKind, semanticIdentity = `fsharp:${nodeKind}`, ordinal = 0) => ({
  case: 'fsharp-node',
  payload: { node_kind: nodeKind, semantic_identity: semanticIdentity, site: site(ordinal) },
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
  const pureNode = external('node:path/posix.join')
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

  const fileSystemNode = external('node:fs.readFileSync', 1)
  assert.deepEqual(classifyCapabilityObservationV1(fileSystemNode).payload.authorities, ['file-system'])
  assert.deepEqual(classifyCapabilityObservationV1(external('Date.now', 2)).payload.authorities, ['clock'])
  assert.deepEqual(classifyCapabilityObservationV1(external('Date.parse', 3)).payload, {
    runtimes: ['node'],
    authorities: [],
    mutable_resources: [],
    semantic_classes: ['pure-representation'],
  })
  assert.deepEqual(classifyCapabilityObservationV1(external('gpt-tokenizer/encoding/o200k_base.encode', 4)).payload, {
    runtimes: ['external-package'],
    authorities: [],
    mutable_resources: [],
    semantic_classes: ['pure-representation'],
  })
  assert.equal(classifyCapabilityObservationV1(external('gpt-tokenizer/encoding/o200k_base64.encode', 5)).case, 'unknown')
  assert.deepEqual(classifyCapabilityObservationV1(external('node:path/posixish.join', 6)).payload.authorities, ['environment'])
  assert.equal(validateCapabilityDispositionV1({
    case: 'classified',
    payload: { runtimes: ['node'], authorities: ['invented'], mutable_resources: [], semantic_classes: ['pure-representation'] },
  }), false)
  assert.equal(classifyCapabilityObservationV1({
    case: 'public-signature-export',
    payload: { export_kind: 'future-export-kind', declaration_identity: 'Fixture.future', site: site(6) },
  }).case, 'unknown')
  assert.deepEqual(classifyCapabilityObservationV1({
    case: 'public-signature-export',
    payload: { export_kind: 'pure-function', declaration_identity: 'Fixture.map', site: site(6) },
  }).payload.semantic_classes, ['pure-representation'])
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
  const forgedFact = forgedFacts.find(({ observation }) => observation.payload.fully_qualified_symbol === 'node:fs.readFileSync')
  forgedFact.disposition = structuredClone(pureDisposition)
  forgedFact.fact_id = capabilityFactIdV1(forgedFact.observation_id, forgedFact.disposition)
  assert.deepEqual(codes(validateCapabilityPartitionV1({ observations, facts: forgedFacts })), ['capability-extraction-incomplete'])

  const unknown = extractObservedCapabilityFactsV1([external('node:unclassified.dynamic', 2)])
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

test('WHAT[STRUCTURED-WORKFLOW-014] FCS-accounted F# structure closes explicitly while mutable and future syntax stay Unknown', () => {
  const compilerAccountedStructure = [
    'anon-record-get',
    'application',
    'call-with-witnesses',
    'coerce',
    'decision-tree',
    'decision-tree-success',
    'default-value',
    'fast-integer-for-loop',
    'if-then-else',
    'lambda',
    'let',
    'let-rec',
    'new-anon-record',
    'new-delegate',
    'new-record',
    'new-tuple',
    'new-union-case',
    'sequential',
    'try-finally',
    'try-with',
    'tuple-get',
    'type-lambda',
    'type-test',
    'union-case-get',
    'union-case-tag',
    'union-case-test',
    'while-loop',
  ]
  for (const [ordinal, nodeKind] of compilerAccountedStructure.entries()) {
    assert.deepEqual(classifyCapabilityObservationV1(fsharpNode(nodeKind, `fsharp:${nodeKind}`, ordinal)), {
      case: 'irrelevant',
      payload: { closed_rule_id: 'fsharp-compiler-accounted-structure' },
    })
  }

  for (const [ordinal, nodeKind] of [
    'address-of',
    'f-sharp-field-get',
    'f-sharp-field-set',
    'i-l-asm',
    'i-l-field-get',
    'new-object',
    'new-array',
    'object-expr',
    'immutable-value',
    'this-value',
    'trait-call',
    'value',
    'value-set',
    'future-expression',
  ].entries()) {
    assert.deepEqual(classifyCapabilityObservationV1(fsharpNode(nodeKind, `fsharp:${nodeKind}`, ordinal + 40)), {
      case: 'unknown',
      payload: {
        unknown_class: 'unsupported-ast',
        syntax_kind: nodeKind,
        raw_identity: `fsharp:${nodeKind}`,
      },
    })
  }

  assert.deepEqual(classifyCapabilityObservationV1(fsharpNode('application', 'System.IO.File.ReadAllText', 60)), {
    case: 'classified',
    payload: {
      runtimes: ['fsharp'],
      authorities: ['file-system'],
      mutable_resources: [],
      semantic_classes: ['capability-value'],
    },
  })

  assert.deepEqual(classifyCapabilityObservationV1(fsharpNode('pure-immutable-value', 'Fixture.count', 70)), {
    case: 'irrelevant',
    payload: { closed_rule_id: 'fsharp-pure-immutable-value' },
  })
  for (const [ordinal, nodeKind] of ['local-mutable-value-read', 'local-mutable-value-set'].entries()) {
    assert.deepEqual(classifyCapabilityObservationV1(fsharpNode(nodeKind, `Fixture.${nodeKind}`, ordinal + 70)), {
      case: 'unknown',
      payload: {
        unknown_class: 'unsupported-ast',
        syntax_kind: nodeKind,
        raw_identity: `Fixture.${nodeKind}`,
      },
    })
  }
  for (const [ordinal, nodeKind] of ['module-mutable-value-read', 'module-mutable-value-set'].entries()) {
    assert.deepEqual(classifyCapabilityObservationV1(fsharpNode(nodeKind, 'Fixture.moduleCell', ordinal + 80)), {
      case: 'classified',
      payload: {
        runtimes: ['fsharp'],
        authorities: [],
        mutable_resources: ['top-level-mutable'],
        semantic_classes: ['capability-value'],
      },
    })
  }
  for (const [ordinal, nodeKind] of [
    'captured-mutable-value-read',
    'captured-mutable-value-set',
    'mutable-container-value',
    'mutable-field-get',
    'mutable-field-set',
  ].entries()) {
    assert.deepEqual(classifyCapabilityObservationV1(fsharpNode(nodeKind, 'Fixture.MutableCell.Value', ordinal + 90)), {
      case: 'classified',
      payload: {
        runtimes: ['fsharp'],
        authorities: [],
        mutable_resources: ['runtime-cell'],
        semantic_classes: ['capability-value'],
      },
    })
  }
  assert.deepEqual(classifyCapabilityObservationV1(fsharpNode('capability-immutable-value', 'Fixture.timer', 100)), {
    case: 'classified',
    payload: {
      runtimes: ['fsharp'],
      authorities: [],
      mutable_resources: [],
      semantic_classes: ['capability-value'],
    },
  })
})

test('WHAT[STRUCTURED-WORKFLOW-014] external FCS symbols use closed semantic families rather than assembly defaults or substring guesses', () => {
  for (const [ordinal, [assembly, symbol]] of [
    ['FSharp.Core', 'Microsoft.FSharp.Collections.List.map'],
    ['FSharp.Core', 'Microsoft.FSharp.Core.LanguagePrimitives.GenericEquality'],
    ['FsToolkit.ErrorHandling', 'FsToolkit.ErrorHandling.ResultCE.Bind'],
    ['Thoth.Json', 'Thoth.Json.Decode.string'],
  ].entries()) {
    assert.deepEqual(classifyCapabilityObservationV1(externalFsharp(assembly, symbol, ordinal + 100)), {
      case: 'classified',
      payload: {
        runtimes: ['external-package'],
        authorities: [],
        mutable_resources: [],
        semantic_classes: ['pure-representation'],
      },
    })
  }

  assert.equal(classifyCapabilityObservationV1(externalFsharp(
    'FSharp.Core',
    'Microsoft.FSharp.Collections.ListModuleUnsafe.Map',
    110,
  )).case, 'unknown')
  assert.equal(classifyCapabilityObservationV1(externalFsharp(
    'FSharp.Core',
    'Microsoft.FSharp.Collections.ArrayModule.Map',
    111,
  )).case, 'unknown')
  assert.equal(classifyCapabilityObservationV1(externalFsharp(
    'FSharp.Core',
    'Microsoft.FSharp.Collections.List.toArray',
    112,
  )).case, 'unknown')
  assert.equal(classifyCapabilityObservationV1(externalFsharp(
    'FSharp.Core',
    'Future.Namespace.Member',
    113,
  )).case, 'unknown')
  assert.deepEqual(classifyCapabilityObservationV1(externalFsharp(
    'System.Runtime',
    'System.IO.File.ReadAllText',
    114,
  )).payload.authorities, ['file-system'])
  assert.equal(classifyCapabilityObservationV1(externalFsharp(
    'Example',
    'Acme.System.IOish.Parser',
    115,
  )).case, 'unknown')

  const exactAuthorities = [
    ['System.Runtime', 'System.Console.WriteLine', 'console'],
    ['System.Runtime', 'System.DateTime.UtcNow', 'clock'],
    ['System.Runtime', 'System.Diagnostics.Process.Kill', 'process-control'],
    ['System.Runtime', 'System.Environment.GetEnvironmentVariable', 'environment'],
    ['System.Runtime', 'System.Guid.NewGuid', 'randomness'],
    ['System.Runtime', 'System.Net.Http.HttpClient.SendAsync', 'network'],
    ['System.Runtime', 'System.Threading.Tasks.Task.Delay', 'timer'],
    ['Host.Runtime', 'Host.Invoke', 'host'],
  ]
  for (const [ordinal, [assembly, symbol, authority]] of exactAuthorities.entries()) {
    assert.deepEqual(
      classifyCapabilityObservationV1(externalFsharp(assembly, symbol, ordinal + 120)).payload.authorities,
      [authority],
    )
  }

  for (const [ordinal, symbol] of [
    'System.Consoleish.WriteLine',
    'System.DateTime.Nowish',
    'System.Diagnostics.Processish.Kill',
    'System.Environmental.GetEnvironmentVariable',
    'System.Randomish.Next',
    'System.Net.Http.HttpClientish.SendAsync',
    'System.Threading.Tasks.Task.Delayed',
    'Hostile.Invoke',
    'Acme.child_process_wrapper.Run',
  ].entries()) {
    assert.equal(
      classifyCapabilityObservationV1(externalFsharp('Example', symbol, ordinal + 140)).case,
      'unknown',
      `${symbol} must not inherit authority from a substring`,
    )
  }
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
