import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = resolve(import.meta.dirname, '../../..')
const source = (path) => readFileSync(resolve(root, path), 'utf8')

const port = source('src/Wanxiangshu/Participant/Provider/Attempt/Fallback/ConfirmedFailurePort.fs')
const ledger = source('src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Ledger.fs')
const workflow = source('src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Workflow.fs')
const continuation = source('src/Wanxiangshu/Enforcer/Continuation.fs')
const repair = source('src/Wanxiangshu/Interaction/Repair/InteractionRepair.fs')

test('WHAT[PAR-003] confirmed-failure port preserves every ledger outcome without bool or option admission collapse', () => {
  assert.match(port, /type ConfirmedFailureOutcome\s*=\s*[\s\S]*RecoveryAdvanced[\s\S]*RecoveryExhausted[\s\S]*AlreadyRecorded[\s\S]*NoActiveRun/)
  assert.match(port, /type ConfirmedFailurePort\s*=\s*SessionId\s*->\s*ProviderRunIdentity\s*->\s*string\s*->\s*Task<Result<ConfirmedFailureOutcome, string>>/)
  assert.doesNotMatch(port, /type RecoveryAdmission/)
  assert.doesNotMatch(ledger, /admitConfirmedFailure/)
  assert.doesNotMatch(ledger, /Result\.map\s*\(function[\s\S]*ContinueRecovery/)
})

test('WHAT[PAR-003] enforcer handles duplicate explicitly and fails closed when no active run or port error exists', () => {
  assert.match(continuation, /ConfirmedFailureOutcome\.RecoveryAdvanced/)
  assert.match(continuation, /ConfirmedFailureOutcome\.RecoveryExhausted/)
  assert.match(continuation, /ConfirmedFailureOutcome\.AlreadyRecorded/)
  assert.match(continuation, /ConfirmedFailureOutcome\.NoActiveRun/)
  assert.doesNotMatch(continuation, /Some\s+RecoveryAdmission/)
  assert.doesNotMatch(continuation, /\|\s*_\s*->\s*onContinue/)
})

test('WHAT[PAR-003] workflow records Blogger child failure against one resolved durable main owner and never continues NoActiveRun', () => {
  assert.match(workflow, /let\s+projection\s*=\s*AgentJournal\.snapshot durable/)
  assert.match(workflow, /tryMainSessionOf[\s\S]*projection\.AgentProjections\.Associations/)
  assert.match(workflow, /FallbackEvidence\.tryCurrentState turn\.SessionId projection/)
  assert.match(workflow, /match ownerSessionId with\s*\| None -> notifyFailure/)
  assert.match(workflow, /recordConfirmedFailure[\s\S]*ownerSessionId[\s\S]*turn\.ProviderRun/)
  assert.doesNotMatch(workflow, /Option\.defaultValue turn\.SessionId/)
  assert.match(workflow, /ConfirmedFailureOutcome\.NoActiveRun\s*->[\s\S]*notifyFailure/)
  assert.doesNotMatch(workflow, /ConfirmedFailureOutcome\.AlreadyRecorded\s*\|\s*Ok ConfirmedFailureOutcome\.NoActiveRun/)
})

test('WHAT[PAR-003] idle Blogger repair snapshots once and uses the resolved main owner for evidence and recording', () => {
  assert.match(repair, /let\s+snapshot\s*=\s*AgentJournal\.snapshot journal/)
  assert.match(repair, /tryMainSessionOf[\s\S]*snapshot\.AgentProjections\.Associations/)
  assert.match(repair, /FallbackEvidence\.mayContinue[\s\S]*mainSessionId[\s\S]*snapshot/)
  assert.match(repair, /FallbackLedger\.recordConfirmedFailure[\s\S]*mainSessionId[\s\S]*turn\.ProviderRun/)
})
