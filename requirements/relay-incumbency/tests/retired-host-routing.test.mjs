import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

test('WHAT[RELAY-005] stale retired provider runs stay absorbed across successor activation', () => {
  const fold = readFileSync(
    resolve(import.meta.dirname, '../../../src/Wanxiangshu/Mission/Relay/Fold.fs'),
    'utf8',
  )
  const source = readFileSync(
    resolve(import.meta.dirname, '../../../src/Wanxiangshu/Mission/Manager/Workflow.fs'),
    'utf8',
  )

  assert.match(fold, /RetiredProviderRunIds: Set<string>/)
  assert.match(fold, /Set\.union current\.RetiredProviderRunIds staleProviderRuns/)
  assert.match(source, /let private isRetiredObservation/)
  assert.match(source, /Set\.contains \(ProviderRunIdentity\.value providerRun\) road\.RetiredProviderRunIds/)
  assert.match(source, /isRetiredObservation journal context\.Turn\.SessionId context\.Turn\.ProviderRun/)
  assert.match(source, /\| true, _, _ -> Task\.FromResult\(\)/)
})

test('WHAT[RELAY-007] manager tool-call intermediate observations wait for idle before ordinary repair', () => {
  const source = readFileSync(
    resolve(import.meta.dirname, '../../../src/Wanxiangshu/Mission/Manager/Workflow.fs'),
    'utf8',
  )

  assert.match(source, /\| false, None, ReconcileProgram\.TurnInProgress/)
  assert.match(source, /\| false, None, ReconcileProgram\.TurnNeedsContinuation _ -> Task\.FromResult\(\)/)
  assert.match(source, /\| false, None, ReconcileProgram\.TurnCompleted ->/)
})
