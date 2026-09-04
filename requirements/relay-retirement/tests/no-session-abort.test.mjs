import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

test('WHAT[RETIRE-008] retirement never issues a session-scoped physical abort', () => {
  const source = readFileSync(
    resolve(import.meta.dirname, '../../../src/Wanxiangshu/Mission/Relay/OpenCode/SuicideTool.fs'),
    'utf8',
  )

  assert.doesNotMatch(source, /\.InterruptAttempt\b/)
  assert.doesNotMatch(source, /\.AbortSession\b/)
  assert.match(source, /retirementTransaction/)
  assert.match(source, /StaleProviderRunIds/)
})

test('WHAT[RETIRE-008] retired continuations are interrupted in the transform hook by gate identity', () => {
  const source = readFileSync(
    resolve(import.meta.dirname, '../../../src/Wanxiangshu/Mission/Relay/OpenCode/NarrativeTransform.fs'),
    'utf8',
  )

  assert.match(source, /successorGateAdmitted/)
  assert.match(source, /GateNudgeAlreadyAdmitted/)
  assert.match(source, /do! interruptAttempt sessionId/)
})
