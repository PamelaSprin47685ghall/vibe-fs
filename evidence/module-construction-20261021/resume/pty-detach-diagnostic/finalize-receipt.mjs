import assert from 'node:assert/strict'
import fs from 'node:fs'
import { createHash } from 'node:crypto'

const root = '/private/tmp/vibe-fs-pty-detach-diagnostic-20261021'
const json = file => JSON.parse(fs.readFileSync(root + '/' + file))
const rows = file => fs.readFileSync(root + '/' + file, 'utf8').trim().split('\n').filter(Boolean).map(JSON.parse)
const sha = file => createHash('sha256').update(fs.readFileSync(root + '/' + file)).digest('hex')
const status = json('status.json')
const cleanup = json('cleanup-receipt.json')
const launch = json('launch.json')
const events = rows('events.jsonl')
const first = events.find(event => event.event === 'creator-detach-observation')
assert.match(first.reason, /credentials do not match/)
assert.equal(status.state, 'Failed')
assert.equal(status.exitCode, 1)
assert.equal(status.error.message, 'HARNESS_BOUND: Stale recovery controller detach')
assert.ok(status.elapsedMs < 90000)
assert.equal(cleanup.allRecordedPidsAbsent, true)
assert.equal(cleanup.allRecordedPidFdsAbsent, true)
for (const source of launch.completeHarnessSources) assert.equal(createHash('sha256').update(fs.readFileSync(source.path)).digest('hex'), source.sha256)
const output = rows('output.jsonl').map(row => row.text).join('')
const files = ['launch.json', 'harness-sha256.txt', 'preflight.log', 'controller.json', 'status.json', 'controller-exit.json', 'events.jsonl', 'resources.jsonl', 'output.jsonl', 'runner.log', 'physical/target-input.jsonl', 'physical/target-eof.json', 'observed-target-exit.json', 'cleanup-receipt.json', 'cleanup.log', 'consumer/controller.mjs', 'consumer/pty-soak.mjs', 'consumer/target.mjs', 'consumer/package.json', 'copied-artifact-receipt.json', 'gen106-build-manifest.json']
const receipt = {
  kind: 'One finite boundary diagnostic, not a replacement for the original failed two-hour endurance experiment',
  at: new Date().toISOString(),
  state: 'Failed, exit1; classified P2 and deferred by root per user workflow priority',
  originalFailure: '/private/tmp/vibe-fs-pty-soak-gen106-20261021-b/final-receipt.json',
  fixedProductionArtifacts: { generation: 106, rawEqualFiles: 833, modified: false },
  elapsedMs: status.elapsedMs,
  activeMs: status.activeElapsedMs,
  firstDetachObservation: first,
  creatorDetach: status.creatorDetach,
  eof: json('physical/target-eof.json'),
  targetExit: json('observed-target-exit.json'),
  actualOutput: { bytes: Buffer.byteLength(output), sha256: createHash('sha256').update(output).digest('hex') },
  unknown: status.unknown,
  failure: status.error,
  rootDecision: 'Record P2 and do not fix, trace, retry or expand the matrix in this batch.',
  conclusion: 'A real credentials-mismatch response established that ordinary creator detach had completed before the custodian was stopped. This same failed scenario therefore narrows to a timed-out recover attachment/control ownership cleanup boundary after pause/resume. It neither identifies a particular socket event nor assigns responsibility to Bun, and does not establish that ordinary recovery always fails.',
  cleanup,
  launch,
  artifacts: Object.fromEntries(files.map(file => [root + '/' + file, { bytes: fs.statSync(root + '/' + file).size, sha256: sha(file) }])),
}
fs.writeFileSync(root + '/final-receipt.json', JSON.stringify(receipt, null, 2) + '\n')
console.log(JSON.stringify({ state: receipt.state, elapsedMs: receipt.elapsedMs, firstDetach: first.reason, ownedAbsent: cleanup.allRecordedPidsAbsent, endpointRemoved: cleanup.controlEndpoint.removed }))
