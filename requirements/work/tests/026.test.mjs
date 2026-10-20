import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { acceptAuthorityRoot, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'
import * as surface from '../../../dist/Mission/Planning/Surface.js'
import * as workspace from '../../../dist/OpenCode/Host/WorkspaceEventStoreSurface.js'
import * as journalSurface from '../../../dist/Persistence/Journal/Surface.js'

const life = lifecycle.LifecycleSurface ?? lifecycle

const withWorkspace = async (body) => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-planning-008-'))
  const commonDir = join(root, '.Herdchestra')
  mkdirSync(commonDir, { recursive: true })
  try {
    await body(workspace.acquire(commonDir), root, commonDir)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
}

const openWork = async (store, commonDir, workId) => {
  const opened = await surface.PlanningSurface.ensurePlanWorkOpened(commonDir, commonDir, workId, 0n)
  assert.equal(opened.ok, true, opened.error)
  const folded = life.readProbe(life.encodeOpening('plan', workId, commonDir, 0n))
  assert.equal(folded.ok, true, folded.reason)
  assert.notEqual(folded.activeId, '')
  return folded.activeId
}

// The provider request the transform hook adjudicates. WHAT[work-026]/[011]:
// the delivery stop happens at the transform hook's delivery-adjudication
// stage, so the request under test must carry the physical user message the
// delivery was bound to.
const providerRequest = (sessionID, userMessageId, assistantMessageId) => ({
  messages: [
    {
      role: 'user',
      info: { id: userMessageId, sessionID, role: 'user' },
      parts: [{ type: 'text', text: 'Plan the feature.' }],
    },
    {
      role: 'assistant',
      info: {
        id: assistantMessageId,
        sessionID,
        role: 'assistant',
        parentID: userMessageId,
        providerID: 'provider',
        modelID: 'plan-model',
      },
      parts: [],
    },
  ],
})

test('WHAT[work-026] authorized deliver without retirement evidence fails closed with no spawn', async () => {
  await withWorkspace(async (store, root, commonDir) => {
    const workId = 'work-d3'
    const written = surface.PlanningSurface.rewritePlanAtomic(root, workId, '# Plan content')
    assert.equal(written.ok, true)
    const activeId = await openWork(store, commonDir, workId)
    let spawnCalls = 0
    const res = await surface.PlanningSurface.executeDeliver(
      commonDir, root, workId, activeId, 'run-d3', 'call-d3', false,
      {
        create: async () => { spawnCalls += 1; return 'exec-never' },
        prompt: async () => { spawnCalls += 1; return { kind: 'Started' } },
        discard: async () => {},
      },
      undefined,
    )
    assert.equal(res.ok, false)
    assert.match(res.error, /retirement cursor evidence is required/i)
    assert.equal(spawnCalls, 0)
  })
})

test('WHAT[work-026] S3 authorized deliver enters post-S3 state without starting S4, interrupts the conversation, disables nudges, and later user-commanded delivers can repeatedly start S4', async () => {
  await withExecutablePlugin(async (hooks, directory, createdIds, runtime) => {
    const commonDir = await journalSurface.JournalSurface_initializeStateRoot(directory)
    const planRoot = join(commonDir, 'wanxiangshu')
    const sessionID = 'ses-plan-post-s3'
    await acceptAuthorityRoot(runtime, sessionID, 'plan', 'user-plan-root')
    runtime.pushHostMessage(sessionID, {
      info: { id: 'user-plan-root', role: 'user', sessionID, time: { created: 1 } },
      parts: [{ type: 'text', text: 'Plan the feature.' }],
    })

    // The work is opened with the root production uses: the transform hook
    // opens the plan work at the git worktree root of the session
    // (`PlanWorkOpenWiring.ensurePlanWorkOpenedForSession`). Opening it at
    // any other root makes the transform hook refuse this session with
    // "existing lifecycle work has a different root".
    const opened = await surface.PlanningSurface.ensurePlanWorkOpened(commonDir, directory, sessionID, 0n)
    assert.equal(opened.ok, true, opened.error)

    // Advance S1 -> S2 -> S3 -> authorized
    for (let stage = 1; stage <= 3; stage += 1) {
      const activeId = life.inspected(commonDir, sessionID).ActiveId
      const rev = await surface.PlanningSurface.executeReview(commonDir, sessionID, activeId, [], null)
      assert.equal(rev.ok, true, rev.error)
      if (stage < 3) {
        const rel = await surface.PlanningSurface.executeDeliver(
          commonDir, planRoot, sessionID, activeId, `run-s${stage}`, `call-s${stage}`, false,
          { create: async () => 'unexpected', prompt: async () => ({ kind: 'Started' }), discard: async () => {} },
          0n,
        )
        assert.equal(rel.ok, true, rel.error)
      }
    }

    const written = surface.PlanningSurface.rewritePlanAtomic(planRoot, sessionID, '# Final S3 Plan\n\nAlgorithm A.\n')
    assert.equal(written.ok, true, written.error)
    assert.equal(life.inspected(commonDir, sessionID).DeliveryAuthorized, true)

    // Push owning exchange for S3 deliver
    runtime.pushHostMessage(sessionID, {
      info: { id: 'run-s3-deliver', role: 'assistant', sessionID, parentID: 'user-plan-root', time: { created: 2 } },
      parts: [{ type: 'tool', tool: 'deliver', callID: 'call-s3-deliver', state: { status: 'pending', input: {} } }],
    })

    const postS3Reply = await hooks.tool.deliver.execute(
      {},
      { sessionID, agent: 'plan', callID: 'call-s3-deliver', messageID: 'run-s3-deliver', providerRunID: 'run-s3-deliver' },
    )
    assert.match(String(postS3Reply), /post-S3/i)
    assert.equal(createdIds.length, 0, 'S3 deliver must enter post-S3 state without immediately starting S4')
    // LC-019: a committed DeliveryRequested IS the durable fact of plan
    // completion; the work is delivered pending the execution handoff. No
    // synthesized Accepted/execution session exists until the user commands
    // the real spawn.
    assert.equal(life.inspected(commonDir, sessionID).PendingDelivery, true, 'a DeliveryRequested is durably pending the execution handoff')
    // WHAT[work-026]/[011]: the deliver tool registers the "delivery is
    // established" fact only. The stop happens at the transform hook's
    // delivery-adjudication stage: the tool must not interrupt by itself.
    assert.deepEqual(runtime.abortedIds, [], 'the deliver tool does not interrupt; the transform hook does')

    // The provider request of the delivering attempt. The delivery is bound
    // to the physical input the delivering call answered, so the request
    // under adjudication must carry that same physical input.
    const delivering = providerRequest(sessionID, 'user-plan-root', 'run-s3-deliver')
    await hooks['experimental.chat.messages.transform']({ sessionID }, delivering)

    assert.deepEqual(
      runtime.abortedIds,
      [sessionID],
      'the transform hook interrupts this attempt after an established delivery',
    )
    assert.equal(
      delivering.messages.length,
      0,
      'the transform hook clears the request messages so the model does not see the deliver receipt',
    )

    // A second request on the SAME physical input is still stopped (the
    // delivery is still the answer to that input), but the interrupt is
    // issued once: "issued" and "observed" are two separately recorded facts.
    const repeated = providerRequest(sessionID, 'user-plan-root', 'run-s3-deliver')
    await hooks['experimental.chat.messages.transform']({ sessionID }, repeated)
    assert.equal(
      runtime.abortedIds.length,
      1,
      'the interrupt of this attempt is issued once, not once per request',
    )
    assert.equal(repeated.messages.length, 0, 'the repeated request on the same physical input is still cleared')

    // User commands deliver in post-S3 state -> starts S4 (first time)
    runtime.pushHostMessage(sessionID, {
      info: { id: 'user-cmd-deliver-1', role: 'user', sessionID, time: { created: 3 } },
      parts: [{ type: 'text', text: 'Please execute deliver now.' }],
    })
    runtime.pushHostMessage(sessionID, {
      info: { id: 'run-post-s3-1', role: 'assistant', sessionID, parentID: 'user-cmd-deliver-1', time: { created: 4 } },
      parts: [{ type: 'tool', tool: 'deliver', callID: 'call-post-s3-1', state: { status: 'pending', input: {} } }],
    })
    const start1 = await hooks.tool.deliver.execute(
      {},
      { sessionID, agent: 'plan', callID: 'call-post-s3-1', messageID: 'run-post-s3-1', providerRunID: 'run-post-s3-1' },
    )
    assert.match(String(start1), /an execution session has been started/i)
    assert.equal(createdIds.length, 1, 'first post-S3 deliver must start one S4 execution session')
    assert.equal(life.inspected(commonDir, sessionID).Delivered, true, 'the accepted execution settles the delivery retirement')

    // User commands deliver again in post-S3 state -> starts another S4 (repeatedly)
    runtime.pushHostMessage(sessionID, {
      info: { id: 'user-cmd-deliver-2', role: 'user', sessionID, time: { created: 5 } },
      parts: [{ type: 'text', text: 'Start another S4 execution from this plan.' }],
    })
    runtime.pushHostMessage(sessionID, {
      info: { id: 'run-post-s3-2', role: 'assistant', sessionID, parentID: 'user-cmd-deliver-2', time: { created: 6 } },
      parts: [{ type: 'tool', tool: 'deliver', callID: 'call-post-s3-2', state: { status: 'pending', input: {} } }],
    })
    const start2 = await hooks.tool.deliver.execute(
      {},
      { sessionID, agent: 'plan', callID: 'call-post-s3-2', messageID: 'run-post-s3-2', providerRunID: 'run-post-s3-2' },
    )
    assert.match(String(start2), /an execution session has been started/i)
    assert.equal(createdIds.length, 2, 'user can repeatedly request starting S4 in post-S3 state')

    // A replayed fence is answered by the fold, not by a second launch. [007]:
    // a completed delivery operation's replay returns the original retirement
    // result, so the launch fence reports its recorded Delivered outcome with
    // zero appends and without starting another execution session.
    const settled = life.inspect(commonDir, sessionID).revision
    const replayLaunch = await hooks.tool.deliver.execute(
      {},
      { sessionID, agent: 'plan', callID: 'call-post-s3-1', messageID: 'run-post-s3-1', providerRunID: 'run-post-s3-1' },
    )
    assert.match(String(replayLaunch), /Delivered/, `a replayed launch fence must report the recorded retirement result: ${replayLaunch}`)
    assert.equal(createdIds.length, 2, 'a replayed launch fence must not start a second execution session')
    assert.equal(life.inspect(commonDir, sessionID).revision, settled, 'a replayed launch fence appends nothing')

    // The same must hold for the delivering fence itself: it already owns a
    // retirement, so re-entering it reports that recorded result instead of
    // reopening the delivered work or freezing the runner with an error.
    const replayDeliver = await hooks.tool.deliver.execute(
      {},
      { sessionID, agent: 'plan', callID: 'call-s3-deliver', messageID: 'run-s3-deliver', providerRunID: 'run-s3-deliver' },
    )
    assert.match(String(replayDeliver), /Delivered/, `replayed delivering fence must report the recorded result: ${replayDeliver}`)
    assert.equal(createdIds.length, 2, 'a replayed delivering fence must not start an execution session')
    assert.equal(life.inspect(commonDir, sessionID).revision, settled, 'a replayed delivering fence appends nothing')
    assert.equal(life.inspected(commonDir, sessionID).Delivered, true, 'the work stays delivered and the tenure is not reopened')
  })
})

test('WHAT[work-026] a new user input after the delivery is not stopped by that delivery', async () => {
  await withExecutablePlugin(async (hooks, directory, createdIds, runtime) => {
    const commonDir = await journalSurface.JournalSurface_initializeStateRoot(directory)
    const planRoot = join(commonDir, 'wanxiangshu')
    const sessionID = 'ses-plan-post-s3-new-input'
    await acceptAuthorityRoot(runtime, sessionID, 'plan', 'user-plan-root')
    runtime.pushHostMessage(sessionID, {
      info: { id: 'user-plan-root', role: 'user', sessionID, time: { created: 1 } },
      parts: [{ type: 'text', text: 'Plan the feature.' }],
    })

    const opened = await surface.PlanningSurface.ensurePlanWorkOpened(commonDir, directory, sessionID, 0n)
    assert.equal(opened.ok, true, opened.error)

    for (let stage = 1; stage <= 3; stage += 1) {
      const activeId = life.inspected(commonDir, sessionID).ActiveId
      await surface.PlanningSurface.executeReview(commonDir, sessionID, activeId, [], null)
      if (stage < 3) {
        await surface.PlanningSurface.executeDeliver(
          commonDir, planRoot, sessionID, activeId, `run-s${stage}`, `call-s${stage}`, false,
          { create: async () => 'unexpected', prompt: async () => ({ kind: 'Started' }), discard: async () => {} },
          0n,
        )
      }
    }
    surface.PlanningSurface.rewritePlanAtomic(planRoot, sessionID, '# Final S3 Plan\n\nAlgorithm A.\n')

    runtime.pushHostMessage(sessionID, {
      info: { id: 'run-s3-deliver', role: 'assistant', sessionID, parentID: 'user-plan-root', time: { created: 2 } },
      parts: [{ type: 'tool', tool: 'deliver', callID: 'call-s3-deliver', state: { status: 'pending', input: {} } }],
    })
    await hooks.tool.deliver.execute(
      {},
      { sessionID, agent: 'plan', callID: 'call-s3-deliver', messageID: 'run-s3-deliver', providerRunID: 'run-s3-deliver' },
    )

    // A real new user input arrives AFTER the delivery. It is a different
    // physical input, so the delivery must not aim its stop at it.
    const newInput = {
      id: 'user-new-input',
      role: 'user',
      agent: 'plan',
      sessionID,
      model: { providerID: 'provider', modelID: 'plan-model' },
    }
    runtime.pushHostMessage(sessionID, {
      info: { ...newInput, time: { created: 3 } },
      parts: [{ type: 'text', text: 'Now execute the plan.' }],
    })
    await hooks['chat.message']({ sessionID, agent: 'plan', messageID: 'user-new-input' }, {
      message: newInput,
      parts: [{ type: 'text', text: 'Now execute the plan.' }],
    })
    runtime.pushHostMessage(sessionID, {
      info: {
        id: 'run-new-input',
        role: 'assistant',
        sessionID,
        parentID: 'user-new-input',
        providerID: 'provider',
        modelID: 'plan-model',
        time: { created: 4 },
        finish: 'stop',
      },
      parts: [],
    })

    const request = providerRequest(sessionID, 'user-new-input', 'run-new-input')
    await hooks['experimental.chat.messages.transform']({ sessionID }, request)

    assert.deepEqual(
      runtime.abortedIds,
      [],
      'a new physical input is not stopped by the delivery bound to the previous input',
    )
    assert.equal(
      request.messages.length,
      2,
      'the new input request is not cleared by the previous delivery',
    )
  })
})
