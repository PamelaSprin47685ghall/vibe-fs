import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { default: fs } = await import("node:fs");
const { default: os } = await import("node:os");
const { default: path } = await import("node:path");

const causal = await import('../../../dist/Execution/Session/Wait/Surface.js')
const mkWait = (waitKind, ownerKind, subject, producer) =>
  causal.createWait({
    waitKind,
    owner: causal.owner(ownerKind, { id: ownerKind }),
    subject,
    producer,
    escapes: [causal.escape('processLifetime')],
    source: 'causal-wait-bridge.test',
  })
const externalProducer = (kind, identity) => causal.externalProducer(kind, identity)
const readDiagnostic = (workspace) =>
  JSON.parse(fs.readFileSync(path.join(workspace, '.wanxiangshu', 'diagnostics', 'causal-waits.json'), 'utf8'))

test('WHAT[causal-wait-008] CAUSAL_BRIDGE_writeSnapshot_overwrites_workspace_json', () => {
  const workspace = fs.mkdtempSync(path.join(os.tmpdir(), 'causal-bridge-'))
  fs.mkdirSync(path.join(workspace, '.git', 'info'), { recursive: true })
  const registry = causal.createRegistry()
  const wait = mkWait(
    'provider-assessment',
    'RelayWorkflow',
    { incumbency: 'I2', road: 'R17' },
    externalProducer('provider', { run: 'P81' }),
  )
  const lease = causal.enter(registry, wait)
  try {
    causal.writeSnapshot(workspace, registry)
    const filePath = path.join(workspace, '.wanxiangshu', 'diagnostics', 'causal-waits.json')
    assert.equal(fs.existsSync(filePath), true)
    const exclude = fs.readFileSync(path.join(workspace, '.git', 'info', 'exclude'), 'utf8')
    assert.ok(exclude.includes('.wanxiangshu/'), 'diagnostic dir must be git-excluded')
    const snap = readDiagnostic(workspace)
    assert.equal(typeof snap.pid, 'number')
    assert.ok(String(snap.sequence).length > 0)
    assert.equal(snap.active.length, 1)
    assert.equal(snap.active[0].waitKind, 'provider-assessment')
    assert.ok(Array.isArray(snap.history))
    assert.ok(Array.isArray(snap.frontiers))
    assert.ok(snap.frontiers.length >= 1)
  } finally {
    causal.dispose(lease)
    fs.rmSync(workspace, { recursive: true, force: true })
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { assertOpaque } = await import("../../verification-system/tests/support/js-contract.mjs");

const causal = await import('../../../dist/Execution/Session/Wait/Surface.js')
const owner = (id) => causal.owner('flow', { id })
const descriptor = (id) =>
  causal.createWait({
    waitKind: 'lifecycle-wait',
    owner: owner(id),
    subject: { target: id },
    producer: causal.externalProducer('capability', { id }),
    escapes: [causal.escape('processLifetime')],
    source: 'wait-lifecycle.test',
  })
const lastTransition = (registry) => {
  const history = causal.snapshot(registry).history
  assert.ok(history.length > 0, 'expected history')
  return history.at(-1)
}

test('WHAT[causal-wait-008] CAUSAL_008_fresh_registry_starts_empty_no_durable_state', () => {
  const snapshot = causal.snapshot(causal.createRegistry())
  assert.equal(snapshot.active.length, 0)
  assert.equal(snapshot.history.length, 0)
  assert.equal(snapshot.sequence, 0)
})
}
