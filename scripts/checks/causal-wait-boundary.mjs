#!/usr/bin/env node
/**
 * Causal-wait architecture gate.
 *
 * CAUSAL-003/004:
 *   - Fact and Journal carriers contain no executable causal-wait vocabulary.
 *   - Snapshot readers, registry implementation, diagnostic bridges and their
 *     file locator stay inside Execution/Session/Wait.
 *
 * Migration guards retained here:
 *   - critical migrated sites do not reintroduce bare TCS.Task awaits;
 *   - CausalWaitRegistry mutable fields carry DSL-MUTABLE annotations.
 */

import { existsSync, readFileSync, realpathSync, statSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
import { walk } from '../lib/walk.mjs'
import { maskFSharpTrivia } from '../lib/fsharp-source.mjs'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..')
const WAIT_OWNER = 'Execution/Session/Wait/'
const DIAGNOSTIC_COMPOSITION_ROOT = 'OpenCode/Plugin/PluginHostWiring.fs'
const DIAGNOSTIC_LOCATOR = 'causal-waits.json'
const normalize = (path) => path.replace(/\\/g, '/')

const DURABLE_VOCABULARY = [
  ['IWaitSnapshotReader', /\bIWaitSnapshotReader\b/],
  ['IWaitObserver', /\bIWaitObserver\b/],
  ['CausalWaitRegistry', /\bCausalWaitRegistry\b/],
  ['CausalWaitHub', /\bCausalWaitHub\b/],
  ['CausalWaitBridge', /\bCausalWaitBridge\b/],
  ['CausalWaitSurface', /\bCausalWaitSurface\b/],
  ['CausalAwait', /\bCausalAwait\b/],
  ['DiagnosticWaitSnapshot', /\bDiagnosticWaitSnapshot\b/],
  ['DiagnosticWaitExit', /\bDiagnosticWaitExit\b/],
  ['DiagnosticWait', /\bDiagnosticWait\b/],
  ['WaitKind', /\bWaitKind\b/],
  ['CausalWait', /\bCausalWait\b/],
]

const SNAPSHOT_READ_CAPABILITIES = [
  ['IWaitSnapshotReader', /\bIWaitSnapshotReader\b/],
  ['DiagnosticWaitSnapshot', /\bDiagnosticWaitSnapshot\b/],
  ['CausalWaitRegistry', /\bCausalWaitRegistry\b/],
  ['CausalWaitBridge', /\bCausalWaitBridge\b/],
  ['CausalWaitSurface', /\bCausalWaitSurface\b/],
  ['CausalWaitHub.reader', /\bCausalWaitHub\.reader\b/],
  ['CausalWaitHub.snapshot', /\bCausalWaitHub\.snapshot\b/],
  ['CausalWaitHub.read', /\bCausalWaitHub\.read\b/],
  ['CausalWaitHub.frontiers', /\bCausalWaitHub\.frontiers\b/],
  ['CausalWaitHub.writeToWorkspace', /\bCausalWaitHub\.writeToWorkspace\b/],
]

const firstToken = (rules, text) => rules.find(([, pattern]) => pattern.test(text))?.[0]

const isDurableCarrier = (relativePath) => {
  const parts = relativePath.split('/')
  const name = parts.at(-1)
  return parts.includes('Journal') || name === 'Fact.fs' || name === 'Facts.fs'
}

const durableViolation = (relativePath, token) =>
  `${relativePath}: causal-wait vocabulary "${token}" must not enter Fact/Journal`

const readViolation = (relativePath, token) =>
  `${relativePath}: diagnostics read capability "${token}" is confined to Execution/Session/Wait`

export function analyzeObservationBoundary(files) {
  if (!Array.isArray(files)) throw new TypeError('causal-wait-boundary: files must be an array')

  const violations = []
  for (const file of files) {
    if (typeof file?.rel !== 'string' || typeof file?.text !== 'string') {
      throw new TypeError('causal-wait-boundary: every file requires string rel and text fields')
    }

    const relativePath = normalize(file.rel)
    const executable = maskFSharpTrivia(file.text)

    if (isDurableCarrier(relativePath)) {
      const token = firstToken(DURABLE_VOCABULARY, executable)
      if (token !== undefined) {
        violations.push(durableViolation(relativePath, token))
        continue
      }
    }

    if (relativePath.startsWith(WAIT_OWNER)) continue

    const withoutDiagnosticInjection = relativePath === DIAGNOSTIC_COMPOSITION_ROOT
      ? executable.replace(/\bCausalWaitBridge\.target\b/g, '')
      : executable
    const capability = firstToken(SNAPSHOT_READ_CAPABILITIES, withoutDiagnosticInjection)
    if (capability !== undefined) {
      violations.push(readViolation(relativePath, capability))
      continue
    }

    if (/\bCausalWaitHub\b/.test(withoutDiagnosticInjection)) {
      violations.push(readViolation(relativePath, 'CausalWaitHub'))
      continue
    }

    if (file.text.includes(DIAGNOSTIC_LOCATOR)) {
      violations.push(readViolation(relativePath, DIAGNOSTIC_LOCATOR))
    }
  }

  return violations
}

export function collectCausalWaitBoundaryFiles(root = ROOT) {
  const sourceRoot = join(root, 'src/Wanxiangshu')
  if (!existsSync(sourceRoot) || !statSync(sourceRoot).isDirectory()) {
    throw new Error('causal-wait-boundary: required scan root missing: src/Wanxiangshu')
  }

  return [...walk(sourceRoot)]
    .filter((path) => path.endsWith('.fs'))
    .map((path) => ({
      rel: normalize(path.slice(sourceRoot.length + 1)),
      text: readFileSync(path, 'utf8'),
    }))
}

const SELF_TEST_LEGAL = [
  {
    rel: 'Execution/Session/Wait/Registry.fs',
    text: 'let reader: IWaitSnapshotReader = registry :> IWaitSnapshotReader\n',
  },
  {
    rel: 'Persistence/Journal/Codec.fs',
    text: 'let decoy = "CausalWait WaitKind IWaitSnapshotReader"\n',
  },
  {
    rel: 'Change/Fact.fs',
    text: 'let decoy = @"DiagnosticWait CausalAwait"\n',
  },
  {
    rel: 'Interaction/Dispatch/FutureDecision.fs',
    text: [
      '// IWaitSnapshotReader CausalWaitHub.snapshot',
      'let ordinary = "CausalWaitBridge CausalWaitSurface"',
      'let triple = """DiagnosticWaitSnapshot"""',
      '',
    ].join('\n'),
  },
  { rel: 'Change/Job.fs', text: 'let observer: IWaitObserver = injectedObserver\n' },
  {
    rel: DIAGNOSTIC_COMPOSITION_ROOT,
    text: 'runtime.BindDiagnosticTarget(CausalWaitBridge.target workspace) |> ignore\n',
  },
]

const mutateSelfTest = (relativePath, addition) => {
  let applied = false
  const files = SELF_TEST_LEGAL.map((file) => {
    if (file.rel !== relativePath) return file
    const text = file.text + addition
    applied = applied || text !== file.text
    return { ...file, text }
  })
  return { applied, files }
}

const same = (left, right) => JSON.stringify(left) === JSON.stringify(right)

export function runObservationBoundarySelfTest() {
  const cases = []
  cases.push({
    name: 'known-good: owner code and lexical decoys pass',
    ok: analyzeObservationBoundary(SELF_TEST_LEGAL).length === 0,
  })

  const expectSingle = (name, relativePath, addition, expected) => {
    const mutation = mutateSelfTest(relativePath, addition)
    const actual = analyzeObservationBoundary(mutation.files)
    cases.push({
      name,
      ok: mutation.applied && same(actual, [expected]),
      detail: mutation.applied ? actual.join(' | ') : 'target mutation was not applied',
    })
  }

  expectSingle(
    'known-bad: Journal vocabulary',
    'Persistence/Journal/Codec.fs',
    'let reader: IWaitSnapshotReader = source\n',
    durableViolation('Persistence/Journal/Codec.fs', 'IWaitSnapshotReader'),
  )
  expectSingle(
    'known-bad: Fact vocabulary',
    'Change/Fact.fs',
    'let wait: DiagnosticWait = source\n',
    durableViolation('Change/Fact.fs', 'DiagnosticWait'),
  )
  expectSingle(
    'known-bad: unlisted decision reader',
    'Interaction/Dispatch/FutureDecision.fs',
    'let reader: IWaitSnapshotReader = source\n',
    readViolation('Interaction/Dispatch/FutureDecision.fs', 'IWaitSnapshotReader'),
  )
  expectSingle(
    'known-bad: opened reader hub alias',
    'Interaction/Dispatch/FutureDecision.fs',
    'open Wanxiangshu.Execution.Session.Wait.CausalWaitHub\nlet leaked = snapshot ()\n',
    readViolation('Interaction/Dispatch/FutureDecision.fs', 'CausalWaitHub'),
  )
  expectSingle(
    'known-bad: global observer hub outside owner',
    'Change/Job.fs',
    'let observer = CausalWaitHub.observer\n',
    readViolation('Change/Job.fs', 'CausalWaitHub'),
  )
  expectSingle(
    'known-bad: diagnostic adapter outside composition root',
    'Change/Job.fs',
    'let sink = CausalWaitBridge.target workspace\n',
    readViolation('Change/Job.fs', 'CausalWaitBridge'),
  )
  expectSingle(
    'known-bad: diagnostic locator outside owner',
    'Interaction/Dispatch/FutureDecision.fs',
    'let path = ".wanxiangshu/diagnostics/causal-waits.json"\n',
    readViolation('Interaction/Dispatch/FutureDecision.fs', DIAGNOSTIC_LOCATOR),
  )

  return cases
}

const criticalWaitSites = [
  'Execution/Delegation/SyncDelegate/Workflow.fs',
  'Mission/Finality/Cohort.fs',
  'Mission/Finality/OpenCode/Tool.fs',
  'Execution/Delegation/Fork/OpenCode/JoinTool.fs',
  'Change/Host/Host.fs',
  'Mission/Review/Barrier/Workflow.fs',
  'Change/Job.fs',
]

const analyzeCriticalWaits = (files) => {
  const violations = []
  const byPath = new Map(files.map((file) => [file.rel, file.text]))

  for (const relativePath of criticalWaitSites) {
    const text = byPath.get(relativePath)
    if (text === undefined) {
      violations.push(`${relativePath}: critical causal-wait site missing on disk`)
      continue
    }

    const lines = text.split('\n')
    for (let index = 0; index < lines.length; index += 1) {
      const line = lines[index]
      if (!/\b(return!|do!)\s+\w[\w.]*\.Task\b/.test(line)) continue
      if (/\bcancel\.Task\b/.test(line)) continue

      const nearby = lines.slice(Math.max(0, index - 40), index + 1).join('\n')
      if (/CausalAwait\.await/.test(nearby)) continue

      const preceding = lines.slice(Math.max(0, index - 80), index).join('\n')
      if (/let private concurrent/.test(preceding) && /return! tcs\.Task/.test(line)) continue

      violations.push(
        `${relativePath}:${index + 1}: bare TCS.Task await outside CausalAwait (${line.trim()})`,
      )
    }
  }

  return violations
}

const analyzeRegistryMutableAnnotations = (files) => {
  const relativePath = 'Execution/Session/Wait/Registry.fs'
  const file = files.find((candidate) => candidate.rel === relativePath)
  if (file === undefined) return [`${relativePath}: missing on disk`]

  const violations = []
  const lines = file.text.split('\n')
  for (let index = 0; index < lines.length; index += 1) {
    if (!/\blet mutable\b/.test(lines[index])) continue
    const preceding = lines.slice(Math.max(0, index - 2), index).join('\n')
    if (!/\/\/\s*DSL-MUTABLE:/.test(preceding)) {
      violations.push(`${relativePath}:${index + 1}: mutable lacks DSL-MUTABLE annotation`)
    }
  }
  return violations
}

const isMainModule = (() => {
  try {
    return import.meta.url === pathToFileURL(realpathSync(process.argv[1])).href
  } catch {
    return false
  }
})()

if (isMainModule) {
  const selfTests = runObservationBoundarySelfTest()
  const failedSelfTests = selfTests.filter((fixture) => !fixture.ok)
  for (const fixture of selfTests) {
    const marker = fixture.ok ? '✓' : '✗'
    console.log(`  ${marker} ${fixture.name}${fixture.detail && !fixture.ok ? ` — ${fixture.detail}` : ''}`)
  }

  let files
  try {
    files = collectCausalWaitBoundaryFiles()
  } catch (error) {
    console.error(`causal-wait-boundary FAILED: ${error.message}`)
    process.exit(1)
  }

  const problems = [
    ...failedSelfTests.map((fixture) => `self-test failed: ${fixture.name}`),
    ...analyzeObservationBoundary(files),
    ...analyzeCriticalWaits(files),
    ...analyzeRegistryMutableAnnotations(files),
  ]

  if (problems.length > 0) {
    console.error('causal-wait-boundary FAILED:')
    for (const problem of problems) console.error(`  - ${problem}`)
    process.exit(1)
  }

  console.log(`causal-wait-boundary OK — ${files.length} production files, ${selfTests.length} fixtures`)
}
