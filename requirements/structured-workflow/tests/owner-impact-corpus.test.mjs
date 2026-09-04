import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import {
  buildOwnerImpactReportV1,
  measureOwnerImpactStructureV1,
  OWNER_IMPACT_CONTROL_CASES,
  OWNER_IMPACT_CONTROL_CASE_IDS,
  OWNER_IMPACT_STABLE_CASES,
  OWNER_IMPACT_STABLE_CASE_IDS,
  OWNER_IMPACT_TIMING_COMMANDS,
  OWNER_IMPACT_TIMING_IDS,
  validateOwnerImpactCorpusV1,
  writeOwnerImpactBaselineV1,
} from '../../../scripts/owner-impact-report.mjs'

const commit = (digit) => digit.repeat(40)
const digest = (digit) => `sha256:${digit.repeat(64)}`

const cases = (definitions) => definitions.map((definition) => ({
  ...definition,
  successor_path: definition.baseline_changed_path,
}))

const corpus = ({ baseline = null } = {}) => ({
  schema_version: 1,
  purpose: 'm6-owner-impact-report-only',
  baseline_commit: commit('a'),
  aggregate_path: 'src/Wanxiangshu/Wanxiangshu.fsproj',
  project_directory: 'src/Wanxiangshu',
  full_threshold: 0.6,
  lockfile_path: 'package-lock.json',
  tool_manifest_path: '.config/dotnet-tools.json',
  stable_cases: cases(OWNER_IMPACT_STABLE_CASES),
  control_cases: cases(OWNER_IMPACT_CONTROL_CASES),
  timing_commands: OWNER_IMPACT_TIMING_COMMANDS.map(({ id, command }) => ({ id, command: [...command] })),
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

const changedPathById = new Map([...OWNER_IMPACT_STABLE_CASES, ...OWNER_IMPACT_CONTROL_CASES]
  .map(({ id, baseline_changed_path: path }) => [id, path]))

const structural = (sourceCount) => [...OWNER_IMPACT_STABLE_CASE_IDS, ...OWNER_IMPACT_CONTROL_CASE_IDS]
  .sort()
  .map((id) => ({
    id,
    changed_path: changedPathById.get(id),
    mode: 'full',
    reason: 'fixture-impact',
    root_project_count: 1,
    project_count: 1,
    production_source_count: sourceCount,
    compile_item_identities: ['src/Wanxiangshu/Fixture.fsi', 'src/Wanxiangshu/Fixture.fs'],
  }))

const timing = (milliseconds) => OWNER_IMPACT_TIMING_IDS.map((id) => ({
  id,
  raw_milliseconds: [milliseconds, milliseconds, milliseconds],
  median_milliseconds: milliseconds,
}))

test('WHAT[STRUCTURED-WORKFLOW-012] fixed owner impact corpus drives the production planner without becoming a verdict', () => {
  const fixture = mkdtempSync(join(tmpdir(), 'owner-impact-corpus-'))
  try {
    mkdirSync(join(fixture, 'src/Wanxiangshu'), { recursive: true })
    mkdirSync(join(fixture, '.config'))
    writeFileSync(join(fixture, 'src/Wanxiangshu/Fixture.fsi'), 'module Fixture\nval value: int\n')
    writeFileSync(join(fixture, 'src/Wanxiangshu/Fixture.fs'), 'module Fixture\nlet value = 1\n')
    writeFileSync(join(fixture, 'src/Wanxiangshu/Wanxiangshu.Owner.host-boundary.host-fatal-effect.fsproj'), `<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <Compile Include="Fixture.fsi"/>
    <Compile Include="Fixture.fs"/>
  </ItemGroup>
</Project>\n`)
    writeFileSync(join(fixture, 'src/Wanxiangshu/Wanxiangshu.fsproj'), `<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <Compile Include="Fixture.fsi"/>
    <Compile Include="Fixture.fs"/>
  </ItemGroup>
</Project>\n`)
    writeFileSync(join(fixture, 'package.json'), '{}\n')
    writeFileSync(join(fixture, 'package-lock.json'), '{}\n')
    writeFileSync(join(fixture, '.config/dotnet-tools.json'), '{"tools":{"fable":{"version":"1.0.0"}}}\n')

    const definition = corpus()
    assert.equal(validateOwnerImpactCorpusV1(definition).baseline_measurement, null)
    const measured = measureOwnerImpactStructureV1(definition, { root: fixture })
    assert.deepEqual(measured.map(({ id }) => id), [...OWNER_IMPACT_STABLE_CASE_IDS, ...OWNER_IMPACT_CONTROL_CASE_IDS].sort())
    assert.ok(measured.every(({ compile_item_identities: identities }) => identities.every((path) => path.startsWith('src/Wanxiangshu/'))))

    const baselineMeasurement = {
      commit: commit('a'),
      environment: environment(),
      structural: structural(10),
      timing: timing(100),
    }
    const report = buildOwnerImpactReportV1({
      corpus: corpus({ baseline: baselineMeasurement }),
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

  const changedDefinition = corpus()
  changedDefinition.stable_cases[0].baseline_changed_path = 'src/Wanxiangshu/Other.fs'
  assert.throws(() => validateOwnerImpactCorpusV1(changedDefinition), /closed report-only schema/)

  const changedCommand = corpus()
  changedCommand.timing_commands[0].command = ['node', 'scripts/checks/locality-slice-report.mjs']
  assert.throws(() => validateOwnerImpactCorpusV1(changedCommand), /closed report-only schema/)
})

test('WHAT[STRUCTURED-WORKFLOW-012] baseline writer binds one clean exact commit and refuses overwrite', () => {
  const fixture = mkdtempSync(join(tmpdir(), 'owner-impact-baseline-'))
  try {
    const corpusPath = join(fixture, 'corpus.json')
    writeFileSync(corpusPath, `${JSON.stringify(corpus(), null, 2)}\n`)
    const measured = writeOwnerImpactBaselineV1(corpusPath, {
      root: fixture,
      inspectGit: () => ({ commit: commit('a'), clean: true }),
      measureStructure: () => structural(10),
      measureTiming: () => ({ environment: environment(), timing: timing(100) }),
    })
    assert.equal(measured.baseline_measurement.commit, commit('a'))
    assert.equal(validateOwnerImpactCorpusV1(JSON.parse(readFileSync(corpusPath))).baseline_measurement.timing.length, 2)
    assert.throws(() => writeOwnerImpactBaselineV1(corpusPath, {
      root: fixture,
      inspectGit: () => ({ commit: commit('a'), clean: true }),
      measureStructure: () => structural(10),
      measureTiming: () => ({ environment: environment(), timing: timing(100) }),
    }), /refusing to overwrite/)

    writeFileSync(corpusPath, `${JSON.stringify(corpus(), null, 2)}\n`)
    assert.throws(() => writeOwnerImpactBaselineV1(corpusPath, {
      root: fixture,
      inspectGit: () => ({ commit: commit('a'), clean: false }),
    }), /clean Git checkout/)
    assert.throws(() => writeOwnerImpactBaselineV1(corpusPath, {
      root: fixture,
      inspectGit: () => ({ commit: commit('b'), clean: true }),
    }), /does not match baseline_commit/)
  } finally {
    rmSync(fixture, { recursive: true, force: true })
  }
})
