import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { execFileSync } = await import("node:child_process");
const { mkdtempSync, mkdirSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const eventStore = await import("../../../dist/Persistence/EventStore/Surface.js");
const casebook = await import("../../../dist/Repository/Knowledge/Casebook/Surface.js");
const bookkeeper = await import("../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js");
const lifecycle = await import("../../../dist/Repository/Knowledge/Casebook/LifecycleSurface.js");
const { CANONICAL_A, installBookkeeperRuntime, scriptedBookkeeperPort } = await import("../../knowledge-reuse/tests/support/bookkeeper-session-support.mjs");

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-delegate-settle-'))
  execFileSync('git', ['init', '--quiet', dir])
  mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
  return {
    dir,
    cleanup: () => rmSync(dir, { recursive: true, force: true }),
  }
}

test('WHAT[MANAGED-SESSION-021] CASE_SETTLE_finalized_releases_identity_exactly_once', async () => {
  const { dir, cleanup } = sandbox()
  try {
    lifecycle.enable(dir)
    const { port, createCalls } = scriptedBookkeeperPort()
    const key = 'insp-settle-finalized'
    await installBookkeeperRuntime(port, [key])
    lifecycle.notePrompt(key, 'What owns PromptAuthority?')
    lifecycle.noteAnswer(key, 'Host owns PromptAuthority.')

    const first = await lifecycle.tryFinalize(dir, key)
    assert.equal(first.ok, true)
    assert.equal(createCalls.length, 1)

    const handle = eventStore.create(join(dir, '.git'), 'insp-settle-read')
    try {
      const fetched = await casebook.fetchCase(handle, 10, key)
      assert.equal(fetched.ok, true)
      assert.notEqual(fetched.value, null)
    } finally {
      eventStore.dispose(handle)
    }

    // Duplicate finalize for the same session is the PhaseConflict path: the
    // completion is neither re-executed nor forgotten — the original Case is
    // retained and no second Bookkeeper child is created.
    lifecycle.notePrompt(key, 'second finalize must not publish')
    lifecycle.noteAnswer(key, 'should be refused')
    const second = await lifecycle.tryFinalize(dir, key)
    assert.equal(second.ok, false)
    assert.match(String(second.error), /already finalized/)
    assert.equal(createCalls.length, 1)

    const reread = eventStore.create(join(dir, '.git'), 'insp-settle-reread')
    try {
      const still = await casebook.fetchCase(reread, 10, key)
      assert.equal(still.ok, true)
      assert.equal(still.value.sessionId, key)
      assert.equal(still.value.a, CANONICAL_A)
    } finally {
      eventStore.dispose(reread)
    }
  } finally {
    bookkeeper.resetRuntime()
    lifecycle.disable()
    cleanup()
  }
})
test('WHAT[MANAGED-SESSION-021] CASE_SETTLE_nothing_to_finalize_is_not_a_failure', async () => {
  const { dir, cleanup } = sandbox()
  try {
    lifecycle.enable(dir)
    const { port, createCalls } = scriptedBookkeeperPort()
    const key = 'insp-settle-empty'
    await installBookkeeperRuntime(port, [key])

    // No draft at all: NothingToFinalize releases the identity without error
    // and without running Bookkeeper.
    const settled = await lifecycle.tryFinalize(dir, key)
    assert.equal(settled.ok, true)
    assert.equal(createCalls.length, 0)
  } finally {
    bookkeeper.resetRuntime()
    lifecycle.disable()
    cleanup()
  }
})
test('WHAT[MANAGED-SESSION-021] CASE_SETTLE_identity_retention_is_owner_driven_not_finally', async () => {
  const { readFileSync } = await import('node:fs')
  const { fileURLToPath } = await import('node:url')
  const root = fileURLToPath(new URL('../../..', import.meta.url))
  const { join: joinPath } = await import('node:path')
  const deletion = readFileSync(joinPath(root, 'src/Wanxiangshu/OpenCode/Host/HostSessionDeletion.fs'), 'utf8')

  // The outer evidence decision must run AFTER the exact finalize evidence is
  // captured: no unconditional finally may drop the identity a failed finalize
  // still needs for resume.
  assert.doesNotMatch(deletion, /finally\s*\n\s*scope\.DropSessionIdentity/)
  assert.match(deletion, /CaseFinalizeCommitment\.Finalized/)
  assert.match(deletion, /CaseFinalizeCommitment\.NothingToFinalize/)
  assert.match(deletion, /CaseFinalizeCommitment\.NotCommitted/)
  assert.match(deletion, /CaseFinalizeCommitment\.Unknown/)
  assert.match(deletion, /CaseFinalizeCommitment\.PhaseConflict/)
})
}

{
const { default: test } = await import("node:test");
const { assertFatalBoundary } = await import("../../structured-workflow/tests/support/m6-boundary-proof.mjs");


test('WHAT[MANAGED-SESSION-021] lifecycle fatal follows exact drain and one injected fuse', () => assertFatalBoundary('managed-session-lifecycle'))
}
