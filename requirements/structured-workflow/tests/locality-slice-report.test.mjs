import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join, resolve } from 'node:path'
import test from 'node:test'

import {
  extractObservedCapabilityFactsV1,
  validateCapabilityPartitionV1,
} from '../../../scripts/lib/capability-observations-v1.mjs'
import {
  buildLocalitySliceReportV1,
  buildLocalitySliceSummaryV1,
  EMPTY_AUTHORIZATION_PROJECTION_V2,
  serializeLocalitySliceReportV1,
  serializeLocalitySliceSummaryV1,
} from '../../../scripts/lib/locality-slice-report-v1.mjs'
const ROOT = resolve(import.meta.dirname, '../../..')
const digest = `sha256:${'0'.repeat(64)}`
const emptyExtraction = () => validateCapabilityPartitionV1({ observations: [], facts: [] }).coverage

const sourcePair = (name) => ({
  implementation_path: `src/${name}.fs`,
  implementation_digest: digest,
  signature_path: `src/${name}.fsi`,
  signature_digest: digest,
})

const reportWorld = () => ({
  schema_version: 1,
  fact_schema_version: 1,
  observed: {
    localities: [
      { id: 'provider', owner: 'provider-owner', kind: 'contract', project_path: 'src/Provider.fsproj', sources: [sourcePair('Provider')] },
      { id: 'consumer', owner: 'consumer-owner', kind: 'runtime', project_path: 'src/Consumer.fsproj', sources: [sourcePair('Consumer')] },
    ],
    project_references: [{ consumer_locality: 'consumer', provider_locality: 'provider' }],
    generated_artifacts: [],
    javascript_traversals: [],
    capability_extraction: emptyExtraction(),
    capability_facts: [],
  },
  normative: structuredClone(EMPTY_AUTHORIZATION_PROJECTION_V2),
})

const unknownCensusWorld = () => {
  const world = reportWorld()
  world.observed.localities = Array.from({ length: 7 }, (_, index) => ({
    id: `locality-${index}`,
    owner: `owner-${index}`,
    kind: 'runtime',
    project_path: `src/Locality${index}.fsproj`,
    sources: [sourcePair(`Locality${index}`)],
  }))
  world.observed.project_references = []
  const observations = world.observed.localities.map((locality, index) => ({
    case: 'fable-import',
    payload: {
      module_specifier: 'node:unclassified.dynamic',
      selector: 'dynamic',
      generated_artifact_id: null,
      site: {
        locality_id: locality.id,
        source_path: locality.sources[0].implementation_path,
        semantic_declaration_anchor: `Locality${index}.future`,
        same_anchor_occurrence_ordinal: 0,
      },
    },
  }))
  observations.push({
    case: 'fable-import',
    payload: {
      module_specifier: 'node:unclassified.other',
      selector: 'other',
      generated_artifact_id: null,
      site: {
        locality_id: 'locality-0',
        source_path: 'src/Locality0.fs',
        semantic_declaration_anchor: 'Locality0.other',
        same_anchor_occurrence_ordinal: 0,
      },
    },
  })
  const extraction = extractObservedCapabilityFactsV1(observations)
  world.observed.capability_extraction = extraction.coverage
  world.observed.capability_facts = extraction.facts
  return world
}

test('WHAT[STRUCTURED-WORKFLOW-013] report-only locality scan exposes every fresh canonical query without granting authority', () => {
  const world = reportWorld()
  const report = buildLocalitySliceReportV1({
    world,
    findings: [
      { code: 'z-finding', locality_id: 'provider' },
      { code: 'a-finding', locality_id: 'consumer' },
    ],
  })

  assert.equal(report.report_kind, 'm6.3b-report-only')
  assert.equal(report.census.locality_count, 2)
  assert.equal(report.census.production_source_count, 2)
  assert.deepEqual(report.localities.map(({ locality_id: localityId }) => localityId), ['consumer', 'provider'])
  assert.ok(report.localities.every(({ reasons }) => reasons.includes('TerminalClassificationRequired')))
  assert.deepEqual(report.localities.find(({ locality_id: localityId }) => localityId === 'provider').queries.audience, {
    direct_project_consumers: ['consumer'],
    reverse_closure_effective_consumers: ['consumer'],
    relation_endpoints: [],
  })
  assert.deepEqual(report.findings.map(({ code }) => code), ['a-finding', 'z-finding'])
  assert.deepEqual(JSON.parse(serializeLocalitySliceReportV1({ world, findings: report.findings })), report)
  assert.deepEqual(world.normative, EMPTY_AUTHORIZATION_PROJECTION_V2)

  const summary = buildLocalitySliceSummaryV1({ world, findings: report.findings })
  assert.equal(summary.report_kind, 'm6.3b-report-only-summary')
  assert.equal(summary.canonical_world_digest, report.canonical_world_digest)
  assert.deepEqual(summary.census, report.census)
  assert.equal(summary.finding_count, 2)
  assert.deepEqual(summary.finding_counts, [
    { code: 'a-finding', count: 1 },
    { code: 'z-finding', count: 1 },
  ])
  assert.deepEqual(summary.localities, report.localities.map(({ locality_id: localityId, reasons }) => ({
    locality_id: localityId,
    reasons,
  })))
  assert.deepEqual(JSON.parse(serializeLocalitySliceSummaryV1({ world, findings: report.findings })), summary)
})

