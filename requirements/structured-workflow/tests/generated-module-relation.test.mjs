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
}

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
  return {
    relations: [relation],
    artifacts: [artifact],
    actual_imports: [{
      consumer_locality: 'fixture-consumer',
      import_specifier: '#fixture-generated',
      package_import_target: './dist/Fixture.js',
      artifact_id: artifact.id,
      imported_members: ['name'],
    }],
    traversals: [traversal],
    artifact_references: [{ artifact_id: artifact.id, observation_case: 'fable-import' }],
    artifact_bytes_by_path: new Map([[artifact.artifact_path, artifactBytes]]),
    selected_input_rows_by_artifact: new Map([[artifact.id, inputs.map(({ path, blob_digest: blobDigest }) => ({ path, blob_digest: blobDigest }))]]),
    capability_facts: [{
      artifact_id: artifact.id,
      disposition: {
        case: 'classified',
        payload: { runtimes: ['node'], authorities: [], mutable_resources: [], semantic_classes: ['pure-representation'] },
      },
    }],
    deterministic_relation_ids: ['fixture-generated'],
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
    ['generated-module-specifier-mismatch', (value) => { value.actual_imports[0].import_specifier = '#decoy' }],
    ['generated-module-target-mismatch', (value) => { value.actual_imports[0].package_import_target = './dist/Decoy.js' }],
    ['generated-module-nondeterministic', (value) => { value.deterministic_relation_ids = [] }],
    ['generated-module-physical-authority', (value) => { value.capability_facts[0].disposition.payload.authorities = ['file-system'] }],
    ['generated-artifact-missing', (value) => { value.artifacts = [] }],
    ['generated-artifact-stale', (value) => { value.artifacts.push({ ...structuredClone(value.artifacts[0]), id: 'generated-artifact/v1:stale' }) }],
    ['generated-artifact-duplicate', (value) => { value.artifacts.push(structuredClone(value.artifacts[0])) }],
    ['generated-artifact-reference-missing', (value) => { value.artifact_references = [] }],
    ['generated-artifact-linkage-mismatch', (value) => { value.artifacts[0].linkage.generator_entry = 'decoy' }],
    ['generated-artifact-digest-mismatch', (value) => { value.artifact_bytes_by_path.set(value.artifacts[0].artifact_path, Buffer.from('changed')) }],
    ['generated-artifact-inputs-digest-mismatch', (value) => { value.selected_input_rows_by_artifact.get(value.artifacts[0].id)[0].blob_digest = `sha256:${'f'.repeat(64)}` }],
    ['javascript-traversal-missing', (value) => { value.traversals = [] }],
    ['javascript-traversal-stale', (value) => { value.traversals.push({ ...structuredClone(value.traversals[0]), id: 'stale-traversal', source_id: 'generated-artifact/v1:stale' }) }],
    ['javascript-traversal-duplicate', (value) => { value.traversals.push(structuredClone(value.traversals[0])) }],
    ['javascript-traversal-source-mismatch', (value) => { value.traversals[0].source_id = 'generated-artifact/v1:decoy' }],
  ]

  for (const [expected, mutate] of cases) {
    const value = legalFixture()
    mutate(value)
    assert.deepEqual(codes(value), [expected])
  }

  const duplicateInputReader = createTrackingReaderV1({ readFile: () => Buffer.from('same') })
  assert.throws(() => readSelectedInputsV1(['src/A.fs', 'src/A.fs'], duplicateInputReader), { code: 'generated-selected-input-duplicate' })
})
