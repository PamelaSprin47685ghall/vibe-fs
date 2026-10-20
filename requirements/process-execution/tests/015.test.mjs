import assert from 'node:assert/strict'
import test from 'node:test'
import * as toolResultBound from '../../../dist/Host/Contract/ToolResultBound.js'
import { sandbox, runProgram, caseName, render, nodeCommand } from './support/executor.mjs'
import { oneCommand } from './support/runnable.mjs'
import { runWithSpoolFault } from './support/spool-fault.mjs'
import { existsSync } from 'node:fs'
import { join } from 'node:path'
import { parse } from 'smol-toml'
import { refreshGlobalLanguage } from '../../../dist/Participant/Provider/LanguageSurface.js'

// Host-native `run` tool is retired; `js.run` supersedes it (repository-programming [050]-[052])
// and complete provider results are bounded to the UTF-8 window by ToolResultBound.

for (const offset of [0, 1, 2, 3, 4, 7]) {
  test(`WHAT[process-execution-015] UTF-8 tail bound at alignment offset ${offset} fits HostMaxBytes without splitting a code point`, () => {
    const original = 'a'.repeat(offset) + '中文🔥'.repeat(6000)
    assert.ok(Buffer.byteLength(original, 'utf8') > toolResultBound.HostMaxBytes())
    const bounded = toolResultBound.bound(original)
    assert.ok(bounded.startsWith(toolResultBound.Marker))
    const tail = bounded.slice(toolResultBound.Marker.length)
    assert.equal(tail.includes('\uFFFD'), false)
    assert.ok(Buffer.byteLength(tail, 'utf8') <= toolResultBound.ContentMaxBytes())
    assert.ok(Buffer.byteLength(bounded, 'utf8') <= toolResultBound.HostMaxBytes())
    assert.ok(original.endsWith(tail))
    assert.equal(new TextDecoder('utf-8', { fatal: true }).decode(Buffer.from(bounded)), bounded)
    assert.equal(toolResultBound.fitsInWindow(bounded), true)
  })
}

test('WHAT[process-execution-015] the complete visible result fits the Host window before any outer truncation', async () => {
  const { dir, cleanup } = sandbox('wxs-proc-015-')
  try {
    const cmd = nodeCommand("process.stdout.write(('中文🔥line\\n').repeat(3000))")
    const outcome = await runProgram(
      dir,
      `    return await js.run(class extends js.Runnable {
      maxDuration = 5000;
      interval = 50;
      next(ctx) { return ctx.step === 0 ? ${JSON.stringify(cmd)} : null; }
      watch(ctx) { return ctx.remainingMs > 0; }
      sanity() { return { nextDone: () => this.next({ step: 1 }), watchVeto: () => this.watch({ remainingMs: 0 }) }; }
    });`,
    )
    assert.equal(caseName(outcome), 'Succeeded')
    const wire = render(outcome)
    assert.ok(Buffer.byteLength(wire, 'utf8') <= toolResultBound.HostMaxBytes())
    assert.ok(wire.split('\n').length <= toolResultBound.HostMaxLines())
    assert.equal(toolResultBound.fitsInWindow(wire), true)
  } finally {
    cleanup()
  }
})

test('WHAT[process-execution-015] a lowered Host ceiling refuses before spawn and a usable ceiling bounds the complete wire', async () => {
  const { dir, cleanup } = sandbox('wxs-host-ceiling-015-')
  const marker = join(dir, 'started')
  const command = nodeCommand(`require('node:fs').writeFileSync(${JSON.stringify(marker)}, 'started'); process.stdout.write('中文😀'.repeat(10000))`)
  try {
    for (const [lines, bytes] of [[2000, 128], [3, 51200]]) {
      toolResultBound.setHostLimits(lines, bytes)
      const refused = await runProgram(dir, `return await js.run(${oneCommand(command)});`)
      assert.equal(caseName(refused), 'Failed', render(refused))
      assert.equal(existsSync(marker), false, 'unrepresentable results must refuse before effects')
    }
    toolResultBound.setHostLimits(2000, 4096)
    const result = await runProgram(dir, `return await js.run(${oneCommand(command)});`)
    assert.equal(caseName(result), 'Succeeded', render(result))
    assert.equal(existsSync(marker), true)
    assert.ok(Buffer.byteLength(render(result)) <= 4096)
    assert.equal(parse(render(result)).data.truncated, true)
  } finally {
    toolResultBound.clearHostLimitsForTests()
    cleanup()
  }
})

