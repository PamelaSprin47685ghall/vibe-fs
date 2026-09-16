import assert from 'node:assert/strict'
import test from 'node:test'
import * as Runtime from '../../../dist/Execution/Session/ChatExecution/RecoveryRuntimeSurface.js'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import * as chatExecution from '../../../dist/Execution/Session/ChatExecution/Surface.js'
import * as recoveryHost from '../../../dist/OpenCode/Host/SessionRecoveryHostSurface.js'

// R10 / CHATEXEC-012: explicit /continue owns resume. The host below is the
// compiled production owner; only the snapshot port (physical read) and the
// accept/reject port answers are test inputs. Publications are counted on the
// production registry, never emulated in JS. The host, scope and journal cross
// the registered SessionRecoveryHostSurface as opaque handles; resume output
// (port calls plus manuals) returns as plain JSON. Recovery retains no resume
// DTO: the resume view is the whole effect.
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

test('WHAT[CHATEXEC-008] recovery begins from durable activation and re-enters only on causal events', async () => {
  const [beforeDurability, afterDurability] = (await Runtime.admissionCrashPointScenarios(
    ['A', 'B'],
    'ProcessRestart',
    'NotCommitted',
    'Applied',
  )).scenarios

  assert.deepEqual(beforeDurability.decisions, ['NoDurableExecution', 'NoDurableExecution'])
  assert.deepEqual(beforeDurability.effects, [])
  assert.deepEqual(afterDurability.decisions, ['ResumePreProvider', 'ResumePreProvider'])
  assert.deepEqual(afterDurability.effects, ['ResumePreProvider', 'ResumePreProvider'])
  assert.deepEqual(Runtime.lifecycleSignals(), [
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
    const result = await Runtime.recoverScenarios([scenario])
    assert.deepEqual(result.decisions, [decision], scenario)
    assert.deepEqual(result.effects, expected, scenario)
  }
})

test('WHAT[CHATEXEC-012] only causal lifecycle signals enter the shared recovery runtime', () => {
  assert.deepEqual(Runtime.lifecycleSignals(), [
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

test('WHAT[CHATEXEC-013] exact terminal settlement revokes the manual', async () => {
  await withRecoveryHost('terminal', 'absent', async (host) => {
    const sessionId = sessionOf('terminal')
    const physicalId = physicalOf('terminal')
    const providerRun = 'provider-recovery-terminal'

    await recoveryHost.seedProviderStarted(host, sessionId, physicalId, providerRun)

    const before = await recoveryHost.resumeAccepted(host, sessionId, physicalId)
    assert.equal(before.manuals.length, 1)

    const settled = await recoveryHost.finalizeCompleted(host, sessionId, physicalId, providerRun)

    assert.deepEqual(settled.manuals, [])
    assert.equal(settled.lifecycle, 'Terminal')
    assert.equal(settled.disposition, 'Completed')
    assert.equal(settled.sessionId, sessionId)
    assert.equal(settled.physicalUserMessageId, physicalId)
  })
})

test('WHAT[CHATEXEC-013] pre-provider cancellation settlement revokes the manual', async () => {
  await withRecoveryHost('accepted-cancel', 'absent', async (host) => {
    const sessionId = sessionOf('accepted-cancel')
    const physicalId = physicalOf('accepted-cancel')

    await recoveryHost.seedAccepted(host, sessionId, physicalId)

    const before = await recoveryHost.resumeAccepted(host, sessionId, physicalId)
    assert.equal(before.manuals.length, 1)

    const settled = await recoveryHost.signalCancelled(host, sessionId, physicalId)

    assert.deepEqual(settled.manuals, [])
    assert.equal(settled.lifecycle, 'Terminal')
    assert.equal(settled.disposition, 'Cancelled')
    assert.equal(settled.sessionId, sessionId)
    assert.equal(settled.physicalUserMessageId, physicalId)
  })
})

// R02 / CHATEXEC-004: the same immutable admitted plan drives later exact-run
// binding through the production admission owner; conflicts fail closed.
const tagged = (name, value) => [name, value]

const plainEvidence = (sessionId, physicalUserMessageId) => ({
  sessionId,
  physicalUserMessageId,
  logicalRunId: `run-${sessionId}`,
  authorityRootUserMessageId: `root-${sessionId}`,
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
})

const evidenceWire = (evidence) => ({
  SessionId: tagged('SessionId', evidence.sessionId),
  LogicalRunId: tagged('LogicalRunId', evidence.logicalRunId),
  AuthorityRootUserMessageId: tagged('AuthorityRootUserMessageId', evidence.authorityRootUserMessageId),
  AuthorityKind: evidence.authorityKind,
  IdentitySeed: [
    'RootSelection',
    {
      InitialTier: 'deep',
      Origin: 'ResolvedAtRoot',
      Persona: evidence.identitySeed.participantIdentity.persona,
      PersonaCatalogVersion: evidence.identitySeed.participantIdentity.personaCatalogVersion,
      Role: evidence.identitySeed.participantIdentity.canonicalRole,
      SelectedAgent: evidence.identitySeed.participantIdentity.selectedAgent,
    },
  ],
  PhysicalUserMessageId: tagged('PhysicalUserMessageId', evidence.physicalUserMessageId),
  Origin: ['AuthorityRoot', evidence.origin],
})

const acceptedWire = (evidence) =>
  JSON.stringify([
    'Agent',
    [
      'ChatExecution',
      [
        'Accepted',
        {
          SchemaVersion: 1,
          Key: {
            SessionId: tagged('SessionId', evidence.sessionId),
            PhysicalUserMessageId: tagged('PhysicalUserMessageId', evidence.physicalUserMessageId),
          },
          Evidence: evidenceWire(evidence),
        },
      ],
    ],
  ])

test('WHAT[CHATEXEC-004] same admitted plan binds the same admission twice', () => {
  const evidence = plainEvidence('ses-r02-same', 'msg-r02-same')
  const facts = [acceptedWire(evidence)]
  const message = {
    sessionId: evidence.sessionId,
    physicalUserMessageId: evidence.physicalUserMessageId,
    explicitAgent: null,
  }

  const first = chatExecution.admitIntent(facts, message, evidence)
  const second = chatExecution.admitIntent(facts, message, evidence)

  assert.equal(first.ok, true, JSON.stringify(first.error))
  assert.equal(second.ok, true, JSON.stringify(second.error))
  assert.equal(first.intent?.kind, 'ResumeAccepted')
  assert.deepEqual(first.intent?.evidence, second.intent?.evidence)
})

test('WHAT[CHATEXEC-004] conflicting plan against the same key fails closed', () => {
  const evidence = plainEvidence('ses-r02-conflict', 'msg-r02-conflict')
  const facts = [acceptedWire(evidence)]
  const message = {
    sessionId: evidence.sessionId,
    physicalUserMessageId: evidence.physicalUserMessageId,
    explicitAgent: null,
  }

  const result = chatExecution.admitIntent(facts, message, {
    ...evidence,
    logicalRunId: 'run-r02-other',
  })

  assert.equal(result.ok, false)
  assert.equal(result.intent, null)
  assert.equal(result.error?.kind, 'ExistingEvidenceConflict')
})
