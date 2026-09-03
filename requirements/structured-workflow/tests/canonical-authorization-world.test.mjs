import assert from 'node:assert/strict'
import test from 'node:test'

import {
  canonicalDigestV1,
  encodeCanonicalJsonV1,
} from '../../../scripts/lib/canonical-json-v1.mjs'
import {
  buildCanonicalWorldV1,
  canonicalWorldDigestV1,
  classifyTerminalV1,
  deriveAdjudicationCandidates,
  queryCanonicalLocalityV1,
  serializeCanonicalWorldV1,
} from '../../../scripts/lib/locality-slice-world-v1.mjs'
import {
  extractObservedCapabilityFactsV1,
  javascriptSourceIdV1,
  javascriptTraversalIdV1,
} from '../../../scripts/lib/capability-observations-v1.mjs'

const digest = (digit) => `sha256:${digit.repeat(64)}`

const minimalWorld = () => ({
  schema_version: 1,
  fact_schema_version: 1,
  observed: {
    localities: [
      {
        id: 'consumer',
        owner: 'consumer-owner',
        kind: 'composition',
        project_path: 'src/Consumer.fsproj',
        sources: [{
          implementation_path: 'src/Consumer.fs',
          implementation_digest: digest('1'),
          signature_path: 'src/Consumer.fsi',
          signature_digest: digest('2'),
        }],
      },
      {
        id: 'provider',
        owner: 'provider-owner',
        kind: 'contract',
        project_path: 'src/Provider.fsproj',
        sources: [{
          implementation_path: 'src/Provider.fs',
          implementation_digest: digest('3'),
          signature_path: 'src/Provider.fsi',
          signature_digest: digest('4'),
        }],
      },
    ],
    project_references: [{ consumer_locality: 'consumer', provider_locality: 'provider' }],
    actual_source_edges: [{
      consumer_locality: 'consumer',
      consumer_source: 'src/Consumer.fs',
      provider_locality: 'provider',
      provider_source: 'src/Provider.fs',
    }],
    generated_artifacts: [],
    javascript_traversals: [],
    capability_extraction: extractObservedCapabilityFactsV1([]).coverage,
    capability_facts: [],
  },
  normative: {
    authorization_schema_version: 2,
    slices: [{
      id: 'provider-slice',
      owner: 'provider-owner',
      provider_locality: 'provider',
      classification: { kind: 'contract', exposure: 'shared' },
      allowed_direct_consumers: ['consumer'],
      laws: ['PROVIDER-001'],
      semantic_evidence: [{
        path: 'requirements/provider/tests/provider.test.mjs',
        title: 'WHAT[PROVIDER-001] provider law',
        what_id: 'PROVIDER-001',
        surface_module: 'ProviderSurface.js',
      }],
    }],
    capability_relations: [],
    generated_module_relations: [],
  },
})

