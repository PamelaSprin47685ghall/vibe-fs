#!/usr/bin/env node

import { createHash } from 'node:crypto'
import { spawnSync } from 'node:child_process'
import { readFileSync, renameSync, unlinkSync, writeFileSync } from 'node:fs'
import { homedir, platform, arch, cpus, release } from 'node:os'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { performance } from 'node:perf_hooks'

import { compareCanonicalTextV1, encodeCanonicalJsonV1 } from './lib/canonical-json-v1.mjs'
import { planImpactCompile } from './lib/owner-compile.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const DEFAULT_CORPUS = join(ROOT, 'scripts/checks/owner-impact-corpus.json')

export const OWNER_IMPACT_STABLE_CASE_IDS = Object.freeze([
  'canonical-codec-implementation',
  'canonical-codec-signature',
  'delegation-pty-adapter',
  'fatal-process-implementation',
  'host-signal-adapter',
  'host-signal-bootstrap',
  'loop-detector-runtime',
])

export const OWNER_IMPACT_CONTROL_CASE_IDS = Object.freeze([
  'fsproj-control',
  'toolchain-control',
])

export const OWNER_IMPACT_TIMING_IDS = Object.freeze([
  'fresh-production-scan',
  'full-release-build',
])

export const OWNER_IMPACT_STABLE_CASES = Object.freeze([
  {
    id: 'canonical-codec-implementation',
    baseline_changed_path: 'src/Wanxiangshu/Persistence/EventStore/CanonicalEventCodec.fs',
    change_kind: 'target-contract-implementation',
    coverage: 'low-closure',
  },
  {
    id: 'canonical-codec-signature',
    baseline_changed_path: 'src/Wanxiangshu/Persistence/EventStore/CanonicalEventCodec.fsi',
    change_kind: 'public-sibling-signature',
    coverage: 'high-or-full-fallback',
  },
  {
    id: 'delegation-pty-adapter',
    baseline_changed_path: 'src/Wanxiangshu/Execution/Delegation/Fork/Host/Pty.fs',
    change_kind: 'adapter-implementation',
    coverage: 'high-closure',
  },
  {
    id: 'fatal-process-implementation',
    baseline_changed_path: 'src/Wanxiangshu/Foundation/FatalProcess.fs',
    change_kind: 'effect-implementation',
    coverage: 'low-closure',
  },
  {
    id: 'host-signal-adapter',
    baseline_changed_path: 'src/Wanxiangshu/OpenCode/Signals/HostSignalAdapter.fs',
    change_kind: 'adapter-implementation',
    coverage: 'medium-or-high-closure',
  },
  {
    id: 'host-signal-bootstrap',
    baseline_changed_path: 'src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs',
    change_kind: 'composition-implementation',
    coverage: 'high-or-full-fallback',
  },
  {
    id: 'loop-detector-runtime',
    baseline_changed_path: 'src/Wanxiangshu/Execution/Session/LoopDetector.fs',
    change_kind: 'runtime-implementation',
    coverage: 'medium-closure',
  },
])

export const OWNER_IMPACT_CONTROL_CASES = Object.freeze([
  {
    id: 'fsproj-control',
    baseline_changed_path: 'src/Wanxiangshu/Wanxiangshu.Owner.host-boundary.host-fatal-effect.fsproj',
    change_kind: 'project-control',
    coverage: 'full-fallback-control',
  },
  {
    id: 'toolchain-control',
    baseline_changed_path: 'package.json',
    change_kind: 'toolchain-control',
    coverage: 'full-fallback-control',
  },
])

export const OWNER_IMPACT_TIMING_COMMANDS = Object.freeze([
  { id: 'fresh-production-scan', command: Object.freeze(['node', 'scripts/checks/locality-dependencies.mjs', '--report-only']) },
  { id: 'full-release-build', command: Object.freeze(['npm', 'run', 'format-build-test']) },
])

const OWNER_IMPACT_CONFIG = Object.freeze({
  aggregate_path: 'src/Wanxiangshu/Wanxiangshu.fsproj',
  project_directory: 'src/Wanxiangshu',
  full_threshold: 0.6,
  lockfile_path: 'package-lock.json',
  tool_manifest_path: '.config/dotnet-tools.json',
})

const exactKeys = (value, keys) => {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) return false
  const actual = Object.keys(value).sort(compareCanonicalTextV1)
  const expected = [...keys].sort(compareCanonicalTextV1)
  return actual.length === expected.length && actual.every((key, index) => key === expected[index])
}

