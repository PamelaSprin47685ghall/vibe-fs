// requirements/causal-wait/tests/escape-taxonomy.test.mjs
// CAUSAL-006 / CCE-005 — every wait carries explicit termination paths (escapes);
// the five WaitEscape cases are typed and rendered distinctly in diagnostics.

import assert from 'node:assert/strict'
import test from 'node:test'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'

import { WaitEscape } from '../../../dist/Kernel/CausalWait.js'
import { writeSnapshot } from '../../../dist/Session/CausalWaitBridge.js'
import {
  caseNames,
  causalWait,
  CausalWaitRegistry,
  utcOffset,
} from '../../verification-system/tests/support/domain.mjs'

const owner = (id) => causalWait.owner('flow', [['id', id]])

const readDiagnostic = (workspace) => {
  const filePath = path.join(workspace, '.wanxiangshu', 'diagnostics', 'causal-waits.json')
  return JSON.parse(fs.readFileSync(filePath, 'utf8'))
}

test('CAUSAL_006_wait_escape_has_five_typed_cases', () => {
  assert.deepEqual(caseNames(WaitEscape), [
    'DeadlineAt',
    'CancelledBy',
    'ProcessLifetime',
    'SessionLifetime',
    'OpenEndedExternal',
  ])
})

test('CAUSAL_006_escapes_render_distinctly_in_diagnostics', () => {
  const wait = causalWait.create({
    waitKind: 'escape-taxonomy',
    owner: owner('A'),
    subject: [['target', 'X']],
    producer: causalWait.externalProducer('capability', [['id', 'X']]),
    escapes: [
      new WaitEscape(0, [utcOffset('2026-01-01T00:00:00Z')]),
      new WaitEscape(1, [owner('review-attempt')]),
      WaitEscape.ProcessLifetime,
      WaitEscape.SessionLifetime,
      WaitEscape.OpenEndedExternal,
    ],
    source: 'escape-taxonomy.test',
  })

  const workspace = fs.mkdtempSync(path.join(os.tmpdir(), 'escape-taxonomy-'))
  const registry = new CausalWaitRegistry()
  const lease = registry.Enter(wait)
  try {
    writeSnapshot(workspace, registry)
    const snap = readDiagnostic(workspace)
    assert.equal(snap.active.length, 1)
    const tags = snap.active[0].escapes.map((e) => e.tag).sort()
    assert.deepEqual(tags, ['cancelledBy', 'deadlineAt', 'openEndedExternal', 'processLifetime', 'sessionLifetime'])
  } finally {
    lease.Dispose()
    fs.rmSync(workspace, { recursive: true, force: true })
  }
})

test('CAUSAL_006_deadline_escape_carries_typed_instant', () => {
  const wait = causalWait.create({
    waitKind: 'deadline-escape',
    owner: owner('A'),
    subject: [],
    producer: causalWait.externalProducer('capability', [['id', 'X']]),
    escapes: [new WaitEscape(0, [utcOffset('2026-01-01T00:00:00Z')])],
    source: 'escape-taxonomy.test',
  })

  const workspace = fs.mkdtempSync(path.join(os.tmpdir(), 'escape-deadline-'))
  const registry = new CausalWaitRegistry()
  const lease = registry.Enter(wait)
  try {
    writeSnapshot(workspace, registry)
    const escape = readDiagnostic(workspace).active[0].escapes[0]
    assert.equal(escape.tag, 'deadlineAt')
    assert.match(escape.at, /^2026-01-01T00:00:00(\.\d+)?(Z|\+00:00)$/)
  } finally {
    lease.Dispose()
    fs.rmSync(workspace, { recursive: true, force: true })
  }
})
