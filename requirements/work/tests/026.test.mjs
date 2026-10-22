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
import { projectionObservation } from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import { query as executionStatus } from '../../../dist/Execution/Session/ChatExecution/StatusSurface.js'

const life = lifecycle.LifecycleSurface ?? lifecycle

const assertS4Admission = (runtime, sessionID) => {
  const physical = `msg-${sessionID}-0`
  const profile = projectionObservation(runtime.journal, sessionID).activeLogicalRun
  assert.ok(profile, 'the spawned S4 must have a durable managed authority profile')
  assert.equal(profile.session, sessionID)
  assert.equal(profile.authorityRoot, physical)
  assert.deepEqual(executionStatus(runtime.journal, sessionID, physical), {
    accepted: true, providerStarted: false, terminal: false, disposition: null,
  })
}

const installS4Prompt = (hooks, runtime) => {
  runtime.client.session.promptAsync = async args => {
    const request = JSON.parse(JSON.stringify(args))
    const sessionID = request.path.id
    const body = request.body
    assert.equal(body.agent, 'manager')
    const message = {
      id: `msg-${sessionID}-0`, role: 'user', sessionID, agent: body.agent,
      ...(Object.hasOwn(body, 'metadata') ? { metadata: body.metadata } : {}),
    }
    const parts = body.parts
    runtime.prompts.push(request)
    runtime.pushHostMessage(sessionID, { info: message, parts })
    await hooks['chat.message']({ sessionID, messageID: message.id, agent: body.agent }, { message, parts })
    assertS4Admission(runtime, sessionID)
    return {}
  }
}

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

const acceptPlanInput = async (hooks, runtime, sessionID, id) => {
  const message = {
    id, role: 'user', agent: 'plan', sessionID,
    model: { providerID: 'fixture', modelID: 'plan-model' },
  }
  const parts = [{ type: 'text', text: id }]
  runtime.pushHostMessage(sessionID, { info: { ...message, time: { created: 1 } }, parts })
  await hooks['chat.message']({ sessionID, agent: 'plan', messageID: id }, { message, parts })
}

const withAuthorizedS3 = async (name, body) => {
  await withExecutablePlugin(async (hooks, directory, createdIds, runtime) => {
    installS4Prompt(hooks, runtime)
    const commonDir = await journalSurface.JournalSurface_initializeStateRoot(directory)
    const planRoot = join(commonDir, 'wanxiangshu')
    const sessionID = `plan-physical-delivery-${name}`
    await acceptAuthorityRoot(runtime, sessionID, 'plan', 'physical-a')
    const opened = await surface.PlanningSurface.ensurePlanWorkOpened(commonDir, directory, sessionID, 0n)
    assert.equal(opened.ok, true, opened.error)
    for (let stage = 1; stage <= 3; stage += 1) {
      const activeId = life.inspected(commonDir, sessionID).ActiveId
      const reviewed = await surface.PlanningSurface.executeReview(commonDir, sessionID, activeId, [], null)
      assert.equal(reviewed.ok, true, reviewed.error)
      if (stage < 3) {
        const delivered = await surface.PlanningSurface.executeDeliver(
          commonDir, planRoot, sessionID, activeId, `setup-run-${stage}`, `setup-call-${stage}`, false,
          { create: async () => 'unexpected', prompt: async () => ({ kind: 'Started' }), discard: async () => {} },
          0n,
        )
        assert.equal(delivered.ok, true, delivered.error)
      }
    }
    const written = surface.PlanningSurface.rewritePlanAtomic(planRoot, sessionID, '# Authorized S3 plan\n')
    assert.equal(written.ok, true, written.error)
    await acceptPlanInput(hooks, runtime, sessionID, 'physical-a')
    await body({ hooks, runtime, sessionID, commonDir, createdIds })
  })
}

const deliverCall = (runtime, sessionID, run, call, physical) => {
  runtime.pushHostMessage(sessionID, {
    info: { id: run, role: 'assistant', sessionID, parentID: physical, time: { created: 2 } },
    parts: [{ type: 'tool', tool: 'deliver', callID: call, state: { status: 'pending', input: {} } }],
  })
  return { sessionID, agent: 'plan', callID: call, messageID: run, providerRunID: run, physicalInput: physical }
}

const completeDelivery = async (hooks, runtime, context, output) => {
  await hooks.event({
    event: {
      type: 'message.part.updated',
      properties: {
        part: {
          type: 'tool', sessionID: context.sessionID, messageID: context.messageID,
          tool: 'deliver', callID: context.callID, state: { status: 'completed', input: {}, output },
        },
      },
    },
  })
  const snapshot = await runtime.client.session.messages({ path: { id: context.sessionID } })
  await hooks['experimental.chat.messages.transform']({ sessionID: context.sessionID }, { messages: snapshot.data })
}

