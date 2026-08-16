import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

import { listItems } from '../../verification-system/tests/support/domain.mjs'

const { ToolHostCodec_factory } = await import('../../../dist/OpenCode/Codec/ToolHostCodec.js')
const { managerSpec, orchestratorSpec } = await import('../../../dist/Execution/Delegation/Fork/OpenCode/Tool.js')
const { spec: inspectSpec } = await import('../../../dist/OpenCode/Tools/InspectorTool.js')
const { establishSpec, repairSpec } = await import('../../../dist/OpenCode/Tools/CoderTool.js')
const { ToolRuntimeScope } = await import('../../../dist/OpenCode/Tools/ToolRuntimeScope.js')

const chain = (kind, extra = {}) => ({
  kind,
  ...extra,
  int: () => chain(`${kind}-int`, extra),
  nonnegative: () => chain(`${kind}-nonnegative`, extra),
  describe: (description) => chain(`${kind}-described`, { ...extra, description }),
  optional: () => chain(`${kind}-optional`, extra),
})

const factory = ToolHostCodec_factory({
  tool: {
    schema: {
      string: () => chain('string'),
      number: () => chain('number'),
      enum: (values) => chain('enum', { values }),
      array: (inner) => chain('array', { inner }),
      union: (parts) => chain('union', { parts }),
    },
  },
})

const scope = new ToolRuntimeScope(
  {},
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

const names = (spec) => listItems(spec.Arguments).map(([name]) => name)

test('WHAT[DELEG-022] DELEG_021_022_public_delegation_surfaces_are_exact', () => {
  assert.deepEqual(names(managerSpec(factory, scope)), [
    'calling',
    'name',
    'charge',
    'keywords',
    'attach',
    'expected_tool_calls',
  ])
  assert.deepEqual(names(orchestratorSpec(factory, scope)), [
    'calling',
    'name',
    'charge',
    'expected_tool_calls',
  ])
  assert.deepEqual(names(inspectSpec(factory, scope, undefined)), [
    'charge',
    'keywords',
    'expected_tool_calls',
  ])
  assert.deepEqual(names(establishSpec(factory, scope, undefined)), [
    'charge',
    'keywords',
    'expected_tool_calls',
  ])
  assert.deepEqual(names(repairSpec(factory, scope, undefined)), [
    'charge',
    'keywords',
    'expected_tool_calls',
  ])
})

test('WHAT[DELEG-021] DELEG_021_attach_belongs_only_to_fork_not_commission_or_sync_delegate', () => {
  for (const spec of [orchestratorSpec(factory, scope), inspectSpec(factory, scope, undefined), establishSpec(factory, scope, undefined), repairSpec(factory, scope, undefined)]) {
    assert.ok(!names(spec).includes('attach'))
  }
})

test('WHAT[DELEG-022] DELEG_022_never_reuses_host_maxSteps_as_the_estimate', () => {
  const files = [
    '../../../src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/Tool.fs',
    '../../../src/Wanxiangshu/OpenCode/Tools/InspectorTool.fs',
    '../../../src/Wanxiangshu/OpenCode/Tools/CoderTool.fs',
    '../../../src/Wanxiangshu/OpenCode/Plugin/PluginHooks.fs',
  ]

  for (const relative of files) {
    const source = readFileSync(new URL(relative, import.meta.url), 'utf8')
    assert.ok(!source.includes('maxSteps'), `${relative} must not turn the advisory estimate into Host enforcement`)
  }
})
