import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import * as recovery from '../../../dist/Execution/Session/ChatExecution/RecoveryRuntimeSurface.js'
import * as chatExecutionRecoverySurface from '../../../dist/Execution/Session/ChatExecution/RecoverySurface.js'
import * as recoveryHost from '../../../dist/OpenCode/Host/SessionRecoveryHostSurface.js'
import * as transaction from '../../../dist/OpenCode/Host/ChatAdmission/TransactionSurface.js'
import * as lifecycle from '../../../dist/Execution/Session/ChatExecution/Surface.js'

const ROOT = fileURLToPath(new URL('../../..', import.meta.url))
const recoveryHostSource = readFileSync(join(ROOT, 'src/Wanxiangshu/OpenCode/Host/SessionRecoveryHost.fs'), 'utf8')

const evidence = (cut) => ({
  sessionId: `ses-crash-${cut}`,
  physicalUserMessageId: `msg-crash-${cut}`,
  logicalRunId: `run-crash-${cut}`,
  authorityRootUserMessageId: `root-crash-${cut}`,
  identitySeed: {
    participantIdentity: {
      selectedAgent: 'coder',
    },
  },
  providerRun: `provider-crash-${cut}`,
})

const action = (kind, cut, extra = {}) => ({
  kind,
  evidence: {
    ...evidence(cut),
    authorityKind: 'HumanRoot',
    identitySeed: {
      kind: 'RootSelection',
      ownerSession: null,
      ownerLogicalRun: null,
      ownerAuthorityRoot: null,
      participantIdentity: {
        selectedAgent: 'coder',
        canonicalRole: 'coder',
        selectedTier: 'deep',
        persona: 'Coder',
        personaCatalogVersion: 1,
        origin: 'ResolvedAtRoot',
      },
    },
    origin: 'HumanRoot',
    requestKind: 'work-main',
    projectionChoice: { kind: 'UseCommittedEpoch' },
  },
  appendOutcome: 'Committed',
  ...extra,
})

const CUTS = [
  ['A', 'before Accepted append', [], 'None', { activeCapacity: 0, providerBinding: 0, hostProjected: false }, 0, 'NoDurableExecution'],
  ['B', 'after Accepted before lease', ['Accepted'], 'Accepted', { activeCapacity: 0, providerBinding: 0, hostProjected: false }, 0, 'ResumePreProvider'],
  ['C', 'after lease before execution binding', ['Accepted'], 'Accepted', { activeCapacity: 1, providerBinding: 0, hostProjected: false }, 0, 'ResumePreProvider'],
  ['D', 'after binding before Host projection', ['Accepted'], 'Accepted', { activeCapacity: 1, providerBinding: 1, hostProjected: false }, 0, 'ResumePreProvider'],
  ['E', 'after Host projection before ProviderStarted', ['Accepted'], 'Accepted', { activeCapacity: 1, providerBinding: 1, hostProjected: true }, 0, 'ResumePreProvider'],
  ['F', 'after ProviderStarted before Terminal', ['Accepted', 'ProviderStarted'], 'ProviderStarted', { activeCapacity: 1, providerBinding: 1, hostProjected: true }, 1, 'Ignore'],
  ['G', 'after Terminal before exact release', ['Accepted', 'ProviderStarted', 'Terminal'], 'Terminal', { activeCapacity: 1, providerBinding: 1, hostProjected: true }, 1, 'ReconcilePhysical'],
  ['H', 'after exact release before Hook return', ['Accepted', 'ProviderStarted', 'Terminal'], 'Terminal', { activeCapacity: 0, providerBinding: 0, hostProjected: true }, 1, 'Ignore'],
  ['I', 'after exact release before fatal propagation', ['Accepted', 'ProviderStarted', 'Terminal'], 'Terminal', { activeCapacity: 0, providerBinding: 0, hostProjected: true }, 1, 'Ignore'],
]