const sortedUniqueIds = (rows) => rows.map(({ id }) => id).sort(compareCanonicalTextV1)
const sameIds = (actual, expected) => encodeCanonicalJsonV1(actual) === encodeCanonicalJsonV1(expected)
const text = (value) => typeof value === 'string' && value.length > 0
const repositoryPath = (value) => text(value) && !value.startsWith('/') && !value.includes('\\')
  && value.split('/').every((part) => part.length > 0 && !['.', '..'].includes(part))

const caseValid = (row) => exactKeys(row, [
  'id',
  'baseline_changed_path',
  'successor_path',
  'change_kind',
  'coverage',
]) && [row.id, row.change_kind, row.coverage].every(text)
  && repositoryPath(row.baseline_changed_path)
  && repositoryPath(row.successor_path)

const timingCommandValid = (row) => exactKeys(row, ['id', 'command'])
  && text(row.id)
  && Array.isArray(row.command)
  && row.command.length > 0
  && row.command.every(text)

const exactDefinition = (row, expected) => row.id === expected.id
  && row.baseline_changed_path === expected.baseline_changed_path
  && row.change_kind === expected.change_kind
  && row.coverage === expected.coverage

const exactDefinitions = (rows, expected) => rows.length === expected.length
  && expected.every((definition) => {
    const row = rows.find(({ id }) => id === definition.id)
    return row !== undefined && exactDefinition(row, definition)
  })

const planRowValid = (row) => exactKeys(row, [
  'id',
  'changed_path',
  'mode',
  'reason',
  'root_project_count',
  'project_count',
  'production_source_count',
  'compile_item_identities',
]) && text(row.id)
  && repositoryPath(row.changed_path)
  && ['none', 'focused', 'full'].includes(row.mode)
  && text(row.reason)
  && [row.root_project_count, row.project_count, row.production_source_count]
    .every((value) => Number.isSafeInteger(value) && value >= 0)
  && Array.isArray(row.compile_item_identities)
  && row.compile_item_identities.every(repositoryPath)

const environmentValid = (value) => exactKeys(value, [
  'platform',
  'release',
  'architecture',
  'cpu_model',
  'cpu_count',
  'node_version',
  'fable_version',
  'lockfile_digest',
  'tool_manifest_digest',
  'dependency_cache_identity_digest',
]) && [
  value.platform,
  value.release,
  value.architecture,
  value.cpu_model,
  value.node_version,
  value.fable_version,
].every(text)
  && Number.isSafeInteger(value.cpu_count)
  && value.cpu_count > 0
  && [value.lockfile_digest, value.tool_manifest_digest, value.dependency_cache_identity_digest]
    .every((digest) => /^sha256:[0-9a-f]{64}$/.test(digest))

const timingRowValid = (row) => exactKeys(row, ['id', 'raw_milliseconds', 'median_milliseconds'])
  && text(row.id)
  && Array.isArray(row.raw_milliseconds)
  && row.raw_milliseconds.length === 3
  && row.raw_milliseconds.every((value) => Number.isSafeInteger(value) && value >= 0)
  && Number.isSafeInteger(row.median_milliseconds)
  && [...row.raw_milliseconds].sort((left, right) => left - right)[1] === row.median_milliseconds

const measurementValid = (measurement, expectedIds, timingIds) => measurement === null || (
  exactKeys(measurement, ['commit', 'environment', 'structural', 'timing'])
  && /^[0-9a-f]{40}$/.test(measurement.commit)
  && environmentValid(measurement.environment)
  && Array.isArray(measurement.structural)
  && measurement.structural.every(planRowValid)
  && sameIds(sortedUniqueIds(measurement.structural), expectedIds)
  && Array.isArray(measurement.timing)
  && measurement.timing.every(timingRowValid)
  && sameIds(sortedUniqueIds(measurement.timing), timingIds)
)

