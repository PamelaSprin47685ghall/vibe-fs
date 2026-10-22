import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as forkTool from '../../../dist/Execution/Delegation/Fork/OpenCode/ToolSurface.js'
import * as horizonCalibration from '../../../dist/Execution/Session/OpenCode/HorizonCalibrationSurface.js'
import { placeEngineer, withForkRuntime } from './support/fork-runtime.mjs'

process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'

test('WHAT[participant-horizon-011] a participant calibration states the four numbers from the authored resource', () => {
  const cases = [
    ['English', 'en.md', 'Ada', 4, 6, 125000, '2 minutes 5 seconds', '3 minutes 7 seconds'],
    ['SimplifiedChinese', 'zh-CN.md', 'Ada', 4, 6, 125000, '2 分钟 5 秒', '3 分钟 7 秒'],
    ['English', 'en.md', 'Ada', 0, 2, 90000, '1 minute 30 seconds', '3 minutes 0 seconds'],
  ]

  for (const [language, file, label, used, left, elapsedMs, timeUsed, timeLeft] of cases) {
    const rendered = horizonCalibration.render(language, label, used, left, elapsedMs)
    const resource = readFileSync(
      new URL(`../../../resources/provider/tool/horizon/calibration/${file}`, import.meta.url),
      'utf8',
    )
    const expected = resource
      .trim()
      .replaceAll('{{label}}', label)
      .replaceAll('{{turns_used}}', String(used))
      .replaceAll('{{turns_left}}', String(left))
      .replaceAll('{{time_used}}', timeUsed)
      .replaceAll('{{time_left}}', timeLeft)
    assert.equal(rendered, expected, `${language}: ${used}/${left} turns at ${elapsedMs}ms`)
  }
})

test('WHAT[participant-horizon-011] real cancellation keeps an accepted child and its undelivered consequence visible', async (context) => {
  await withForkRuntime(async ({ runtime, owner }) => {
    await context.test('accepted child is visible without its physical identity', async () => {
      const child = await placeEngineer(runtime, owner, 'Ada')
      const active = await forkTool.executeHorizon(runtime, owner)
      assert.match(active, /Ada.*still away/)
      assert.ok(!active.includes(child), 'physical child identity stays private')
    })
    await context.test('cancellation keeps the undelivered consequence visible', async () => {
      await forkTool.cancelOwnerChildren(runtime, owner)
      assert.match(await forkTool.executeHorizon(runtime, owner), /Ada.*did not return/)
      assert.equal(forkTool.abortCount(runtime), 1)
    })
  })
})

test('WHAT[participant-horizon-011] a restarted process voids the previous process pending child work and reports it once', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-horizon-restart-voided-'))
  const owner = 'manager-horizon-restart-voided'
  const first = await forkTool.createRuntime(directory, [{ sessionId: owner, agent: 'manager' }])

  try {
    // The child work stays pending: it is accepted and visible, never cancelled.
    await placeEngineer(first, owner, 'Ada')
    assert.equal(forkTool.durableLifecycleByname(first, owner, 'Ada'), 'Active')
  } finally {
    await forkTool.disposeRuntime(first)
  }

  // Restart: the pending child work can no longer be delivered, so load
  // settlement voids it. A void is not a completion, but it settles the
  // outstanding obligation: it stays visible until it is reported once.
  const restarted = await forkTool.createRuntime(directory, [{ sessionId: owner, agent: 'manager' }])

  try {
    const horizonReceipt = await forkTool.executeHorizonReceipt(restarted, owner)
    assert.equal(horizonReceipt.ok, true)
    const voidedRow = horizonReceipt.agents.find((row) => row.label === 'Ada')
    assert.ok(voidedRow, 'the voided work keeps a row in the horizon roster')
    assert.equal(voidedRow.status, 'voided', 'the voided work is reported as voided, not as still away')

    const joinPromise = forkTool.executeJoinReceipt(restarted, owner)
    let timer
    const timeout = new Promise((_, reject) => { timer = setTimeout(() => reject(new Error('JOIN_HANG_DETECTED')), 1500) })
    const joinResult = await Promise.race([joinPromise, timeout]).finally(() => clearTimeout(timer))
    assert.equal(joinResult.ok, true)
    const adaRecord = joinResult.records.find((record) => record.name === 'Ada')
    assert.ok(adaRecord, 'join reports the voided work by its Byname')
    assert.equal(adaRecord.status, 'voided')
    assert.notEqual(adaRecord.status, 'completed')
    assert.notEqual(adaRecord.status, 'undelivered')
    assert.equal(joinResult.byName.Ada.status, 'voided')

    // Reported once: the work leaves the roster and the handle retires.
    assert.doesNotMatch(await forkTool.executeHorizon(restarted, owner), /Ada/)
    assert.equal(forkTool.durableLifecycleByname(restarted, owner, 'Ada'), 'Retired')
  } finally {
    await forkTool.disposeRuntime(restarted)
  }

  // A further process on the same directory does not report the void again.
  const third = await forkTool.createRuntime(directory, [{ sessionId: owner, agent: 'manager' }])

  try {
    const thirdJoinPromise = forkTool.executeJoin(third, owner)
    let timer
    const thirdTimeout = new Promise((_, reject) => { timer = setTimeout(() => reject(new Error('JOIN_HANG_DETECTED')), 1500) })
    const thirdJoin = await Promise.race([thirdJoinPromise, thirdTimeout]).finally(() => clearTimeout(timer))
    assert.match(thirdJoin, /NothingToJoin|无可等待|没有|nothing away to receive/i)
    assert.doesNotMatch(await forkTool.executeHorizon(third, owner), /Ada/)
  } finally {
    await forkTool.disposeRuntime(third)
    rmSync(directory, { recursive: true, force: true })
  }
})

test.todo('WHAT[participant-horizon-011] Join delivers the abandoned consequence and retires the handle; real scenario stalls at Join after successful cancellation, root cause still under investigation (GAP-080)')
test.todo('WHAT[participant-horizon-011] latest durable record selection and corrupt-latest rejection must exercise journal/blob reads; a separately implemented HorizonSurface renderer is insufficient (GAP-079)')

test('WHAT[participant-horizon-011] the same roster reaches a program as this.horizon data with the four numbers withheld when no delegated estimate exists', async () => {
  await withForkRuntime(async ({ runtime, owner }) => {
    await placeEngineer(runtime, owner, 'Ada')
    const receipt = await forkTool.executeHorizonReceipt(runtime, owner)

    assert.equal(receipt.ok, true, 'the roster crosses as a data envelope')
    assert.ok(Array.isArray(receipt.agents), 'the envelope carries the agents roster array')
    assert.ok(Array.isArray(receipt.ptys), 'the envelope carries the ptys roster array')

    // The runtime-bound fixed DevOps may share this roster (participant-horizon-010),
    // so the accepted child is located by her own Byname instead of by position.
    const participant = receipt.agents.find((row) => row.label === 'Ada')
    assert.ok(participant, 'the accepted child carries a row under her Byname')
    assert.equal(participant.status, 'still-away')
    assert.ok(
      ['latest', 'none', 'unavailable'].includes(participant.workState),
      `unexpected workState ${participant.workState}`,
    )
    assert.equal(typeof participant.record, 'string')
    // No delegated-tool estimate exists for this child, so the four calibration
    // numbers are 0 rather than guessed from a neighbouring participant.
    assert.equal(participant.turnsUsed, 0)
    assert.equal(participant.turnsLeft, 0)
    assert.equal(participant.timeUsedMs, 0)
    assert.equal(participant.timeLeftMs, 0)
  })
})
