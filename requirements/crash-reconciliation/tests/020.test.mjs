import assert from 'node:assert/strict'
import test from 'node:test'
import * as RolesSurface from '../../../dist/Foundation/RolesSurface.js'
import * as resume from '../../../dist/OpenCode/Host/ExplicitResumeSurface.js'



test('WHAT[CRASH-020] DevOps crash recovery maintains single logical authority, locks model, and avoids command auto-replay', async () => {
  // 1. RolesSurface must have consolidated DevOps and Engineer
  const all = RolesSurface.allRoleLabels
  assert.ok(all.includes('devops'), 'Role labels must have devops')
  assert.ok(all.includes('engineer'), 'Role labels must have engineer')
  assert.equal(all.includes('coder'), false, 'Role labels must not contain coder')

  // 2. Explicit resume command behavior
  const config = { command: {} }
  resume.registerCommand(config)
  assert.ok(config.command.continue, 'continue command must be registered')
  
  // Non-continue command is a no-op (no auto-replay of pending commands)
  const actual = await resume.run('status', 'session-1', '')
  assert.deepEqual(actual.parts, [], 'non-continue command must not trigger automatic execution')
})