test('WHAT[STRUCTURED-WORKFLOW-013] canonical world has one closed byte identity and one terminal classifier', () => {
  assert.equal(
    encodeCanonicalJsonV1({ '\u{10000}': 2, '\ue000': 1, '10': 10, '2': 2, composed: '\u00e9', decomposed: 'e\u0301' }),
    '{"10":10,"2":2,"composed":"\u00e9","decomposed":"e\u0301","\ue000":1,"\ud800\udc00":2}',
  )
  assert.equal(canonicalDigestV1('fixture/v1\0', { b: 2, a: 1 }), 'sha256:f950d14cbf0111132f3ba206c910b9903f8dc351dcb0d6f54b66d5ae8a117033')

  for (const invalid of [undefined, -0, 1.5, Number.NaN, Number.POSITIVE_INFINITY, 1n, new Date(0), [, 1]]) {
    assert.throws(() => encodeCanonicalJsonV1(invalid), { name: 'CanonicalJsonV1Error' })
  }
  const keyedArray = [1]
  keyedArray.extra = 2
  assert.throws(() => encodeCanonicalJsonV1(keyedArray), { code: 'canonical-json-invalid-value' })
  assert.throws(() => encodeCanonicalJsonV1({ [Symbol('hidden')]: 1 }), { code: 'canonical-json-invalid-value' })
  const accessor = {}
  Object.defineProperty(accessor, 'value', { enumerable: true, get: () => 1 })
  assert.throws(() => encodeCanonicalJsonV1(accessor), { code: 'canonical-json-invalid-value' })
  assert.throws(() => encodeCanonicalJsonV1('\ud800'), { code: 'canonical-json-unpaired-surrogate' })

  const world = buildCanonicalWorldV1(minimalWorld())
  assert.equal(classifyTerminalV1(world, 'provider').case, 'contract-shared')
  assert.equal(classifyTerminalV1(world, 'consumer').case, 'private')
  const candidates = deriveAdjudicationCandidates(world)
  assert.deepEqual(candidates.map(({ locality_id: localityId }) => localityId), ['consumer', 'provider'])
  assert.ok(candidates.find(({ locality_id: localityId }) => localityId === 'provider').reasons.includes('RelationEndpoint:slice:provider:provider-slice'))
  const providerQuery = queryCanonicalLocalityV1(world, 'provider')
  assert.deepEqual(Object.keys(providerQuery.capability).sort(), [
    'declared_kind_mismatch',
    'facts',
    'generated_artifacts',
    'javascript_traversals',
  ])
  assert.equal(providerQuery.capability.declared_kind_mismatch, false)
  assert.match(canonicalWorldDigestV1(world), /^sha256:[0-9a-f]{64}$/)
  assert.equal(Buffer.from(serializeCanonicalWorldV1(world)).at(-1), 0x7d)

  const impureInput = minimalWorld()
  const impureExtraction = extractObservedCapabilityFactsV1([{
    case: 'fcs-external-symbol-use',
    payload: {
      assembly: 'node',
      fully_qualified_symbol: 'node:fs.readFileSync',
      site: {
        locality_id: 'provider',
        source_path: 'src/Provider.fs',
        semantic_declaration_anchor: 'Provider.read',
        same_anchor_occurrence_ordinal: 0,
      },
    },
  }])
  impureInput.observed.capability_extraction = impureExtraction.coverage
  impureInput.observed.capability_facts = impureExtraction.facts
  const impureWorld = buildCanonicalWorldV1(impureInput)
  const impureProvider = deriveAdjudicationCandidates(impureWorld).find(({ locality_id: localityId }) => localityId === 'provider')
  assert.ok(impureProvider.reasons.includes('CapabilityBearing'))
  assert.ok(impureProvider.reasons.includes('KindCapabilityMismatch'))
  assert.equal(queryCanonicalLocalityV1(impureWorld, 'provider').capability.declared_kind_mismatch, true)

  const unknownField = minimalWorld()
  unknownField.observed.localities[0].current_owner = 'decoy'
  assert.throws(() => buildCanonicalWorldV1(unknownField), { code: 'canonical-world-schema' })

  const duplicateIdentity = minimalWorld()
  duplicateIdentity.observed.localities.push({ ...duplicateIdentity.observed.localities[0] })
  assert.throws(() => buildCanonicalWorldV1(duplicateIdentity), { code: 'canonical-world-duplicate-identity' })
})