test('WHAT[STRUCTURED-WORKFLOW-014] report-only Unknown census is a bounded deterministic projection of canonical facts', () => {
  const world = unknownCensusWorld()
  const reorderedWorld = structuredClone(world)
  reorderedWorld.observed.capability_facts.reverse()
  reorderedWorld.observed.capability_facts.push(structuredClone(reorderedWorld.observed.capability_facts[0]))

  const full = buildLocalitySliceReportV1({ world })
  const summary = buildLocalitySliceSummaryV1({ world: reorderedWorld })
  assert.deepEqual(summary.unknown_capability_census, full.unknown_capability_census)

  const census = summary.unknown_capability_census
  assert.equal(census.census_kind, 'm6.3b-report-only-unknown-capability-census')
  assert.equal(census.unknown_fact_count, 8)
  assert.equal(census.group_count, 2)
  assert.equal(census.sample_limit, 5)
  assert.equal(census.groups.reduce((count, group) => count + group.fact_count, 0), 8)
  assert.deepEqual(census.groups.map(({ observation_case: observationCase }) => observationCase), [
    'fable-import',
    'fable-import',
  ])
  const dynamic = census.groups[0]
  assert.equal(dynamic.unknown_class, 'dynamic-target')
  assert.equal(dynamic.syntax_kind, 'fable-import')
  assert.equal(dynamic.raw_identity, 'node:unclassified.dynamic:dynamic')
  assert.equal(dynamic.fact_count, 7)
  assert.equal(dynamic.affected_locality_count, 7)
  assert.equal(dynamic.affected_source_count, 7)
  assert.deepEqual(dynamic.representative_localities, [
    'locality-0',
    'locality-1',
    'locality-2',
    'locality-3',
    'locality-4',
  ])
  assert.deepEqual(dynamic.representative_sources, [
    'src/Locality0.fs',
    'src/Locality1.fs',
    'src/Locality2.fs',
    'src/Locality3.fs',
    'src/Locality4.fs',
  ])
  assert.match(dynamic.affected_locality_digest, /^sha256:[0-9a-f]{64}$/)
  assert.match(dynamic.affected_source_digest, /^sha256:[0-9a-f]{64}$/)
  const other = census.groups[1]
  assert.equal(other.unknown_class, 'dynamic-target')
  assert.equal(other.syntax_kind, 'fable-import')
  assert.equal(other.raw_identity, 'node:unclassified.other:other')
  assert.equal(other.fact_count, 1)
  assert.equal(other.affected_locality_count, 1)
  assert.match(census.groups_digest, /^sha256:[0-9a-f]{64}$/)
  assert.deepEqual(world.normative, EMPTY_AUTHORIZATION_PROJECTION_V2)
})

test('WHAT[STRUCTURED-WORKFLOW-016] M6.3b report remains outside every release entry', () => {
  const check = readFileSync(join(ROOT, 'scripts/check.mjs'), 'utf8')
  const packageManifest = readFileSync(join(ROOT, 'package.json'), 'utf8')
  const integrationSteps = readFileSync(
    join(ROOT, 'requirements/verification-system/tests/support/integration-node-test-steps.mjs'),
    'utf8',
  )
  for (const releaseSource of [check, packageManifest, integrationSteps]) {
    assert.ok(!releaseSource.includes('locality-slice-report.mjs'))
    assert.ok(!releaseSource.includes('OWNER-CONTRACT-SLICE-ADJUDICATION-WORKSHEET.json'))
  }
})
