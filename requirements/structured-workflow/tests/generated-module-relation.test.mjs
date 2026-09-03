import assert from 'node:assert/strict'
import test from 'node:test'

import {
  buildGeneratedArtifactRowV1,
  createTrackingReaderV1,
  generatedArtifactIdV1,
  readSelectedInputsV1,
  selectedInputsDigestV1,
  validateGeneratedModuleRelationV1,
} from '../../../scripts/lib/generated-artifact-v1.mjs'
import {
  extractObservedCapabilityFactsV1,
  javascriptTraversalIdV1,
} from '../../../scripts/lib/capability-observations-v1.mjs'

const linkage = {
  import_specifier: '#fixture-generated',
  package_import_target: './dist/Fixture.js',
  generator_path: 'scripts/generate-fixture.mjs',
  generator_entry: 'writeFixture',
  input_selector_path: 'scripts/select-fixture-inputs.mjs',
  input_selector_entry: 'fixtureInputFiles',
  build_path: 'scripts/build.mjs',
  build_entry: 'verifyArtifacts',
}

const relation = {
  id: 'fixture-generated',
  kind: 'compile-contract-support',
  consumer_locality: 'fixture-consumer',
  import_specifier: linkage.import_specifier,
  generated_owner: 'fixture-owner',
  package_import_target: linkage.package_import_target,
  generator: { path: linkage.generator_path, entry: linkage.generator_entry },
  build_invocation: { path: linkage.build_path, entry: linkage.build_entry },
  input_selector: { path: linkage.input_selector_path, entry: linkage.input_selector_entry },
  runtime_surface_module: 'FixtureSurface.js',
  laws: ['FIXTURE-001'],
  determinism_proof: {
    path: 'requirements/fixture/tests/generated.test.mjs',
    title: 'WHAT[FIXTURE-001] generated fixture is deterministic',
    what_id: 'FIXTURE-001',
  },
  justification: 'Fixture deterministic artifact.',
}

const site = (ordinal) => ({
  locality_id: 'fixture-consumer',
  source_path: 'src/FixtureConsumer.fs',
  semantic_declaration_anchor: 'FixtureConsumer.value',
  same_anchor_occurrence_ordinal: ordinal,
})

const capabilityFactsFor = (artifactId, javascriptObservations = [{ kind: 'static-import', root: 'node:path/posix', member_path: [], binding_provenance: 'imported' }]) =>
  extractObservedCapabilityFactsV1([
    {
      case: 'fable-import',
      payload: {
        module_specifier: '#fixture-generated',
        selector: 'name',
        generated_artifact_id: artifactId,
        site: site(0),
      },
    },
    ...javascriptObservations.map((javascriptObservation, index) => ({
      case: 'javascript-capability',
      payload: {
        source_kind: 'generated-artifact',
        source_id: artifactId,
        generated_artifact_id: artifactId,
        javascript_observation: javascriptObservation,
        site: site(index + 1),
      },
    })),
  ]).facts

