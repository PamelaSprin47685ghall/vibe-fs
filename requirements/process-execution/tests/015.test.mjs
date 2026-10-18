import assert from 'node:assert/strict'
import test from 'node:test'
import { parse } from 'smol-toml'
import { run, nodeCommand } from './support/executor.mjs'
import { mkdtempSync, existsSync, rmSync } from 'node:fs'
import { join } from 'node:path'
import { tmpdir } from 'node:os'
import { refreshGlobalLanguage } from '../../../dist/Participant/Provider/LanguageSurface.js'
import * as output from '../../../dist/OpenCode/Tools/ExecutorToolSurface.js'
import childProcess from 'node:child_process'
import { syncBuiltinESMExports } from 'node:module'
import { registerBounded } from '../../../dist/OpenCode/Codec/ToolHostSurface.js'

process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'

for (const budget of [1, 3, 4, 7, 16, 64]) {
  test(`WHAT[process-execution-015] raw UTF-8 tail fits ${budget} bytes without splitting a code point`, () => {
    const original = 'prefix-' + '中文🔥'.repeat(40)
    const tail = output.retainOutputTail(budget, original)
    assert.equal(tail.includes('\uFFFD'), false)
    assert.ok(Buffer.byteLength(tail, 'utf8') <= budget)
    assert.ok(original.endsWith(tail))
    assert.equal(new TextDecoder('utf-8', { fatal: true }).decode(Buffer.from(tail)), tail)
  })
}

for (const [name, text, budget] of [
  ['line boundary', '\n'.repeat(1996), 2039],
  ['line overflow', '\n'.repeat(2500), 65536],
  ['byte overflow', 'x'.repeat(70000), 65536],
]) {
  test(`WHAT[process-execution-015] actual Host registration preserves the complete ${name} result`, async () => {
    const inner = await run({ command: nodeCommand(`require('node:fs').writeFileSync(1, ${JSON.stringify(text)}); process.exit(7)`), output_budget_bytes: budget })
    const definition = registerBounded({ tool: value => value }, 'run', 'bounded execution', async () => inner)
    const wire = await definition.execute({}, { sessionID: `registered-budget-${name}` })
    assert.equal(wire, inner, 'the actual registration boundary must leave the complete result unchanged')
    assert.ok(Buffer.byteLength(wire) <= budget)
    assert.ok(Buffer.byteLength(wire) <= 51200)
    assert.ok(wire.split('\n').length <= 2000)
    const result = parse(wire)
    assert.equal(result.exit_code, 7)
    assert.equal(result.output_truncated, true)
    assert.ok(result.output.length > 0)
    assert.ok(text.endsWith(result.output))
  })
}

test('WHAT[process-execution-015] the complete visible result including metadata fits the explicit budget', async () => {
  const budget = 512
  const wire = await run({ command: nodeCommand("process.stdout.write('x'.repeat(2000))"), output_budget_bytes: budget })
  assert.equal(parse(wire).exit_code, 0)
  assert.ok(Buffer.byteLength(wire, 'utf8') <= budget, `complete result is ${Buffer.byteLength(wire)} bytes for ${budget}`)
})