export const validateOwnerImpactCorpusV1 = (corpus) => {
  if (!exactKeys(corpus, [
    'schema_version',
    'purpose',
    'baseline_commit',
    'aggregate_path',
    'project_directory',
    'full_threshold',
    'lockfile_path',
    'tool_manifest_path',
    'stable_cases',
    'control_cases',
    'timing_commands',
    'baseline_measurement',
  ]) || corpus.schema_version !== 1
    || corpus.purpose !== 'm6-owner-impact-report-only'
    || !/^[0-9a-f]{40}$/.test(corpus.baseline_commit)
    || ![corpus.aggregate_path, corpus.project_directory, corpus.lockfile_path, corpus.tool_manifest_path].every(repositoryPath)
    || corpus.full_threshold !== 0.6
    || !Array.isArray(corpus.stable_cases)
    || !corpus.stable_cases.every(caseValid)
    || !sameIds(sortedUniqueIds(corpus.stable_cases), OWNER_IMPACT_STABLE_CASE_IDS)
    || !exactDefinitions(corpus.stable_cases, OWNER_IMPACT_STABLE_CASES)
    || !Array.isArray(corpus.control_cases)
    || !corpus.control_cases.every(caseValid)
    || !sameIds(sortedUniqueIds(corpus.control_cases), OWNER_IMPACT_CONTROL_CASE_IDS)
    || !exactDefinitions(corpus.control_cases, OWNER_IMPACT_CONTROL_CASES)
    || !Array.isArray(corpus.timing_commands)
    || !corpus.timing_commands.every(timingCommandValid)
    || encodeCanonicalJsonV1(corpus.timing_commands) !== encodeCanonicalJsonV1(OWNER_IMPACT_TIMING_COMMANDS)
    || Object.entries(OWNER_IMPACT_CONFIG).some(([key, value]) => corpus[key] !== value)) {
    throw new TypeError('owner impact corpus does not match its closed report-only schema')
  }
  const timingIds = sortedUniqueIds(corpus.timing_commands)
  if (!sameIds(timingIds, OWNER_IMPACT_TIMING_IDS)
    || !measurementValid(
      corpus.baseline_measurement,
      [...OWNER_IMPACT_STABLE_CASE_IDS, ...OWNER_IMPACT_CONTROL_CASE_IDS].sort(compareCanonicalTextV1),
      timingIds,
    )
    || (corpus.baseline_measurement !== null && (
      corpus.baseline_measurement.commit !== corpus.baseline_commit
      || [...corpus.stable_cases, ...corpus.control_cases].some((definition) =>
        corpus.baseline_measurement.structural.find(({ id }) => id === definition.id)?.changed_path
          !== definition.baseline_changed_path)
    ))) {
    throw new TypeError('owner impact corpus baseline does not match its declared commit and cases')
  }
  return structuredClone(corpus)
}

const repositoryIdentity = (root, path) => relative(root, path).replaceAll('\\', '/')

export const measureOwnerImpactStructureV1 = (corpusInput, { root = ROOT } = {}) => {
  const corpus = validateOwnerImpactCorpusV1(corpusInput)
  const aggregatePath = resolve(root, corpus.aggregate_path)
  const projectDirectory = resolve(root, corpus.project_directory)
  return [...corpus.stable_cases, ...corpus.control_cases]
    .map((row) => {
      const plan = planImpactCompile({
        changedPaths: [resolve(root, row.successor_path)],
        projectDirectory,
        aggregatePath,
        fullThreshold: corpus.full_threshold,
      })
      return {
        id: row.id,
        changed_path: row.successor_path,
        mode: plan.mode,
        reason: plan.reason,
        root_project_count: plan.rootProjectPaths.length,
        project_count: plan.projectPaths.length,
        production_source_count: plan.compileItems.filter((path) => path.endsWith('.fs')).length,
        compile_item_identities: plan.compileItems.map((path) => repositoryIdentity(root, path)),
      }
    })
    .sort((left, right) => compareCanonicalTextV1(left.id, right.id))
}

const sha256 = (value) => `sha256:${createHash('sha256').update(value).digest('hex')}`

const environmentV1 = (corpus, root) => {
  const toolManifest = JSON.parse(readFileSync(resolve(root, corpus.tool_manifest_path), 'utf8'))
  const dependencyCache = resolve(process.env.NUGET_PACKAGES ?? join(process.env.DOTNET_CLI_HOME ?? homedir(), '.nuget/packages'))
  return {
    platform: platform(),
    release: release(),
    architecture: arch(),
    cpu_model: cpus()[0]?.model ?? 'unknown',
    cpu_count: cpus().length,
    node_version: process.version,
    fable_version: toolManifest?.tools?.fable?.version ?? 'unknown',
    lockfile_digest: sha256(readFileSync(resolve(root, corpus.lockfile_path))),
    tool_manifest_digest: sha256(readFileSync(resolve(root, corpus.tool_manifest_path))),
    dependency_cache_identity_digest: sha256(dependencyCache),
  }
}

