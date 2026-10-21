import assert from 'node:assert/strict'
import fs from 'node:fs'
import { createHash } from 'node:crypto'
import { spawnSync } from 'node:child_process'

const root = '/private/tmp/vibe-fs-physical-soak-20261021-b'
const json = file => JSON.parse(fs.readFileSync(root + '/' + file, 'utf8'))
const rows = file => fs.readFileSync(root + '/' + file, 'utf8').trim().split('\n').filter(Boolean).map(JSON.parse)
const sha = file => createHash('sha256').update(fs.readFileSync(root + '/' + file)).digest('hex')
const status = json('status.json')
const preflight = json('preflight-receipt.json')
assert.equal(status.state, 'Passed')
assert.deepEqual(status.results, [{ lane: 'node22', code: 0, signal: null }, { lane: 'bun', code: 0, signal: null }])
assert.equal(fs.readFileSync(root + '/exit', 'utf8'), '0')
for (const source of preflight.harness) assert.equal(createHash('sha256').update(fs.readFileSync(source.path)).digest('hex'), source.sha256)
for (const file of preflight.artifacts) assert.equal(sha('consumer/node_modules/wanxiangli/' + file.path), file.rawSha256)
const pids = [...new Set([status.coordinatorPid, ...Object.values(status.lanes).flatMap(lane => [lane.pid, ...lane.ownedPids])])]
const ps = spawnSync('/bin/ps', ['-p', pids.join(','), '-o', 'pid=,ppid=,pgid=,rss=,stat=,comm='], { encoding: 'utf8', timeout: 5000 })
const fd = spawnSync('/usr/sbin/lsof', ['-a', '-p', pids.join(','), '-F', 'pf'], { encoding: 'utf8', timeout: 10000 })
assert.equal(ps.status, 1)
assert.equal(ps.stdout.trim(), '')
assert.equal(fd.status, 1)
assert.equal(fd.stdout.trim(), '')
const expectedOutput = (channel, frames, supervised) => {
  const digest = createHash('sha256')
  let bytes = 0
  const add = value => { bytes += value.length; digest.update(value) }
  add(Buffer.from(`\uFEFFSTREAM:${channel}\r\n`))
  const unit = Buffer.from('\uFEFF中🧭\r\n')
  const burst = Buffer.alloc(3 * 1024 * 1024)
  for (let offset = 0; offset < burst.length; offset += unit.length) unit.copy(burst, offset)
  add(burst)
  for (let frame = 0; frame < frames; frame += 1) add(Buffer.from(`\uFEFF${channel}|${String(frame).padStart(6, '0')}|中🧭\r\n`))
  if (supervised) add(Buffer.from(channel === 'stdout' ? 'DESCENDANT_READY\r\n' : 'DESCENDANT_TERM\r\n'))
  return { bytes, sha256: digest.digest('hex') }
}
const cpuSeconds = value => value.split(':').reduce((total, part) => total * 60 + Number(part), 0)
const summarizeResources = lane => {
  const samples = rows(lane + '/resources.jsonl')
  const processes = {}
  for (const sample of samples) {
    const descriptors = {}
    let pid
    for (const line of sample.descriptors.split('\n')) {
      if (/^p\d+$/.test(line)) { pid = line.slice(1); descriptors[pid] = 0 }
      else if (/^f\d+$/.test(line) && pid) descriptors[pid] += 1
    }
    for (const line of sample.ps.trim().split('\n').filter(Boolean)) {
      const [id, , , rss, cpu, state] = line.trim().split(/\s+/)
      const previous = processes[id] ?? { samples: 0, firstAt: sample.at, maxRssKiB: 0, minNumericFds: Number.MAX_SAFE_INTEGER, maxNumericFds: 0, maxObservedCpuSeconds: 0 }
      previous.samples += 1
      previous.lastAt = sample.at
      previous.maxRssKiB = Math.max(previous.maxRssKiB, Number(rss))
      previous.maxObservedCpuSeconds = Math.max(previous.maxObservedCpuSeconds, cpuSeconds(cpu))
      previous.lastObservedState = state
      if (descriptors[id] !== undefined) {
        previous.minNumericFds = Math.min(previous.minNumericFds, descriptors[id])
        previous.maxNumericFds = Math.max(previous.maxNumericFds, descriptors[id])
      }
      processes[id] = previous
    }
  }
  return { samples: samples.length, firstAt: samples[0].at, lastAt: samples.at(-1).at, harnessRssBytes: { min: Math.min(...samples.map(sample => sample.memory.rss)), max: Math.max(...samples.map(sample => sample.memory.rss)), last: samples.at(-1).memory.rss }, ownProcessSamples: processes, limitation: 'Only finite own-PID observations. Max sampled RSS/CPU and numeric FD counts are not universal leak guarantees or new production limits.' }
}
const lanes = {}
for (const [lane, state] of Object.entries(status.lanes)) {
  assert.equal(state.state, 'Passed')
  assert.equal(state.exitCode, 0)
  assert.equal(state.activeWaits, 0)
  assert.equal(state.completedPhases.length, 2)
  assert.equal(state.ownedKeeper.exitWon, false)
  assert.ok(state.ownedKeeper.elapsedMs >= 14400000)
  const events = rows(lane + '/events.jsonl')
  const phases = state.completedPhases.map(phase => {
    assert.ok(phase.elapsedMs >= 7200000)
    const supervised = phase.name === 'supervision'
    const witness = json(lane + '/' + phase.name + '/witness.json')
    const input = createHash('sha256')
    let inputBytes = 0
    for (let sequence = 0; sequence < phase.inputStages; sequence += 1) {
      const bytes = Buffer.from(`\uFEFFinput:${lane}:${phase.name}:${sequence}\r\n中🧭`)
      input.update(bytes)
      inputBytes += bytes.length
    }
    assert.equal(witness.inputBytes, inputBytes)
    assert.equal(witness.inputHash, input.digest('hex'))
    const expected = Object.fromEntries(['stdout', 'stderr'].map(channel => [channel, expectedOutput(channel, witness.frames, supervised)]))
    for (const channel of ['stdout', 'stderr']) assert.equal(expected[channel].bytes, phase.bytes[channel])
    if (supervised) assert.equal(fs.readFileSync(root + '/' + lane + '/supervision/term-seen', 'utf8'), 'seen')
    return {
      name: phase.name,
      elapsedMs: phase.elapsedMs,
      actualObserved: { bytes: phase.bytes, frames: phase.frames, inputStages: phase.inputStages, exit: phase.exit, eofWitness: witness },
      actualStreamAssertionsPassedInOriginalHarness: { contiguousChannelOffsets: true, maximumChunkBytes: 65536, streamShaEqualsExpected: true, exact4096ByteTail: true, validUtf8SuffixText: true, closedWithNoCaptureFailure: true, nativeTargetGone: true, waitRegistryEmpty: true, source: root + '/consumer/lane.mjs', sha256: preflight.harness.find(source => source.path.endsWith('/lane.mjs')).sha256, lineAnchors: { actualEofAndInputDigest: 166, actualStreamSha: 189, actualTail: 190, actualPhysicalClosed: 181, nativeGone: 194, waitRegistryEmpty: 200 } },
      postHocRecomputedExpectedOutput: expected,
      digestProvenance: 'Expected hashes above were recomputed offline from retained witness.frames and fixed payload code. The original runtime compared each actual stream hash to its expected hash before writing phase-completed. No second capture or separately persisted original output digest is claimed.',
      backpressure: events.find(event => event.event === 'physical-backpressure-observed' && event.name === phase.name),
      supervisedClosure: supervised ? { sourceAssertionsAtLines: [172, 173, 174, 175, 176, 177, 178, 179, 195, 196, 197, 198], preservedTargetExit: phase.exit, termWitness: root + '/' + lane + '/supervision/term-seen', descendant: witness.descendant, meaning: 'Real target exit23 while descendant holds pipes; Draining with supervisor alive; TERM witnessed and 25ms wait TimedOut; group KILL; Closed capture; descendant and supervisor absent.' } : null,
    }
  })
  const remainingTmp = fs.readdirSync(root + '/' + lane + '/tmp').sort()
  assert.equal(remainingTmp.some(name => name.startsWith('wanxiangli-supervision-') || name.startsWith('wanxiangli-pipes-')), false)
  lanes[lane] = { totalElapsedMs: state.elapsedMs, actualObservedOutputBytes: state.bytes, phases, actualOwnedKeeperReceipt: state.ownedKeeper, exitCode: 0, activeWaitsAtEnd: state.activeWaits, remainingTmp, resourceSummary: summarizeResources(lane) }
}
const cleanup = { at: new Date().toISOString(), ownedPids: pids, ps: { command: '/bin/ps -p <only recorded owned PIDs> -o pid=,ppid=,pgid=,rss=,stat=,comm=', exitCode: ps.status, stdout: ps.stdout, stderr: ps.stderr }, lsof: { exitCode: fd.status, stdout: fd.stdout, stderr: fd.stderr }, allRecordedPidsAbsent: true, allRecordedPidFdsAbsent: true, ownNativeScopeAndPipeDirectoriesAbsent: true, remainingBunCache: 'Only own Bun cache may remain; all logs and witnesses retained.' }
fs.writeFileSync(root + '/cleanup-receipt.json', JSON.stringify(cleanup, null, 2) + '\n')
const files = ['launch.json', 'preflight-receipt.json', 'harness-sha256.txt', 'source-continuity.diff', 'install.log', 'preflight.log', 'status.json', 'exit', 'coordinator.log', 'consumer/coordinator.mjs', 'consumer/lane.mjs', 'consumer/worker.mjs', 'consumer/package.json', 'consumer/package-lock.json', 'cleanup-receipt.json']
for (const lane of ['node22', 'bun']) {
  for (const file of ['status.json', 'exit', 'events.jsonl', 'resources.jsonl', 'runner.log', 'process/writer.ready', 'process/writer.burst-done', 'process/witness.json', 'supervision/writer.ready', 'supervision/writer.burst-done', 'supervision/witness.json', 'supervision/term-seen']) files.push(lane + '/' + file)
}
const receipt = { state: 'Passed finite four-hour Node22 and Bun Process/Supervision experiments', at: new Date().toISOString(), startedAt: status.startedAt, endedAt: status.endedAt, sourceCommit: status.source, packageInputFingerprint: { version: '0.11.0', path: preflight.package, sha256: preflight.packageSha256, packageAndNodeModulesExcludedFromArchiveList: true }, runtimes: { node22: preflight.node, bun: preflight.bun }, originalCommand: json('launch.json').command, actualCoordinatorAndLaneExitCodes: status.results, actualCoordinatorExecExitCode: 0, lanes, cleanup, boundaries: ['Only these finite 4h queues on these runtimes; no universal absence of leaks', 'No PTY success is implied; separate 2h PTY overall exit1 is recorded as P2', 'Post-hoc expected hashes are separate from original actual-stream assertions; no raw complete output recording is invented', 'No new tests or matrix were run during finalization'], completeHarnessSources: preflight.harness, artifacts: Object.fromEntries(files.map(file => [root + '/' + file, { bytes: fs.statSync(root + '/' + file).size, sha256: sha(file) }])) }
fs.writeFileSync(root + '/final-receipt.json', JSON.stringify(receipt, null, 2) + '\n')
console.log(JSON.stringify({ state: receipt.state, lanes, cleanup }, null, 2))
