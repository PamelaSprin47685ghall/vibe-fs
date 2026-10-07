import assert from 'node:assert/strict'
import test from 'node:test'
import { execFileSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { mkdtempSync, mkdirSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import * as casebook from '../../../dist/Repository/Knowledge/Casebook/Surface.js'
import * as bookkeeper from '../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js'
import * as lifecycle from '../../../dist/Repository/Knowledge/Casebook/LifecycleSurface.js'
import * as pluginLifecycle from '../../../dist/OpenCode/Plugin/PluginLifecycleSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as executionStatus from '../../../dist/Execution/Session/ChatExecution/StatusSurface.js'
import * as semanticTrace from '../../../dist/Context/Trace/SemanticTraceSurface.js'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import { withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'
import {
  CANONICAL_A,
  installBookkeeperRuntime,
  scriptedBookkeeperPort,
} from '../../knowledge-reuse/tests/support/bookkeeper-session-support.mjs'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-delegate-settle-'))
  execFileSync('git', ['init', '--quiet', dir])
  mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
  return {
    dir,
    cleanup: () => rmSync(dir, { recursive: true, force: true }),
  }
}

test('WHAT[managed-session-lifecycle-022] CASE_SETTLE_uncommitted_finalize_does_not_publish_a_case', async () => {
  const { dir, cleanup } = sandbox()
  try {
    lifecycle.enable(dir)
    // No Bookkeeper runtime: observe archive refusal, not identity retention.
    const key = 'insp-settle-uncommitted'
    lifecycle.notePrompt(key, 'What owns PromptAuthority?')
    lifecycle.noteAnswer(key, 'Host owns PromptAuthority.')

    const settled = await lifecycle.tryFinalize(dir, key)
    assert.equal(settled.ok, false)
    assert.match(String(settled.error), /bookkeeper runtime unavailable/)

    const handle = eventStore.create(join(dir, '.git'), 'insp-settle-uncommitted-read')
    try {
      const fetched = await casebook.fetchCase(handle, 10, key)
      assert.equal(fetched.ok, true)
      assert.equal(fetched.value, null)
    } finally {
      eventStore.dispose(handle)
    }
  } finally {
    bookkeeper.resetRuntime()
    lifecycle.disable()
    cleanup()
  }
})

test('WHAT[managed-session-lifecycle-022] archive commits once and duplicate finalize creates no second Bookkeeper child', async () => {
  const { dir, cleanup } = sandbox()
  try {
    lifecycle.enable(dir)
    const { port, createCalls } = scriptedBookkeeperPort()
    const key = 'insp-settle-finalized'
    await installBookkeeperRuntime(port, [key])
    lifecycle.notePrompt(key, 'What owns PromptAuthority?')
    lifecycle.noteAnswer(key, 'Host owns PromptAuthority.')
    const first = await lifecycle.tryFinalize(dir, key)
    assert.equal(first.ok, true)
    assert.equal(createCalls.length, 1)

    const handle = eventStore.create(join(dir, '.git'), 'insp-settle-read')
    try {
      const fetched = await casebook.fetchCase(handle, 10, key)
      assert.equal(fetched.ok, true)
      assert.notEqual(fetched.value, null)
    } finally {
      eventStore.dispose(handle)
    }

    lifecycle.notePrompt(key, 'second finalize must not publish')
    lifecycle.noteAnswer(key, 'should be refused')
    const second = await lifecycle.tryFinalize(dir, key)
    assert.equal(second.ok, false)
    assert.match(String(second.error), /already finalized/)
    assert.equal(createCalls.length, 1)

    const reread = eventStore.create(join(dir, '.git'), 'insp-settle-reread')
    try {
      const still = await casebook.fetchCase(reread, 10, key)
      assert.equal(still.ok, true)
      assert.equal(still.value.sessionId, key)
      assert.equal(still.value.a, CANONICAL_A)
    } finally {
      eventStore.dispose(reread)
    }
  } finally {
    bookkeeper.resetRuntime()
    lifecycle.disable()
    cleanup()
  }
})

test('WHAT[managed-session-lifecycle-022] no draft finalizes successfully without creating a Bookkeeper child', async () => {
  const { dir, cleanup } = sandbox()
  try {
    lifecycle.enable(dir)
    const { port, createCalls } = scriptedBookkeeperPort()
    const key = 'insp-settle-empty'
    await installBookkeeperRuntime(port, [key])
    const settled = await lifecycle.tryFinalize(dir, key)
    assert.equal(settled.ok, true)
    assert.equal(createCalls.length, 0)
  } finally {
    bookkeeper.resetRuntime()
    lifecycle.disable()
    cleanup()
  }
})

test('WHAT[managed-session-lifecycle-022] finalize prerequisite uses the original plugin scope for Manager admission, attached Engineer completion and its production draft', async () => {
  let originalRuntime
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    assert.strictEqual(pluginLifecycle.hooks(originalRuntime), hooks)
    assert.equal(lifecycle.isEnabled(), true)
    const manager = 'd0p-manager'
    const managerPhysical = 'd0p-manager-physical'
    const charge = 'D0-P charge: establish the original attached execution boundary.'
    const answer = 'D0-P actual assistant completion: the attached execution owns this response.'
    const managerMessage = { id: managerPhysical, sessionID: manager, role: 'user', agent: 'manager', model: {} }
    const managerParts = [{ type: 'text', text: 'Delegate one controlled Engineer investigation.' }]
    runtime.pushHostMessage(manager, { info: managerMessage, parts: managerParts })
    await hooks['chat.message']({ sessionID: manager, messageID: managerPhysical, agent: 'manager' },
      { message: managerMessage, parts: managerParts })
    const owner = dispatch.projectionObservation(runtime.journal, manager).activeLogicalRun
    assert.notEqual(owner, null)
    assert.equal(owner.session, manager)
    assert.equal(owner.authorityKind, 'HumanRoot')
    assert.equal(owner.authorityRoot, managerPhysical)
    assert.equal(typeof owner.logicalRun, 'string')
    assert.ok(owner.logicalRun.length > 0)
    assert.equal(owner.participantIdentity.role, 'manager')
    assert.equal(owner.participantIdentity.participant, 'manager')
    assert.deepEqual(executionStatus.query(runtime.journal, manager, managerPhysical), {
      accepted: true, providerStarted: false, terminal: false, disposition: null,
    })
    assert.equal(pluginLifecycle.attachedEngineer(originalRuntime, manager), null)

    let signalPrompt
    const prompted = new Promise(resolve => { signalPrompt = resolve })
    const originalPrompt = runtime.client.session.promptAsync
    runtime.client.session.promptAsync = async function (args) {
      const result = await Reflect.apply(originalPrompt, this, [args])
      if (args.body?.agent === 'engineer') {
        const child = args.path.id
        const physicalMessages = (await runtime.client.session.messages({ path: { id: child } })).data
        const physical = physicalMessages.filter(message => (message.info ?? message).role === 'user').at(-1)
        signalPrompt({ child, message: physical, args })
      }
      return result
    }
    let invocation
    try {
      invocation = pluginLifecycle.invokeEngineer(originalRuntime, manager, charge)
      const sent = await Promise.race([
        prompted,
        invocation.then(result => { throw new Error(`Engineer finished before its physical prompt receipt: ${JSON.stringify(result)}`) }),
      ])
      assert.equal(pluginLifecycle.attachedEngineer(originalRuntime, manager), sent.child)
      assert.equal(createdIds.includes(sent.child), true)
      assert.equal(sent.args.body.agent, 'engineer')
      assert.ok(sent.message)
      const physicalInfo = sent.message.info ?? sent.message
      const physicalParts = sent.message.parts
      assert.equal(typeof physicalInfo.id, 'string')
      assert.ok(physicalInfo.id.length > 0)
      assert.equal(Array.isArray(physicalParts), true)
      assert.ok(physicalParts.some(part => part.type === 'text' && part.text.length > 0))
      assert.ok(physicalInfo.model)
      assert.equal(physicalInfo.model.providerID, 'provider')
      assert.equal(physicalInfo.model.modelID, 'engineer-model')
      assert.equal(await pluginLifecycle.awaitAssignmentReady(originalRuntime, sent.child), true)
      const childProfile = dispatch.projectionObservation(runtime.journal, sent.child).activeLogicalRun
      assert.notEqual(childProfile, null)
      assert.equal(childProfile.authorityKind, 'AgentOwnerRoot')
      assert.equal(childProfile.authorityRoot, physicalInfo.id)
      assert.equal(childProfile.participantIdentity.role, 'engineer')
      assert.equal(childProfile.identitySeed.kind, 'InheritedFromOwner')
      assert.equal(childProfile.identitySeed.ownerSession, manager)
      assert.equal(childProfile.identitySeed.ownerLogicalRun, owner.logicalRun)
      assert.equal(childProfile.identitySeed.ownerAuthorityRoot, managerPhysical)
      assert.deepEqual(executionStatus.query(runtime.journal, sent.child, physicalInfo.id), {
        accepted: true, providerStarted: false, terminal: false, disposition: null,
      })

      const assistant = { info: {
        id: 'd0p-assistant-run', sessionID: sent.child, parentID: physicalInfo.id,
        role: 'assistant', agent: 'engineer',
        providerID: physicalInfo.model.providerID, modelID: physicalInfo.model.modelID,
        time: { created: 2 },
      }, parts: [] }
      runtime.pushHostMessage(sent.child, assistant)
      const user = { info: { ...physicalInfo, sessionID: sent.child, agent: 'engineer' },
        parts: physicalParts }
      assert.strictEqual(user.parts, sent.message.parts)
      await hooks['experimental.chat.messages.transform']({ sessionID: sent.child }, { messages: [user] })
      assert.deepEqual(executionStatus.query(runtime.journal, sent.child, physicalInfo.id), {
        accepted: true, providerStarted: true, terminal: false, disposition: null,
      })

      assistant.parts.push({ id: 'd0p-answer-part', type: 'text', text: answer })
      assistant.info.time.completed = 3
      assistant.info.finish = 'stop'
      await hooks.event({ event: { type: 'message.updated', properties: { info: assistant.info } } })
      await hooks.event({ event: { type: 'session.idle', properties: { sessionID: sent.child } } })
      const result = await invocation
      assert.equal(result.ok, true, result.reason)
      assert.equal(typeof result.workRecord, 'string')
      assert.ok(result.workRecord.trim().length > 0)
      assert.ok(result.workRecord.includes(answer))
      assert.doesNotMatch(result.workRecord, /^Opening(?:\r?\n|$)/)
      assert.equal(pluginLifecycle.attachedEngineer(originalRuntime, manager), sent.child)
      assert.equal(runtime.prompts.filter(prompt => prompt.path?.id === sent.child).length, 1)

      const trace = semanticTrace.snapshot(runtime.journal, sent.child)
      assert.equal(semanticTrace.hasOpening(trace), true)
      const terminal = semanticTrace.terminalEvidenceForProviderRun(assistant.info.id, trace)
      assert.ok(terminal)
      assert.equal(terminal.providerRun, assistant.info.id)
      assert.deepEqual(terminal.frontier, semanticTrace.headCursor(trace))
      assert.ok(terminal.frontier.sequence > 0)
      assert.match(terminal.textRef, /^blobs\/[0-9a-f]{64}$/)
      const terminalBody = await journal.JournalSurface_readPayload(runtime.journal, terminal.textRef)
      assert.deepEqual(terminalBody, { ok: true, content: answer })
      assert.equal(terminal.textDigest, createHash('sha256').update(terminalBody.content, 'utf8').digest('hex'))

      // Consume the original draft only after proving completion.
      assert.deepEqual(pluginLifecycle.takeEngineerDraft(originalRuntime, manager), {
        sessionId: sent.child,
        turns: [{ question: charge, answer: result.workRecord }],
      })
      assert.equal(pluginLifecycle.takeEngineerDraft(originalRuntime, manager), null)
    } finally {
      runtime.client.session.promptAsync = originalPrompt
      // Dispose cancels/drains any pending call before we wait for its settlement.
      try {
        await hooks.dispose()
      } finally {
        if (invocation) await Promise.allSettled([invocation])
      }
    }
  }, {}, async input => {
    mkdirSync(join(input.directory, '.wanxiang', 'casebook'), { recursive: true })
    originalRuntime = await pluginLifecycle.create(input)
    return pluginLifecycle.hooks(originalRuntime)
  })
})

test.todo('WHAT[managed-session-lifecycle-022] real deletion preserves exact Inspector identity for NotCommitted, Unknown and PhaseConflict; only committed or empty finalization releases it (GAP-133)')
