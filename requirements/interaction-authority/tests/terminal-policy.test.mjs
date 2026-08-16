// INTERACTION-AUTHORITY proof — top-level Manager is authority-root-owned.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'

test('WHAT[INTERACTION-AUTHORITY-003] TPOL_top_level_manager_has_fail_closed_parent_rules', () => {
  const source = readFileSync(join(process.cwd(), 'src/Wanxiangshu/OpenCode/Host/TerminalPolicy.fs'), 'utf8')
  assert.match(source, /let isTopLevelManager/)
  assert.match(source, /journal with\n\s*\| None -> not \(sessionParents\.ContainsKey sessionKey\)/)
  assert.match(source, /parentedByOrchestrator/)
  assert.match(source, /run\.CanonicalRole = Role\.Manager/)
  assert.match(source, /profile\.CanonicalRole = Role\.Manager/)
  assert.match(source, /unlinkedTopLevel sessionParents journal sessionKey/)
})
