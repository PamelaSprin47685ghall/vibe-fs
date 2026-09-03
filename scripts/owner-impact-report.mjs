#!/usr/bin/env node

import { createHash } from 'node:crypto'
import { spawnSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
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
    || !Array.isArray(corpus.control_cases)
    || !corpus.control_cases.every(caseValid)
    || !sameIds(sortedUniqueIds(corpus.control_cases), OWNER_IMPACT_CONTROL_CASE_IDS)
    || !Array.isArray(corpus.timing_commands)
    || !corpus.timing_commands.every(timingCommandValid)) {
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
  for (let index = 0; index < arguments_.length; index += 1) {
    if (arguments_[index] === '--measure-timing') measureTiming = true
    else if (arguments_[index] === '--corpus' && arguments_[index + 1]) corpusPath = resolve(arguments_[++index])
    else throw new Error(`unknown or incomplete option: ${arguments_[index]}`)
  }
  return { corpusPath, measureTiming }
}

const currentCommit = () => {
  const result = spawnSync('git', ['rev-parse', 'HEAD'], { cwd: ROOT, encoding: 'utf8' })
  if (result.status !== 0) throw new Error(result.stderr || 'cannot resolve current Git commit')
  return result.stdout.trim()
}

const main = () => {
  const { corpusPath, measureTiming } = parseArguments(process.argv.slice(2))
  const corpus = validateOwnerImpactCorpusV1(JSON.parse(readFileSync(corpusPath, 'utf8')))
  const structural = measureOwnerImpactStructureV1(corpus)
  const timing = measureTiming ? measureOwnerImpactTimingV1(corpus) : null
  process.stdout.write(`${JSON.stringify(buildOwnerImpactReportV1({
    corpus,
    candidate_commit: currentCommit(),
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