const legalFixture = () => {
  const selected = new Map([
    ['requirements/fixture/WHAT.md', Buffer.from('law\n')],
    ['src/Fixture.fs', Buffer.from('module Fixture\n')],
  ])
  const reader = createTrackingReaderV1({
    readFile: (path) => selected.get(path),
  })
  const inputs = readSelectedInputsV1([...selected.keys()].reverse(), reader)
  const artifactBytes = Buffer.from('import path from "node:path/posix"\nexport const name = path.join("a", "b")\n')
  const artifact = buildGeneratedArtifactRowV1({
    artifact_path: 'dist/Fixture.js',
    artifact_bytes: artifactBytes,
    selected_inputs: inputs,
    linkage,
  })
  const traversal = {
    id: artifact.javascript_traversal_id,
    source_kind: 'generated-artifact',
    source_id: artifact.id,
    ast_node_count: 8,
    visited_node_count: 8,
    no_capability_node_count: 7,
    capability_emitting_node_count: 1,
    unknown_node_count: 0,
    ast_node_set_digest: `sha256:${'1'.repeat(64)}`,
    visit_partition_digest: `sha256:${'2'.repeat(64)}`,
  }
  const capabilityFacts = capabilityFactsFor(artifact.id)
  return {
    relations: [structuredClone(relation)],
    artifacts: [artifact],
    actual_imports: [{
      consumer_locality: 'fixture-consumer',
      import_specifier: '#fixture-generated',
      package_import_target: './dist/Fixture.js',
      artifact_id: artifact.id,
      imported_members: ['name'],
    }],
    traversals: [traversal],
    artifact_bytes_by_path: new Map([[artifact.artifact_path, artifactBytes]]),
    selected_input_rows_by_artifact: new Map([[artifact.id, inputs.map(({ path, blob_digest: blobDigest }) => ({ path, blob_digest: blobDigest }))]]),
    capability_facts: capabilityFacts,
    traversal_observation_sets: [{
      traversal_id: traversal.id,
      emitted_observation_ids: capabilityFacts
        .filter(({ observation }) => observation.case === 'javascript-capability')
        .map(({ observation_id: observationId }) => observationId),
    }],
    execution_lineage: [{
      consumer_locality: relation.consumer_locality,
      import_specifier: relation.import_specifier,
      artifact_id: artifact.id,
      entry_reachability: [
        {
          from_entry: structuredClone(relation.build_invocation),
          to_entry: structuredClone(relation.generator),
        },
        {
          from_entry: structuredClone(relation.generator),
          to_entry: structuredClone(relation.input_selector),
        },
      ],
    }],
    runtime_surfaces: [{
      module: relation.runtime_surface_module,
      owner: relation.generated_owner,
      laws: [...relation.laws],
      exported_members: ['name'],
    }],
    proof_observations: [{
      consumer_locality: relation.consumer_locality,
      import_specifier: relation.import_specifier,
      owner: relation.generated_owner,
      path: relation.determinism_proof.path,
      title: relation.determinism_proof.title,
      what_id: relation.determinism_proof.what_id,
      reached_entries: [structuredClone(relation.generator)],
      used_surface_modules: [relation.runtime_surface_module],
    }],
  }
}

const codes = (fixture) => validateGeneratedModuleRelationV1(fixture).map(({ code }) => code)

