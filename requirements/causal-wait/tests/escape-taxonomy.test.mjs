// CAUSAL-006 — explicit wait escapes remain distinct in diagnostics.

import assert from 'node:assert/strict'
import test from 'node:test'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'

const causal = await import('../../../dist/Execution/Session/Wait/Surface.js')

const owner = (id) => causal.owner('flow', { id })
const readDiagnostic = (workspace) =>
  JSON.parse(fs.readFileSync(path.join(workspace, '.wanxiangshu', 'diagnostics', 'causal-waits.json'), 'utf8'))

const write = (descriptor) => {
  const workspace = fs.mkdtempSync(path.join(os.tmpdir(), 'escape-taxonomy-'))
  const registry = causal.createRegistry()
  const lease = causal.enter(registry, descriptor)
  causal.writeSnapshot(workspace, registry)
  return { workspace, lease, registry }
}

test('WHAT[CAUSAL-006] CAUSAL_006_wait_escape_has_five_typed_cases', () => {
  const tags = [
    causal.escape('deadlineAt', '2026-01-01T00:00:00Z'),
    causal.escape('cancelledBy', owner('review-attempt')),
    causal.escape('processLifetime'),
    causal.escape('sessionLifetime'),
    causal.escape('openEndedExternal'),
  ].map((value) => value.kind)

  assert.deepEqual(tags, ['deadlineAt', 'cancelledBy', 'processLifetime', 'sessionLifetime', 'openEndedExternal'])
})

test('WHAT[CAUSAL-006] CAUSAL_006_escapes_render_distinctly_in_diagnostics', () => {
  const wait = causal.createWait({
    waitKind: 'escape-taxonomy',
    owner: owner('A'),
    subject: { target: 'X' },
    producer: causal.externalProducer('capability', { id: 'X' }),
    escapes: [
      causal.escape('deadlineAt', '2026-01-01T00:00:00Z'),
      causal.escape('cancelledBy', owner('review-attempt')),
      causal.escape('processLifetime'),
      causal.escape('sessionLifetime'),
      causal.escape('openEndedExternal'),
    ],
    source: 'escape-taxonomy.test',
  })

  const { workspace, lease } = write(wait)
  try {
    const snap = readDiagnostic(workspace)
    assert.equal(snap.active.length, 1)
    const tags = snap.active[0].escapes.map(({ tag }) => tag).sort()
    assert.deepEqual(tags, ['cancelledBy', 'deadlineAt', 'openEndedExternal', 'processLifetime', 'sessionLifetime'])
  } finally {
    causal.dispose(lease)
    fs.rmSync(workspace, { recursive: true, force: true })
  }
})

test('WHAT[CAUSAL-006] CAUSAL_006_deadline_escape_carries_typed_instant', () => {
  const wait = causal.createWait({
    waitKind: 'deadline-escape',
    owner: owner('A'),
    subject: {},
    producer: causal.externalProducer('capability', { id: 'X' }),
    escapes: [causal.escape('deadlineAt', '2026-01-01T00:00:00Z')],
    source: 'escape-taxonomy.test',
  })

  const { workspace, lease } = write(wait)
  try {
    const [{ tag: deadlineKind, at: deadlineAt }] = readDiagnostic(workspace).active[0].escapes
    assert.equal(deadlineKind, 'deadlineAt')
    assert.match(deadlineAt, /^2026-01-01T00:00:00(\.\d+)?(Z|\+00:00)$/)
  } finally {
    causal.dispose(lease)
    fs.rmSync(workspace, { recursive: true, force: true })
  }
})
