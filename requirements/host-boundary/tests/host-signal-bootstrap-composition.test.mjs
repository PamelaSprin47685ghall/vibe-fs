import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = fileURLToPath(new URL('../../..', import.meta.url))
const read = (path) => readFileSync(join(ROOT, path), 'utf8')

test('WHAT[HOST-BOUNDARY-021] HostSignalBootstrap is strictly a wiring composition root with 0 foreign internal imports', () => {
  const bootstrapSource = read('src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs')

  // Prohibit forbidden foreign internal domain opens (AGENTS.md Chapter 26)
  const forbiddenOpens = [
    /open\s+Wanxiangshu\.Mission\.Review\.Judgement\b/,
    /open\s+Wanxiangshu\.Mission\.Obligation\.Todo\b/,
    /open\s+Wanxiangshu\.Mission\.Finality\b/,
    /open\s+Wanxiangshu\.Mission\.Manager\.Life\b/,
    /open\s+Wanxiangshu\.Strength\.Prediction\b/,
    /open\s+Wanxiangshu\.Strength\.Replica\b/,
    /open\s+Wanxiangshu\.Enforcer\.Guidance\b/,
    /open\s+Wanxiangshu\.Enforcer\.Cycle\b/,
    /open\s+Wanxiangshu\.Context\.Trace\b/,
  ]

  for (const pattern of forbiddenOpens) {
    assert.doesNotMatch(
      bootstrapSource,
      pattern,
      `HostSignalBootstrap must not import foreign internal domain namespace: ${pattern}`,
    )
  }

  // Verify pure 5 responsibilities: construct, subscribe, route typed signal,
  // register the Host-owned reconcile drain, and register subscription disposal.
  assert.match(bootstrapSource, /module\s+HostSignalBootstrap\b/)
  assert.match(bootstrapSource, /type\s+WiredSignals\b/)
  assert.match(bootstrapSource, /let\s+wire\b/)
  assert.match(bootstrapSource, /HostSignalSubscribe\.trySubscribe/)
  assert.match(bootstrapSource, /do scope\.TrackReconcileShutdown\(fun \(\) -> reconciler\.StopAndDrain\(\)\)/)
  assert.match(bootstrapSource, /do scope\.TrackSubscription subscription/)

  // Prohibit foreign domain policy decisions or implicit workflow PC
  assert.doesNotMatch(bootstrapSource, /\bdecideModelPolicy\b/)
  assert.doesNotMatch(bootstrapSource, /\bdecideRecoverySemantics\b/)
  assert.doesNotMatch(bootstrapSource, /\bdecideFissionPolicy\b/)
  assert.doesNotMatch(bootstrapSource, /\bdecideFinality\b/)
  assert.doesNotMatch(bootstrapSource, /\bdecideAssistanceSuccessor\b/)
  assert.doesNotMatch(bootstrapSource, /\bCurrentStage\b/)
  assert.doesNotMatch(bootstrapSource, /\bNextStep\b/)
  assert.doesNotMatch(bootstrapSource, /\bResumeAt\b/)
})

test('WHAT[HOST-BOUNDARY-021] HostSignalBootstrap delegates policy to published owner contracts', () => {
  const bootstrapSource = read('src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs')

  // Host observations are persisted through the exact execution-binding owner;
  // provider-step lifecycle remains behind ModelRouting's published contract.
  assert.match(bootstrapSource, /SessionExecutionBinding\.persistProviderStartedFromObservation/)
  assert.match(bootstrapSource, /ModelRouting\.endProviderStep/)

  // Managed chat admission is one transaction. The composition root decodes
  // once, resolves through PromptIngress, and supplies only ModelRouting's Host
  // projection port to the transaction owner.
  assert.match(bootstrapSource, /PromptIngress\.resolveDecision/)
  assert.match(bootstrapSource, /ChatAdmissionTransaction\.production/)
  assert.match(bootstrapSource, /ChatAdmissionTransaction\.execute/)
  assert.match(bootstrapSource, /createTransaction \(ModelRouting\.projectHostModel output\)/)
  assert.doesNotMatch(bootstrapSource, /PromptIngress\.create(?:Decision)?Hook/)
  assert.doesNotMatch(bootstrapSource, /ModelRouting\.routeChatExecution/)
  assert.doesNotMatch(bootstrapSource, /SessionExecutionBinding\.acceptRoutedExecution/)
  assert.doesNotMatch(bootstrapSource, /ModelRouting\.projectRoutedModel/)
  assert.doesNotMatch(bootstrapSource, /ModelRouting\.releasePhysicalExecution/)

  // Fission policy delegated to FissionHost owner
  assert.match(bootstrapSource, /FissionHost\.routeAttemptAborted/)
  assert.match(bootstrapSource, /FissionHost\.observePhysicalExecutionEnd/)

  // Session deletion delegated to HostSessionDeletion owner
  assert.match(bootstrapSource, /HostSessionDeletion\.handle/)
})

test('WHAT[HOST-BOUNDARY-003] coarse attempt abort never aborts every current chat execution', () => {
  const bootstrapSource = read('src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs')
  const abortBranch = bootstrapSource.match(/\| AttemptAborted failure ->([\s\S]*?)\| SessionDeleted/)

  assert.ok(abortBranch, 'AttemptAborted branch must remain explicit')
  assert.doesNotMatch(abortBranch[1], /SignalChatRecoverySession|ChatExecutionRecoveryLifecycleEvent\.SessionAborted/)
  assert.match(abortBranch[1], /FissionHost\.routeAttemptAborted/)
  assert.match(abortBranch[1], /reconciler\.Signal signal/)
})
