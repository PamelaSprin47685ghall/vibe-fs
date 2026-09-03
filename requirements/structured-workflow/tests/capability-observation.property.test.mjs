import assert from 'node:assert/strict'
import test from 'node:test'
import fc from 'fast-check'

import {
  enumerateJavaScriptAstNodesV1,
  extractObservedCapabilityFactsV1,
  validateCapabilityPartitionV1,
  validateJavaScriptTraversalV1,
  visitJavaScriptNodeV1,
} from '../../../scripts/lib/capability-observations-v1.mjs'

const SEED = 0x43415041

const observation = (ordinal) => ({
  case: 'fcs-external-symbol-use',
  payload: {
    assembly: 'node',
    fully_qualified_symbol: 'node:path/posix.join',
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

    const ast = {
      type: 'Program',
      body: Array.from({ length: count }, (_, value) => ({
        type: 'ExpressionStatement',
        expression: { type: 'Literal', value },
      })),
    }
    const nodes = enumerateJavaScriptAstNodesV1(ast, 'generated-artifact/v1:property')
    const visits = nodes.map(visitJavaScriptNodeV1)
    const nodeIndex = rawIndex % nodes.length
    const legalTraversal = validateJavaScriptTraversalV1({
      source_kind: 'generated-artifact',
      source_id: 'generated-artifact/v1:property',
      nodes,
      visits,
      capability_observation_ids: [],
    })
    assert.deepEqual(legalTraversal.violations, [])

    const unvisited = validateJavaScriptTraversalV1({
      source_kind: 'generated-artifact',
      source_id: 'generated-artifact/v1:property',
      nodes,
      visits: visits.filter((_, visitIndex) => visitIndex !== nodeIndex),
      capability_observation_ids: [],
    })
    assert.deepEqual(unvisited.violations, [{ code: 'javascript-ast-node-unvisited', node_id: nodes[nodeIndex].node_id }])

    const duplicateVisit = validateJavaScriptTraversalV1({
      source_kind: 'generated-artifact',
      source_id: 'generated-artifact/v1:property',
      nodes,
      visits: [...visits, visits[nodeIndex]],
      capability_observation_ids: [],
    })
    assert.deepEqual(duplicateVisit.violations, [{ code: 'javascript-ast-node-duplicate-visit', node_id: nodes[nodeIndex].node_id }])
  }), { seed: SEED, numRuns: 100 })
})
