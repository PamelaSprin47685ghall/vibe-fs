// INTERACTION-AUTHORITY proof — top-level Manager is authority-root-owned.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as tp from '../../../dist/OpenCode/Host/TerminalPolicy.js'
import { Role } from '../../../dist/Foundation/Roles.js'

test('WHAT[INTERACTION-AUTHORITY-003] TPOL_top_level_manager_has_fail_closed_parent_rules_table_driven', () => {
  const sessionParents = new Map()

  // Case 1: Unlinked + unregistered => top-level manager (true)
  assert.equal(tp.isTopLevelManager(sessionParents, undefined, 'ses-root-manager'), true)

  // Case 2: Registered parent in host map => not top-level (false)
  sessionParents.set('ses-child', 'ses-parent')
  assert.equal(tp.isTopLevelManager(sessionParents, undefined, 'ses-child'), false)

  // Case 3: Port indicates linked child => not top-level (false)
  const portLinked = {
    IsLinkedChild: (id) => id.fields[0] === 'ses-linked-child',
    TryCanonicalRole: () => undefined,
  }
  assert.equal(tp.isTopLevelManager(sessionParents, portLinked, 'ses-linked-child'), false)

  // Case 4: Durable role Manager with non-orchestrator parent => top-level manager guard applies (true)
  const portManagerWithRegularParent = {
    IsLinkedChild: () => true,
    TryCanonicalRole: (id) => {
      if (id.fields[0] === 'ses-child') return Role.Manager
      if (id.fields[0] === 'ses-parent') return Role.Coder
      return undefined
    },
  }
  assert.equal(tp.isTopLevelManager(sessionParents, portManagerWithRegularParent, 'ses-child'), true)

  // Case 5: Durable role Manager with Orchestrator parent => not top-level manager (false)
  const portManagerWithOrchParent = {
    IsLinkedChild: () => true,
    TryCanonicalRole: (id) => {
      if (id.fields[0] === 'ses-child') return Role.Manager
      if (id.fields[0] === 'ses-parent') return Role.Orchestrator
      return undefined
    },
  }
  assert.equal(tp.isTopLevelManager(sessionParents, portManagerWithOrchParent, 'ses-child'), false)

  // Case 6: Non-manager durable role (e.g. Coder) => not top-level manager (false)
  const portCoder = { IsLinkedChild: () => false, TryCanonicalRole: () => Role.Coder }
  assert.equal(tp.isTopLevelManager(sessionParents, portCoder, 'ses-coder'), false)
})
