import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as journal from '../../../../dist/Persistence/Journal/Surface.js'
import * as dispatch from '../../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as authority from '../../../../dist/Interaction/Authority/RuntimeSurface.js'

export { journal, dispatch }

export async function withDispatch(scenario) {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-effect-dispatch-'))
  const handles = []
  const open = async (writer) => {
    const opened = await journal.JournalSurface_bootWithWriterId(directory, writer, `runtime-${writer}`, 4242, '9999-01-01T00:00:00Z')
    assert.equal(opened.ok, true, JSON.stringify(opened.error))
    handles.push(opened.journal)
    return opened.journal
  }
  try {
    const handle = await open('first-writer')
    const owner = await dispatch.acceptHumanRootSelection(handle, 'owner', 'root-message', {
      kind: 'RootSelection',
      ownerSession: null,
      ownerLogicalRun: null,
      ownerAuthorityRoot: null,
      participantIdentity: { participant: 'manager', role: 'manager', persona: 'Lead', personaCatalogVersion: 1, origin: 'ResolvedAtRoot' },
    })
    assert.equal(owner.ok, true, JSON.stringify(owner.error))
    const inherited = authority.issueInheritedIdentitySeed('engineer', owner.profile)
    assert.equal(inherited.ok, true, inherited.error)
    const send = (port) => dispatch.sendAgentOwnerRootAwait({
      SubscribeTerminal: () => ({ Dispose() {} }),
      SubscribeFutureTerminal: () => ({ Dispose() {} }),
      ...port,
    }, handle, 'child', 'Perform the recorded effect.', inherited.value)
    await scenario({ directory, handle, open, send })
  } finally {
    for (const handle of handles) journal.JournalSurface_dispose(handle)
    rmSync(directory, { recursive: true, force: true })
  }
}
