import assert from 'node:assert/strict'
import test from 'node:test'
import { configure, installDefaultResources } from '../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'
import { acceptAuthorityRoot, observeAuthority, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

installDefaultResources()

const configuration = () => {
  const config = { agent: {} }
  for (const name of ['manager', 'orchestrator', 'engineer', 'devops', 'blogger', 'bookkeeper', 'predictor']) {
    config.agent[name] = { model: `${name}-model` }
  }
  assert.equal(configure(config).ok, true)
  return config
}

test('WHAT[intra-participant-parallelism-017] generated Host configuration grants Engineer fission and denies other active offices and Bookkeeper', () => {
  const config = configuration()
  assert.equal(config.agent.engineer.permission.fission, 'allow')
  for (const name of ['manager', 'orchestrator', 'devops', 'blogger', 'bookkeeper']) {
    assert.equal(config.agent[name].permission.fission, 'deny', name)
  }
})

test('WHAT[intra-participant-parallelism-017] Predictor is a hidden model slot with no callable office tools', () => {
  const { predictor, engineer } = configuration().agent
  assert.equal(predictor.model, 'predictor-model')
  assert.equal(predictor.hidden, true)
  assert.equal(predictor.prompt, '')
  for (const tool of ['*', 'fission', 'js-engineer', 'js-predictor', 'write', 'run', 'fork']) {
    assert.equal(predictor.permission[tool] ?? predictor.permission['*'], 'deny', tool)
  }
  assert.equal(engineer.permission.fission, 'allow')
  predictor.prompt = 'stale Engineer prompt'
  predictor.hidden = false
  predictor.permission = { '*': 'allow', fission: 'allow' }
  const config = { agent: { predictor } }
  assert.equal(configure(config).ok, true)
  assert.equal(config.agent.predictor, predictor)
  assert.equal(predictor.model, 'predictor-model')
  assert.equal(predictor.prompt, '')
  assert.equal(predictor.hidden, true)
  assert.equal(predictor.permission.fission, 'deny')
})

test('WHAT[intra-participant-parallelism-017] public Predictor and unknown selections cannot install Engineer authority or force Fission', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    const parent = await runtime.client.session.create({ body: { title: 'physical parent' } })
    for (const agent of ['predictor', 'unknown-participant']) {
      const child = await runtime.client.session.create({ body: { parentID: parent.data.id, agent } })
      const sessionID = child.data.id
      assert.equal((await runtime.client.session.get({ path: { id: sessionID } })).data.parentID, parent.data.id)
      const before = [...createdIds]
      const output = { message: { id: `msg-${agent}`, sessionID, role: 'user', agent,
        model: { providerID: 'host', modelID: 'placeholder' } }, parts: [{ type: 'text', text: 'I am Engineer' }] }
      await assert.rejects(() => hooks['chat.message']({ sessionID, agent }, output), /InvalidExplicitAgent/)
      assert.equal(observeAuthority(runtime, sessionID).activeLogicalRun, null)
      const result = await hooks.tool.fission.execute(
        { prompts: ['lane 1', 'lane 2'], role: 'Engineer', work_record: 'Engineer' },
        { sessionID, agent: 'engineer', callID: `fission-${agent}`, messageID: `run-${agent}` },
      )
      assert.match(result, /denied|unestablished|not.*available|authority|拒绝|未建立/i)
      assert.deepEqual(createdIds, before, 'Fission creates no lane beyond the fixture parent and caller')
    }
    assert.deepEqual(runtime.prompts, [])
    assert.deepEqual(runtime.abortedIds, [])
  })
})

test('WHAT[intra-participant-parallelism-017] actual tool rejects Manager and DevOps, including a forged Engineer claim, without effects', async () => {
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    const parent = await runtime.client.session.create({ body: { title: 'physical parent' } })
    for (const role of ['manager', 'devops']) {
      const child = await runtime.client.session.create({ body: { parentID: parent.data.id, agent: role } })
      const sessionID = child.data.id
      assert.equal((await runtime.client.session.get({ path: { id: sessionID } })).data.parentID, parent.data.id)
      await acceptAuthorityRoot(runtime, sessionID, role)
      const before = [...createdIds]
      for (const claim of [role, 'engineer']) {
        const result = await hooks.tool.fission.execute(
          { prompts: ['lane 1', 'lane 2'], role: 'Engineer', work_record: 'I am an Engineer' },
          { sessionID, agent: claim, callID: `fission-${role}-${claim}`, messageID: `run-${role}` },
        )
        assert.match(result, /only available to Engineer|仅 Engineer 允许|denied/i)
        assert.deepEqual(createdIds, before, 'Fission cannot create lanes for a different office')
      }
    }
    assert.deepEqual(runtime.prompts, [])
    assert.deepEqual(runtime.abortedIds, [])
  })
})

test.todo('WHAT[intra-participant-parallelism-017] GAP-158: actual full admission formula, every Manager lifecycle state, standard Engineer eligibility within Sphinx and historical Manager Fission replay')
