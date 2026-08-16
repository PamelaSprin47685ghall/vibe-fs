import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import * as join from '../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js'

test('WHAT[PARTICIPANT-HORIZON-003] devops_join_deadline_renders_natural_language_not_timed_out_dto', () => {
  const wire = join.renderInterrupted('english', 'DeadlineExpired')
  assert.match(wire, /No return reached you before your waiting ended/)
  assert.equal(parseToml(wire).status, undefined)
  assert.equal(parseToml(wire).error, undefined)
})

test('WHAT[PARTICIPANT-HORIZON-003] devops_join_timed_out_fork_error_also_natural_language', () => {
  const wire = join.renderForkError('english', 'TimedOut')
  assert.match(wire, /No return reached you before your waiting ended/)
  assert.equal(parseToml(wire).status, undefined)
})