for (const language of ['en', 'zh-CN']) {
  test(`WHAT[process-execution-015] ${language} minimum budget rejects before effects and admits its exact boundary`, async () => {
    const directory = mkdtempSync(join(tmpdir(), 'wxs-wire-budget-'))
    const marker = join(directory, 'started')
    const originalSpawn = childProcess.spawn
    const originalBunSpawn = globalThis.Bun?.spawn
    const originalBunSetup = globalThis.Bun?.spawnSync
    let launches = 0
    childProcess.spawn = (...args) => {
      launches++
      return originalSpawn(...args)
    }
    if (originalBunSpawn) {
      globalThis.Bun.spawn = (...args) => {
        launches++
        return originalBunSpawn(...args)
      }
    }
    if (originalBunSetup) {
      globalThis.Bun.spawnSync = (...args) => {
        launches++
        return originalBunSetup(...args)
      }
    }
    syncBuiltinESMExports()
    process.env.WANXIANGSHU_PROVIDER_LANGUAGE = language
    refreshGlobalLanguage()
    const context = { sessionID: `budget-boundary-${language}` }
    const command = nodeCommand(`require('node:fs').writeFileSync(${JSON.stringify(marker)}, 'once'); require('node:fs').writeFileSync(1, 'x'.repeat(2000)); process.exit(7)`)
    try {
      const refusal = parse(await run({ command, output_budget_bytes: 0, world_lock: true }, { context }))
      assert.equal(refusal.executed, false)
      assert.equal(refusal.rejected, 'output_budget_too_small')
      const minimum = refusal.minimum_output_budget_bytes
      assert.ok(Number.isInteger(minimum) && minimum > 0)
      assert.equal(existsSync(marker), false)
      assert.equal(launches, 0)
      const below = parse(await run({ command, output_budget_bytes: minimum - 1 }, { context }))
      assert.equal(below.executed, false)
      assert.equal(existsSync(marker), false)
      assert.equal(launches, 0)
      for (const budget of [minimum, minimum + 1, 512, 549]) {
        assert.ok(budget >= minimum)
        const wire = await run({ command, output_budget_bytes: budget }, { context })
        assert.equal(parse(wire).exit_code, 7)
        assert.equal(existsSync(marker), true)
        assert.ok(Buffer.byteLength(wire) <= budget)
      }
      assert.ok(launches > 0, 'the observer must see the real accepted launch')
      const timeout = await run({ command: 'sleep 5', deadline_seconds: 0.01, output_budget_bytes: minimum }, { context })
      assert.ok(Buffer.byteLength(timeout) <= minimum)
      assert.equal(parse(timeout).stop_requested, 'deadline')
      assert.equal(parse(timeout).deadline_ms, 10)
      const signalWire = await run({ command: `exec ${nodeCommand("process.kill(process.pid, 'SIGTERM')")}`, output_budget_bytes: minimum }, { context })
      assert.equal(parse(signalWire).signal, 'SIGTERM')
      assert.equal(parse(signalWire).exit_code, undefined)
      assert.ok(Buffer.byteLength(signalWire) <= minimum)
      const controller = new AbortController()
      controller.abort()
      const beforeCancellation = launches
      const cancelled = await run({ command, output_budget_bytes: minimum }, { context: { ...context, signal: controller.signal } })
      assert.equal(launches, beforeCancellation)
      assert.ok(Buffer.byteLength(cancelled) <= minimum)
      assert.equal(parse(cancelled).not_started, 'cancelled_before_start')
      assert.doesNotMatch(cancelled, /command stopped|command 在完成前已停止/)
    } finally {
      childProcess.spawn = originalSpawn
      if (originalBunSpawn) globalThis.Bun.spawn = originalBunSpawn
      if (originalBunSetup) globalThis.Bun.spawnSync = originalBunSetup
      syncBuiltinESMExports()
      process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'
      refreshGlobalLanguage()
      rmSync(directory, { recursive: true, force: true })
    }
  })
}

test('WHAT[process-execution-015] an uncertain worker start keeps uncertainty through real registration and bounded diagnostics', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-uncertain-start-'))
  const marker = join(directory, 'must-not-run')
  const originalSpawn = childProcess.spawn
  const originalBunSpawn = globalThis.Bun?.spawn
  let blockedStarts = 0
  const block = () => {
    blockedStarts++
    throw new Error('Controlled uncertain worker start: ' + '\0中文'.repeat(500))
  }
  try {
    childProcess.spawn = block
    if (originalBunSpawn) globalThis.Bun.spawn = block
    syncBuiltinESMExports()
    const wire = await run({ command: nodeCommand(`require('node:fs').writeFileSync(${JSON.stringify(marker)}, 'unexpected')`), output_budget_bytes: 512 })
    const definition = registerBounded({ tool: value => value }, 'run', 'uncertain worker start', async () => wire)
    const registered = await definition.execute({}, { sessionID: 'uncertain-start' })
    assert.equal(registered, wire)
    const result = parse(registered)
    assert.ok(blockedStarts > 0)
    assert.equal(existsSync(marker), false)
    assert.equal(result.executed, undefined)
    assert.equal(result.execution_unconfirmed, true)
    assert.equal(result.end_unconfirmed, true)
    assert.equal(result.output_complete, false)
    assert.equal(result.fault_present, true)
    assert.equal(result.diagnostic_truncated, true)
    assert.ok(Buffer.byteLength(registered) <= 512)
  } finally {
    childProcess.spawn = originalSpawn
    if (originalBunSpawn) globalThis.Bun.spawn = originalBunSpawn
    syncBuiltinESMExports()
    rmSync(directory, { recursive: true, force: true })
  }
})

for (const [name, text] of [
  ['NUL and controls', '\0\x01\x7f\r\n'.repeat(100)],
  ['quote escape transitions', "'''\"\\\n".repeat(100)],
  ['multibyte and CRLF', '中文🔥\r\n'.repeat(100)],
  ['leading BOM', '\uFEFFbody'],
  ['empty', ''],
]) {
  test(`WHAT[process-execution-015] complete encoded ${name} remains bounded and preserves a raw suffix`, async () => {
    const budget = 512
    const wire = await run({ command: nodeCommand(`process.stdout.write(${JSON.stringify(text)})`), output_budget_bytes: budget })
    const result = parse(wire)
    assert.equal(result.exit_code, 0)
    assert.ok(Buffer.byteLength(wire) <= budget)
    if (result.output_truncated) {
      assert.ok(result.output.length > 0, 'the admitted budget can retain at least one complete code point')
      assert.ok(text.endsWith(result.output))
      assert.equal(result.output.includes('\uFFFD'), false)
    } else {
      assert.equal(result.stdout, text)
    }
  })
}
