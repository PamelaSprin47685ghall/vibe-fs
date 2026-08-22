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

  // Verify pure 5 responsibilities: construct, subscribe, route typed signal, drain, dispose
  assert.match(bootstrapSource, /module\s+HostSignalBootstrap\b/)
  assert.match(bootstrapSource, /type\s+WiredSignals\b/)
  assert.match(bootstrapSource, /let\s+wire\b/)
  assert.match(bootstrapSource, /HostSignalSubscribe\.trySubscribe/)
  assert.match(bootstrapSource, /scope\.TrackReconcileShutdown/)
  assert.match(bootstrapSource, /scope\.TrackSubscription/)

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

  // Model routing policy delegated to ModelRouting owner
  assert.match(bootstrapSource, /ModelRouting\.endProviderStep/)
  assert.match(bootstrapSource, /ModelRouting\.releasePhysicalExecution/)
  assert.match(bootstrapSource, /ModelRouting\.chatExecutionAdmission/)
  assert.match(bootstrapSource, /ModelRouting\.routeChatExecution/)

  // Fission policy delegated to FissionHost owner
  assert.match(bootstrapSource, /FissionHost\.routeAttemptAborted/)
  assert.match(bootstrapSource, /FissionHost\.observePhysicalExecutionEnd/)

  // Session deletion delegated to HostSessionDeletion owner
  assert.match(bootstrapSource, /HostSessionDeletion\.handle/)
})
