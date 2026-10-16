import assert from 'node:assert/strict'
import * as authority from '../../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as dispatch from '../../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as journal from '../../../../dist/Persistence/Journal/Surface.js'
import { withJournal, withSharedJournal, acceptOwner, hostPort } from '../../../interaction-authority/tests/support/authority.mjs'

export const withAdmittedChildren = async (label, parent, children, run) => {
  return withChildJournal(withJournal, label, parent, children, run)
}

export const withSharedAdmittedChildren = async (label, parent, children, run) => {
  return withChildJournal(withSharedJournal, label, parent, children, run)
}

const withChildJournal = async (open, label, parent, children, run) => {
  return open(label, async (handle, reopen, directory) => {
    const owner = await acceptOwner(handle, parent)
    const profiles = new Map()
    const port = hostPort(async () => dispatch.admittedWithReceipt(`child-work:${label}`))
    for (const { agentId, sessionId, role, linked: hasBinding = true } of children) {
      if (hasBinding) {
        const linked = await journal.JournalSurface_appendAgent(handle, { kind: 'Session', session: parent }, null, {
        family: 'Execution', case: 'HandleLinked', payload: {
          ParentSessionId: parent, ChildSessionId: sessionId, Handle: agentId,
          TargetAgent: role, Byname: agentId, CanonicalRole: role, Ownership: 'DurableParentHandle',
        },
      })
        assert.equal(linked.ok, true, JSON.stringify(linked.error))
      }
      const seed = authority.issueInheritedIdentitySeed(role, owner)
      assert.equal(seed.ok, true, seed.error)
      const sent = await dispatch.sendAgentOwnerRootAwait(port, handle, sessionId, `WORK-${agentId}`, seed.value)
      assert.equal(sent.ok, true, sent.error)
      const accepted = await dispatch.acceptAgentOwnerRoot(handle, sessionId, sent.key, `physical-${label}-${agentId}`)
      assert.equal(accepted.ok, true, JSON.stringify(accepted.error))
      profiles.set(agentId, accepted.profile)
    }
    return run(handle, profiles, directory, reopen, owner)
  })
}