const runCommand = (root, command) => {
  const executable = command[0] === 'node' ? process.execPath : command[0]
  const started = performance.now()
  const result = spawnSync(executable, command.slice(1), {
    cwd: root,
    encoding: 'utf8',
    env: process.env,
    maxBuffer: 256 * 1024 * 1024,
  })
  if (result.status !== 0) {
    throw new Error(`${command.join(' ')} failed (${result.status ?? result.signal})\n${result.stdout}\n${result.stderr}`)
  }
  return Math.round(performance.now() - started)
}

export const measureOwnerImpactTimingV1 = (corpusInput, { root = ROOT, run = runCommand } = {}) => {
  const corpus = validateOwnerImpactCorpusV1(corpusInput)
  return {
    environment: environmentV1(corpus, root),
    timing: corpus.timing_commands.map(({ id, command }) => {
      run(root, command)
      const rawMilliseconds = Array.from({ length: 3 }, () => run(root, command))
      return {
        id,
        raw_milliseconds: rawMilliseconds,
        median_milliseconds: [...rawMilliseconds].sort((left, right) => left - right)[1],
      }
    }),
  }
}

const median = (values) => {
  const sorted = [...values].sort((left, right) => left - right)
  return sorted[Math.floor(sorted.length / 2)]
}

export const buildOwnerImpactReportV1 = ({ corpus: corpusInput, candidate_commit: candidateCommit, structural, timing = null }) => {
  const corpus = validateOwnerImpactCorpusV1(corpusInput)
  const expectedStructuralIds = [...OWNER_IMPACT_STABLE_CASE_IDS, ...OWNER_IMPACT_CONTROL_CASE_IDS].sort(compareCanonicalTextV1)
  if (!/^[0-9a-f]{40}$/.test(candidateCommit)
    || !Array.isArray(structural)
    || !structural.every(planRowValid)
    || !sameIds(sortedUniqueIds(structural), expectedStructuralIds)
    || [...corpus.stable_cases, ...corpus.control_cases].some((definition) =>
      structural.find(({ id }) => id === definition.id)?.changed_path !== definition.successor_path)
    || timing !== null && (
      !exactKeys(timing, ['environment', 'timing'])
      || !environmentValid(timing.environment)
      || !Array.isArray(timing.timing)
      || !timing.timing.every(timingRowValid)
      || !sameIds(sortedUniqueIds(timing.timing), OWNER_IMPACT_TIMING_IDS)
    )) {
    throw new TypeError('owner impact candidate measurement is invalid')
  }
  const baseline = corpus.baseline_measurement
  const stableIds = new Set(OWNER_IMPACT_STABLE_CASE_IDS)
  const stableStructural = structural.filter(({ id }) => stableIds.has(id))
  const findings = []
  let comparison = null
  if (baseline !== null) {
    const baselineStable = baseline.structural.filter(({ id }) => stableIds.has(id))
    const baselineMedian = median(baselineStable.map(({ production_source_count: count }) => count))
    const candidateMedian = median(stableStructural.map(({ production_source_count: count }) => count))
    const environmentMatches = timing === null
      || encodeCanonicalJsonV1(timing.environment) === encodeCanonicalJsonV1(baseline.environment)
    if (!environmentMatches) findings.push({ code: 'measurement-environment-mismatch' })
    const timingComparison = timing === null || !environmentMatches ? [] : timing.timing.map((candidate) => {
      const previous = baseline.timing.find(({ id }) => id === candidate.id)
      const changePercent = ((candidate.median_milliseconds - previous.median_milliseconds) / previous.median_milliseconds) * 100
      if (changePercent > 5) findings.push({ code: 'wall-clock-regression-over-five-percent', command_id: candidate.id, change_percent: Math.round(changePercent * 100) / 100 })
      return { id: candidate.id, baseline_milliseconds: previous.median_milliseconds, candidate_milliseconds: candidate.median_milliseconds, change_percent: Math.round(changePercent * 100) / 100 }
    })
    comparison = {
      structural_median: {
        baseline_source_count: baselineMedian,
        candidate_source_count: candidateMedian,
        reduction_percent: Math.round(((baselineMedian - candidateMedian) / baselineMedian) * 10_000) / 100,
      },
      timing: timingComparison,
    }
  }
  return {
    schema_version: 1,
    report_kind: 'owner-impact-report-only',
    baseline_commit: corpus.baseline_commit,
    candidate_commit: candidateCommit,
    structural,
    timing,
    comparison,
    findings,
  }
}

