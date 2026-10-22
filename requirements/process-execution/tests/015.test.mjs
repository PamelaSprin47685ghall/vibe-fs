import assert from 'node:assert/strict'
import test from 'node:test'
import * as toolResultBound from '../../../dist/Host/Contract/ToolResultBound.js'
import { sandbox, runProgram, caseName, render, nodeCommand, execute } from './support/executor.mjs'
import { oneCommand } from './support/runnable.mjs'
import { runWithSpoolFault } from './support/spool-fault.mjs'
import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { parse } from 'smol-toml'
import { refreshGlobalLanguage } from '../../../dist/Participant/Provider/LanguageSurface.js'
import { parkedHandle, parkedPayload } from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'
import { setTimeout as pause } from 'node:timers/promises'
import { spawnSync } from 'node:child_process'

// Host-native `run` tool is retired; `js.call` supersedes it (repository-programming [050]-[052])
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
      `    return await this.call(class extends this.Callable {
      maxDuration = 5000;
      interval = 50;
      next(ctx) { return ctx.step === 0 ? ${JSON.stringify(cmd)} : null; }
      watch(ctx) { return ctx.remainingMs > 0; }
      sanity() { return { nextDone: () => [{ step: 1 }], watchVeto: () => [{ remainingMs: 0 }] }; }
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
      const refused = await runProgram(dir, `return await this.call(${oneCommand(command)});`)
      assert.equal(caseName(refused), 'Failed', render(refused))
      assert.equal(existsSync(marker), false, 'unrepresentable results must refuse before effects')
    }
    toolResultBound.setHostLimits(2000, 4096)
    const result = await runProgram(dir, `return await this.call(${oneCommand(command)});`)
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
      const refused = await runProgram(dir, `return await this.call(${oneCommand(command, { fields: 'outputBudgetBytes = 0;' })});`)
      const refusal = parse(render(refused))
      assert.equal(caseName(refused), 'Failed')
      assert.equal(refusal.code, 'CALL_OUTPUT_BUDGET_TOO_SMALL')
      const minimum = refusal.minimumOutputBudgetBytes
      assert.ok(Number.isSafeInteger(minimum) && minimum > 0)
      const below = await runProgram(dir, `return await this.call(${oneCommand(command, { fields: `outputBudgetBytes = ${minimum - 1};` })});`)
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
        const outcome = await runProgram(dir, `return await this.call(${oneCommand(script, { maxDuration, fields: `outputBudgetBytes = ${minimum};` })});`)
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
      const outcome = await runProgram(dir, `return await this.call(${oneCommand(command, { fields: 'outputBudgetBytes = 2048;' })});`)
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
    const outcome = await runProgram(dir, `let observed; await this.call(${runnable}); return observed;`)
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
      await this.call(class extends this.Callable {
        maxDuration = 2000;
        interval = 25;
        outputBudgetBytes = 2048;
        next(ctx) { return ctx.step === 0 ? ${JSON.stringify(command)} : null; }
        watch(ctx) {
          if (ctx.stdout) { observed = ctx; return { abort: 'observation captured' }; }
          return ctx.remainingMs > 0;
        }
        sanity() { return { nextDone: () => [{ step: 1 }], watchVeto: () => [{ remainingMs: 0 }] }; }
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

test('WHAT[process-execution-015] a string call preserves both UTF-8 output tails and the complete encoded Host budget', async () => {
  const { dir, cleanup } = sandbox('wxs-direct-output-015-')
  const stdout = '\uFEFFHEAD中文🔥\n' + '中文🔥'.repeat(40000) + '\nSTDOUT-TAIL🔥'
  const stderr = 'ERROR-HEAD中文\n' + '诊断😀'.repeat(20000) + '\nSTDERR-TAIL😀'
  const command = `exec ${nodeCommand("process.stdout.write('\\uFEFFHEAD中文🔥\\n' + '中文🔥'.repeat(40000) + '\\nSTDOUT-TAIL🔥'); process.stderr.write('ERROR-HEAD中文\\n' + '诊断😀'.repeat(20000) + '\\nSTDERR-TAIL😀'); process.exitCode = 7")}`
  try {
    toolResultBound.setHostLimits(2000, 4096)
    const outcome = await runProgram(dir, `return await this.call(${JSON.stringify(command)}, "Inspect the actual output before choosing the next command.");`)
    assert.equal(caseName(outcome), 'Succeeded', render(outcome))
    const wire = render(outcome)
    const result = parse(wire).data
    assert.equal(result.exitCode, 7)
    assert.equal(result.scopeClosed, true)
    assert.equal(result.endUnconfirmed, false)
    assert.equal(result.outputComplete, true)
    assert.equal(result.bytes, Buffer.byteLength(stdout) + Buffer.byteLength(stderr))
    assert.equal(result.truncated, true)
    assert.equal(result.stdout.startsWith('\uFEFFHEAD中文🔥\n'), true)
    assert.equal(result.stdout.endsWith('\nSTDOUT-TAIL🔥'), true)
    assert.equal(result.stderr.endsWith('\nSTDERR-TAIL😀'), true)
    assert.equal((result.stdout + result.stderr).includes('\uFFFD'), false)
    assert.ok(Buffer.byteLength(wire) <= 4096)
    assert.equal(toolResultBound.fitsInWindow(wire), true)
  } finally {
    toolResultBound.clearHostLimitsForTests()
    cleanup()
  }
})

test('WHAT[process-execution-015] a string call reports the actual termination signal without inventing exit zero', async () => {
  const { dir, cleanup } = sandbox('wxs-direct-signal-015-')
  try {
    const command = `exec ${nodeCommand("process.kill(process.pid, 'SIGTERM')")}`
    const outcome = await runProgram(dir, `return await this.call(${JSON.stringify(command)}, "Inspect the actual target termination before retrying.");`)
    assert.equal(caseName(outcome), 'Succeeded', render(outcome))
    const result = parse(render(outcome)).data
    assert.equal(result.signal, 'SIGTERM')
    assert.equal(result.exitCode ?? null, null)
    assert.equal(result.status, 'signalled')
    assert.equal(result.scopeClosed, true)
    assert.equal(result.endUnconfirmed, false)
  } finally {
    cleanup()
  }
})

const waitForFile = async (path) => {
  const deadline = performance.now() + 3000
  while (!existsSync(path) && performance.now() < deadline) await pause(10)
  assert.equal(existsSync(path), true, `the actual process must publish ${path}`)
}

test('WHAT[process-execution-015] a yielded string call returns only the output after its authorized delta', async () => {
  const { dir, cleanup } = sandbox('wxs-direct-delta-015-')
  const release = join(dir, 'release')
  const ready = join(dir, 'ready')
  let handle
  try {
    const command = `exec ${nodeCommand(`const fs = require('node:fs'); process.stdout.write('FIRST-OUT'); process.stderr.write('FIRST-ERR'); fs.writeFileSync(${JSON.stringify(ready)}, String(process.pid)); const timer = setInterval(() => { if (fs.existsSync(${JSON.stringify(release)})) { clearInterval(timer); process.stdout.write('LAST-OUT'); process.stderr.write('LAST-ERR'); } }, 10)` )}`
    const first = await runProgram(dir, `return await this.call(${JSON.stringify(command)}, "Inspect this running command before continuing.");`, 'direct-delta-015')
    assert.equal(caseName(first), 'Parked', render(first))
    handle = parkedHandle(first)
    await waitForFile(ready)
    const delta = parkedPayload(first).delta
    assert.equal(delta.ok, true)
    assert.equal(delta.stdout, 'FIRST-OUT')
    assert.equal(delta.stderr, 'FIRST-ERR')
    writeFileSync(release, 'finish')
    const resumed = await execute(dir, 'DevOps', 'en', 'direct-delta-015', { unpark: { handle, value: {} } }, 20_000, Date.now() + 120_000, 1 << 20, null)
    handle = caseName(resumed) === 'Parked' ? parkedHandle(resumed) : undefined
    assert.equal(caseName(resumed), 'Succeeded', render(resumed))
    const result = parse(render(resumed)).data
    assert.equal(result.stdout, 'LAST-OUT')
    assert.equal(result.stderr, 'LAST-ERR')
    assert.equal(result.bytes, Buffer.byteLength('LAST-OUTLAST-ERR'))
    assert.equal(result.scopeClosed, true)
  } finally {
    writeFileSync(release, 'finish')
    if (handle) await execute(dir, 'DevOps', 'en', 'direct-delta-015', { abandon: { handle } }, 2000, Date.now() + 5000, 1 << 20, null)
    cleanup()
  }
})

test('WHAT[process-execution-015] registered string call cancellation drains the actual process before owner disposal', async () => {
  const { withExecutablePlugin, acceptAuthorityRoot } = await import('../../verification-system/tests/support/plugin-fixture.mjs')
  await withExecutablePlugin(async (hooks, directory, _children, runtime) => {
    const session = 'direct-cancel-015'
    const physical = 'direct-cancel-user'
    await acceptAuthorityRoot(runtime, session, 'devops', physical)
    const root = { info: { id: physical, sessionID: session, role: 'user', agent: 'devops', model: { providerID: 'provider', modelID: 'devops-model' } }, parts: [{ type: 'text', text: 'Observe actual command cancellation.' }] }
    runtime.pushHostMessage(session, root)
    await hooks['chat.message']({ sessionID: session, messageID: physical, agent: 'devops' }, { message: root.info, parts: root.parts })
    const marker = join(directory, 'direct-pid')
    const command = `exec ${nodeCommand(`require('node:fs').writeFileSync(${JSON.stringify(marker)}, String(process.pid)); setInterval(() => {}, 1000)`)}`
    const input = { program: `class Js extends JsProgram { async run() { return await this.call(${JSON.stringify(command)}, ${JSON.stringify('Inspect the still running target before retrying. '.repeat(20))}); } }` }
    const assistant = { info: { id: 'direct-cancel-run', parentID: physical, sessionID: session, role: 'assistant', agent: 'devops', providerID: 'provider', modelID: 'devops-model', time: { created: 2 } }, parts: [{ id: 'direct-cancel-part', type: 'tool', tool: 'todowrite-for-devops', callID: 'direct-cancel-call', state: { status: 'pending', input } }] }
    runtime.pushHostMessage(session, assistant)
    await hooks.event({ event: { type: 'message.updated', properties: { info: assistant.info } } })
    const controller = new AbortController()
    const pending = hooks.tool['todowrite-for-devops'].execute(input, { sessionID: session, agent: 'devops', messageID: assistant.info.id, callID: 'direct-cancel-call', abort: controller.signal })
    try {
      await waitForFile(marker)
      const pid = Number(readFileSync(marker, 'utf8'))
      controller.abort()
      await pending
      await hooks.dispose()
      assert.throws(() => process.kill(pid, 0), { code: 'ESRCH' })
    } finally {
      controller.abort()
      await pending.catch(() => {})
    }
  })
})

test('WHAT[process-execution-015] concurrent string calls send and signal only the call whose timer actually yielded', async () => {
  const { dir, cleanup } = sandbox('wxs-direct-target-015-')
  const ready = join(dir, 'winner-ready')
  const laterReady = join(dir, 'later-ready')
  const release = join(dir, 'release-control')
  const received = join(dir, 'winner-input')
  const wrong = join(dir, 'later-input')
  const finish = join(dir, 'finish-later')
  const longPlan = 'Inspect the actual process before choosing another action. '.repeat(20)
  const winner = `exec ${nodeCommand(`const fs = require('node:fs'); process.stdin.on('data', data => fs.writeFileSync(${JSON.stringify(received)}, data)); fs.writeFileSync(${JSON.stringify(ready)}, String(process.pid)); setInterval(() => {}, 1000)`)}`
  const later = `exec ${nodeCommand(`const fs = require('node:fs'); process.stdin.on('data', data => fs.writeFileSync(${JSON.stringify(wrong)}, data)); fs.writeFileSync(${JSON.stringify(laterReady)}, String(process.pid)); const timer = setInterval(() => { if (fs.existsSync(${JSON.stringify(finish)})) { clearInterval(timer); process.stdin.destroy(); } }, 10)`)}`
  const waitFor = path => `exec ${nodeCommand(`const fs = require('node:fs'); const timer = setInterval(() => { if (fs.existsSync(${JSON.stringify(path)})) clearInterval(timer); }, 10)`)}`
  let handle
  try {
    const outcome = await runProgram(dir, `
      const winner = this.call(${JSON.stringify(winner)}, "A");
      const later = this.call(${JSON.stringify(later)}, ${JSON.stringify(longPlan)});
      const control = this.call(${JSON.stringify(waitFor(release))}, ${JSON.stringify(longPlan)}).then(async () => {
        await this.send("winner-input");
        await this.call(${JSON.stringify(waitFor(received))}, ${JSON.stringify(longPlan)});
        return await this.signal("SIGTERM");
      });
      return { winner: await winner, later: await later, control: await control };
    `, 'direct-target-015')
    assert.equal(caseName(outcome), 'Parked', render(outcome))
    handle = parkedHandle(outcome)
    assert.equal(parkedPayload(outcome).planB, 'A', 'the first call actually owns the automatic park')
    await Promise.all([waitForFile(ready), waitForFile(laterReady)])
    writeFileSync(release, 'control')
    const resuming = execute(dir, 'DevOps', 'en', 'direct-target-015', { unpark: { handle, value: {} } }, 20_000, Date.now() + 120_000, 1 << 20, null)
    resuming.catch(() => {})
    await waitForFile(received)
    assert.equal(readFileSync(received, 'utf8'), 'winner-input\n')
    assert.equal(existsSync(wrong), false, 'the later call never receives the parked call input')
    writeFileSync(finish, 'finish')
    const resumed = await resuming
    handle = caseName(resumed) === 'Parked' ? parkedHandle(resumed) : undefined
    assert.equal(caseName(resumed), 'Succeeded', render(resumed))
    const result = parse(render(resumed)).data
    assert.equal(result.winner.signal, 'SIGTERM')
    assert.equal(result.winner.exitCode ?? null, null)
    assert.equal(result.winner.scopeClosed, true)
    assert.equal(result.later.exitCode, 0)
    assert.equal(result.later.scopeClosed, true)
    assert.equal(result.control.requestedSignal, 'SIGTERM')
  } finally {
    writeFileSync(release, 'control')
    writeFileSync(finish, 'finish')
    if (handle) await execute(dir, 'DevOps', 'en', 'direct-target-015', { abandon: { handle } }, 2000, Date.now() + 5000, 1 << 20, null)
    cleanup()
  }
})

test('WHAT[process-execution-015] an explicit park retains its own resumption while a concurrent string call is running', async () => {
  const { dir, cleanup } = sandbox('wxs-direct-manual-015-')
  const ready = join(dir, 'ready')
  const release = join(dir, 'release')
  const command = `exec ${nodeCommand(`const fs = require('node:fs'); fs.writeFileSync(${JSON.stringify(ready)}, String(process.pid)); const timer = setInterval(() => { if (fs.existsSync(${JSON.stringify(release)})) { clearInterval(timer); process.stdout.write('COMMAND-DONE'); } }, 10)`)}`
  let handle
  try {
    const parked = await runProgram(dir, `
      const running = this.call(${JSON.stringify(command)}, "A");
      const decision = await this.park({ owner: "explicit-park" });
      return { decision, command: await running };
    `, 'direct-manual-015')
    assert.equal(caseName(parked), 'Parked', render(parked))
    handle = parkedHandle(parked)
    assert.deepEqual(parkedPayload(parked), { owner: 'explicit-park' })
    await waitForFile(ready)
    writeFileSync(release, 'finish')
    const resumed = await execute(dir, 'DevOps', 'en', 'direct-manual-015', { unpark: { handle, value: 'EXPLICIT-ANSWER' } }, 20_000, Date.now() + 120_000, 1 << 20, null)
    handle = caseName(resumed) === 'Parked' ? parkedHandle(resumed) : undefined
    assert.equal(caseName(resumed), 'Succeeded', render(resumed))
    const result = parse(render(resumed)).data
    assert.equal(result.decision, 'EXPLICIT-ANSWER')
    assert.equal(result.command.stdout, 'COMMAND-DONE')
    assert.equal(result.command.exitCode, 0)
    assert.equal(result.command.scopeClosed, true)
  } finally {
    writeFileSync(release, 'finish')
    if (handle) await execute(dir, 'DevOps', 'en', 'direct-manual-015', { abandon: { handle } }, 2000, Date.now() + 5000, 1 << 20, null)
    cleanup()
  }
})

for (const exhaustAfterRun of [false, true]) {
  test(`WHAT[process-execution-015] concurrent string calls borrow a 150ms VM remainder and ${exhaustAfterRun ? 'restore its original exhaustion boundary' : 'finish their actual physical commands'}`, () => {
    const result = spawnSync(process.execPath, [join(import.meta.dirname, 'support/direct-deadline-lease.fixture.mjs'), String(exhaustAfterRun)], {
      timeout: 3000,
      encoding: 'utf8',
    })
    assert.equal(result.error, undefined, result.stderr)
    assert.equal(result.signal, null, result.stderr)
    assert.equal(result.status, 0, result.stderr || result.stdout)
  })
}
