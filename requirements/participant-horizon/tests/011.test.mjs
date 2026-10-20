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

test('WHAT[participant-horizon-011] a restarted process does not present the previous process abandoned child', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-horizon-restart-abandoned-'))
  const owner = 'manager-horizon-restart-abandoned'
  const first = await forkTool.createRuntime(directory, [{ sessionId: owner, agent: 'manager' }])

  try {
    await placeEngineer(first, owner, 'Ada')
    await forkTool.cancelOwnerChildren(first, owner)
    assert.match(await forkTool.executeHorizon(first, owner), /Ada.*did not return/)
  } finally {
    forkTool.disposeRuntime(first)
  }

  const restarted = await forkTool.createRuntime(directory, [{ sessionId: owner, agent: 'manager' }])

  try {
    const horizon = await forkTool.executeHorizon(restarted, owner)
    assert.doesNotMatch(horizon, /Ada/)
  } finally {
    forkTool.disposeRuntime(restarted)
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
