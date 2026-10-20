import assert from 'node:assert/strict'
import test from 'node:test'
import { sandbox, runProgram, caseName, parseData, nodeCommand } from './support/executor.mjs'
import * as toolResultBound from '../../../dist/Host/Contract/ToolResultBound.js'
import { oneCommand } from './support/runnable.mjs'
import { program } from './support/executor.mjs'
import { parse } from 'smol-toml'
import { refreshGlobalLanguage } from '../../../dist/Participant/Provider/LanguageSurface.js'

// Host-native `run` tool is retired; `js.call` supersedes it (repository-programming [050]-[052]).

test('WHAT[process-execution-013] js.call bounds large output deterministically to head+tail without model distillation', async () => {
  const { dir, cleanup } = sandbox('wxs-proc-013a-')
  try {
    const cmd1 = nodeCommand(`process.stdout.write('HEAD-MARK-' + 'a'.repeat(200000))`)
    const cmd2 = nodeCommand(`process.stdout.write('b'.repeat(200000) + '-TAIL-MARK')`)
    const outcome = await runProgram(
      dir,
      `    const r = await js.call(class extends js.Callable {
      maxDuration = 10000;
      interval = 100;
      next(ctx) {
        if (ctx.step === 0) return ${JSON.stringify(cmd1)};
        if (ctx.step === 1) return ${JSON.stringify(cmd2)};
        return null;
      }
      watch(ctx) { return ctx.remainingMs > 0; }
      sanity() { return { nextDone: () => [{ step: 2 }], watchVeto: () => [{ remainingMs: 0 }] }; }
    });
    return {
      exitCode: r.exitCode,
      truncated: r.truncated,
      bytes: r.bytes,
      keptLength: r.stdout.length,
      startsWithHead: r.stdout.startsWith("HEAD-MARK-"),
      endsWithTail: r.stdout.endsWith("-TAIL-MARK"),
    };`,
    )
    assert.equal(caseName(outcome), 'Succeeded')
    const parsed = parseData(outcome)
    assert.equal(parsed.exitCode, 0)
    assert.equal(parsed.truncated, true)
    assert.equal(parsed.bytes, 10 + 200000 + 200000 + 10)
    assert.ok(parsed.keptLength > 0 && parsed.keptLength <= toolResultBound.HostMaxBytes())
    assert.equal(parsed.startsWithHead, true)
    assert.equal(parsed.endsWithTail, true)
  } finally {
    cleanup()
  }
})

test('WHAT[process-execution-013] small output preserves separate stdout and stderr in onExit without truncation', async () => {
  const { dir, cleanup } = sandbox('wxs-proc-013b-')
  try {
    const cmd = nodeCommand("process.stdout.write('hello'); process.stderr.write('warning')")
    const outcome = await runProgram(
      dir,
      `    let exited = null;
    const r = await js.call(class extends js.Callable {
      maxDuration = 5000;
      interval = 50;
      next(ctx) { return ctx.step === 0 ? ${JSON.stringify(cmd)} : null; }
      watch(ctx) { return ctx.remainingMs > 0; }
      sanity() { return { nextDone: () => [{ step: 1 }], watchVeto: () => [{ remainingMs: 0 }] }; }
      onExit(res) { exited = { stdout: res.stdout, stderr: res.stderr }; }
    });
    return { exitCode: r.exitCode, truncated: r.truncated, stdout: exited.stdout, stderr: exited.stderr };`,
    )
    assert.equal(caseName(outcome), 'Succeeded')
    const parsed = parseData(outcome)
    assert.equal(parsed.exitCode, 0)
    assert.equal(parsed.truncated, false)
    assert.equal(parsed.stdout, 'hello')
    assert.equal(parsed.stderr, 'warning')
  } finally {
    cleanup()
  }
})

test('WHAT[process-execution-013] ToolResultBound keeps deterministic raw tail when output exceeds host window', () => {
  const original = 'EARLY-ERROR-\n' + 'x'.repeat(60000) + '\nLATEST-RAW-OUTPUT'
  const bounded = toolResultBound.bound(original)
  assert.ok(bounded.startsWith(toolResultBound.Marker))
  assert.ok(bounded.endsWith('\nLATEST-RAW-OUTPUT'))
  assert.equal(toolResultBound.fitsInWindow(bounded), true)
})

test('WHAT[process-execution-013] the actual registered tool returns bilingual bounded raw data without a model or Host re-truncation', async () => {
  const { withExecutablePlugin, acceptAuthorityRoot } = await import('../../verification-system/tests/support/plugin-fixture.mjs')
  const previous = process.env.WANXIANGSHU_PROVIDER_LANGUAGE
  try {
    await withExecutablePlugin(async (hooks, directory, created, runtime) => {
      const sessionID = 'registered-large-output'
      await acceptAuthorityRoot(runtime, sessionID, 'devops', 'large-output-user')
      await hooks['chat.message']({ sessionID, messageID: 'large-output-user', agent: 'devops' }, {
        message: { id: 'large-output-user', role: 'user', agent: 'devops' },
        parts: [{ type: 'text', text: 'Check raw process output.' }],
      })
      for (const language of ['en', 'zh-CN']) {
        process.env.WANXIANGSHU_PROVIDER_LANGUAGE = language
        refreshGlobalLanguage()
        const beforeCreated = created.length
        const beforePrompts = runtime.prompts.length
        const command = nodeCommand("process.stdout.write('RAW-HEAD-' + '中文🔥'.repeat(40000) + '-RAW-TAIL'); process.stderr.write('诊断😀'.repeat(30000)); process.exitCode = 7")
        const wire = await hooks.tool['todowrite-for-devops'].execute({
          program: program(`return await js.call(${oneCommand(command, { fields: 'outputBudgetBytes = 2048; worldLock = true;' })});`),
        }, { sessionID, agent: 'devops', messageID: 'large-output-assistant', callID: `large-${language}` })
        const result = parse(wire).data
        assert.equal(result.status, 'completed', wire)
        assert.equal(result.exitCode, 7)
        assert.equal(result.scopeClosed, true)
        assert.equal(result.outputComplete, true)
        assert.equal(result.truncated, true)
        assert.equal(result.stdout.startsWith('RAW-HEAD-'), true)
        assert.equal(result.stdout.endsWith('-RAW-TAIL'), true)
        assert.match(result.outputNotice, language === 'en' ? /head and tail/ : /头部和尾部/)
        assert.equal(wire.includes('\uFFFD'), false)
        assert.ok(Buffer.byteLength(wire) <= 2048)
        assert.equal(toolResultBound.fitsInWindow(wire), true)
        assert.deepEqual(wire.split('\n').filter(line => line.startsWith('#')), ['# ok'])
        assert.equal(created.length, beforeCreated)
        assert.equal(runtime.prompts.length, beforePrompts)
      }
    })
  } finally {
    if (previous === undefined) delete process.env.WANXIANGSHU_PROVIDER_LANGUAGE
    else process.env.WANXIANGSHU_PROVIDER_LANGUAGE = previous
    refreshGlobalLanguage()
  }
})
