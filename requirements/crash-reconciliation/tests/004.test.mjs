import assert from 'node:assert/strict'
import test from 'node:test'
import * as family from '../../../dist/Execution/Session/Recovery/SessionRecoveryFamilySurface.js'

test('WHAT[CRASH-004] RECOVERY_FAMILY_dsl_module_and_private_permit_exist', () => {
  assert.ok(family.permitModule)
})
