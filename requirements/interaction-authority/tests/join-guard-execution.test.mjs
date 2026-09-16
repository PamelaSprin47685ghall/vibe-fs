// INTERACTION-AUTHORITY proof — JoinGuard is a continuation with stable instructions.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'

test('WHAT[INTERACTION-AUTHORITY-014] EXEC_016_join_guard_is_a_continuation', () => {
  assert.deepEqual(authority.originForContinuation('JoinGuard'), { kind: 'Continuation', label: 'JoinGuard' })
})

test('WHAT[INTERACTION-AUTHORITY-014] EXEC_016_join_guard_instruction_requires_join_before_finish', () => {
  const instructions = readFileSync(join(process.cwd(), 'resources/provider/runtime/background-join/en.md'), 'utf8')
  assert.match(instructions, /Work remains away/)
  assert.match(instructions, /Receive arrived consequences before claiming completion/)
  assert.match(instructions, /Use join/)
})
