import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { applyToolDefinitionHook, createMagicTodoContractHooks, decodeV1TodoWriteArgs, hostTrigger, runHostV1ToolExecutePath, sampleObligationTodoWriteAdvertisement, sampleObligationTodoWriteArgs, projectObligationsToV1TodoRows, v1TodoWriteToolSeed, V1_TODOWRITE_PARAMETERS } = await import("../../verification-system/tests/support/plugin-fixture.mjs");

const SESSION = 'ses_magic_todo_canary'
const CALL = 'call_magic_todo_1'

test('WHAT[OBLIGATION-LEDGER-024] definition replaces description, parameters, and jsonSchema while the original decoder stays the execute-path decoder', async () => {
  const hooks = createMagicTodoContractHooks()
  const seed = v1TodoWriteToolSeed()
  const advertised = sampleObligationTodoWriteAdvertisement()

  const defined = await applyToolDefinitionHook(hooks['tool.definition'], seed)

  assert.equal(defined.description, advertised.description, 'B: description must be replaced')
  // Structural replacement (deep equal); Host may rebuild schema objects without
  // preserving advertisement object identity.
  assert.deepEqual(defined.parameters, advertised.parameters, 'B: parameters must be replaced')
  assert.deepEqual(defined.jsonSchema, advertised.jsonSchema, 'B: jsonSchema must be replaced')
  assert.notEqual(defined.parameters, seed.parameters, 'B: parameters identity must change')
  assert.notEqual(defined.jsonSchema, seed.jsonSchema, 'B: jsonSchema identity must change')

  // Host keeps the init-time execute wrapper bound to ORIGINAL parameters.
  assert.equal(defined.execute, seed.execute, 'B: execute wrapper is not replaced by definition')
  assert.equal(
    defined.originalParameters,
    V1_TODOWRITE_PARAMETERS,
    'B: original V1 parameters decoder identity is preserved for execute',
  )

  // Provider-facing advertisement is the clean-break account; legacy sink
  // fields do not cross the horizon. The original executor decoder stays V1.
  assert.equal(defined.parameters.properties.planComplete.type, 'boolean')
  const providerItem = defined.parameters.properties.obligations.items
  assert.deepEqual(providerItem.required, ['name', 'horizon', 'work'])
  assert.deepEqual(providerItem.properties.horizon.enum, ['near', 'mid', 'far'])
  assert.equal(providerItem.properties.id, undefined)
  assert.equal(providerItem.properties.kind, undefined)
  assert.equal(providerItem.properties.status, undefined)
  assert.equal(providerItem.properties.priority, undefined)
  assert.deepEqual(defined.jsonSchema.required, ['planComplete', 'workingOn', 'obligations'])

  const v1Row = {
    todos: [{ content: 'only-v1', status: 'pending', priority: 'low' }],
  }
  const decoded = decodeV1TodoWriteArgs(v1Row)
  assert.equal(decoded.ok, true, 'B: original V1 decoder still accepts V1 rows after definition update')
  assert.deepEqual(decoded.value.todos[0], v1Row.todos[0])
})
test('WHAT[OBLIGATION-LEDGER-024] jsonSchema ternary: both parameters and jsonSchema are replaced together', async () => {
  // registry.ts ternary:
  //   output.parameters === tool.parameters || output.jsonSchema !== tool.jsonSchema
  //     ? output.jsonSchema : undefined
  // Replacing only parameters (same jsonSchema ref) would drop jsonSchema.
  // Membrane must replace both — freeze that both-replaced keeps jsonSchema.
  const seed = v1TodoWriteToolSeed()
  const onlyParameters = async (_input, output) => {
    output.parameters = { type: 'object', properties: { todos: { type: 'array' } } }
    // deliberately leave jsonSchema identity unchanged
  }
  const dropped = await applyToolDefinitionHook(onlyParameters, seed)
  assert.equal(
    dropped.jsonSchema,
    undefined,
    'B: replacing only parameters drops jsonSchema (Host ternary) — membrane must not do this',
  )

  const both = createMagicTodoContractHooks()['tool.definition']
  const kept = await applyToolDefinitionHook(both, seed)
  assert.equal(typeof kept.jsonSchema, 'object')
  assert.notEqual(kept.jsonSchema, undefined, 'B: replacing parameters+jsonSchema keeps advertised jsonSchema')
  assert.equal(kept.description.length > 0, true)
  assert.notEqual(kept.parameters, seed.parameters)
  assert.notEqual(kept.jsonSchema, seed.jsonSchema)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const host = await import("../../../dist/Mission/Obligation/Todo/OpenCode/MagicTodoHostSurface.js");


test('WHAT[OBLIGATION-LEDGER-024] advertises planComplete in description, parameters, and jsonSchema', () => {
  const output = { description: '', parameters: {}, jsonSchema: {} }
  host.applyDefinition(output)

  assert.match(output.description, /owed-work|owed work|current.*account/i)
  assert.deepEqual(output.parameters.required, ['planComplete', 'workingOn', 'obligations'])
  assert.deepEqual(output.jsonSchema.required, ['planComplete', 'workingOn', 'obligations'])
  assert.equal(output.parameters.properties.planComplete.type, 'boolean')
  assert.equal(output.parameters.properties.workingOn.type, 'string')
  assert.match(output.parameters.properties.workingOn.description, /obligation|name/i)
  assert.match(output.parameters.properties.workingOn.description, /empty|string|空字符串/i)
  assert.equal(output.jsonSchema.properties.workingOn.description, output.parameters.properties.workingOn.description)
  assert.match(output.parameters.properties.planComplete.description, /false/i)
  assert.match(output.parameters.properties.planComplete.description, /true/i)
  assert.match(output.parameters.properties.planComplete.description, /irreversible|cannot.*return|forever|不可逆|永久/i)
  assert.match(output.parameters.properties.planComplete.description, /coverage|覆盖/i)
  assert.match(output.parameters.properties.planComplete.description, /uniform|均匀/i)
  assert.equal(output.jsonSchema.properties.planComplete.description, output.parameters.properties.planComplete.description)
  assert.deepEqual(output.parameters.properties.obligations.items.required, ['name', 'horizon', 'work'])
  assert.deepEqual(output.parameters.properties.obligations.items.properties.horizon.enum, ['near', 'mid', 'far'])
  assert.match(output.parameters.properties.obligations.items.properties.horizon.description, /frontier|前沿/i)
  assert.match(output.parameters.properties.obligations.items.properties.horizon.description, /not.*status|不是.*status|不是.*状态/i)
  assert.match(output.parameters.properties.obligations.items.properties.horizon.description, /near/i)
  assert.match(output.parameters.properties.obligations.items.properties.horizon.description, /mid/i)
  assert.match(output.parameters.properties.obligations.items.properties.horizon.description, /far/i)
  assert.match(output.parameters.properties.obligations.items.properties.name.description, /planComplete/i)
  assert.match(output.parameters.properties.obligations.items.properties.name.description, /planning/i)
  assert.match(output.parameters.properties.obligations.items.properties.name.description, /placeholder/i)
  assert.match(output.parameters.properties.obligations.items.properties.work.description, /planComplete/i)
  assert.match(output.parameters.properties.obligations.items.properties.work.description, /completion counterfactual/i)
  assert.match(output.parameters.properties.obligations.items.properties.work.description, /close|closure|闭环/i)
  assert.match(output.parameters.properties.obligations.items.properties.work.description, /TBD/i)
  assert.equal(output.jsonSchema.properties.obligations.items.properties.name.description, output.parameters.properties.obligations.items.properties.name.description)
  assert.equal(output.jsonSchema.properties.obligations.items.properties.work.description, output.parameters.properties.obligations.items.properties.work.description)
  assert.equal(output.jsonSchema.properties.obligations.items.properties.horizon.description, output.parameters.properties.obligations.items.properties.horizon.description)
  assert.equal(output.parameters.properties.obligations.items.properties.status, undefined)
  assert.equal(output.parameters.properties.obligations.items.properties.id, undefined)
})
}
