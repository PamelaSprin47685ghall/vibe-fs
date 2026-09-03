import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import test from 'node:test'

import { validateCapabilityPartitionV1 } from '../../../scripts/lib/capability-observations-v1.mjs'
import {
  buildLocalitySliceReportV1,
  buildLocalitySliceSummaryV1,
  EMPTY_AUTHORIZATION_PROJECTION_V2,
  serializeLocalitySliceReportV1,
  serializeLocalitySliceSummaryV1,
} from '../../../scripts/lib/locality-slice-report-v1.mjs'
import {
  scanProductionLocalitySliceReportV1,
  writeFreshMigrationWorksheetFileV1,
} from '../../../scripts/checks/locality-slice-report.mjs'

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
    actual_source_edges: [{
      consumer_locality: 'consumer',
      consumer_source: 'src/Consumer.fs',
      provider_locality: 'provider',
      provider_source: 'src/Provider.fs',
    }],
    generated_artifacts: [],
    javascript_traversals: [],
    capability_extraction: emptyExtraction(),
    capability_facts: [],
  },
  normative: structuredClone(EMPTY_AUTHORIZATION_PROJECTION_V2),
})

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
    actual_source_consumers: ['consumer'],
    reverse_closure_effective_consumers: ['consumer'],
    relation_endpoints: [],
    missing_closure_violations: [],
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

test('WHAT[STRUCTURED-WORKFLOW-013] production report composes one injected compiler world and keeps model findings nonfatal', async () => {
  const fixture = mkdtempSync(join(tmpdir(), 'locality-slice-report-'))
  try {
    mkdirSync(join(fixture, 'src'), { recursive: true })
    writeFileSync(join(fixture, 'src/A.fs'), 'module A\nlet value = 1\n')
    writeFileSync(join(fixture, 'src/A.fsi'), 'module A\nval value: int\n')
    const calls = { inventory: 0, compiler: 0, extraction: 0 }
    const stages = []
    const inventory = {
      aggregatePath: 'src/Aggregate.fsproj',
      productionFiles: ['src/A.fs'],
      signatureFiles: ['src/A.fsi'],
      localities: [{
        id: 'a',
        owner: 'owner-a',
        kind: 'contract',
        projectPath: 'src/A.fsproj',
        sources: [{ implementationPath: 'src/A.fs', signaturePath: 'src/A.fsi' }],
        references: [],
      }],
      projectReferences: [],
    }
    const compilerObservations = {
      schemaVersion: 1,
      projectFile: 'src/Aggregate.fsproj',
      productionFiles: ['src/A.fs'],
      signatureFiles: ['src/A.fsi'],
      declarationUses: [],
      externalSymbolUses: [],
      fsharpNodes: [],
      fableInterop: [],
      signatureExports: [],
      diagnostics: [{ code: 'compiler-note', sourcePath: 'src/A.fs' }],
      elapsedMilliseconds: 1,
    }
    const result = await scanProductionLocalitySliceReportV1({
      repositoryRoot: fixture,
      sourceRoot: join(fixture, 'src'),
      aggregate: join(fixture, 'src/Aggregate.fsproj'),
      readInventory: () => { calls.inventory += 1; return inventory },
      scanCompiler: () => { calls.compiler += 1; return compilerObservations },
      deriveGeneratedArtifacts: async () => ({ artifacts: [], units: [] }),
      extractCapabilities: (input) => {
        calls.extraction += 1
        assert.deepEqual(Object.keys(input).sort(), ['compilerObservations', 'generatedArtifacts', 'inventory', 'javascriptUnits'])
        return {
          capabilityFacts: [],
          capabilityExtraction: emptyExtraction(),
          javascriptTraversals: [],
          traversalObservationSets: [],
          diagnostics: [
            { code: 'compiler-note', sourcePath: 'src/A.fs' },
            { code: 'extractor-note', sourcePath: 'src/A.fs' },
          ],
          violations: [{ code: 'model-finding', locality_id: 'a' }],
        }
      },
      observeStage: ({ stage }) => stages.push(stage),
    })

    assert.deepEqual(calls, { inventory: 1, compiler: 1, extraction: 1 })
    assert.deepEqual(stages, [
      'owner-inventory',
      'compiler-observations',
      'dependency-analysis',
      'generated-artifacts',
      'capability-extraction',
      'canonical-world',
    ])
    assert.equal(result.report.census.locality_count, 1)
    assert.deepEqual(result.report.findings, [{ code: 'model-finding', locality_id: 'a' }])
    assert.deepEqual(result.diagnostics.map(({ code }) => code), ['compiler-note', 'extractor-note'])
  } finally {
    rmSync(fixture, { recursive: true, force: true })
  }
})

test('WHAT[STRUCTURED-WORKFLOW-016] M6.3b report and worksheet remain outside every release entry', () => {
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

  const fixture = mkdtempSync(join(tmpdir(), 'migration-worksheet-write-'))
  try {
    const worksheetPath = join(fixture, 'worksheet.json')
    writeFileSync(worksheetPath, JSON.stringify({
      schema_version: 1,
      purpose: 'm6.3b-migration-only',
      records: [{
        locality_id: 'consumer',
        status: 'decided',
        draft_reason: 'The locality owns one terminal migration decision.',
        draft_target_classification: { case: 'runtime-effect', payload: {} },
        draft_migration_path: 'Keep the runtime behind an injected contract.',
        draft_what_ids: ['STRUCTURED-WORKFLOW-016'],
        draft_proofs: [{
          what_id: 'STRUCTURED-WORKFLOW-016',
          path: 'requirements/structured-workflow/tests/locality-slice-report.test.mjs',
          title: 'WHAT[STRUCTURED-WORKFLOW-016] M6.3b report and worksheet remain outside every release entry',
        }],
      }],
    }))
    assert.throws(() => writeFreshMigrationWorksheetFileV1({
      report: buildLocalitySliceReportV1({ world: reportWorld() }),
      worksheetPath,
    }), /refusing to overwrite/)

    rmSync(worksheetPath)
    const worksheet = writeFreshMigrationWorksheetFileV1({
      report: buildLocalitySliceReportV1({ world: reportWorld() }),
      worksheetPath,
    })
    assert.equal(worksheet.records.length, 2)
    assert.ok(worksheet.records.every(({ status }) => status === 'undecided'))
  } finally {
    rmSync(fixture, { recursive: true, force: true })
  }
})
