import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import test from 'node:test'

const seed = 0x36a11ce
const restartCount = 16
const operationsPerRestart = 6
const surfaceUrl = new URL('../../../dist/OpenCode/Host/ModelRoutingSurface.js', import.meta.url).href
const childProgram = String.raw`
import assert from 'node:assert/strict'
import * as routing from ${JSON.stringify(surfaceUrl)}

const cycle = Number.parseInt(process.argv[1], 10)
let maxRetained = 0
const retained = (snapshot) =>
  snapshot.ledgerEntries.length + snapshot.tokens.length + snapshot.custodies.length +
  snapshot.executions.length + snapshot.waiters.length + snapshot.owners.length + snapshot.lineage.length
const audit = () => {
  const snapshot = routing.sharedCapacitySnapshot()
  assert.ok(snapshot.activeCount >= 0 && snapshot.activeCount <= snapshot.ledgerEntries.length)
  assert.equal(
    snapshot.tokenStateCounts.idle + snapshot.tokenStateCounts.inFlight + snapshot.tokenStateCounts.retiring,
    snapshot.tokens.length,
  )
  assert.ok(snapshot.waiters.length <= 32)
  assert.deepEqual(routing.reconcileCapacityEvidence(snapshot), { kind: 'NoOp' })
  maxRetained = Math.max(maxRetained, retained(snapshot))
  return snapshot
}

await routing.initialize()
const initial = audit()
await routing.initialize()
const afterReload = audit()
assert.deepEqual(afterReload, initial, 'plugin reload preserves the process singleton without replaying work')

const exact = {
  sessionId: 'restart-session-' + cycle,
  physicalUserMessageId: 'restart-physical-' + cycle,
  effectiveAgent: 'restart-agent',
}
const first = await routing.acquireSharedExecutionAdmission(
  exact.sessionId,
  exact.physicalUserMessageId,
  exact.effectiveAgent,
)
assert.equal(first.kind, 'Acquired')
const afterAcquire = audit()
assert.equal(afterAcquire.ledgerEntries.length, 1)
const projected = routing.sharedExecutionAdmissionTarget(first.lease)
assert.deepEqual(
  routing.commitSharedExecutionAdmission(first.lease, { ...exact, target: projected }),
  { kind: 'Applied' },
)
const observed = audit()

await routing.initialize()
const beforeDuplicateReload = audit()
assert.deepEqual(beforeDuplicateReload, observed)
const duplicate = await routing.acquireSharedExecutionAdmission(
  exact.sessionId,
  exact.physicalUserMessageId,
  exact.effectiveAgent,
)
assert.equal(duplicate.kind, 'Acquired')
assert.equal(duplicate.lease, first.lease, 'plugin reload reuses the exact physical admission fence')
const afterDuplicateReload = audit()
assert.deepEqual(afterDuplicateReload, observed, 'plugin reload cannot duplicate ledger, token, custody, or execution work')

process.stdout.write(JSON.stringify({ initial, observed, afterDuplicateReload, maxRetained }))
`

const runProcess = (home, cycle) =>
  JSON.parse(
    execFileSync(process.execPath, ['--input-type=module', '--eval', childProgram, String(cycle)], {
      encoding: 'utf8',
      env: { ...process.env, HOME: home },
    }),
  )

test('WHAT[EMR-003] process restart drops process-local capacity and rebuilds only from explicit physical observations', (context) => {
  const home = mkdtempSync(join(tmpdir(), 'wanxiangshu-capacity-restart-'))
  const config = join(home, '.config', 'opencode', 'wanxiangshu.mjs')
  mkdirSync(dirname(config), { recursive: true })
  writeFileSync(
    config,
    "export default function route(_role, running) { return running.length < 1 ? { model: 'provider/restart', reasoning: 'none' } : null }\n",
  )

  let maxRetained = 0
  try {
    for (let cycle = 0; cycle < restartCount; cycle += 1) {
      const result = runProcess(home, (seed + cycle) >>> 0)
      assert.equal(result.initial.ledgerEntries.length, 0, 'a new process cannot inherit a dead process token')
      assert.equal(result.initial.tokens.length, 0)
      assert.equal(result.initial.custodies.length, 0)
      assert.equal(result.initial.executions.length, 0)
      assert.equal(result.initial.waiters.length, 0)
      assert.equal(result.initial.owners.length, 0)
      assert.equal(result.initial.lineage.length, 0)

      assert.equal(result.observed.ledgerEntries.length, 1, 'only the new explicit admission observation reconstructs capacity')
      assert.equal(result.observed.tokens.length, 1)
      assert.equal(result.observed.executions.length, 1)
      assert.deepEqual(result.afterDuplicateReload, result.observed)
      maxRetained = Math.max(maxRetained, result.maxRetained)
    }
  } finally {
    rmSync(home, { recursive: true, force: true })
  }

  assert.ok(maxRetained <= 5, 'one live exact execution retains only ledger, token, custody, execution, and owner nodes')
  context.diagnostic(
    `task36 restart seed=${seed} restarts=${restartCount} operations=${restartCount * operationsPerRestart} maxRetained=${maxRetained}`,
  )
})