const expectedEffects = (decision) => ({
  NoDurableExecution: [],
  ResumePreProvider: ['ResumePreProvider', 'ResumePreProvider'],
  Ignore: [],
  ReconcilePhysical: [
    'ReconcilePhysical:ReleaseTerminalResource',
    'ReconcilePhysical:ReleaseTerminalResource',
  ],
})[decision]

const withRecoveryHost = async (label, portOutcome, action) => {
  const directory = mkdtempSync(join(tmpdir(), `wxs-chat-recovery-${label}-`))
  const host = await recoveryHost.bootRecoveryHost(directory, portOutcome)

  try {
    await action(host)
  } finally {
    recoveryHost.disposeRecoveryHost(host)
    rmSync(directory, { recursive: true, force: true })
  }
}

const sessionOf = (suffix) => `ses-recovery-${suffix}`
const physicalOf = (suffix) => `msg-recovery-${suffix}`

const assertManualDisposition = (manual, sessionId, physicalId) => {
  assert.equal(manual.reason, 'NoAuthorizedProviderDisposition')
  assert.equal(manual.observation, 'ProviderAbsent')
  assert.equal(manual.lifecycle, 'Accepted')
  assert.equal(manual.sessionId, sessionId)
  assert.equal(manual.physicalUserMessageId, physicalId)
}

const matrix = [
  ['CrashAfterAcceptance', 'ResumePreProvider', 'ResumeAcceptedAdmission', null],
  ['AcceptedProviderAlive', 'ReconcilePhysical', 'PersistProviderStarted', null],
  ['AcceptedProviderTerminal', 'ReconcilePhysical', 'PersistProviderStartedAndTerminal', 'Completed'],
  ['ProviderAlive', 'Ignore', 'ProviderStillAlive', null],
  ['ProviderTerminalCompleted', 'Finalize', 'PersistTerminal', 'Completed'],
  ['ProviderTerminalFailed', 'Finalize', 'PersistTerminal', 'Failed'],
  ['ProviderTerminalCancelled', 'Finalize', 'PersistTerminal', 'Cancelled'],
  ['ProviderTerminalRejected', 'Finalize', 'PersistTerminal', 'Rejected'],
  ['RetryEligible', 'Ignore', 'ProviderRecoveryOwned', null],
  ['RetryExhausted', 'Finalize', 'PersistTerminal', 'Failed'],
  ['Superseded', 'Finalize', 'PersistTerminal', 'Cancelled'],
  ['MissingReceipt', 'MarkManualIntervention', 'MissingExternalReceipt', null],
  ['AmbiguousReceipt', 'MarkManualIntervention', 'AmbiguousExternalReceipt', null],
  ['PhysicalOutcomeUnknown', 'MarkManualIntervention', 'PhysicalOutcomeUnknown', null],
  ['PersistenceUnknown', 'MarkManualIntervention', 'PersistenceOutcomeUnknown', null],
  ['DuplicateRecovery', 'Ignore', 'RecoveryAlreadyCommitted', null],
  ['StaleProvider', 'Ignore', 'StalePhysicalEvidence', null],
  ['StaleKey', 'Ignore', 'StalePhysicalEvidence', null],
  ['StalePolicy', 'Ignore', 'StalePolicyEvidence', null],
  ['TerminalResourceHeld', 'ReconcilePhysical', 'ReleaseTerminalResource', 'Completed'],
  ['TerminalResourceReleased', 'Ignore', 'DurableTerminalAlreadySettled', null],
  ['ProviderAbsentWithoutPolicy', 'MarkManualIntervention', 'NoAuthorizedProviderDisposition', null],
]

