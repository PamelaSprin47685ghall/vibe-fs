// Split from tests/unit/host/session-flattening.test.mjs (cutover Wave 2a);
// owner: managed-session-lifecycle. MANAGED-SESSION-003 反向覆盖：abort 级联
// 以 family root 为键 —— 逻辑中间层无物理 children，abort family root 级联到
// 每个扁平后代。物理扁平断言归 session-ontology（SESSION-ONTOLOGY-006）。
// 原文件直接 import 编译器运行时（ofArray/ok）已改写为 support 等价调用
// （toList/okResult）。

import assert from 'node:assert/strict'
import test from 'node:test'

import { sessionId, toList, okResult } from '../../verification-system/tests/support/domain.mjs'
const sessionsModule = await import('../../../dist/OpenCode/Host/Sessions.js')
const createPort = Object.entries(sessionsModule).find(([k]) => k.startsWith('InjectedSessionPort_$ctor'))?.[1]

// HOST-015: abort of the family root cascades to every flattened descendant;
// the logical middle layer owns no physical children.

const eventPort = { SubscribeTerminalListener: () => ({ Dispose: () => {} }) }
let childSequence = 0

const openCodePort = (createdParents, aborts) => ({
  CreateChildSession: async (parentId) => {
    createdParents.push(parentId.fields[0])
    childSequence += 1
    return okResult(sessionId(`child-${childSequence}`))
  },
  ListChildren: async () => okResult(toList([])),
  AbortSession: async (id) => {
    aborts.push(id.fields[0])
    return okResult(undefined)
  },
  SendPrompt: async () => {
    throw new Error('unused in these scenarios')
  },
})

test('WHAT[MANAGED-SESSION-003] HOST_015_abort_children_cascade_stays_keyed_on_family_root', async () => {
  const createdParents = []
  const aborts = []
  const port = createPort(openCodePort(createdParents, aborts), eventPort)
  const root = sessionId('root')

  const child1 = (await port.CreateChildSession(root, {})).fields[0]
  const child2 = (await port.CreateChildSession(child1, {})).fields[0]

  await port.AbortChildren(child1)
  assert.deepEqual(aborts, [], 'the logical middle layer owns no physical children')

  await port.AbortChildren(root)
  assert.deepEqual(
    [...aborts].sort(),
    [child1.fields[0], child2.fields[0]].sort(),
    'aborting the family root cascades to every flattened descendant',
  )
})
