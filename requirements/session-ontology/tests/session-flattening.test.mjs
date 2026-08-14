// Split from tests/unit/host/session-flattening.test.mjs (cutover Wave 2a);
// owner: session-ontology. SESSION-ONTOLOGY-006：物理扁平 —— 每个 managed child
// session 物理挂在 family root 下（son's son is a son，树恰好两层），ownership
// 由 journal 链接证明而非 Host parentID；restored journal parents 仍解析 family
// root。abort 级联断言归 managed-session-lifecycle。
// 原文件直接 import 编译器运行时（ofArray/ok）已改写为 support 等价调用
// （toList/okResult）。

import assert from 'node:assert/strict'
import test from 'node:test'

import { sessionId, toList, okResult } from '../../verification-system/tests/support/domain.mjs'
const sessionsModule = await import('../../../dist/OpenCode/Host/Sessions.js')
const createPort = Object.entries(sessionsModule).find(([k]) => k.startsWith('InjectedSessionPort_$ctor'))?.[1]

// HOST-015: every managed child session is physically parented to the family
// root — a son's son is a son. The Host tree is exactly two levels deep so the
// UI renders every session; ownership is proven by journal links, never by
// Host parentID.

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

test('HOST_015_child_of_child_is_physically_parented_to_family_root', async () => {
  const createdParents = []
  const port = createPort(openCodePort(createdParents, []), eventPort)
  const root = sessionId('root')

  const first = await port.CreateChildSession(root, {})
  assert.equal(first.tag, 0)
  const child1 = first.fields[0]
  assert.deepEqual(createdParents, ['root'], 'direct child hangs under its parent (the root)')

  const second = await port.CreateChildSession(child1, {})
  assert.equal(second.tag, 0)
  const child2 = second.fields[0]
  assert.deepEqual(
    createdParents,
    ['root', 'root'],
    "a son's son is physically re-parented to the family root, never a grandchild",
  )

  const third = await port.CreateChildSession(child2, {})
  assert.equal(third.tag, 0)
  assert.deepEqual(createdParents, ['root', 'root', 'root'], 'depth never exceeds two levels')

  assert.equal(port.FamilyRootOf(child1).fields[0], 'root')
  assert.equal(port.FamilyRootOf(child2).fields[0], 'root')
  assert.equal(port.FamilyRootOf(third.fields[0]).fields[0], 'root')
  assert.equal(port.FamilyRootOf(root).fields[0], 'root')
})

test('HOST_015_family_root_resolves_through_restored_journal_parents', async () => {
  const createdParents = []
  // After a restart the in-memory registry is empty; the logical parent chain
  // restored from durable HandleLinked facts still finds the family root.
  const familyParent = (id) => {
    if (id.fields[0] === 'devops') return sessionId('manager')
    return undefined
  }
  const port = createPort(openCodePort(createdParents, []), eventPort, familyParent)

  const created = await port.CreateChildSession(sessionId('devops'), {})
  assert.equal(created.tag, 0)
  assert.deepEqual(createdParents, ['manager'], "devops's child is physically parented to the root manager")
  assert.equal(port.FamilyRootOf(sessionId('devops')).fields[0], 'manager')
})
