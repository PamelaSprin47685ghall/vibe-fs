import assert from 'node:assert/strict'
import test from 'node:test'
import * as forkTool from '../../../dist/Execution/Delegation/Fork/OpenCode/ToolSurface.js'
import { placeEngineer, withForkRuntime } from './support/fork-runtime.mjs'

process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'

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

test.todo('WHAT[participant-horizon-011] Join delivers the abandoned consequence and retires the handle; real scenario stalls at Join after successful cancellation, root cause still under investigation (GAP-080)')
test.todo('WHAT[participant-horizon-011] latest durable record selection and corrupt-latest rejection must exercise journal/blob reads; a separately implemented HorizonSurface renderer is insufficient (GAP-079)')
