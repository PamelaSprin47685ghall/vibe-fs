import assert from 'node:assert/strict'
import test from 'node:test'

import {
  capabilityObservationIdV1,
  classifyCapabilityObservationV1,
  enumerateJavaScriptAstNodesV1,
  extractObservedCapabilityFactsV1,
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

const codes = (result) => result.violations.map(({ code }) => code)

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

  const dispositionRows = extracted.facts.map(({ observation_id: observationId, disposition }) => ({ observation_id: observationId, disposition }))
  assert.deepEqual(codes(validateCapabilityPartitionV1({ observations, dispositions: dispositionRows.slice(1) })), ['capability-observation-missing'])
  assert.deepEqual(codes(validateCapabilityPartitionV1({ observations, dispositions: [...dispositionRows, dispositionRows[0]] })), ['capability-observation-duplicate'])

  const collidingFacts = structuredClone(extracted.facts)
  collidingFacts[1].fact_id = collidingFacts[0].fact_id
  assert.deepEqual(codes(validateCapabilityPartitionV1({ observations, facts: collidingFacts })), ['capability-fact-id-collision'])

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
  const nodes = enumerateJavaScriptAstNodesV1(ast, 'generated-artifact/v1:fixture')
  const visits = nodes.map(visitJavaScriptNodeV1)
  const emitted = visits.flatMap((visit) => visit.result.case === 'emitted-capability-observations'
    ? visit.result.payload.observation_ids
    : [])
  const legalTraversal = validateJavaScriptTraversalV1({
    source_kind: 'generated-artifact',
    source_id: 'generated-artifact/v1:fixture',
    nodes,
    visits,
    capability_observation_ids: emitted,
  })
  assert.deepEqual(legalTraversal.violations, [])
  assert.equal(legalTraversal.coverage.ast_node_count, nodes.length)

  assert.deepEqual(codes(validateJavaScriptTraversalV1({ source_kind: 'generated-artifact', source_id: 'generated-artifact/v1:fixture', nodes, visits: visits.slice(1), capability_observation_ids: emitted })), ['javascript-ast-node-unvisited'])
  assert.deepEqual(codes(validateJavaScriptTraversalV1({ source_kind: 'generated-artifact', source_id: 'generated-artifact/v1:fixture', nodes, visits: [...visits, visits[0]], capability_observation_ids: emitted })), ['javascript-ast-node-duplicate-visit'])

  const unknownNodes = structuredClone(nodes)
  unknownNodes[0].node.type = 'FutureSyntax'
  const unknownVisits = unknownNodes.map(visitJavaScriptNodeV1)
  assert.deepEqual(codes(validateJavaScriptTraversalV1({ source_kind: 'generated-artifact', source_id: 'generated-artifact/v1:fixture', nodes: unknownNodes, visits: unknownVisits, capability_observation_ids: emitted })), ['javascript-ast-node-unknown'])

  assert.deepEqual(codes(validateJavaScriptTraversalV1({ source_kind: 'generated-artifact', source_id: 'generated-artifact/v1:fixture', nodes, visits, capability_observation_ids: [...emitted, capabilityObservationIdV1({ kind: 'call', root: 'decoy', member_path: [] }, site(9))] })), ['javascript-traversal-source-mismatch'])
})
