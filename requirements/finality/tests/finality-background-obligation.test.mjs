// FINALITY-027: background child or PTY present → fixed join prompt; join is a
// Finality prerequisite. The registered FinalitySurface folds plain durable
// handle events and exposes only the Manager's parent-visible obligation.

import assert from 'node:assert/strict'
import test from 'node:test'

const finality = await import('../../../dist/Mission/Manager/FinalitySurface.js')

const MANAGER = 'ses_mgr'

const handleLinked = (ownership = 'durable-parent-handle') => ({
  kind: 'handle-linked',
  sessionId: MANAGER,
  childSessionId: 'ses_child',
  handleId: 'h1',
  targetAgent: 'fast-coder',
  byname: 'finality-background-child',
  role: 'coder',
  ownership,
})

const worldOf = (events) => {
  const result = finality.project(events)
  assert.equal(result.ok, true, JSON.stringify(result.error))
  return result.world
}

test('WHAT[FINALITY-027] malformed handle role ownership and completion fail closed', () => {
  for (const event of [
    { ...handleLinked(), role: 'unknown' },
    { ...handleLinked(), ownership: 'unknown' },
    { kind: 'handle-completed', sessionId: MANAGER, handleId: 'h1', completionKind: 'unknown' },
  ]) {
    const result = finality.project([event])
    assert.equal(result.ok, false)
    assert.match(JSON.stringify(result.error), /unknown (role|handle ownership|handle completion kind)/)
  }
})

test('WHAT[FINALITY-027] Manager without journal or handles is never outstanding', () => {
  assert.equal(finality.backgroundOutstanding(worldOf([]), MANAGER), false)
})

test('WHAT[FINALITY-027] Manager with a listable child handle has a join obligation', () => {
  assert.equal(finality.backgroundOutstanding(worldOf([handleLinked()]), MANAGER), true)
})

test('WHAT[FINALITY-027] hidden Reviewer handles do not become a Manager join obligation', () => {
  assert.equal(finality.backgroundOutstanding(worldOf([handleLinked('host-owned-hidden')]), MANAGER), false)
})

test('WHAT[FINALITY-027] completed-but-unjoined handles remain outstanding until retired', () => {
  const completed = worldOf([
    handleLinked(),
    {
      kind: 'handle-completed',
      sessionId: MANAGER,
      handleId: 'h1',
      completionKind: 'terminal',
    },
  ])
  assert.equal(finality.backgroundOutstanding(completed, MANAGER), true)

  const retired = worldOf([
    handleLinked(),
    {
      kind: 'handle-completed',
      sessionId: MANAGER,
      handleId: 'h1',
      completionKind: 'terminal',
    },
    { kind: 'handle-retired', sessionId: MANAGER, handleId: 'h1' },
  ])
  assert.equal(finality.backgroundOutstanding(retired, MANAGER), false)
})