test('WHAT[process-execution-015] the encoded minimum rejects before execution and admits actual normal, signal, deadline and capture fault facts', async () => {
  const { dir, cleanup } = sandbox('wxs-run-minimum-015-')
  const previous = process.env.WANXIANGSHU_PROVIDER_LANGUAGE
  try {
    for (const language of ['en', 'zh-CN']) {
      process.env.WANXIANGSHU_PROVIDER_LANGUAGE = language
      refreshGlobalLanguage()
      const marker = join(dir, `started-${language}`)
      const command = nodeCommand(`require('node:fs').writeFileSync(${JSON.stringify(marker)}, 'started')`)
      const refused = await runProgram(dir, `return await js.run(${oneCommand(command, { fields: 'outputBudgetBytes = 0;' })});`)
      const refusal = parse(render(refused))
      assert.equal(caseName(refused), 'Failed')
      assert.equal(refusal.code, 'RUN_OUTPUT_BUDGET_TOO_SMALL')
      const minimum = refusal.minimumOutputBudgetBytes
      assert.ok(Number.isSafeInteger(minimum) && minimum > 0)
      const below = await runProgram(dir, `return await js.run(${oneCommand(command, { fields: `outputBudgetBytes = ${minimum - 1};` })});`)
      assert.equal(parse(render(below)).minimumOutputBudgetBytes, minimum)
      assert.equal(existsSync(marker), false, 'minimum-1 must have zero physical effects')
      const cases = [
        [command, 5000, result => assert.equal(result.exitCode, 0)],
        [`exec ${nodeCommand('process.exit(143)')}`, 5000, result => assert.equal(result.exitCode, 143)],
        [`exec ${nodeCommand("process.kill(process.pid, 'SIGTERM')")}`, 5000, result => assert.equal(result.signal, 'SIGTERM')],
        ['sleep 5', 150, result => {
          assert.equal(result.status, 'timeout')
          assert.equal(result.stopRequested, 'deadline')
          assert.equal(result.outputComplete, false)
        }],
      ]
      for (const [script, maxDuration, verify] of cases) {
        const outcome = await runProgram(dir, `return await js.run(${oneCommand(script, { maxDuration, fields: `outputBudgetBytes = ${minimum};` })});`)
        assert.equal(caseName(outcome), 'Succeeded', render(outcome))
        const wire = render(outcome)
        assert.ok(Buffer.byteLength(wire) <= minimum, wire)
        assert.equal(toolResultBound.fitsInWindow(wire), true)
        verify(parse(wire).data)
      }
      assert.equal(existsSync(marker), true)
      const fault = await runWithSpoolFault({ fields: `outputBudgetBytes = ${minimum};` })
      assert.equal(caseName(fault), 'Succeeded', render(fault))
      const wire = render(fault)
      assert.ok(Buffer.byteLength(wire) <= minimum, wire)
      assert.equal(toolResultBound.fitsInWindow(wire), true)
      assert.equal(parse(wire).data.status, 'failed')
      assert.equal(parse(wire).data.outputComplete, false)
      assert.ok(parse(wire).data.fault)
    }
  } finally {
    if (previous === undefined) delete process.env.WANXIANGSHU_PROVIDER_LANGUAGE
    else process.env.WANXIANGSHU_PROVIDER_LANGUAGE = previous
    refreshGlobalLanguage()
    cleanup()
  }
})

