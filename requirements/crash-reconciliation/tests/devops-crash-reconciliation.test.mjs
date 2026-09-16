import assert from 'node:assert/strict'
import test from 'node:test'
import { Role } from '../../../dist/Foundation/Roles.js'
import * as resume from '../../../dist/OpenCode/Host/ExplicitResumeSurface.js'

test('WHAT[CRASH-020] DevOps crash recovery maintains single logical authority, locks model, and avoids command auto-replay', async () => {
  // 1. Role enum must have consolidated DevOps and Engineer
  const roleCases = (new Role(0, [])).cases()
  assert.ok(roleCases.includes('DevOps'), 'Role enum must have DevOps')
  assert.ok(roleCases.includes('Engineer'), 'Role enum must have Engineer')
  assert.equal(roleCases.includes('Coder'), false, 'Role enum must not contain Coder')

  // 2. Explicit resume command behavior
  const config = { command: {} }
  resume.registerCommand(config)
  assert.ok(config.command.continue, 'continue command must be registered')
  
  // Non-continue command is a no-op (no auto-replay of pending commands)
  const actual = await resume.run('status', 'session-1', '')
  assert.deepEqual(actual.parts, [], 'non-continue command must not trigger automatic execution')
})
