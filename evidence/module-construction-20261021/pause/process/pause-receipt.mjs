import assert from 'node:assert/strict'
import fs from 'node:fs'
import { createHash, createHmac } from 'node:crypto'
import { spawnSync } from 'node:child_process'

const processRoot = '/private/tmp/vibe-fs-physical-soak-20261021-a'
const ptyRoot = '/private/tmp/vibe-fs-pty-soak-gen100-20261021'
const json = path => JSON.parse(fs.readFileSync(path, 'utf8'))
const sha = path => createHash('sha256').update(fs.readFileSync(path)).digest('hex')
const records = path => fs.readFileSync(path, 'utf8').trim().split('\n').filter(Boolean).map(JSON.parse)
const processStatus = json(processRoot + '/status.json')
const ptyStatus = json(ptyRoot + '/status.json')
const ptyController = json(ptyRoot + '/controller-exit.json')
const recovery = ptyRoot + '/physical/recovery'
const request = json(recovery + '/request')
const terminal = json(recovery + '/terminal')
assert.equal(terminal.proof, createHmac('sha256', request.secret).update(JSON.stringify(terminal.receipt)).digest('hex'))
assert.equal(terminal.receipt.key, request.key)
assert.equal(terminal.receipt.incarnation, request.incarnation)
assert.equal(terminal.receipt.result.kind, 'Ended')
assert.equal(terminal.receipt.result.stopped, true)
assert.deepEqual(terminal.receipt.result.exit, { code: null, signal: 'SIGTERM' })
const pids = [...new Set([processStatus.coordinatorPid, ...Object.values(processStatus.lanes).flatMap(lane => [lane.pid, ...lane.ownedPids]), 84967, ptyStatus.pid, ...ptyStatus.ownedPids])]
const ps = spawnSync('/bin/ps', ['-p', pids.join(','), '-o', 'pid=,ppid=,pgid=,rss=,stat=,comm='], { encoding: 'utf8', timeout: 5000 })
assert.equal(ps.status, 1)
assert.equal(ps.stdout.trim(), '')
const fd = spawnSync('/usr/sbin/lsof', ['-a', '-p', pids.join(','), '-F', 'pf'], { encoding: 'utf8', timeout: 10000 })
assert.equal(fd.status, 1)
assert.equal(fd.stdout.trim(), '')
const output = records(ptyRoot + '/output.jsonl').map(line => line.text).join('')
let expected = `READY|${ptyStatus.scope.target}|tty\r\n`
const input = createHash('sha256')
for (let sequence = 0; sequence < ptyStatus.requests; sequence += 1) {
  const value = `\uFEFFPTY input ${sequence}: 中🧭\r\nvalue remains data`
  const payload = Buffer.from(value)
  const digest = createHash('sha256').update(payload).digest('hex')
  input.update(JSON.stringify({ sequence, value }) + '\n')
  expected += `ACK|${sequence}|${payload.length}|${digest}|${payload.toString('base64')}\r\n`
}
assert.equal(output, expected)
assert.equal(Buffer.byteLength(output), ptyStatus.outputBytes)
const list = path => fs.readdirSync(path).sort()
const physicalDirectories = {
  processNode: list(processRoot + '/node22/tmp'),
  processBun: list(processRoot + '/bun/tmp'),
  ptyScopeExists: fs.existsSync(ptyStatus.scope.directory),
  ptyEndpointExists: fs.existsSync(request.endpoint),
  ptyRemainingTmp: list(ptyRoot + '/tmp'),
  ptyDurableRecoveryEvidence: list(recovery),
}
assert.equal(physicalDirectories.ptyScopeExists, false)
assert.equal(physicalDirectories.ptyEndpointExists, false)
assert.equal(physicalDirectories.processNode.some(name => name.startsWith('wanxiangli-supervision-')), false)
assert.equal(physicalDirectories.processBun.some(name => name.startsWith('wanxiangli-supervision-')), false)
const resourceSummary = path => {
  const samples = records(path)
  return {
    samples: samples.length,
    first: samples[0].at,
    last: samples.at(-1).at,
    harnessRssBytes: { minimum: Math.min(...samples.map(row => row.memory.rss)), maximum: Math.max(...samples.map(row => row.memory.rss)), last: samples.at(-1).memory.rss },
    limitation: 'Finite sampled observations; no general absence-of-leaks claim. Raw own-PID CPU and FD samples remain in resources.jsonl.',
  }
}
const receipt = {
  state: 'PausedByUser; incomplete experiments, not duration acceptance',
  at: new Date().toISOString(),
  reason: 'User revoked the minimum five-hour continuation and requested pause at a suitable saved point.',
  interpretation: 'Original immutable harness records Failed/exit1 for requested cancellation. No product assertion had failed before cancellation. These original statuses are retained without relabeling.',
  process: {
    source: processStatus.source,
    completedPhases: [],
    lanes: Object.fromEntries(Object.entries(processStatus.lanes).map(([lane, state]) => [lane, {
      elapsedMs: state.elapsedMs,
      observedBytes: state.bytes,
      channelState: state.currentState,
      inputWriteBytes: state.inputBytes,
      inputWriteStages: state.inputStages,
      backpressureWitness: records(processRoot + '/' + lane + '/events.jsonl').find(row => row.event === 'physical-backpressure-observed'),
      completedPhases: state.completedPhases,
      exitCode: state.exitCode,
      cancellation: state.error.message,
      resourceSummary: resourceSummary(processRoot + '/' + lane + '/resources.jsonl'),
    }])),
    unavailableEvidence: 'The collector retained running SHA state only in memory; cancellation bypassed normal final digest materialization. No actual complete-channel digest or target stdin EOF digest is claimed. The byte counts and contiguous chunk checks were observed before cancellation; expected synthetic bytes must not stand in for actual captured digests.',
    uncompleted: ['Two-hour Process natural stdin EOF/exit0 and final channel hash/tail checks', 'Entire two-hour Supervision phase, descendant retained-pipe TERM/KILL assertions and temp cleanup at its normal close', 'Four-hour owned keepalive completion', 'Zero active Wait registry after natural closure; final cancellation snapshot still has one active descriptor per terminated harness'],
    originalExit: 1,
    restartCommand: '/private/tmp/vibe-fs-sm07-node22.h1udgm/node-v22.13.0-darwin-arm64/bin/node /private/tmp/vibe-fs-physical-soak-20261021-a/consumer/coordinator.mjs',
    restartCondition: 'Use a fresh directory and adjust the three explicit harness root constants before restart; existing witness directories and logs must be retained, not reused as a fresh run.',
  },
  pty: {
    generation: ptyStatus.generation,
    elapsedFromHarnessStartMs: ptyStatus.elapsedMs,
    observedActiveMs: Date.parse(ptyStatus.endedAt) - Date.parse(ptyStatus.activeStartedAt),
    completedPhases: ptyStatus.phases,
    requests: ptyStatus.requests,
    liveControlChecks: ptyStatus.socketChecks,
    observedOutputBytes: ptyStatus.outputBytes,
    actualOutputSha256: createHash('sha256').update(output).digest('hex'),
    outputMatchesAllAcknowledgedExpectedLines: true,
    generatedAcknowledgedInputSha256: input.digest('hex'),
    inputDigestLimitation: 'All response payload hashes and sequences matched; the target was canceled before a real stdin EOF aggregate witness. The generated input digest is not a target EOF receipt.',
    originalExit: ptyController,
    cancellation: ptyStatus.error.message,
    cleanupTerminal: terminal.receipt,
    cleanupTerminalHmacVerified: true,
    resourceSummary: resourceSummary(ptyRoot + '/resources.jsonl'),
    uncompleted: ['120-minute active interval', 'Real canonical stdin EOF aggregate witness and original target exit23', 'Descendant retained-scope observation', 'Paused custodian Unknown bounded recovery', 'Resume then TERM/KILL descendant recovery preserving target exit23'],
    restartCommand: '/private/tmp/vibe-fs-sm07-node22.h1udgm/node-v22.13.0-darwin-arm64/bin/node /private/tmp/vibe-fs-pty-soak-gen100-20261021/consumer/controller.mjs',
    restartCondition: 'Use a fresh directory and adjust explicit root constants; do not reuse the durable claim/terminal or overwrite this experiment.',
  },
  cleanup: { ownedPids: pids, ps: { command: '/bin/ps -p <only recorded owned PIDs> -o pid=,ppid=,pgid=,rss=,stat=,comm=', exitCode: ps.status, stdout: ps.stdout, stderr: ps.stderr }, lsof: { exitCode: fd.status, stdout: fd.stdout, stderr: fd.stderr }, allRecordedPidsAbsent: true, allRecordedPidFdListsAbsent: true, physicalDirectories, limitation: 'Durable recovery claim/request/available/terminal and own Bun cache directory are retained as evidence/cache, not live process resources.' },
  artifacts: Object.fromEntries([
    processRoot + '/launch.json', processRoot + '/harness-sha256.txt', processRoot + '/status.json',
    ...['node22', 'bun'].flatMap(lane => ['events.jsonl', 'resources.jsonl', 'runner.log', 'status.json'].map(name => processRoot + '/' + lane + '/' + name)),
    ptyRoot + '/launch.json', ptyRoot + '/copied-artifact-receipt.json', ptyRoot + '/harness-sha256.txt', ptyRoot + '/status.json', ptyRoot + '/events.jsonl', ptyRoot + '/resources.jsonl', ptyRoot + '/output.jsonl', ptyRoot + '/controller-exit.json', recovery + '/terminal',
  ].map(path => [path, { bytes: fs.statSync(path).size, sha256: sha(path) }])),
}
fs.writeFileSync(processRoot + '/pause-receipt.json', JSON.stringify(receipt, null, 2) + '\n')
fs.writeFileSync(ptyRoot + '/pause-receipt.json', JSON.stringify(receipt, null, 2) + '\n')
console.log(JSON.stringify(receipt, null, 2))
