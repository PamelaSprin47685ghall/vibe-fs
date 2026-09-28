import assert from 'node:assert/strict'
import test from 'node:test'
import * as AttachmentSurface from '../../../dist/Execution/Session/Attachment/AttachmentSurface.js'



test('WHAT[managed-session-lifecycle-005] EXEC_026_get_or_create_reuses_the_existing_binding_and_keeps_the_bound_agent', async () => {
  const observed = await AttachmentSurface.scenario('owner', 'Engineer', 'engineer', 'different-requested-agent', true)
  assert.equal(observed.created, 1)
  assert.equal(observed.secondChild, observed.firstChild)
  assert.equal(observed.secondAgent, 'engineer')
})

test('WHAT[managed-session-lifecycle-005] two requests from one owner reuse the binding', async () => {
  const observed = await AttachmentSurface.scenario('ses-owner-a', 'Engineer', 'engineer', 'engineer', true)
  assert.equal(observed.owner, 'ses-owner-a')
  assert.equal(observed.firstChild, observed.secondChild)
})

test('WHAT[managed-session-lifecycle-005] explicit removal permits a new binding on the same owner', async () => {
  const observed = await AttachmentSurface.scenario('owner', 'Engineer', 'engineer', 'engineer', false)
  assert.equal(observed.created, 2)
  assert.notEqual(observed.firstChild, observed.secondChild)
})

function attachmentPorts() {
  const creations = []
  const bindings = []
  return {
    creations,
    bindings,
    create: async (owner, scope, role, agent) => {
      const child = `child-${creations.length + 1}`
      creations.push({ owner, scope, role, agent, child })
      return child
    },
    bind: (owner, child, agent) => bindings.push({ owner, child, agent }),
  }
}

test('WHAT[managed-session-lifecycle-005] one actual owner isolates live scopes and retains each binding independently', async () => {
  const runtime = AttachmentSurface.createOwner()
  const ports = attachmentPorts()
  try {
    const first = await AttachmentSurface.getOrCreate(runtime, 'scope-a', 'Engineer', 'engineer-a', ports.create, ports.bind)
    const second = await AttachmentSurface.getOrCreate(runtime, 'scope-b', 'Engineer', 'engineer-b', ports.create, ports.bind)
    assert.notEqual(first.child, second.child)
    assert.deepEqual(ports.creations.map(({ owner, scope, role, agent }) => [owner, scope, role, agent]), [
      ['scope-a', 'scope-a', 'engineer', 'engineer-a'],
      ['scope-b', 'scope-b', 'engineer', 'engineer-b'],
    ])
    assert.equal(AttachmentSurface.tryFind(runtime, 'scope-a', 'Engineer'), first.child)
    assert.equal(AttachmentSurface.tryFind(runtime, 'scope-b', 'Engineer'), second.child)
    assert.deepEqual(await AttachmentSurface.getOrCreate(runtime, 'scope-a', 'Engineer', 'changed', ports.create, ports.bind), first)
    assert.equal(ports.creations.length, 2)
    assert.equal(ports.bindings.length, 2)
  } finally {
    AttachmentSurface.clear(runtime)
  }
})

