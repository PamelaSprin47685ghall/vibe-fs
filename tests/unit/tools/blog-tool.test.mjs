// chronicle: ENFORCER-020/022/023/040/041/061 contract.
//
// Live-cycle gate and canonical-text gate are pure; execute paths are driven
// with a fake parked transform host and fake session port.

import assert from 'node:assert/strict'
import test from 'node:test'
import { listItems, payloadOf, sessionId, toList } from '../support/domain.mjs'

const {
  HostToolArguments_$ctor_4E60E31B: makeArgs,
  HostToolContext,
  ToolHostCodec_factory,
} = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
const { spec, tryCanonicalText, hasLiveCycle, tipFieldNames } = await import(
  '../../../dist/Infrastructure/OpenCode/Tools/ChronicleTool.js'
)
const { ToolRuntimeScope } = await import('../../../dist/Infrastructure/OpenCode/Tools/ToolRuntimeScope.js')
const { RuntimeResourcesModule_load: loadResources, RuntimeResourcesModule_install: installResources } = await import(
  '../../../dist/Infrastructure/Resources/RuntimeResources.js'
)
const { EnforcerCodec } = await import('../../../dist/Domain/EnforcerCodec.js')

installResources(loadResources())

const fakeSchema = {
  string: () => ({ optional: () => ({ kind: 'string-optional' }) }),
  enum: (values) => ({ kind: 'enum', values }),
}
const factory = ToolHostCodec_factory({ tool: { schema: fakeSchema } })

const context = ({ sessionId: sid = 'ses-blog', providerRunId, toolCallId } = {}) =>
  new HostToolContext(sid, undefined, toolCallId, providerRunId, undefined, () => () => {})

const scope = (calls = []) => {
  const sessions = {
    AbortSession: async (id) => {
      calls.push(['AbortSession', id])
      return { tag: 0, fields: [] }
    },
  }
  return {
    scope: new ToolRuntimeScope(
      sessions,
      undefined,
      undefined,
      undefined,
      new Map(),
      () => undefined,
      new Set(),
      new Map(),
      undefined,
      undefined,
      undefined,
      undefined,
      undefined,
    ),
    calls,
  }
}

const parkedHost = (hasFlight = false) => ({ HasFlight: (sid) => hasFlight })

const run = async (tool, args, ctx) => tool.Execute(makeArgs(args), ctx)

// ── pure gates ──────────────────────────────────────────────────────────────

test('CHRONICLE_canonical_text_trims_and_rejects_empty', () => {
  const ok = tryCanonicalText('  work entry  ')
  assert.equal(ok.tag, 0)
  assert.equal(ok.fields[0], 'work entry')

  const empty = tryCanonicalText('   ')
  assert.equal(empty.tag, 1)
  assert.equal(empty.fields[0], 'CHRONICLE_EMPTY_ENFORCER_061')

  const nil = tryCanonicalText(undefined)
  assert.equal(nil.tag, 1)
  assert.equal(nil.fields[0], 'CHRONICLE_EMPTY_ENFORCER_061')
})

test('CHRONICLE_live_cycle_requires_a_host_with_a_flight', () => {
  assert.equal(hasLiveCycle(undefined, 'ses-blog'), false)
  assert.equal(hasLiveCycle(parkedHost(false), 'ses-blog'), false)
  assert.equal(hasLiveCycle(parkedHost(true), 'ses-blog'), true)
  assert.equal(hasLiveCycle(parkedHost(true), 'ses-other'), true, 'flight is per host query, session passed through')
})

test('CHRONICLE_tip_enum_equals_catalog_field_names', () => {
  const fields = listItems(tipFieldNames())
  assert.equal(fields.length, 120)
  assert.ok(fields.includes('primitive-obsession'))
})

test('CHRONICLE_spec_exposes_identity_and_argument_surface', () => {
  const tool = spec(factory, scope().scope, undefined)
  assert.equal(tool.Name, 'chronicle')
  const args = listItems(tool.Arguments)
  assert.deepEqual(args.map(([n]) => n), ['entry', 'tip'])
  const v = args[1][1]
  assert.equal(v.fields[0].values.length, 120)
})

// ── execute: gate first, then canonical text, then tip validation ───────────

test('CHRONICLE_no_live_cycle_rejects_and_aborts_the_session', async () => {
  const { scope: s, calls } = scope()
  const tool = spec(factory, s, undefined)
  await assert.rejects(() => run(tool, { entry: 'x', tip: 'primitive-obsession' }, context()), {
    message: 'CHRONICLE_NO_LIVE_CYCLE',
  })
  assert.deepEqual(calls, [['AbortSession', sessionId('ses-blog')]], 'the doomed blogger session must be aborted')
})

test('CHRONICLE_no_live_cycle_does_not_abort_a_blank_session', async () => {
  const { scope: s, calls } = scope()
  const tool = spec(factory, s, undefined)
  await assert.rejects(() => run(tool, { entry: 'x', tip: 'primitive-obsession' }, context({ sessionId: '' })), (error) => {
    assert.match(error?.message ?? String(error), /CHRONICLE_NO_LIVE_CYCLE/)
    return true
  })
  assert.deepEqual(calls, [], 'blank session must not be aborted')
})

test('CHRONICLE_empty_canonical_text_returns_public_consequence', async () => {
  const tool = spec(factory, scope().scope, parkedHost(true))
  const text = await run(tool, { entry: '   ', tip: 'primitive-obsession' }, context())
  assert.match(text, /no occurrence here to remember/)
})

test('CHRONICLE_missing_tip_returns_rulebook_consequence', async () => {
  const tool = spec(factory, scope().scope, parkedHost(true))
  const text = await run(tool, { entry: 'entry' }, context())
  assert.match(text, /Rulebook|missing required argument: tip/i)
})

test('CHRONICLE_unknown_tip_is_rejected_at_runtime', async () => {
  const tool = spec(factory, scope().scope, parkedHost(true))
  const text = await run(tool, { entry: 'entry', tip: 'not-a-field' }, context())
  assert.match(text, /not in the Rulebook/)
})

test('CHRONICLE_valid_entry_with_identity_returns_fixed_ok', async () => {
  const tool = spec(factory, scope().scope, parkedHost(true))
  const text = await run(
    tool,
    { entry: '  entry  ', tip: 'primitive-obsession' },
    context({ providerRunId: 'run-1', toolCallId: 'call-1' }),
  )
  assert.match(text, /The Chronicle remembers this\./)
})

test('CHRONICLE_valid_entry_without_tool_identity_still_returns_ok', async () => {
  const tool = spec(factory, scope().scope, parkedHost(true))
  assert.match(
    await run(tool, { entry: 'entry', tip: 'primitive-obsession' }, context()),
    /The Chronicle remembers this\./,
  )
})
