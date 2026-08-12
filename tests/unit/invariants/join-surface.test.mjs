// EXEC-004 / EXEC-030 — join renderer output must not carry legacy DTO keys.

import assert from 'node:assert/strict'
import test from 'node:test'

import { joinResultRenderer, nonEmptyBatch, agentCompletion } from '../support/domain.mjs'

const LEGACY_DTO = /\b(status|count|ordinal|kind|agent|code|message)\s*=|\[\[result\]\]|\[error\]|work_record\s*=/

const assertClean = (wire, label) => {
  assert.ok(!LEGACY_DTO.test(wire), `${label}: ${wire}`)
}

test('JOIN_SURFACE_completed_batch_is_natural_language_plus_work_record', () => {
  const runtime = joinResultRenderer.stubRuntime()
  const batch = nonEmptyBatch.ofHeadTail(agentCompletion.completedRun({
    runId: 'run-1',
    agentId: 'a1',
    agentName: 'fast-coder',
    role: 'Coder',
    workRecord: 'Chronicle\nClosing report',
  }))
  const wire = joinResultRenderer.renderCompletedBatch(runtime, batch)
  assert.match(wire, /# fast-coder has returned\./)
  assert.match(wire, /Chronicle/)
  assertClean(wire, 'completed')
})

test('JOIN_SURFACE_interrupt_and_fork_error_are_natural_language_only', async () => {
  const { ForkError } = await import('../../../dist/Session/ForkTypes.js')
  assertClean(joinResultRenderer.renderInterrupted(), 'operator abort')
  assertClean(joinResultRenderer.renderForkError(ForkError.NothingToJoin), 'nothing to join')
  assertClean(joinResultRenderer.renderForkError(ForkError.TimedOut), 'timed out')
})
