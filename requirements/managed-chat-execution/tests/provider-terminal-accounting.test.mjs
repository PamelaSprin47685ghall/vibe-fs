import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

import * as hostSignals from '../../../dist/OpenCode/Host/HostSignalSurface.js'

const codecSource = await readFile(new URL('../../../src/Wanxiangshu/OpenCode/Codec/HostEventCodec.fs', import.meta.url), 'utf8')
const adapterSource = await readFile(new URL('../../../src/Wanxiangshu/OpenCode/Signals/HostSignalAdapter.fs', import.meta.url), 'utf8')
const bindingSource = await readFile(new URL('../../../src/Wanxiangshu/OpenCode/Host/SessionExecutionBinding.fs', import.meta.url), 'utf8')
const bootstrapSource = await readFile(new URL('../../../src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs', import.meta.url), 'utf8')
const recoveryHostSource = await readFile(new URL('../../../src/Wanxiangshu/OpenCode/Host/SessionRecoveryHost.fs', import.meta.url), 'utf8')
const recoveryRuntimeSource = await readFile(new URL('../../../src/Wanxiangshu/Execution/Session/ChatExecution/RecoveryRuntime.fs', import.meta.url), 'utf8')
const recoverySource = await readFile(new URL('../../../src/Wanxiangshu/OpenCode/Host/PluginRecoveryScope.fs', import.meta.url), 'utf8')

const terminal = ({
  sessionId = 'ses-terminal',
  physicalUserMessageId = 'msg-terminal',
  providerRun = 'run-terminal',
  finish,
  error,
  completed = 2,
} = {}) => ({
  type: 'message.updated',
  properties: {
    info: {
      sessionID: sessionId,
      id: providerRun,
      role: 'assistant',
      parentID: physicalUserMessageId,
      time: { created: 1, completed },
      ...(finish === undefined ? {} : { finish }),
      ...(error === undefined ? {} : { error }),
    },
  },
})

