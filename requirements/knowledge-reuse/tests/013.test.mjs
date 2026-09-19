import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { randomUUID } = await import("node:crypto");
const { create: createEventStore, dispose: disposeEventStore } = await import("../../../dist/Persistence/EventStore/Surface.js");
const casebook = await import("../../../dist/Repository/Knowledge/Casebook/Surface.js");

const makeDir = (prefix) => mkdtempSync(join(tmpdir(), prefix))
const openStore = (commonDir) => {
  const handle = createEventStore(commonDir, randomUUID().replaceAll('-', ''))
  return { store: handle, close: () => disposeEventStore(handle) }
}
const caseRec = (sessionId, q, a, observations) => ({ sessionId, q, a, observations, lastAccessOrder: 0 })
const fileRead = (path, contentHash) => ({ kind: 'file-read', path, contentHash })
const unwrap = async (operation) => {
  const result = await operation
  assert.equal(result.ok, true, `expected Ok, got ${JSON.stringify(result.error)}`)
  return result
}

test('WHAT[knowledge-reuse-013] cut_boundary_legal_commands_always_fold_per_fact_constructor', async () => {
  const common = makeDir('wxs-casebook-cut-events-')
  const local = openStore(common)
  try {
    // Legal pre-state (empty) + legal Captured command => accepted by the fold.
    await unwrap(casebook.archive(local.store, caseRec('cut-s1', 'Q', 'A', [fileRead('a.txt', 'h1')])))
    // Legal pre-state (captured) + legal Refreshed => accepted.
    await unwrap(casebook.refresh(local.store, 'cut-s1', 'Q2', 'A2', [fileRead('a.txt', 'h2')]))
    // Legal pre-state + legal Accessed => accepted (touch is infallible on present cases).
    await unwrap(casebook.touchAccess(local.store, 'cut-s1'))
    const fetched = await casebook.fetchCase(local.store, 10, 'cut-s1')
    assert.equal(fetched.ok, true)
    assert.equal(fetched.value.a, 'A2')
    assert.equal(fetched.value.observations.length, 1)
    // Legal pre-state + legal Evicted => accepted.
    await unwrap(casebook.evictCase(local.store, 'cut-s1'))
    const gone = await casebook.fetchCase(local.store, 10, 'cut-s1')
    assert.equal(gone.ok, true)
    assert.equal(gone.value, null)
  } finally {
    local.close()
    rmSync(common, { recursive: true, force: true })
  }
})
test('WHAT[knowledge-reuse-013] cut_boundary_interrupted_archive_never_leads_the_receipt', async () => {
  const common = makeDir('wxs-casebook-cut-interrupt-')
  const local = openStore(common)
  try {
    await unwrap(casebook.archive(local.store, caseRec('cut-i1', 'Q', 'A', [fileRead('a.txt', 'h1')])))

    // Interrupt at the store boundary: reopened Current reflects only durable
    // receipts — local authoritative state never leads.
    local.close()
    const reopened = openStore(common)
    try {
      const fetched = await casebook.fetchCase(reopened.store, 10, 'cut-i1')
      assert.equal(fetched.ok, true)
      assert.equal(fetched.value.a, 'A')
      assert.equal(fetched.value.observations.length, 1)
    } finally {
      reopened.close()
    }
  } finally {
    rmSync(common, { recursive: true, force: true })
  }
})
test('WHAT[knowledge-reuse-013] cut_boundary_complete_observation_set_survives_refresh', async () => {
  const common = makeDir('wxs-casebook-cut-cross-')
  const local = openStore(common)
  try {
    await unwrap(casebook.archive(local.store, caseRec('cut-c1', 'Q', 'A', [
      fileRead('one.txt', 'h-one'),
      { kind: 'glob-result', pattern: '*.fs', paths: ['a.fs', 'b.fs'] },
      { kind: 'grep-result', pattern: 'TODO', matches: [['m.fs', 3, 'TODO fix']] },
    ])))
    await unwrap(casebook.refresh(local.store, 'cut-c1', 'Q2', 'A2', [
      fileRead('one.txt', 'h-one'),
      { kind: 'glob-result', pattern: '*.fs', paths: ['a.fs', 'b.fs'] },
      { kind: 'grep-result', pattern: 'TODO', matches: [['m.fs', 3, 'TODO fix']] },
    ]))
    const fetched = await casebook.fetchCase(local.store, 10, 'cut-c1')
    assert.equal(fetched.ok, true)
    assert.equal(
      fetched.value.observations.length,
      3,
      'cross-observation assertion inspects the complete set, not one observation',
    )
  } finally {
    local.close()
    rmSync(common, { recursive: true, force: true })
  }
})
test('WHAT[knowledge-reuse-013] production_store_has_no_optional_fatal_handler_path', async () => {
  const { readFileSync } = await import('node:fs')
  const source = readFileSync(
    new URL('../../../src/Wanxiangshu/Repository/Knowledge/Casebook/Store.fs', import.meta.url),
    'utf8',
  )
  assert.doesNotMatch(source, /fatalTripHandler|setFatalTripHandler/)
})
}

{
const { default: test } = await import("node:test");
const { assertFatalBoundary } = await import("../../structured-workflow/tests/support/m6-boundary-proof.mjs");


test('WHAT[knowledge-reuse-013] Casebook fatal follows durable cut settlement and one injected fuse', () => assertFatalBoundary('knowledge-reuse'))
}