test('WHAT[process-execution-015] a small explicit budget counts escaped Unicode, both streams, metadata and bilingual truncation data', async () => {
  const { dir, cleanup } = sandbox('wxs-run-wire-015-')
  const previous = process.env.WANXIANGSHU_PROVIDER_LANGUAGE
  const head = '\uFEFFHEAD中文🔥\r\n'
  const tail = '\r\nTAIL🔥中文'
  const command = nodeCommand(`process.stdout.write(${JSON.stringify(head)} + ('"\\\\中文🔥\\r\\n').repeat(2000) + ${JSON.stringify(tail)}); process.stderr.write('诊断😀'.repeat(1000)); process.exitCode = 7`)
  try {
    for (const language of ['en', 'zh-CN']) {
      process.env.WANXIANGSHU_PROVIDER_LANGUAGE = language
      refreshGlobalLanguage()
      const outcome = await runProgram(dir, `return await js.run(${oneCommand(command, { fields: 'outputBudgetBytes = 2048;' })});`)
      assert.equal(caseName(outcome), 'Succeeded', render(outcome))
      const wire = render(outcome)
      const result = parse(wire).data
      assert.ok(Buffer.byteLength(wire) <= 2048)
      assert.equal(toolResultBound.fitsInWindow(wire), true)
      assert.equal(result.exitCode, 7)
      assert.equal(result.truncated, true)
      assert.equal(result.stdout.startsWith(head), true)
      assert.equal(result.stdout.endsWith(tail), true)
      assert.equal((result.stdout + result.stderr).includes('\uFFFD'), false)
      assert.match(result.outputNotice, language === 'en' ? /head and tail/ : /头部和尾部/)
      assert.deepEqual(wire.split('\n').filter(line => line.startsWith('#')), ['# ok'])
    }
  } finally {
    if (previous === undefined) delete process.env.WANXIANGSHU_PROVIDER_LANGUAGE
    else process.env.WANXIANGSHU_PROVIDER_LANGUAGE = previous
    refreshGlobalLanguage()
    cleanup()
  }
})

test('WHAT[process-execution-015] returning onExit observation cannot bypass the run byte budget', async () => {
  const { dir, cleanup } = sandbox('wxs-on-exit-budget-015-')
  try {
    const command = nodeCommand("process.stdout.write('中文🔥'.repeat(6000)); process.exitCode = 7")
    const runnable = oneCommand(command, { fields: 'outputBudgetBytes = 2048;', onExit: 'onExit(result) { observed = result; }' })
    const outcome = await runProgram(dir, `let observed; await js.run(${runnable}); return observed;`)
    assert.equal(caseName(outcome), 'Succeeded')
    const wire = render(outcome)
    assert.ok(Buffer.byteLength(wire) <= 2048, 'callback observations must use the same complete encoding budget')
    assert.equal(parse(wire).data.exitCode, 7)
  } finally {
    cleanup()
  }
})

test('WHAT[process-execution-015] returning one watch observation cannot bypass the run byte budget', async () => {
  const { dir, cleanup } = sandbox('wxs-watch-budget-015-')
  try {
    const command = nodeCommand("process.stdout.write('中文🔥'.repeat(6000)); setInterval(() => {}, 1000)")
    const outcome = await runProgram(dir, `let observed;
      await js.run(class extends js.Runnable {
        maxDuration = 2000;
        interval = 25;
        outputBudgetBytes = 2048;
        next(ctx) { return ctx.step === 0 ? ${JSON.stringify(command)} : null; }
        watch(ctx) {
          if (ctx.stdout) { observed = ctx; return { abort: 'observation captured' }; }
          return ctx.remainingMs > 0;
        }
        sanity() { return { nextDone: () => this.next({ step: 1 }), watchVeto: () => this.watch({ remainingMs: 0 }) }; }
      });
      return observed;`)
    assert.equal(caseName(outcome), 'Succeeded')
    const wire = render(outcome)
    assert.ok(Buffer.byteLength(wire) <= 2048)
    assert.equal(parse(wire).data.exited, false)
  } finally {
    cleanup()
  }
})
