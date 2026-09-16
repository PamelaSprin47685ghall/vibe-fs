// INTERACTION-AUTHORITY proof — top-level Manager is authority-root-owned.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as tp from '../../../dist/OpenCode/Host/TerminalPolicySurface.js'
import * as roles from '../../../dist/Foundation/RolesSurface.js'

test('WHAT[INTERACTION-AUTHORITY-003] TPOL_top_level_manager_has_fail_closed_parent_rules_table_driven', () => {
  assert.equal(tp.sessionDeadWithoutJournal('ses-root-manager'), false)
  assert.ok(roles.allRoleLabels.includes('manager'))
  assert.ok(roles.allRoleLabels.includes('orchestrator'))
  assert.ok(roles.allRoleLabels.includes('engineer'))
  assert.equal(tp.outstandingWithoutJournal('Manager', false, 'ses-root-manager'), false)
  assert.equal(tp.outstandingWithoutJournal('Engineer', true, 'ses-engineer'), false)
})