test('WHAT[work-026] a delivery awaiting its owning snapshot cannot aim its interrupt at a later accepted physical input', async () => {
  await withAuthorizedS3('late-snapshot', async ({ hooks, runtime, sessionID, commonDir, createdIds }) => {
    const context = deliverCall(runtime, sessionID, 'run-physical-a', 'call-physical-a', 'physical-a')
    const readMessages = runtime.client.session.messages
    const entered = Promise.withResolvers()
    const release = Promise.withResolvers()
    runtime.client.session.messages = async (args) => {
      const snapshot = await readMessages(args)
      if (args?.path?.id === sessionID) {
        entered.resolve()
        await release.promise
      }
      return snapshot
    }
    const pending = hooks.tool.deliver.execute({}, context)
    try {
      await Promise.race([entered.promise, pending.then(() => { throw new Error('deliver did not read its owning snapshot') })])
      await acceptPlanInput(hooks, runtime, sessionID, 'physical-b')
      runtime.pushHostMessage(sessionID, {
        info: { id: 'run-physical-b', role: 'assistant', sessionID, parentID: 'physical-b', agent: 'plan', providerID: 'fixture', modelID: 'plan-model', time: { created: 3 } },
        parts: [],
      })
      release.resolve()
      const reply = await pending
      assert.match(String(reply), /post-S3/i)
      assert.equal(life.inspected(commonDir, sessionID).PendingDelivery, true)
      assert.equal(createdIds.length, 0)
      await completeDelivery(hooks, runtime, context, reply)
      assert.deepEqual(runtime.abortedIds, [], 'the old delivery must not interrupt the newly accepted physical input')
      await completeDelivery(hooks, runtime, context, reply)
      assert.deepEqual(runtime.abortedIds, [], 'replayed completion must not resurrect the old interrupt')
    } finally {
      release.resolve()
      runtime.client.session.messages = readMessages
      await pending
    }
  })
})

test('WHAT[work-026] each successful post-S3 execution launch registers one interrupt for its bound physical input', async () => {
  await withAuthorizedS3('repeated-launch', async ({ hooks, runtime, sessionID, commonDir, createdIds }) => {
    const initial = deliverCall(runtime, sessionID, 'run-s3-bound', 'call-s3-bound', 'physical-a')
    const postS3 = await hooks.tool.deliver.execute({}, initial)
    assert.match(String(postS3), /post-S3/i)
    await completeDelivery(hooks, runtime, initial, postS3)
    assert.deepEqual(runtime.abortedIds, [sessionID], 'the S3 positive control must interrupt its own input')
    await completeDelivery(hooks, runtime, initial, postS3)
    assert.deepEqual(runtime.abortedIds, [sessionID], 'the S3 completion is idempotent')
    await acceptPlanInput(hooks, runtime, sessionID, 'physical-launch')
    const launch = deliverCall(runtime, sessionID, 'run-launch-bound', 'call-launch-bound', 'physical-launch')
    const reply = await hooks.tool.deliver.execute({}, launch)
    assert.match(String(reply), /an execution session has been started/i)
    assert.equal(createdIds.length, 1)
    assertS4Admission(runtime, createdIds[0])
    await completeDelivery(hooks, runtime, launch, reply)
    assert.deepEqual(runtime.abortedIds, [sessionID, sessionID], 'the successful launch must interrupt its own input')
    await completeDelivery(hooks, runtime, launch, reply)
    assert.deepEqual(runtime.abortedIds, [sessionID, sessionID], 'a repeated launch completion interrupts only once')
  })
})

test('WHAT[work-026] an owning exchange without physical parent evidence cannot borrow the current input for an interrupt', async () => {
  await withAuthorizedS3('missing-parent', async ({ hooks, runtime, sessionID, commonDir, createdIds }) => {
    await acceptPlanInput(hooks, runtime, sessionID, 'physical-b-without-owner')
    const context = deliverCall(runtime, sessionID, 'run-unbound', 'call-unbound', 'missing-physical-parent')
    const refused = await hooks.tool.deliver.execute({}, context)
    assert.match(String(refused), /deliver failed: Unable to bind the current work safely/)
    assert.equal(life.inspected(commonDir, sessionID).PendingDelivery, false)
    assert.equal(createdIds.length, 0)
    assert.deepEqual(runtime.abortedIds, [], 'the current input does not prove which physical input owned this delivery')
  })
})

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
    installS4Prompt(hooks, runtime)
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
    await hooks['chat.message']({ sessionID, agent: 'plan', messageID: 'user-plan-root' }, {
      message: { id: 'user-plan-root', role: 'user', agent: 'plan', sessionID },
      parts: [{ type: 'text', text: 'Plan the feature.' }],
    })

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
    assertS4Admission(runtime, createdIds[0])
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
    assertS4Admission(runtime, createdIds[1])

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