test('WHAT[CHATEXEC-012] A–I production transaction and lifecycle prefixes drive recovery decisions', async () => {
  const transactionResults = new Map()
  for (const cut of ['A', 'B', 'C', 'D', 'E']) {
    transactionResults.set(cut, await transaction.transactionScenario(evidence(cut), `Crash${cut}`, 'None'))
  }

  const lifecycleResults = new Map()
  lifecycleResults.set('F', await lifecycle.providerLifecycleScenario([
    action('Accept', 'F'),
    action('ProviderStarted', 'F'),
    { kind: 'ProviderWork' },
  ]))
  for (const cut of ['G', 'H', 'I']) {
    lifecycleResults.set(cut, await lifecycle.providerLifecycleScenario([
      action('Accept', cut),
      action('ProviderStarted', cut),
      { kind: 'ProviderWork' },
      action('Terminal', cut, { disposition: 'Completed' }),
    ]))
  }

  for (const restart of ['PluginReload', 'ProcessRestart']) {
    const recovered = await recovery.admissionCrashPointScenarios(CUTS.map(([cut]) => cut), restart, 'NotCommitted', 'Applied')

    for (const [index, [cut, label, facts, phase, local, providerCount, decision]] of CUTS.entries()) {
      const recoveryResult = recovered.scenarios[index]
      assert.equal(recoveryResult.cut, cut, label)
      assert.equal(recoveryResult.restart, restart, label)
      assert.deepEqual(recoveryResult.decisions, [decision, decision], label)
      assert.deepEqual(recoveryResult.effects, expectedEffects(decision), label)
      assert.equal(recoveryResult.commitment, 'NotCommitted', label)

      if (transactionResults.has(cut)) {
        const result = transactionResults.get(cut)
        assert.equal(result.crashed, true, label)
        assert.equal(result.durableLifecycle, phase, label)
        assert.deepEqual(result.admission, local, label)
        assert.equal(result.providerCount, providerCount, label)
      } else {
        const result = lifecycleResults.get(cut)
        assert.equal(result.ok, true, `${label}: ${JSON.stringify(result.error)}`)
        assert.equal(result.projection.phase, phase, label)
        assert.deepEqual(result.appendCounts, {
          accepted: facts.includes('Accepted') ? 1 : 0,
          providerStarted: facts.includes('ProviderStarted') ? 1 : 0,
          terminal: facts.includes('Terminal') ? 1 : 0,
        }, label)
        assert.equal(result.semanticTransitionCount, facts.length, label)
        assert.equal(result.providerWorkCount, providerCount, label)
      }
    }
  }
})

test('WHAT[CHATEXEC-012] duplicate crash-cut requests and input permutations preserve canonical production recovery', async () => {
  const permuted = ['B', 'A', 'E', 'C', 'D']
  const recovered = await recovery.admissionCrashPointScenarios(permuted, 'ProcessRestart', 'NotCommitted', 'Applied')
  assert.equal(recovered.scenarios.length, 5)
  for (const item of recovered.scenarios) {
    assert.equal(item.restart, 'ProcessRestart')
    assert.ok(['NoDurableExecution', 'ResumePreProvider'].includes(item.decisions[0]))
  }
})

test('WHAT[CHATEXEC-012] duplicate terminal and stale recovery evidence are semantically inert', () => {
  for (const scenario of ['DuplicateRecovery', 'StaleProvider', 'StaleKey', 'StalePolicy']) {
    const decision = chatExecutionRecoverySurface.decideScenario(scenario)
    assert.equal(decision.kind, 'Ignore')
  }
})

test('WHAT[CHATEXEC-012] lifecycle recovery interprets every typed decision through its owner port', async () => {
  const cases = [
    ['ProviderAlive', 'Ignore', []],
    ['AcceptedProviderAlive', 'ReconcilePhysical', ['ReconcilePhysical:PersistProviderStarted']],
    ['CrashAfterAcceptance', 'ResumePreProvider', ['ResumePreProvider']],
    ['RetryEligible', 'Ignore', []],
    ['ProviderTerminalCompleted', 'Finalize', ['Finalize:Completed']],
    ['MissingReceipt', 'MarkManualIntervention', ['MarkManualIntervention:MissingExternalReceipt']],
  ]

  for (const [scenario, decision, expected] of cases) {
    const result = await recovery.recoverScenarios([scenario])
    assert.deepEqual(result.decisions, [decision], scenario)
    assert.deepEqual(result.effects, expected, scenario)
  }
})

