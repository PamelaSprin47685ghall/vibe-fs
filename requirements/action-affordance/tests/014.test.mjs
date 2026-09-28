import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import * as admission from '../../../dist/Participant/Cognition/AdmissionSurface.js'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'
import { acceptAuthorityRoot, withExecutablePlugin, withPlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

test('WHAT[action-affordance-014] assume admission accepts one update and a complete todo declaration without prescribing canvas fields', () => {
  const update = '{ideas:["compressed memory","random access"], custom:null}'
  const todos = [{ content: 'Compare the ideas', status: 'pending', priority: 'high' }]
  assert.deepEqual(admission.tryDecode({ update, todos }), { ok: true, value: { update, todos } })
  assert.deepEqual(admission.tryDecode({ update: '.', todos: [] }), { ok: true, value: { update: '.', todos: [] } })
})

test('WHAT[action-affordance-014] retired query and extra control arguments cannot silently change the assume contract', () => {
  for (const key of ['query', 'selector', 'vars', 'revision', 'mode']) {
    assert.equal(admission.rejectsBecause({ update: '.', todos: [], [key]: '.' }), 'UnknownArgument', key)
  }
  assert.equal(admission.rejectsBecause({ todos: [] }), 'MissingUpdate')
  assert.equal(admission.rejectsBecause({ update: '.' }), 'MissingTodos')
  assert.equal(admission.rejectsBecause({ update: 42, todos: [] }), 'UpdateNotString')
  assert.equal(admission.rejectsBecause({ update: '.', todos: 'pending' }), 'TodosNotArray')
})

integrationTest('WHAT[action-affordance-014] the registered assume schema exposes exactly update and todos', async () => {
  await withPlugin(async (hooks) => {
    const args = hooks.tool.assume.args
    assert.deepEqual(Object.keys(args).sort(), ['todos', 'update'])
    assert.equal(args.update.safeParse('.').success, true)
    assert.equal(args.todos.safeParse([]).success, true)
    assert.equal(args.update.safeParse(undefined).success, false)
    assert.equal(args.todos.safeParse(undefined).success, false)
  })
})

integrationTest('WHAT[action-affordance-014] registered assume executes jq against its session canvas and leaves it unchanged on rejected updates', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'assume-contract'
    const otherSessionID = 'assume-other'
    await acceptAuthorityRoot(runtime, sessionID, 'engineer')
    await acceptAuthorityRoot(runtime, otherSessionID, 'engineer')
    let ordinal = 0
    const call = (update, owner = sessionID) => {
      const callID = `assume-${++ordinal}`
      return hooks.tool.assume.execute({ update, todos: [] }, { sessionID: owner, agent: 'engineer', callID, messageID: `msg-${callID}` })
    }
    assert.deepEqual(parseToml(await call('{counter:0, ideas:["first","second"]}')), { counter: 0, ideas: ['first', 'second'] })
    assert.deepEqual(parseToml(await call('.counter += 1')), { counter: 1, ideas: ['first', 'second'] })
    for (const update of ['empty', '1, 2', 'error("rejected-update")', '[']) {
      const rejected = await call(update)
      assert.deepEqual(parseToml(rejected), {}, 'rejection carries no successful canvas')
      assert.ok(rejected.trim().length > 0, 'rejection explains failure')
      assert.deepEqual(parseToml(await call('.')), { counter: 1, ideas: ['first', 'second'] })
    }
    assert.deepEqual(parseToml(await call('.', otherSessionID)), {}, 'another physical session starts with its own empty canvas')
    assert.deepEqual(parseToml(await call('.')), { counter: 1, ideas: ['first', 'second'] })
  })
})

test.todo('WHAT[action-affordance-014] full-canvas delivery must also preserve null mixed arrays and special keys; object-only execution does not prove lossless rendering or UI delivery')
test.todo('WHAT[action-affordance-014] tool description explains free-form cognition and commitment discipline; jq execution alone does not establish descriptive completeness (GAP-078)')
