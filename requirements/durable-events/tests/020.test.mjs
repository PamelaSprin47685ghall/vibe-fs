import assert from 'node:assert/strict'
import test from 'node:test'
import * as boot from '../../../dist/Persistence/Journal/EventStoreJournalBootSurface.js'

test('WHAT[DURABLE-EVENTS-020] empty_boot_is_read_only_and_keeps_RuntimeStarted_in_memory_until_activation', () => {
  const ctx = boot.createBootContext()
  assert.equal(boot.isDiskModifiedOnBoot(ctx), false)
  assert.equal(boot.isRuntimeStartedInMemory(ctx), true)
})

test('WHAT[DURABLE-EVENTS-020] plugin host does not pre-scan canonical history before EventStore activation', () => {
  const ctx = boot.createBootContext()
  assert.equal(boot.historyScannedCount(ctx), 0)
})

test('WHAT[DURABLE-EVENTS-020] plugin load defers EventStore replay and durable-session seeding until activation', () => {
  const ctx = boot.createBootContext()
  assert.equal(boot.isReplayExecuted(ctx), false)
})
