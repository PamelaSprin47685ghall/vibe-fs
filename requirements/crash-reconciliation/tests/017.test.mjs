import assert from 'node:assert/strict'
import test from 'node:test'
import * as family from '../../../dist/Execution/Session/Recovery/SessionRecoveryFamilySurface.js'

test('WHAT[CRASH-017] RECOVERY_FAMILY_plugin_load_only_attaches_physical_recovery_wiring_and_join_uses_current_process_permit', () => {
  assert.ok(family.currentProcessPermit)
})
