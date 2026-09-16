// TIME-002 — the deadline branch renders natural language, not a DTO.

import assert from 'node:assert/strict'
import test from 'node:test'

const process = await import('../../../dist/Process/Surface.js')

test('WHAT[TIME-002] EXEC_025_join_deadline_expired_renders_waiting_ended_natural_language', () => {
  assert.match(process.renderDeadlineExpired(), /No return reached you before your waiting ended/)
})