test('WHAT[CHATEXEC-006] production Host terminal owner persists before exact capacity settlement', () => {
  assert.match(adapterSource, /onExactAssistantObservation[\s\S]*?tryDecodeExactProviderStart[\s\S]*?tryDecodeExactProviderTerminal/)
  assert.match(bootstrapSource, /let\s+startedEvidenceForTerminal[\s\S]*?exactStarted key/)
  assert.match(bootstrapSource, /let\s+applyObservedTerminal[\s\S]*?ExactAssistantTerminal[\s\S]*?NotifyProjectionChanged/)
  assert.match(bootstrapSource, /let\s+settleExactTerminal[\s\S]*?match observation\.Outcome, observation\.Disposition, startedEvidenceForTerminal observation with[\s\S]*?HostProviderTerminalOutcome\.ProviderFailure failure, None, Some _[\s\S]*?ReconcileWake\.FailureWake\([\s\S]*?Some observation\.PhysicalUserMessageId/)
  assert.match(bootstrapSource, /onExactAssistantObservation\s*=\s*\(fun[\s\S]*?\(started: ExactProviderStartObservation\)[\s\S]*?\(terminal: ExactProviderTerminalObservation option\)[\s\S]*?persistProviderStartedFromObservation[\s\S]*?continueProviderStart started providerStepEnded terminal providerStarted/)

  assert.match(recoveryRuntimeSource, /let recover[\s\S]*?ChatExecutionRecovery\.decide evidence[\s\S]*?interpret ports decision/)
  assert.match(recoveryHostSource, /ExactAssistantTerminal\(started, disposition\)[\s\S]*?ProviderPhysicalObservation\.ProviderTerminal\(started, disposition\)/)
  assert.match(recoveryHostSource, /let finalize[\s\S]*?persistTerminal request\.ExecutionKey request\.TerminalEvidence request\.TerminalDisposition/)
  assert.match(recoveryHostSource, /let persistTerminal[\s\S]*?ManagedChatProviderLifecycle\.terminal journal key started disposition[\s\S]*?requirePersistence "terminal" result[\s\S]*?do! release key/)
  assert.match(recoveryHostSource, /let release[\s\S]*?ModelRouting\.releasePhysicalExecution key\.SessionId key\.PhysicalUserMessageId/)
  assert.match(codecSource, /ProviderRunIdentity/)
})

test('WHAT[CHATEXEC-012] superseded exact capacity release is an idempotent recovery no-op', () => {
  const release = recoveryHostSource.match(/let release \(key: ChatExecutionKey\) =([\s\S]*?)\n\s*let requirePersistence/)
  assert.ok(release, 'recovery capacity release boundary must remain explicit')
  assert.match(release[1], /CapacityTransitionOutcome\.Applied\s*\n\s*\| CapacityTransitionOutcome\.AlreadyApplied\s*\n\s*\| CapacityTransitionOutcome\.StaleFence ->\s*Task\.FromResult\(\(\)\)/)
  assert.match(release[1], /CapacityTransitionOutcome\.Conflict ->[\s\S]*?managed chat recovery exact capacity release was rejected/)
})

test('WHAT[CHATEXEC-010] recovery drain completion uses the Fable-compatible completion owner', () => {
  assert.match(recoveryHostSource, /AsyncSupport\.trySetResult completion \(\)/)
  assert.doesNotMatch(recoveryHostSource, /completion\.TrySetResult/)
})

test('WHAT[CHATEXEC-005] exact public assistant observation alone establishes provider start', () => {
  const started = terminal({ completed: undefined })
  delete started.properties.info.time.completed
  assert.deepEqual(hostSignals.tryDecodeExactProviderStart(started), {
    sessionId: 'ses-terminal',
    physicalUserMessageId: 'msg-terminal',
    providerRun: 'run-terminal',
  })

  for (const mutation of [
    (event) => { event.properties.info.role = 'user' },
    (event) => { event.properties.info.id = '' },
    (event) => { event.properties.info.parentID = '' },
    (event) => { event.properties.info.sessionID = '' },
    (event) => { delete event.properties.info.time.created },
  ]) {
    const ambiguous = structuredClone(started)
    mutation(ambiguous)
    assert.equal(hostSignals.tryDecodeExactProviderStart(ambiguous), null)
  }

  assert.match(bootstrapSource, /let\s+continueStartedLifecycle[\s\S]*?ModelRouting\.endProviderStep[\s\S]*?settleObservedTerminal/)
  assert.match(bootstrapSource, /let signalNewProviderStart started providerStarted =[\s\S]*?if providerStarted then[\s\S]*?signalProviderStarted started/)
  assert.match(bootstrapSource, /let continueProviderStart[\s\S]*?match persistence with[\s\S]*?\| Error \w+ ->[\s\S]*?rejectProviderStart started[\s\S]*?\| Ok providerStarted ->[\s\S]*?BindPhysicalUserMaterial\(started\.SessionId, started\.PhysicalUserMessageId\)[\s\S]*?signalNewProviderStart started providerStarted[\s\S]*?continueStartedLifecycle/)
  assert.match(bootstrapSource, /persistProviderStartedFromObservation[\s\S]*?continueProviderStart started providerStepEnded terminal providerStarted/)
  assert.match(bindingSource, /persistObservedProviderStart[\s\S]*?ChatExecutionProjection\.byKey key/)
  assert.match(bindingSource, /match execution \|> Option\.map _\.Lifecycle, execution \|> Option\.bind _\.ProviderStarted with[\s\S]*?ChatExecutionLifecycle\.Terminal[\s\S]*?AcceptedExecutionAlreadyTerminal[\s\S]*?\| _, Some _ -> Task\.FromResult\(Ok false\)[\s\S]*?\| _, None ->[\s\S]*?bindAttemptPlan[\s\S]*?do! persistPreparedProviderStarted[\s\S]*?return true/)
  assert.match(bindingSource, /persistObservedProviderStart[\s\S]*?bindAttemptPlan observation\.SessionId observation\.PhysicalUserMessageId observation\.ProviderRun/)
  assert.match(recoverySource, /TryBindAttemptPlan[\s\S]*?established\.Profile\.PhysicalUserMessageId = physicalUserMessageId/)
  assert.match(recoverySource, /\| Some _ -> None[\s\S]*?\| None -> this\.BindPendingAttemptPlan/)
  assert.match(bindingSource, /ManagedChatProviderLifecycle\.providerStarted/)
})

test('WHAT[CHATEXEC-006] exact successful Host terminals retain typed finish outcomes', () => {
  for (const [finish, outcome] of [
    ['stop', 'Stop'],
    ['length', 'Length'],
    ['content-filter', 'ContentFiltered'],
  ]) {
    assert.deepEqual(hostSignals.tryDecodeExactProviderTerminal(terminal({ finish })), {
      sessionId: 'ses-terminal',
      physicalUserMessageId: 'msg-terminal',
      providerRun: 'run-terminal',
      outcome,
      failure: '',
      disposition: 'Completed',
    })
  }
})

test('WHAT[CHATEXEC-006] exact cancel and interruption become closed typed terminal dispositions', () => {
  assert.deepEqual(hostSignals.tryDecodeExactProviderTerminal(terminal({ error: { name: 'AbortError' } })), {
    sessionId: 'ses-terminal',
    physicalUserMessageId: 'msg-terminal',
    providerRun: 'run-terminal',
    outcome: 'Cancelled',
    failure: 'UserCancelled',
    disposition: 'Cancelled',
  })

  assert.deepEqual(hostSignals.tryDecodeExactProviderTerminal(terminal({ error: { name: 'StreamInterruptedError' } })), {
    sessionId: 'ses-terminal',
    physicalUserMessageId: 'msg-terminal',
    providerRun: 'run-terminal',
    outcome: 'Interrupted',
    failure: 'StreamInterruptedAfterFirstToken',
    disposition: 'Failed',
  })
})

test('WHAT[CHATEXEC-006] exact provider failure remains typed but awaits retry-owner disposition', () => {
  assert.deepEqual(hostSignals.tryDecodeExactProviderTerminal(terminal({ error: { name: 'TimeoutError', message: 'AbortError' } })), {
    sessionId: 'ses-terminal',
    physicalUserMessageId: 'msg-terminal',
    providerRun: 'run-terminal',
    outcome: 'ProviderFailure',
    failure: 'ProviderTransient',
    disposition: '',
  })
})

test('WHAT[CHATEXEC-006] ambiguous and deleted evidence fail closed', () => {
  assert.equal(hostSignals.tryDecodeExactProviderTerminal(terminal({ providerRun: '' })), null)
  assert.equal(hostSignals.tryDecodeExactProviderTerminal({ type: 'session.deleted', properties: { sessionID: 'ses-terminal' } }), null)
  assert.match(bootstrapSource, /match observation\.Outcome, observation\.Disposition, startedEvidenceForTerminal observation with/)
  assert.match(bootstrapSource, /\| _ ->[\s\S]*?rejectProviderTerminal observation/)
  assert.match(recoveryHostSource, /eventKey[\s\S]*?ExactAssistantTerminal\(started, _\)[\s\S]*?keyOfStarted started/)
})