test('WHAT[STRUCTURED-WORKFLOW-015] generated artifact binds tracked inputs bytes lineage traversal and import', () => {
  const legal = legalFixture()
  assert.deepEqual(codes(legal), [])
  assert.equal(
    generatedArtifactIdV1({ artifact_path: 'dist/Fixture.js', linkage }),
    'generated-artifact/v1:72c84cd5d0c80ad4832976479fa2a813a6dccff801f5dcd5b9061b957c090536',
  )
  assert.equal(
    selectedInputsDigestV1([{ path: 'src/A.fs', blob_digest: `sha256:${'a'.repeat(64)}` }]),
    'sha256:d39a30c836d9dc1fba3b37dea2194feebcb92d9122f7fdd99a1f709bdfa38fdd',
  )
  assert.throws(() => buildGeneratedArtifactRowV1({
    artifact_path: 'dist/Fixture.js',
    artifact_bytes: Buffer.from('fixture'),
    selected_inputs: [],
    linkage,
    javascript_traversal_id: 'test-owned-placeholder',
  }), { code: 'generated-artifact-traversal-id-mismatch' })

  const cases = [
    ['missing-generated-module-relation', (value) => { value.relations = [] }],
    ['stale-generated-module-relation', (value) => { value.actual_imports = [] }],
    ['duplicate-generated-module-relation', (value) => { value.relations.push(structuredClone(value.relations[0])) }],
    ['duplicate-generated-module-semantic-key', (value) => { value.relations.push({ ...structuredClone(value.relations[0]), id: 'fixture-generated-decoy' }) }],
    ['generated-module-specifier-mismatch', (value) => { value.actual_imports[0].import_specifier = '#decoy' }],
    ['generated-module-target-mismatch', (value) => { value.actual_imports[0].package_import_target = './dist/Decoy.js' }],
    ['generated-module-member-mismatch', (value) => { value.actual_imports[0].imported_members = ['decoy'] }],
    ['generated-module-lineage-missing', (value) => { value.execution_lineage = [] }],
    ['generated-module-lineage-duplicate', (value) => { value.execution_lineage.push(structuredClone(value.execution_lineage[0])) }],
    ['generated-module-lineage-mismatch', (value) => { value.execution_lineage[0].entry_reachability[0].to_entry.entry = 'decoy' }],
    ['generated-module-lineage-mismatch', (value) => { value.execution_lineage[0].entry_reachability[1].to_entry.entry = 'decoy' }],
    ['generated-module-nondeterministic', (value) => { value.proof_observations = [] }],
    ['generated-module-proof-duplicate', (value) => { value.proof_observations.push(structuredClone(value.proof_observations[0])) }],
    ['generated-module-determinism-proof-owner-mismatch', (value) => { value.proof_observations[0].owner = 'decoy-owner' }],
    ['generated-module-determinism-proof-owner-mismatch', (value) => { value.runtime_surfaces[0].owner = 'decoy-owner' }],
    ['generated-module-determinism-proof-law-mismatch', (value) => { value.runtime_surfaces[0].laws = ['DECOY-001'] }],
    ['generated-module-determinism-proof-mismatch', (value) => { value.proof_observations[0].title = 'WHAT[FIXTURE-001] decoy' }],
    ['generated-module-runtime-surface-missing', (value) => { value.runtime_surfaces = [] }],
    ['generated-module-runtime-surface-duplicate', (value) => { value.runtime_surfaces.push(structuredClone(value.runtime_surfaces[0])) }],
    ['generated-module-runtime-surface-callback-mismatch', (value) => { value.proof_observations[0].used_surface_modules = ['DecoySurface.js'] }],
    ['generated-module-runtime-surface-callback-mismatch', (value) => { value.proof_observations[0].reached_entries = [] }],
    ['generated-module-runtime-surface-callback-mismatch', (value) => {
      value.proof_observations[0].reached_entries = [
        structuredClone(value.relations[0].build_invocation),
        structuredClone(value.relations[0].generator),
      ]
    }],
    ['generated-module-member-mismatch', (value) => { value.runtime_surfaces[0].exported_members = [] }],
    ['generated-module-observed-evidence-invalid', (value) => { value.relations[0].untrusted_claim = true }],
    ['generated-module-observed-evidence-invalid', (value) => { value.artifacts[0].untrusted_claim = true }],
    ['generated-module-observed-evidence-invalid', (value) => { value.traversals[0].untrusted_claim = true }],
    ['generated-module-observed-evidence-invalid', (value) => { value.traversal_observation_sets[0].untrusted_claim = true }],
    ['generated-module-observed-evidence-invalid', (value) => { value.traversals[0].visited_node_count -= 1 }],
    ['generated-module-observed-evidence-invalid', (value) => {
      value.traversals[0] = {
        ...value.traversals[0],
        ast_node_count: 0,
        visited_node_count: 0,
        no_capability_node_count: 0,
        capability_emitting_node_count: 0,
      }
    }],
    ['javascript-traversal-observation-set-missing', (value) => { value.traversal_observation_sets = [] }],
    ['javascript-traversal-observation-set-duplicate', (value) => { value.traversal_observation_sets.push(structuredClone(value.traversal_observation_sets[0])) }],
    ['javascript-traversal-observation-set-stale', (value) => { value.traversal_observation_sets.push({ traversal_id: 'stale-traversal', emitted_observation_ids: [] }) }],
    ['javascript-traversal-source-mismatch', (value) => {
      value.capability_facts = value.capability_facts.filter(({ observation }) => observation.case !== 'javascript-capability')
    }],
    ['javascript-traversal-source-mismatch', (value) => {
      value.capability_facts = value.capability_facts.filter(({ observation }) => observation.case !== 'javascript-capability')
      value.traversal_observation_sets[0].emitted_observation_ids = []
    }],
    ['javascript-ast-node-unknown', (value) => {
      value.traversals[0].no_capability_node_count -= 1
      value.traversals[0].unknown_node_count = 1
    }],
    ['generated-module-observed-evidence-duplicate', (value) => { value.capability_facts.push(structuredClone(value.capability_facts[0])) }],
    ['generated-module-physical-authority', (value) => {
      value.capability_facts = capabilityFactsFor(value.artifacts[0].id, [{ kind: 'member-read', root: 'process', member_path: ['env'], binding_provenance: 'free' }])
      value.traversal_observation_sets[0].emitted_observation_ids = value.capability_facts
        .filter(({ observation }) => observation.case === 'javascript-capability')
        .map(({ observation_id: observationId }) => observationId)
    }],
    ['generated-artifact-missing', (value) => { value.artifacts = [] }],
    ['generated-artifact-stale', (value) => {
      value.artifacts.push(buildGeneratedArtifactRowV1({
        artifact_path: 'dist/Stale.js',
        artifact_bytes: Buffer.from('stale'),
        selected_inputs: [],
        linkage,
      }))
    }],
    ['generated-artifact-duplicate', (value) => { value.artifacts.push(structuredClone(value.artifacts[0])) }],
    ['generated-artifact-reference-missing', (value) => { value.capability_facts = value.capability_facts.filter(({ observation }) => observation.case !== 'fable-import') }],
    ['generated-artifact-linkage-mismatch', (value) => { value.artifacts[0].linkage.generator_entry = 'decoy' }],
    ['generated-artifact-digest-mismatch', (value) => { value.artifact_bytes_by_path.set(value.artifacts[0].artifact_path, Buffer.from('changed')) }],
    ['generated-artifact-inputs-digest-mismatch', (value) => { value.selected_input_rows_by_artifact.get(value.artifacts[0].id)[0].blob_digest = `sha256:${'f'.repeat(64)}` }],
    ['javascript-traversal-missing', (value) => { value.traversals = [] }],
    ['javascript-traversal-stale', (value) => {
      const sourceId = 'generated-artifact/v1:stale'
      value.traversals.push({
        ...structuredClone(value.traversals[0]),
        id: javascriptTraversalIdV1('generated-artifact', sourceId),
        source_id: sourceId,
      })
    }],
    ['javascript-traversal-duplicate', (value) => { value.traversals.push(structuredClone(value.traversals[0])) }],
    ['javascript-traversal-source-mismatch', (value) => { value.traversals[0].source_id = 'generated-artifact/v1:decoy' }],
  ]

  for (const [expected, mutate] of cases) {
    const value = legalFixture()
    mutate(value)
    assert.deepEqual(codes(value), [expected], expected)
  }

  {
    const value = legalFixture()
    value.capability_facts[0].artifact_id = value.artifacts[0].id
    assert.deepEqual(codes(value), ['generated-module-observed-evidence-invalid'])
  }

  {
    const value = legalFixture()
    value.capability_facts = value.capability_facts.filter(({ observation }) => observation.case !== 'fable-import')
    value.artifact_references = [{ artifact_id: value.artifacts[0].id }]
    assert.deepEqual(codes(value), ['generated-artifact-reference-missing'])
  }

  {
    const value = legalFixture()
    value.proof_observations = []
    value.deterministic_relation_ids = [value.relations[0].id]
    assert.deepEqual(codes(value), ['generated-module-nondeterministic'])
  }

  {
    const value = legalFixture()
    value.execution_lineage.push({ ...structuredClone(value.execution_lineage[0]), import_specifier: '#stale' })
    assert.deepEqual(codes(value), ['generated-module-lineage-stale'])
  }

  {
    const value = legalFixture()
    value.proof_observations.push({ ...structuredClone(value.proof_observations[0]), import_specifier: '#stale' })
    assert.deepEqual(codes(value), ['generated-module-proof-stale'])
  }

  {
    const value = legalFixture()
    value.runtime_surfaces.push({ ...structuredClone(value.runtime_surfaces[0]), module: 'StaleSurface.js' })
    assert.deepEqual(codes(value), ['generated-module-runtime-surface-stale'])
  }

  {
    const value = legalFixture()
    value.actual_imports.push(structuredClone(value.actual_imports[0]))
    assert.deepEqual(codes(value), ['generated-module-observed-evidence-duplicate'])
  }

  {
    const value = legalFixture()
    value.actual_imports[0].untrusted_claim = true
    assert.deepEqual(codes(value), ['generated-module-observed-evidence-invalid'])
  }

  assert.deepEqual(codes(legalFixture()), [], 'Runtime.Node plus PureRepresentation must remain legal')

  const duplicateInputReader = createTrackingReaderV1({ readFile: () => Buffer.from('same') })
  assert.throws(() => readSelectedInputsV1(['src/A.fs', 'src/A.fs'], duplicateInputReader), { code: 'generated-selected-input-duplicate' })
})
