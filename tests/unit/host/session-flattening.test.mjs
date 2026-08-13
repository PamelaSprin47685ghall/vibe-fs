import assert from 'node:assert/strict'
import test from 'node:test'

if (!process.env.WANXIANGSHU_PROVIDER_LANGUAGE) {
  process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'
}

import { InjectedSessionPort_$ctor_Z60D0357E as createPort } from '../../../dist/Infrastructure/OpenCode/Host/Sessions.js'
import { SessionIdModule_create as sessionId } from '../../../dist/Kernel/Identity.js'
import { ofArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'
import { FSharpResult$2_Ok as ok } from '../../../dist/fable_modules/fable-library-js.5.13.0/Result.js'

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
    return ok(sessionId(`child-${childSequence}`))
  },
  ListChildren: async () => ok(ofArray([])),
  AbortSession: async (id) => {
    aborts.push(id.fields[0])
    return ok(undefined)
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

test('HOST_015_abort_children_cascade_stays_keyed_on_family_root', async () => {
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
