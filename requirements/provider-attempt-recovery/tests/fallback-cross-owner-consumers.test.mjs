import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = resolve(import.meta.dirname, '../../..')
const source = (path) => readFileSync(resolve(root, path), 'utf8')

const ledger = source('src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Ledger.fs')
const workflow = source('src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Workflow.fs')
const continuation = source('src/Wanxiangshu/Enforcer/Continuation.fs')
const repair = source('src/Wanxiangshu/Interaction/Repair/InteractionRepair.fs')

test('WHAT[PAR-003] policy-authorized ledger preserves every confirmed-failure outcome', () => {
  assert.match(ledger, /type ConfirmedFailureOutcome\s*=\s*[\s\S]*RecoveryAdvanced[\s\S]*RecoveryExhausted[\s\S]*AlreadyRecorded[\s\S]*NoActiveRun/)
  assert.match(ledger, /let recordAuthorizedFailure[\s\S]*ProviderRecoveryAuthorization/)
  assert.doesNotMatch(ledger, /admitConfirmedFailure/)
  assert.doesNotMatch(ledger, /Result\.map\s*\(function[\s\S]*ContinueRecovery/)
})

test('WHAT[PAR-003] Enforcer protocol repair does not own fallback accounting', () => {
  assert.doesNotMatch(continuation, /ConfirmedFailureOutcome|ConfirmedFailurePort|FallbackLedger/)
  assert.match(continuation, /firstAabbOrExhaust/)
})

test('WHAT[PAR-003] workflow records Blogger child failure against one resolved durable main owner and never continues NoActiveRun', () => {
  assert.match(workflow, /let\s+projection\s*=\s*AgentJournal\.snapshot durable/)
  assert.match(workflow, /tryMainSessionOf[\s\S]*projection\.AgentProjections\.Associations/)
  assert.match(workflow, /match ownerSessionId with\s*\| None -> Task\.FromResult\(Ok ConfirmedFailureOutcome\.NoActiveRun\)/)
  assert.match(workflow, /recordAuthorizedFailure[\s\S]*ownerSessionId[\s\S]*authorization/)
  assert.doesNotMatch(workflow, /Option\.defaultValue turn\.SessionId/)
  assert.match(workflow, /ConfirmedFailureOutcome\.NoActiveRun\s*->[\s\S]*notifyFailure/)
  assert.doesNotMatch(workflow, /ConfirmedFailureOutcome\.AlreadyRecorded\s*\|\s*Ok ConfirmedFailureOutcome\.NoActiveRun/)
})

test('WHAT[PAR-003] idle Blogger repair snapshots once and uses the resolved main owner for evidence and recording', () => {
  assert.match(repair, /ProviderRecoveryWorkflow\.admitPolicyAuthorizedFailure[\s\S]*ExecutionFailure\.ProviderTransient[\s\S]*requestKind/)
  assert.doesNotMatch(repair, /FallbackLedger|FallbackEvidence\.mayContinue/)
})
