import assert from 'node:assert/strict'
import test from 'node:test'
import * as wire from '../../../dist/Execution/Delegation/JoinV2WireSurface.js'

test('WHAT[DELEG-005] JOIN_V2_rendered_wire_is_parseable_without_legacy_fields', () => {
  const parsed = wire.parseJoinResult('Work completed cleanly')
  assert.equal(parsed.legacyFieldsPresent, false)
  assert.equal(parsed.ok, true)
})
