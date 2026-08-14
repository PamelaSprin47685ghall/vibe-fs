// Split from tests/unit/tools/blog-tool.test.mjs (cutover Wave 2a); owner: capability-enforcement
// chronicle tool spec/registry surface: public tool identity and argument schema.
//
// behavior-diagnosis WHAT.md boundary: "chronicle 工具名与权限归 capability-enforcement；
// 这里只锁参数语义" — the tool NAME and argument surface belong to the tool registry.
// The catalog-field count (120) is the tip catalog cross-reference (behavior-diagnosis).

import assert from 'node:assert/strict'
import test from 'node:test'
import { listItems } from '../../verification-system/tests/support/domain.mjs'

const {
  HostToolContext,
  ToolHostCodec_factory,
} = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
const { spec } = await import('../../../dist/Infrastructure/OpenCode/Tools/ChronicleTool.js')
const { ToolRuntimeScope } = await import('../../../dist/Infrastructure/OpenCode/Tools/ToolRuntimeScope.js')
const { RuntimeResourcesModule_load: loadResources, RuntimeResourcesModule_install: installResources } = await import(
  '../../../dist/Infrastructure/Resources/RuntimeResources.js'
)

installResources(loadResources())

const fakeSchema = {
  string: () => ({ optional: () => ({ kind: 'string-optional' }) }),
  enum: (values) => ({ kind: 'enum', values }),
}
const factory = ToolHostCodec_factory({ tool: { schema: fakeSchema } })

const scope = () => {
  const sessions = {
    AbortSession: async (id) => ({ tag: 0, fields: [] }),
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
  }
}

test('CHRONICLE_spec_exposes_identity_and_argument_surface', () => {
  const tool = spec(factory, scope().scope, undefined)
  assert.equal(tool.Name, 'chronicle')
  const args = listItems(tool.Arguments)
  assert.deepEqual(args.map(([n]) => n), ['entry', 'tip'])
  const v = args[1][1]
  assert.equal(v.fields[0].values.length, 120)
})
