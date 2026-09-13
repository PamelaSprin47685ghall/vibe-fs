// Deadline surface: instant offset semantics and timezone independence.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as deadline from '../../../dist/Process/DeadlineSurface.js'

test('WHAT[PROC-004] Process_deadline_uses_explicit_offset_semantics', () => {
  const value = deadline.create('2026-01-01T00:00:00Z', 5000)

  assert.equal(deadline.remainingMs('2026-01-01T00:00:02Z', value), 3000)
  assert.equal(deadline.isExpired('2026-01-01T00:00:02Z', value), false)
  assert.equal(deadline.remainingMs('2026-01-01T00:00:05Z', value), 0)
  assert.equal(deadline.isExpired('2026-01-01T00:00:05Z', value), true)
  assert.equal(deadline.isExpired('2026-01-01T00:00:06Z', value), true)

  // Same instant expressed with a non-zero offset must produce the same answer.
  assert.equal(deadline.remainingMs('2026-01-01T08:00:02+08:00', value), 3000)
  assert.equal(deadline.isExpired('2026-01-01T08:00:02+08:00', value), false)
})

test('WHAT[PROC-004] Process_deadline_is_independent_of_ambient_timezone', () => {
  const original = process.env.TZ
  const value = deadline.create('2026-01-01T00:00:00Z', 5000)

  try {
    for (const zone of ['UTC', 'Asia/Shanghai', 'America/Los_Angeles']) {
      process.env.TZ = zone
      assert.equal(deadline.isExpired('2026-01-01T00:00:02Z', value), false, `expired under TZ=${zone}`)
      assert.equal(deadline.remainingMs('2026-01-01T00:00:02Z', value), 3000, `remaining under TZ=${zone}`)
    }
  } finally {
    if (original === undefined) delete process.env.TZ
    else process.env.TZ = original
  }
})
