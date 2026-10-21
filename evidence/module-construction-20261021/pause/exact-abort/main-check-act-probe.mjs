import assert from 'node:assert/strict'
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { createHash } from 'node:crypto'
import { join } from 'node:path'
import { acceptAuthorityRoot, withExecutablePlugin } from '/Users/yuanxi/.codex/worktrees/upstream-plan-20261020/vibe-fs/requirements/verification-system/tests/support/plugin-fixture.mjs'
import * as lifecycle from '/Users/yuanxi/.codex/worktrees/upstream-plan-20261020/vibe-fs/dist/Mission/Lifecycle/Surface.js'
import * as planning from '/Users/yuanxi/.codex/worktrees/upstream-plan-20261020/vibe-fs/dist/Mission/Planning/Surface.js'
import * as journal from '/Users/yuanxi/.codex/worktrees/upstream-plan-20261020/vibe-fs/dist/Persistence/Journal/Surface.js'
import { query } from '/Users/yuanxi/.codex/worktrees/upstream-plan-20261020/vibe-fs/dist/Execution/Session/ChatExecution/StatusSurface.js'

const folder = '/private/tmp/vibe-fs-exact-abort-review-20261021'
const root = '/Users/yuanxi/.codex/worktrees/upstream-plan-20261020/vibe-fs'
const artifact = JSON.parse(readFileSync(folder + '/main-artifact-before.json', 'utf8'))
const life = lifecycle.LifecycleSurface ?? lifecycle
const reports = []
const accept = async (hooks, runtime, sessionID, id) => {
  const message = { id, role: 'user', agent: 'plan', sessionID, model: { providerID: 'fixture', modelID: 'plan-model' } }
  const parts = [{ type: 'text', text: id }]
  runtime.pushHostMessage(sessionID, { info: { ...message, time: { created: 1 } }, parts })
  await hooks['chat.message']({ sessionID, agent: 'plan', messageID: id }, { message, parts })
}
for (const racing of [false, true]) {
  await withExecutablePlugin(async (hooks, directory, createdIds, runtime) => {
    const sessionID = 'exact-abort-' + (racing ? 'racing' : 'same')
    const common = await journal.JournalSurface_initializeStateRoot(directory)
    const planRoot = join(common, 'wanxiangshu')
    await acceptAuthorityRoot(runtime, sessionID, 'plan', 'physical-a')
    assert.equal((await planning.PlanningSurface.ensurePlanWorkOpened(common, planRoot, sessionID, 0n)).ok, true)
    for (let stage = 1; stage <= 3; stage += 1) {
      const active = life.inspected(common, sessionID).ActiveId
      assert.equal((await planning.PlanningSurface.executeReview(common, sessionID, active, [], null)).ok, true)
      if (stage < 3) {
        const result = await planning.PlanningSurface.executeDeliver(common, planRoot, sessionID, active, `setup-${stage}`, `call-${stage}`, false,
          { create: async () => 'unexpected', prompt: async () => ({ kind: 'Started' }), discard: async () => {} }, 0n)
        assert.equal(result.ok, true, result.error)
      }
    }
    assert.equal(planning.PlanningSurface.rewritePlanAtomic(planRoot, sessionID, '# Authorized S3 Plan\n').ok, true)
    await accept(hooks, runtime, sessionID, 'physical-a')
    runtime.pushHostMessage(sessionID, {
      info: { id: 'run-a', role: 'assistant', sessionID, parentID: 'physical-a', time: { created: 2 } },
      parts: [{ type: 'tool', tool: 'deliver', callID: 'call-a', state: { status: 'pending', input: {} } }],
    })
    const context = { sessionID, agent: 'plan', callID: 'call-a', messageID: 'run-a', providerRunID: 'run-a' }
    const reply = await hooks.tool.deliver.execute({}, context)
    assert.match(String(reply), /post-S3/i)
    const entered = Promise.withResolvers()
    const release = Promise.withResolvers()
    const nativeAbort = runtime.client.session.abort
    let hostPhysical = 'physical-a'
    const transport = []
    runtime.client.session.abort = async args => {
      entered.resolve()
      if (racing) await release.promise
      transport.push({ payload: JSON.parse(JSON.stringify(args)), hostPhysicalAtEffect: hostPhysical })
      return nativeAbort(args)
    }
    const completion = hooks.event({ event: { type: 'message.part.updated', properties: { part: {
      type: 'tool', sessionID, messageID: 'run-a', tool: 'deliver', callID: 'call-a', state: { status: 'completed', input: {}, output: reply },
    } } } })
    try {
      await entered.promise
      if (racing) {
        await accept(hooks, runtime, sessionID, 'physical-b')
        assert.equal(query(runtime.journal, sessionID, 'physical-b').accepted, true)
        hostPhysical = 'physical-b'
        release.resolve()
      }
      await completion
      assert.deepEqual(runtime.abortedIds, [sessionID])
      reports.push({ racing, abortedIds: [...runtime.abortedIds], transport, bAccepted: racing ? query(runtime.journal, sessionID, 'physical-b') : null })
    } finally {
      release.resolve()
      await completion
      runtime.client.session.abort = nativeAbort
    }
  })
}
for (const expected of artifact.files) {
  assert.equal(createHash('sha256').update(readFileSync(root + '/dist/' + expected.path)).digest('hex'), expected.sha256)
}
writeFileSync(folder + '/main-check-act-results.json', JSON.stringify({ generation: artifact.generation, scope: 'Actual compiled plugin and durable admission; controlled SDK Host transport, not a live provider', reports }, null, 2))
console.log(JSON.stringify(reports))
assert.deepEqual(reports[1].abortedIds, [], 'An old A completion must not issue an unqualified abort effect after B is durably admitted')
