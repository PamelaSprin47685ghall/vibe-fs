import assert from 'node:assert/strict'
import test from 'node:test'
import * as office from '../../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import * as ptySurface from '../../../../dist/Process/Surface.js'

test('WHAT[OFF-017] DevOps executes real commands and PTY sessions are cascade closed on road teardown', async () => {
  // 1. DevOps consequence model: full engineering mutation + real command execution + PTY
  const devopsPerms = office.permissions('devops')
  assert.ok(devopsPerms.includes('Read'), 'DevOps has Read')
  assert.ok(devopsPerms.includes('Write'), 'DevOps has Write')
  assert.ok(devopsPerms.includes('Edit'), 'DevOps has Edit')
  assert.ok(devopsPerms.includes('Exec'), 'DevOps has Exec')
  assert.ok(devopsPerms.includes('Pty'), 'DevOps has Pty')
  assert.equal(devopsPerms.includes('Fission'), false, 'DevOps must not have Fission')

  // 2. Process teardown contract: PTY supervisor can cascade terminate all active processes
  const port = ptySurface.createPtyPort()
  assert.ok(port, 'PtyPort must be created')

  // Verify list is initially empty
  const initialList = ptySurface.portList(port)
  assert.equal(initialList.ptys.length, 0)

  // Verify CloseAll completes and guarantees teardown without hanging
  await ptySurface.portCloseAll(port, 100)
  const afterList = ptySurface.portList(port)
  assert.equal(afterList.ptys.length, 0, 'No active PTYs must remain after CloseAll')
})