test('WHAT[STRUCTURED-WORKFLOW-014] canonical world closes capability artifact and traversal references', () => {
  const observationSite = {
    locality_id: 'provider',
    source_path: 'src/Provider.fs',
    semantic_declaration_anchor: 'Provider.emit',
    same_anchor_occurrence_ordinal: 0,
  }
  const expression = 'console.error($0)'
  const sourceId = javascriptSourceIdV1(expression, observationSite)
  const traversalId = javascriptTraversalIdV1('fable-emit', sourceId)
  const emitObservation = {
    case: 'fable-emit',
    payload: { expression, javascript_traversal_id: traversalId, site: observationSite },
  }
  const emitWorld = minimalWorld()
  const emitFacts = extractObservedCapabilityFactsV1([emitObservation])
  emitWorld.observed.capability_facts = emitFacts.facts
  emitWorld.observed.capability_extraction = emitFacts.coverage
  emitWorld.observed.javascript_traversals = [{
    id: traversalId,
    source_kind: 'fable-emit',
    source_id: sourceId,
    ast_node_count: 1,
    visited_node_count: 1,
    no_capability_node_count: 1,
    capability_emitting_node_count: 0,
    unknown_node_count: 0,
    ast_node_set_digest: digest('5'),
    visit_partition_digest: digest('6'),
  }]
  assert.doesNotThrow(() => buildCanonicalWorldV1(emitWorld))

  const zeroNodeTraversal = structuredClone(emitWorld)
  zeroNodeTraversal.observed.javascript_traversals[0].ast_node_count = 0
  zeroNodeTraversal.observed.javascript_traversals[0].visited_node_count = 0
  zeroNodeTraversal.observed.javascript_traversals[0].no_capability_node_count = 0
  assert.throws(() => buildCanonicalWorldV1(zeroNodeTraversal), {
    code: 'canonical-world-schema',
    path: '$.observed.javascript_traversals[0].ast_node_count',
  })

  const javascriptObservation = {
    case: 'javascript-capability',
    payload: {
      source_kind: 'fable-emit',
      source_id: sourceId,
      generated_artifact_id: null,
      javascript_observation: { kind: 'call', root: 'console', member_path: ['error'], binding_provenance: 'free' },
      site: observationSite,
    },
  }
  const sourceWorld = structuredClone(emitWorld)
  const sourceFacts = extractObservedCapabilityFactsV1([emitObservation, javascriptObservation])
  sourceWorld.observed.capability_facts = sourceFacts.facts
  sourceWorld.observed.capability_extraction = sourceFacts.coverage
  sourceWorld.observed.javascript_traversals[0].no_capability_node_count = 0
  sourceWorld.observed.javascript_traversals[0].capability_emitting_node_count = 1
  assert.doesNotThrow(() => buildCanonicalWorldV1(sourceWorld))

  const wrongCapabilitySite = minimalWorld()
  const foreignJavaScriptObservation = structuredClone(javascriptObservation)
  foreignJavaScriptObservation.payload.site = {
    locality_id: 'consumer',
    source_path: 'src/Consumer.fs',
    semantic_declaration_anchor: 'Consumer.decoy',
    same_anchor_occurrence_ordinal: 0,
  }
  const foreignFacts = extractObservedCapabilityFactsV1([emitObservation, foreignJavaScriptObservation])
  wrongCapabilitySite.observed.capability_facts = foreignFacts.facts
  wrongCapabilitySite.observed.capability_extraction = foreignFacts.coverage
  wrongCapabilitySite.observed.javascript_traversals = structuredClone(sourceWorld.observed.javascript_traversals)
  assert.throws(() => buildCanonicalWorldV1(wrongCapabilitySite), { code: 'canonical-world-schema' })

  const missingTraversal = structuredClone(emitWorld)
  missingTraversal.observed.javascript_traversals = []
  assert.throws(() => buildCanonicalWorldV1(missingTraversal), { code: 'canonical-world-schema' })

  const wrongTraversalSource = structuredClone(emitWorld)
  wrongTraversalSource.observed.javascript_traversals[0].source_id = 'sha256:wrong'
  wrongTraversalSource.observed.javascript_traversals[0].id = javascriptTraversalIdV1('fable-emit', 'sha256:wrong')
  assert.throws(() => buildCanonicalWorldV1(wrongTraversalSource), { code: 'canonical-world-schema' })

  const danglingArtifact = minimalWorld()
  const artifactFacts = extractObservedCapabilityFactsV1([{
    case: 'javascript-capability',
    payload: {
      source_kind: 'generated-artifact',
      source_id: 'generated-artifact/v1:missing',
      generated_artifact_id: 'generated-artifact/v1:missing',
      javascript_observation: { kind: 'call', root: 'console', member_path: ['error'], binding_provenance: 'free' },
      site: observationSite,
    },
  }])
  danglingArtifact.observed.capability_facts = artifactFacts.facts
  danglingArtifact.observed.capability_extraction = artifactFacts.coverage
  assert.throws(() => buildCanonicalWorldV1(danglingArtifact), { code: 'canonical-world-schema' })

  const danglingImport = minimalWorld()
  const importFacts = extractObservedCapabilityFactsV1([{
    case: 'fable-import',
    payload: {
      module_specifier: '#generated',
      selector: 'value',
      generated_artifact_id: 'generated-artifact/v1:missing',
      site: observationSite,
    },
  }])
  danglingImport.observed.capability_facts = importFacts.facts
  danglingImport.observed.capability_extraction = importFacts.coverage
  assert.throws(() => buildCanonicalWorldV1(danglingImport), { code: 'canonical-world-schema' })
})
