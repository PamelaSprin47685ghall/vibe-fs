import assert from 'node:assert/strict'
import fs from 'node:fs'
import { createHash } from 'node:crypto'

const root = '/private/tmp/vibe-fs-pty-soak-gen106-20261021-b'
const json = file => JSON.parse(fs.readFileSync(root + '/' + file, 'utf8'))
const rows = file => fs.readFileSync(root + '/' + file, 'utf8').trim().split('\n').filter(Boolean).map(JSON.parse)
const sha = file => createHash('sha256').update(fs.readFileSync(root + '/' + file)).digest('hex')
const status = json('status.json')
const cleanup = json('cleanup-receipt.json')
assert.equal(status.state, 'Failed')
assert.equal(status.exitCode, 1)
assert.equal(status.error.message, 'HARNESS_BOUND: Stale recovery controller detach')
assert.ok(status.activeElapsedMs >= 7200000)
assert.equal(cleanup.allRecordedPidsAbsent, true)
assert.equal(cleanup.allRecordedPidFdsAbsent, true)
assert.equal(cleanup.terminalReceiptExists, false)
const actual = rows('output.jsonl').map(row => row.text).join('')
let expected = `READY|${status.scope.target}|tty\r\n`
const inputHash = createHash('sha256')
let inputBytes = 0
for (let sequence = 0; sequence < status.requests; sequence += 1) {
  const value = `\uFEFFPTY input ${sequence}: 中🧭\r\nvalue remains data`
  const payload = Buffer.from(value)
  const digest = createHash('sha256').update(payload).digest('hex')
  const line = JSON.stringify({ sequence, value }) + '\n'
  inputHash.update(line)
  inputBytes += Buffer.byteLength(line)
  expected += `ACK|${sequence}|${payload.length}|${digest}|${payload.toString('base64')}\r\n`
}
const inputDigest = inputHash.digest('hex')
const eof = json('physical/target-eof.json')
assert.equal(eof.count, status.requests)
assert.equal(eof.inputHash, inputDigest)
expected += `EOF|${status.requests}|${inputDigest}\r\nDESC_READY|${eof.descendant}\r\n`
assert.equal(actual, expected)
assert.equal(Buffer.byteLength(actual), status.outputBytes)
const targetExit = json('observed-target-exit.json')
assert.equal(targetExit.code, 23)
assert.equal(targetExit.signal, null)
const preflight = json('preflight-receipt.json')
for (const file of preflight.harness) assert.equal(createHash('sha256').update(fs.readFileSync(file.path)).digest('hex'), file.sha256)
const resources = rows('resources.jsonl')
const lastResources = resources.at(-1)
const artifacts = [
  'launch.json', 'preflight-receipt.json', 'copied-artifact-receipt.json', 'gen106-build-manifest.json', 'harness-sha256.txt',
  'consumer/controller.mjs', 'consumer/pty-soak.mjs', 'consumer/target.mjs', 'consumer/package.json',
  'status.json', 'controller.json', 'controller-exit.json', 'runner.log', 'events.jsonl', 'resources.jsonl', 'output.jsonl',
  'physical/target-input.jsonl', 'physical/target-eof.json', 'observed-target-exit.json', 'cleanup-receipt.json', 'cleanup.log',
]
const receipt = {
  state: 'Failed; active duration and finite observations verified, final recovery/closure contract did not pass',
  at: new Date().toISOString(),
  generation: 106,
  immutableArtifacts: { files: 833, manifestSha256: '331e32dd1401edcedccef5f93dc5d2a03676b96267b6e06c1e2ccf1705df4d17', receipt: root + '/copied-artifact-receipt.json', sourceFreshness: preflight.sourceFreshness },
  originalCommand: json('launch.json').command,
  actualExit: json('controller-exit.json'),
  active: { startedAt: status.activeStartedAt, elapsedMs: status.activeElapsedMs, requests: status.requests, controlRefusalChecks: status.socketChecks, actualOutputBytes: Buffer.byteLength(actual), actualOutputSha256: createHash('sha256').update(actual).digest('hex'), allOutputMatchesIndependentExpectedLines: true, actualStdinEof: { bytes: inputBytes, sha256: inputDigest, witness: eof, matchesAllAcknowledgedInputLines: true } },
  targetExit,
  finiteUnknown: status.unknown,
  failure: status.error,
  failedStage: 'After SIGCONT, a control connection remained admitted for the 15-second detach observation. Public fallback drain also rejected live control connection. Which socket owned the slot is not identified by original logs.',
  boundaries: ['No terminal receipt was produced', 'Final normal recovery TERM/KILL and preserved exit23 terminal proof were not verified', 'No universal resource-leak claim', 'A later diagnostic does not turn this original exit1 into pass'],
  resources: { samples: resources.length, first: resources[0].at, last: lastResources.at, harnessRssBytes: { min: Math.min(...resources.map(row => row.memory.rss)), max: Math.max(...resources.map(row => row.memory.rss)), last: lastResources.memory.rss }, lastOwnedCpuRssState: lastResources.ps, rawOwnPidAndFdLog: root + '/resources.jsonl' },
  cleanup,
  completeHarnessSources: preflight.harness,
  originalSetupFailure: { root: '/private/tmp/vibe-fs-pty-soak-gen106-20261021', outcome: 'Initial setup omitted physical cwd; failed before owner creation, no test duration counted; original files retained' },
  rootIndependentGen107Continuity: { path: '/private/tmp/vibe-fs-pty-gen106-to-gen107-artifact-comparison.json', attribution: 'Reported by root, separate from this run: all 833 fixed consumer files unchanged; compared with gen107, 811 raw-equal plus 22 wanxiangli JS equal only after exact generated PID-comment normalization. Not 833 raw-equal and not a source-freshness assertion.' },
  artifacts: Object.fromEntries(artifacts.map(file => [root + '/' + file, { bytes: fs.statSync(root + '/' + file).size, sha256: sha(file) }])),
}
fs.writeFileSync(root + '/final-receipt.json', JSON.stringify(receipt, null, 2) + '\n')
console.log(JSON.stringify({ state: receipt.state, active: receipt.active, failure: status.error.message, pidsGone: cleanup.allRecordedPidsAbsent, endpointRemoved: cleanup.controlEndpoint.removed }, null, 2))
