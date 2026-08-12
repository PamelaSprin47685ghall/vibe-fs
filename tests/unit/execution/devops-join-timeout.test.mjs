// Unit test for DevOps join timeout (10s timeout budget).

import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import { joinResultRenderer } from '../support/domain.mjs'
import { ForkError } from '../../../dist/Session/ForkTypes.js'
import { JoinInterruptReason } from '../../../dist/Session/CompletionMailbox.js'

test('devops_join_deadline_renders_natural_language_not_timed_out_dto', () => {
  const wire = joinResultRenderer.renderInterrupted(JoinInterruptReason.DeadlineExpired)
  assert.match(wire, /No return reached you before your waiting ended/)
  assert.equal(parseToml(wire).status, undefined)
  assert.equal(parseToml(wire).error, undefined)
})

test('devops_join_timed_out_fork_error_also_natural_language', () => {
  const wire = joinResultRenderer.renderForkError(ForkError.TimedOut)
  assert.match(wire, /No return reached you before your waiting ended/)
  assert.equal(parseToml(wire).status, undefined)
})