for (const [independentOwner, independentRole] of [['scope-b', 'Engineer'], ['scope-a', 'Inspector']]) {
  test(`WHAT[managed-session-lifecycle-005] concurrent same-key requests share one creation while ${independentOwner}/${independentRole} can finish independently`, async () => {
    const runtime = AttachmentSurface.createOwner()
    const ports = attachmentPorts()
    const pending = []
    let releaseFirst
    let firstStarted
    const started = new Promise((resolve) => { firstStarted = resolve })
    const blocked = new Promise((resolve) => { releaseFirst = resolve })
    const create = async (...args) => {
      const child = await ports.create(...args)
      if (args[0] === 'scope-a' && args[2] === 'engineer') {
        firstStarted()
        await blocked
      }
      return child
    }
    try {
      const first = AttachmentSurface.getOrCreate(runtime, 'scope-a', 'Engineer', 'original-agent', create, ports.bind)
      pending.push(first)
      await started
      const follower = AttachmentSurface.getOrCreate(runtime, 'scope-a', 'Engineer', 'replacement-agent', create, ports.bind)
      pending.push(follower)
      const independent = await AttachmentSurface.getOrCreate(runtime, independentOwner, independentRole, 'other-agent', create, ports.bind)
      assert.equal(independent.agent, 'other-agent')
      assert.deepEqual(ports.creations.map(({ owner, scope, role, agent }) => [owner, scope, role, agent]), [
        ['scope-a', 'scope-a', 'engineer', 'original-agent'],
        [independentOwner, independentOwner, independentRole.toLowerCase(), 'other-agent'],
      ])
      assert.equal(AttachmentSurface.tryFind(runtime, 'scope-a', 'Engineer'), undefined)
      assert.equal(AttachmentSurface.tryFind(runtime, independentOwner, independentRole), independent.child)
      assert.equal(ports.creations.length, 2)
      assert.equal(ports.bindings.length, 1)
      assert.equal(ports.bindings[0].child, independent.child)
      releaseFirst()
      const [bound, followed] = await Promise.all([first, follower])
      assert.deepEqual(followed, bound)
      assert.equal(bound.agent, 'original-agent')
      assert.notEqual(bound.child, independent.child)
      assert.equal(ports.creations.length, 2)
      assert.equal(ports.bindings.length, 2)
    } finally {
      releaseFirst()
      await Promise.allSettled(pending)
      AttachmentSurface.clear(runtime)
    }
  })
}

// The attachment owner receives typed roles after admission. This proves its key
// isolation; participant-identity tests separately reject retired active roles.
for (const roles of [['Engineer', 'Coder'], ['Engineer', 'Inspector'], ['Coder', 'Inspector']]) {
  test(`WHAT[managed-session-lifecycle-005] typed ${roles.join('/')} bindings in one scope are independently reusable and removable`, async () => {
    const runtime = AttachmentSurface.createOwner()
    const ports = attachmentPorts()
    try {
      const first = await AttachmentSurface.getOrCreate(runtime, 'shared-scope', roles[0], 'first-agent', ports.create, ports.bind)
      const second = await AttachmentSurface.getOrCreate(runtime, 'shared-scope', roles[1], 'second-agent', ports.create, ports.bind)
      assert.notEqual(first.child, second.child)
      assert.equal(first.agent, 'first-agent')
      assert.equal(second.agent, 'second-agent')
      assert.deepEqual(ports.creations.map(({ owner, scope, role, agent }) => [owner, scope, role, agent]), [
        ['shared-scope', 'shared-scope', roles[0].toLowerCase(), 'first-agent'],
        ['shared-scope', 'shared-scope', roles[1].toLowerCase(), 'second-agent'],
      ])
      assert.equal(AttachmentSurface.tryFind(runtime, 'shared-scope', roles[0]), first.child)
      assert.equal(AttachmentSurface.tryFind(runtime, 'shared-scope', roles[1]), second.child)
      assert.deepEqual(await AttachmentSurface.getOrCreate(runtime, 'shared-scope', roles[0], 'changed', ports.create, ports.bind), first)
      assert.deepEqual(await AttachmentSurface.getOrCreate(runtime, 'shared-scope', roles[1], 'changed', ports.create, ports.bind), second)
      assert.equal(ports.creations.length, 2)
      assert.equal(ports.bindings.length, 2)
      assert.equal(AttachmentSurface.remove(runtime, 'shared-scope', roles[0]), true)
      assert.equal(AttachmentSurface.tryFind(runtime, 'shared-scope', roles[0]), undefined)
      assert.equal(AttachmentSurface.tryFind(runtime, 'shared-scope', roles[1]), second.child)
      const replacement = await AttachmentSurface.getOrCreate(runtime, 'shared-scope', roles[0], 'new-agent', ports.create, ports.bind)
      assert.notEqual(replacement.child, first.child)
      assert.notEqual(replacement.child, second.child)
      assert.equal(replacement.agent, 'new-agent')
      assert.equal(AttachmentSurface.tryFind(runtime, 'shared-scope', roles[0]), replacement.child)
      assert.equal(AttachmentSurface.tryFind(runtime, 'shared-scope', roles[1]), second.child)
      assert.equal(ports.creations.length, 3)
    } finally {
      AttachmentSurface.clear(runtime)
    }
  })
}
