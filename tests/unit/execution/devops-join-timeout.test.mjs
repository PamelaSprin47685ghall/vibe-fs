// Unit test for DevOps join timeout (10s timeout budget).

import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import { joinResultRenderer } from '../support/domain.mjs'
import { ForkError } from '../../../dist/Session/ForkTypes.js'

test('devops_join_timed_out_renders_status_failed_and_code_timed_out', () => {
  const wire = joinResultRenderer.renderForkError(ForkError.TimedOut)
  const parsed = parseToml(wire)

  assert.equal(parsed.status, 'failed')
  assert.ok(parsed.error)
  assert.equal(parsed.error.code, 'TIMED_OUT')
})
