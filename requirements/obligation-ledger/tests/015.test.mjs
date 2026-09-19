import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { applyToolDefinitionHook, createMagicTodoContractHooks, decodeV1TodoWriteArgs, hostTrigger, runHostV1ToolExecutePath, sampleObligationTodoWriteAdvertisement, sampleObligationTodoWriteArgs, projectObligationsToV1TodoRows, v1TodoWriteToolSeed, V1_TODOWRITE_PARAMETERS } = await import("../../verification-system/tests/support/plugin-fixture.mjs");

const SESSION = 'ses_magic_todo_canary'
const CALL = 'call_magic_todo_1'

test('WHAT[obligation-ledger-015] obligations project to the original V1 decoder shape', async () => {
  const hooks = createMagicTodoContractHooks()
  const raw = sampleObligationTodoWriteArgs()

  // Clean-break provider args are intentionally not the builtin V1 decoder shape.
  const rawDecoded = decodeV1TodoWriteArgs(structuredClone(raw))
  assert.equal(rawDecoded.ok, false, 'C: raw obligations must require the membrane before V1 execute')

  const args = structuredClone(raw)
  const beforeOutput = { args }
  await hostTrigger(hooks['tool.execute.before'], { tool: 'todowrite', sessionID: SESSION, callID: CALL }, beforeOutput)

  assert.equal('obligations' in beforeOutput.args, true, 'C: provider input remains materializable')
  assert.equal(Object.prototype.propertyIsEnumerable.call(beforeOutput.args, 'todos'), false, 'C: compatibility view stays off JSON persistence')
  assert.equal(JSON.stringify(beforeOutput.args), JSON.stringify(raw), 'C: JSON persistence remains provider obligations only')
  for (const [index, todo] of beforeOutput.args.todos.entries()) {
    assert.equal(typeof todo.content, 'string')
    assert.equal(todo.status, index === 0 ? 'in_progress' : 'pending')
    assert.equal(todo.priority, 'medium')
  }

  const decoded = decodeV1TodoWriteArgs(beforeOutput.args)
  assert.equal(decoded.ok, true, 'C: original V1 decoder succeeds after projection')
  assert.equal(decoded.value.todos.length, raw.obligations.length)
  assert.deepEqual(
    decoded.value.todos.map((t) => t.content),
    raw.obligations.map((t) => `${t.name}: ${t.work}`),
  )
})
test('WHAT[obligation-ledger-015] projection helper mutates original args in place', () => {
  const args = sampleObligationTodoWriteArgs()
  const originalArgs = args
  const originalObligations = args.obligations
  const result = projectObligationsToV1TodoRows(args)
  assert.equal(result, originalArgs, 'C: projection mutates the args object in place')
  assert.equal('obligations' in args, true)
  assert.equal(Object.prototype.propertyIsEnumerable.call(args, 'todos'), false)
  assert.equal(JSON.stringify(args), JSON.stringify({ planComplete: true, workingOn: 'membrane', obligations: originalObligations }))
  assert.equal(args.todos.length, originalObligations.length)
  assert.deepEqual(args.todos.map((t) => t.status), ['in_progress', 'pending'])
  assert.equal(args.todos.every((t) => t.priority === 'medium'), true)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const host = await import("../../../dist/Mission/Obligation/Todo/OpenCode/MagicTodoHostSurface.js");


test('WHAT[obligation-ledger-015] workingOn projects to in_progress and every other obligation to pending', () => {
  assert.deepEqual(
    host.projectCompatibilityRows('proof', [
      { name: 'bridge', horizon: 'near', work: 'Review bridge' },
      { name: 'proof', horizon: 'near', work: 'Close proof' },
      { name: 'ship', horizon: 'far', work: 'Ship result' },
    ]),
    [
      { content: 'bridge: Review bridge', status: 'pending', priority: 'medium' },
      { content: 'proof: Close proof', status: 'in_progress', priority: 'medium' },
      { content: 'ship: Ship result', status: 'pending', priority: 'medium' },
    ],
  )
})
test('WHAT[obligation-ledger-015] projects obligations into a non-enumerable V1 compatibility view', () => {
  const args = { planComplete: false, workingOn: 'provider-only', obligations: [{ name: 'provider-only', horizon: 'near', work: 'must remain durable provider input' }] }
  const output = { args }
  host.replaceCompatibilityArgs(output, [
    { content: 'bridge: Review bridge', status: 'in_progress', priority: 'medium' },
  ])

  assert.equal(output.args, args, 'before must preserve the Host args object identity')
  assert.deepEqual(output.args, {
    planComplete: false,
    workingOn: 'provider-only',
    obligations: [{ name: 'provider-only', horizon: 'near', work: 'must remain durable provider input' }],
  })
  assert.equal(Object.prototype.propertyIsEnumerable.call(output.args, 'todos'), false)
  assert.deepEqual(output.args.todos, [
    { content: 'bridge: Review bridge', status: 'in_progress', priority: 'medium' },
  ])
})
}