test('WHAT[CHATEXEC-012] only causal lifecycle signals enter the shared recovery runtime', () => {
  assert.deepEqual(recovery.lifecycleSignals(), [
    'DurabilityActivated',
    'PluginRuntimeReloaded',
    'ExactAssistantStarted',
    'ExactAssistantTerminal',
    'SessionAborted',
    'SessionDeleted',
    'SessionCancelled',
    'CapacityProjectionReplayed',
  ])
})

test('WHAT[CHATEXEC-012] absent recovery port publishes exactly one manual disposition and no resume', async () => {
  await withRecoveryHost('absent', 'absent', async (host) => {
    const result = await recoveryHost.resumeAccepted(host, sessionOf('absent'), physicalOf('absent'))

    assert.equal(result.calls, 0, 'absent port makes no acceptance call')
    assert.equal(result.manuals.length, 1)
    assertManualDisposition(result.manuals[0], 'ses-recovery-absent', 'msg-recovery-absent')
  })
})

test('WHAT[CHATEXEC-012] rejecting port is awaited and publishes the same single manual', async () => {
  await withRecoveryHost('reject', 'reject', async (host) => {
    const result = await recoveryHost.resumeAccepted(host, sessionOf('reject'), physicalOf('reject'))

    assert.equal(result.calls, 1, 'rejection must be awaited exactly once')
    assert.equal(result.manuals.length, 1)
    assertManualDisposition(result.manuals[0], 'ses-recovery-reject', 'msg-recovery-reject')
  })
})

test('WHAT[CHATEXEC-012] accepting port awaits ordinary admission and emits no manual block', async () => {
  await withRecoveryHost('accept', 'accept', async (host) => {
    const result = await recoveryHost.resumeAccepted(host, sessionOf('accept'), physicalOf('accept'))

    assert.equal(result.calls, 1, 'acceptance must be awaited exactly once')
    assert.deepEqual(result.manuals, [])
  })
})

test('WHAT[CHATEXEC-012] duplicate resume signals keep exactly one manual', async () => {
  await withRecoveryHost('duplicate', 'absent', async (host) => {
    await recoveryHost.resumeAccepted(host, sessionOf('duplicate'), physicalOf('duplicate'))
    const result = await recoveryHost.resumeAccepted(host, sessionOf('duplicate'), physicalOf('duplicate'))

    assert.equal(result.manuals.length, 1)
    assertManualDisposition(result.manuals[0], 'ses-recovery-duplicate', 'msg-recovery-duplicate')
  })
})

test('WHAT[CHATEXEC-012] superseded exact capacity release is an idempotent recovery no-op', () => {
  const release = recoveryHostSource.match(/let release \(key: ChatExecutionKey\) =([\s\S]*?)\n\s*let requirePersistence/)
  assert.ok(release, 'recovery capacity release boundary must remain explicit')
  assert.match(release[1], /CapacityTransitionOutcome\.Applied\s*\n\s*\| CapacityTransitionOutcome\.AlreadyApplied\s*\n\s*\| CapacityTransitionOutcome\.StaleFence ->\s*Task\.FromResult\(\(\)\)/)
  assert.match(release[1], /CapacityTransitionOutcome\.Conflict ->[\s\S]*?managed chat recovery exact capacity release was rejected/)
})

test('WHAT[CHATEXEC-012] durable facts plus explicit physical evidence exhaustively determine recovery', () => {
  for (const [scenario, kind, request, disposition] of matrix) {
    assert.deepEqual(chatExecutionRecoverySurface.decideScenario(scenario), { kind, request, disposition }, scenario)
  }
})

test('WHAT[CHATEXEC-012] duplicate evaluation is deterministic and effect-free', () => {
  for (const [scenario] of matrix) {
    const first = chatExecutionRecoverySurface.decideScenario(scenario)
    const second = chatExecutionRecoverySurface.decideScenario(scenario)
    assert.deepEqual(second, first, scenario)
  }
})
