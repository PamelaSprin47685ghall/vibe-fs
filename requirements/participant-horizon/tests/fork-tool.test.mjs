// Split from tests/unit/tools/fork-tool.test.mjs (cutover Wave 2a); owner: participant-horizon
// Generic calling-denial surface: unavailable/unknown callings refuse with public
// vocabulary only — no machine binding names, no office internals.
//
// PH-009/PH-014 (generic-unavailable → 本包；可见集合执行面 → office-capability /
// capability-enforcement): the fork tool's visible set and consequence copy are the
// horizon-facing surface; `Reviewer`/`fast-`/`deep-` names must never appear.

import assert from 'node:assert/strict'
import test from 'node:test'

const {
  HostToolArguments_$ctor_4E60E31B: makeArgs,
  HostToolContext,
  ToolHostCodec_factory,
} = await import('../../../dist/OpenCode/Codec/ToolHostCodec.js')
const { managerSpec, orchestratorSpec } = await import('../../../dist/Execution/Delegation/Fork/OpenCode/Tool.js')
const { ToolRuntimeScope } = await import('../../../dist/OpenCode/Tools/ToolRuntimeScope.js')

const chain = (kind, extra = {}) => ({
  kind,
  ...extra,
  int: () => chain(`${kind}-int`, extra),
  nonnegative: () => chain(`${kind}-nonnegative`, extra),
  describe: (description) => chain(`${kind}-described`, { ...extra, description }),
  optional: () => chain(`${kind}-optional`, extra),
})
const fakeSchema = {
  string: () => chain('string'),
  number: () => chain('number'),
  enum: (values) => chain('enum', { values }),
  union: (parts) => chain('union', { parts }),
}
const factory = ToolHostCodec_factory({ tool: { schema: fakeSchema } })

const context = (sessionId = 'ses_fork') =>
  new HostToolContext(sessionId, undefined, undefined, undefined, undefined, () => () => {})

/** A scope with no runtime seeded; an orchestrator host can still be pre-seeded. */
const bareScope = ({ orchestratorHost } = {}) => {
  const scope = new ToolRuntimeScope(
    undefined,
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
  )
  if (orchestratorHost) scope.orchestratorHosts.set('ses_fork', orchestratorHost)
  return scope
}

const runManager = (spec, name, charge, extra = {}) =>
  spec.Execute(makeArgs({ name, charge, ...extra }), context())

test('FORK_unavailable_calling_is_denied_generically', async () => {
  const spec = managerSpec(factory, bareScope())
  const result = await runManager(spec, 'Rhea', 'review this', { calling: 'examiner' })
  assert.match(result, /Unknown or unavailable calling/)
  assert.doesNotMatch(result, /Reviewer|fast-|deep-|\berror\s*=/i)
})

test('FORK_unknown_calling_is_generic_and_does_not_dump_machine_bindings', async () => {
  const spec = managerSpec(factory, bareScope())
  const result = await runManager(spec, 'Ada', 'do work', { calling: 'wizard' })
  assert.match(result, /Unknown or unavailable calling/)
  assert.doesNotMatch(result, /fast-|deep-|\berror\s*=/i)
})

test('FORK_orchestrator_rejects_unknown_calling_without_binding_names', async () => {
  const spec = orchestratorSpec(factory, bareScope({ orchestratorHost: {} }))
  const result = await spec.Execute(makeArgs({ calling: 'coder', name: 'Road', charge: 'x' }), context())
  assert.match(result, /Unknown or unavailable calling/)
  assert.doesNotMatch(result, /fast-manager|deep-manager|\berror\s*=/i)
})
