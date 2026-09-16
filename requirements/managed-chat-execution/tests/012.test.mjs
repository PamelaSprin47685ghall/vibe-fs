import assert from 'node:assert/strict'
import test from 'node:test'
import * as recovery from '../../../dist/Execution/Session/ChatExecution/RecoveryRuntimeSurface.js'
import * as accounting from '../../../dist/Execution/Session/ChatExecution/TerminalAccountingSurface.js'
import * as chatExecutionRecoverySurface from '../../../dist/Execution/Session/ChatExecution/RecoverySurface.js'

test('WHAT[CHATEXEC-012] A–I production transaction and lifecycle prefixes drive recovery decisions', () => {
  const r = recovery.testPrefixRecoveryDecisions()
  assert.equal(r.ok, true)
})

test('WHAT[CHATEXEC-012] duplicate crash-cut requests and input permutations preserve canonical production recovery', () => {
  const r = recovery.testCrashCutPermutations()
  assert.equal(r.preserved, true)
})

test('WHAT[CHATEXEC-012] duplicate terminal and stale recovery evidence are semantically inert', () => {
  const r = recovery.testDuplicateStaleInert()
  assert.equal(r.inert, true)
})

test('WHAT[CHATEXEC-012] lifecycle recovery interprets every typed decision through its owner port', () => {
  const r = recovery.testOwnerPortDecisions()
  assert.equal(r.ok, true)
})

test('WHAT[CHATEXEC-012] only causal lifecycle signals enter the shared recovery runtime', () => {
  const r = recovery.testCausalSignalsOnly()
  assert.equal(r.ok, true)
})

test('WHAT[CHATEXEC-012] absent recovery port publishes exactly one manual disposition and no resume', () => {
  const r = recovery.testAbsentRecoveryPort()
  assert.equal(r.manualDispositions, 1)
})

test('WHAT[CHATEXEC-012] rejecting port is awaited and publishes the same single manual', () => {
  const r = recovery.testRejectingPort()
  assert.equal(r.manualDispositions, 1)
})

test('WHAT[CHATEXEC-012] accepting port awaits ordinary admission and emits no manual block', () => {
  const r = recovery.testAcceptingPort()
  assert.equal(r.manualBlocks, 0)
})

test('WHAT[CHATEXEC-012] duplicate resume signals keep exactly one manual', () => {
  const r = recovery.testDuplicateResumeSignals()
  assert.equal(r.manualCount, 1)
})

test('WHAT[CHATEXEC-012] superseded exact capacity release is an idempotent recovery no-op', () => {
  const r = accounting.testSupersededReleaseNoOp()
  assert.equal(r.noOp, true)
})

test('WHAT[CHATEXEC-012] durable facts plus explicit physical evidence exhaustively determine recovery', () => {
  const r = recovery.testExhaustiveRecovery()
  assert.equal(r.exhaustive, true)
})

test('WHAT[CHATEXEC-012] duplicate evaluation is deterministic and effect-free', () => {
  const r = recovery.testDeterministicEvaluation()
  assert.equal(r.deterministic, true)
})
