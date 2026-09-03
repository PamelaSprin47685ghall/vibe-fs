import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import {
  buildOwnerImpactReportV1,
  measureOwnerImpactStructureV1,
  OWNER_IMPACT_CONTROL_CASE_IDS,
  OWNER_IMPACT_STABLE_CASE_IDS,
  OWNER_IMPACT_TIMING_IDS,
  validateOwnerImpactCorpusV1,
} from '../../../scripts/owner-impact-report.mjs'

const commit = (digit) => digit.repeat(40)
const digest = (digit) => `sha256:${digit.repeat(64)}`

const cases = (ids, successorPath) => ids.map((id) => ({
  id,
  baseline_changed_path: successorPath(id),
  successor_path: successorPath(id),
  change_kind: id.includes('signature') ? 'signature' : 'implementation',
  coverage: 'fixed migration corpus',
}))

const corpus = ({ baseline = null, root = 'repo/' } = {}) => ({
  schema_version: 1,
  purpose: 'm6-owner-impact-report-only',
  baseline_commit: commit('a'),
  aggregate_path: `${root}Aggregate.fsproj`,
  project_directory: root === '' ? '.' : root.slice(0, -1),
  full_threshold: 0.6,
  lockfile_path: `${root}package-lock.json`,
  tool_manifest_path: `${root}.config/dotnet-tools.json`,
  stable_cases: cases(OWNER_IMPACT_STABLE_CASE_IDS, () => `${root}Source/A.fs`),
  control_cases: cases(OWNER_IMPACT_CONTROL_CASE_IDS, (id) => id === 'fsproj-control'
    ? `${root}Owner.A.fsproj`
    : `${root}package.json`),
  timing_commands: [
    { id: 'fresh-production-scan', command: ['node', 'scripts/checks/locality-slice-report.mjs'] },
    { id: 'full-release-build', command: ['node', 'scripts/build.mjs'] },
  ],
  baseline_measurement: baseline,
})

const environment = () => ({
  platform: 'fixture',
  release: 'fixture',
  architecture: 'fixture',
  cpu_model: 'fixture',
  cpu_count: 1,
  node_version: 'v1',
  fable_version: '1',
  lockfile_digest: digest('1'),
  tool_manifest_digest: digest('2'),
  dependency_cache_identity_digest: digest('3'),
})

const structural = (sourceCount, prefix = 'repo/') => [...OWNER_IMPACT_STABLE_CASE_IDS, ...OWNER_IMPACT_CONTROL_CASE_IDS]
  .sort()
  .map((id) => ({
    id,
    changed_path: id === 'fsproj-control' ? `${prefix}Owner.A.fsproj` : id === 'toolchain-control' ? `${prefix}package.json` : `${prefix}Source/A.fs`,
    mode: 'full',
    reason: 'fixture-impact',
    root_project_count: 1,
    project_count: 1,
    production_source_count: sourceCount,
    compile_item_identities: [`${prefix}Source/A.fsi`, `${prefix}Source/A.fs`],
  }))

const timing = (milliseconds) => OWNER_IMPACT_TIMING_IDS.map((id) => ({
  id,
  raw_milliseconds: [milliseconds, milliseconds, milliseconds],
  median_milliseconds: milliseconds,
}))

test('WHAT[STRUCTURED-WORKFLOW-012] fixed owner impact corpus drives the production planner without becoming a verdict', () => {
  const fixture = mkdtempSync(join(tmpdir(), 'owner-impact-corpus-'))
  try {
    mkdirSync(join(fixture, 'repo/Source'), { recursive: true })
    mkdirSync(join(fixture, 'repo/.config'))
    writeFileSync(join(fixture, 'repo/Source/A.fsi'), 'module A\nval value: int\n')
    writeFileSync(join(fixture, 'repo/Source/A.fs'), 'module A\nlet value = 1\n')
    writeFileSync(join(fixture, 'repo/Owner.A.fsproj'), `<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <Compile Include="Source/A.fsi"/>
    <Compile Include="Source/A.fs"/>
  </ItemGroup>
</Project>\n`)
    writeFileSync(join(fixture, 'repo/Aggregate.fsproj'), `<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <Compile Include="Source/A.fsi"/>
    <Compile Include="Source/A.fs"/>
  </ItemGroup>
</Project>\n`)
    writeFileSync(join(fixture, 'repo/package.json'), '{}\n')
    writeFileSync(join(fixture, 'repo/package-lock.json'), '{}\n')
    writeFileSync(join(fixture, 'repo/.config/dotnet-tools.json'), '{"tools":{"fable":{"version":"1.0.0"}}}\n')

    const definition = corpus({ root: 'repo/' })
    assert.equal(validateOwnerImpactCorpusV1(definition).baseline_measurement, null)
    const measured = measureOwnerImpactStructureV1(definition, { root: fixture })
    assert.deepEqual(measured.map(({ id }) => id), [...OWNER_IMPACT_STABLE_CASE_IDS, ...OWNER_IMPACT_CONTROL_CASE_IDS].sort())
    assert.ok(measured.every(({ compile_item_identities: identities }) => identities.join(',') === 'repo/Source/A.fsi,repo/Source/A.fs'))

    const baselineMeasurement = {
      commit: commit('a'),
      environment: environment(),
      structural: structural(10),
      timing: timing(100),
    }
    const report = buildOwnerImpactReportV1({
      corpus: corpus({ baseline: baselineMeasurement, root: 'repo/' }),
      candidate_commit: commit('b'),
      structural: structural(8),
      timing: { environment: environment(), timing: timing(106) },
    })
    assert.equal(report.comparison.structural_median.reduction_percent, 20)
    assert.deepEqual(report.findings.map(({ code }) => code), [
      'wall-clock-regression-over-five-percent',
      'wall-clock-regression-over-five-percent',
    ])
    assert.ok(!Object.hasOwn(report, 'ok'))
    assert.ok(!Object.hasOwn(report, 'verdict'))
  } finally {
    rmSync(fixture, { recursive: true, force: true })
  }
})

test('WHAT[STRUCTURED-WORKFLOW-012] owner impact corpus rejects stable-case deletion and baseline drift', () => {
  const missingStableCase = corpus()
  missingStableCase.stable_cases.pop()
  assert.throws(() => validateOwnerImpactCorpusV1(missingStableCase), /closed report-only schema/)

  const driftedBaseline = corpus({
    baseline: {
      commit: commit('b'),
      environment: environment(),
      structural: structural(10),
      timing: timing(100),
    },
  })
  assert.throws(() => validateOwnerImpactCorpusV1(driftedBaseline), /declared commit and cases/)
})