const parseArguments = (arguments_) => {
  let corpusPath = DEFAULT_CORPUS
  let measureTiming = false
  let writeBaseline = false
  for (let index = 0; index < arguments_.length; index += 1) {
    if (arguments_[index] === '--measure-timing') measureTiming = true
    else if (arguments_[index] === '--write-baseline') writeBaseline = true
    else if (arguments_[index] === '--corpus' && arguments_[index + 1]) corpusPath = resolve(arguments_[++index])
    else throw new Error(`unknown or incomplete option: ${arguments_[index]}`)
  }
  if (writeBaseline && measureTiming) throw new Error('--write-baseline always performs timing; do not combine it with --measure-timing')
  return { corpusPath, measureTiming, writeBaseline }
}

const gitState = (root = ROOT) => {
  const commit = spawnSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' })
  const status = spawnSync('git', ['status', '--porcelain=v1', '--untracked-files=all'], { cwd: root, encoding: 'utf8' })
  if (commit.status !== 0 || status.status !== 0) throw new Error(commit.stderr || status.stderr || 'cannot inspect Git state')
  return { commit: commit.stdout.trim(), clean: status.stdout.length === 0 }
}

export const writeOwnerImpactBaselineV1 = (corpusPath, {
  root = ROOT,
  inspectGit = gitState,
  measureStructure = measureOwnerImpactStructureV1,
  measureTiming = measureOwnerImpactTimingV1,
  writeText = writeFileSync,
  rename = renameSync,
  unlink = unlinkSync,
} = {}) => {
  const corpus = validateOwnerImpactCorpusV1(JSON.parse(readFileSync(corpusPath, 'utf8')))
  const before = inspectGit(root)
  if (!before.clean) throw new Error('baseline measurement requires a clean Git checkout')
  if (before.commit !== corpus.baseline_commit) throw new Error('baseline checkout does not match baseline_commit')
  if (corpus.baseline_measurement !== null) throw new Error('refusing to overwrite an existing baseline measurement')
  const structural = measureStructure(corpus, { root })
  const measuredTiming = measureTiming(corpus, { root })
  const after = inspectGit(root)
  if (!after.clean || after.commit !== before.commit) throw new Error('baseline checkout changed during measurement')
  const completed = validateOwnerImpactCorpusV1({
    ...corpus,
    baseline_measurement: {
      commit: before.commit,
      environment: measuredTiming.environment,
      structural,
      timing: measuredTiming.timing,
    },
  })
  const temporaryPath = `${corpusPath}.tmp-${process.pid}`
  try {
    writeText(temporaryPath, `${JSON.stringify(completed, null, 2)}\n`, 'utf8')
    validateOwnerImpactCorpusV1(JSON.parse(readFileSync(temporaryPath, 'utf8')))
    rename(temporaryPath, corpusPath)
  } catch (error) {
    try { unlink(temporaryPath) } catch {}
    throw error
  }
  return completed
}

const main = () => {
  const { corpusPath, measureTiming, writeBaseline } = parseArguments(process.argv.slice(2))
  if (writeBaseline) {
    writeOwnerImpactBaselineV1(corpusPath)
    return
  }
  const corpus = validateOwnerImpactCorpusV1(JSON.parse(readFileSync(corpusPath, 'utf8')))
  if (corpus.baseline_measurement === null) throw new Error('candidate report requires a completed baseline measurement')
  const before = gitState()
  if (!before.clean) throw new Error('candidate measurement requires a clean Git checkout')
  const structural = measureOwnerImpactStructureV1(corpus)
  const timing = measureTiming ? measureOwnerImpactTimingV1(corpus) : null
  const after = gitState()
  if (!after.clean || after.commit !== before.commit) throw new Error('candidate checkout changed during measurement')
  process.stdout.write(`${JSON.stringify(buildOwnerImpactReportV1({
    corpus,
    candidate_commit: before.commit,
    structural,
    timing,
  }), null, 2)}\n`)
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    main()
  } catch (error) {
    process.stderr.write(`${error instanceof Error ? error.stack : String(error)}\n`)
    process.exitCode = 1
  }
}
