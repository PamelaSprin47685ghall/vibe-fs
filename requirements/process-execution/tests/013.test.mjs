import assert from 'node:assert/strict'
import test from 'node:test'
import { parse } from 'smol-toml'
import { run, nodeCommand } from './support/executor.mjs'

process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'

test('WHAT[process-execution-013] real large output retains the exact raw suffix without calling the injected session capability', async () => {
  const latest = 'LATEST_RAW_OUTPUT'
  const original = 'EARLY_ERROR_' + 'x'.repeat(500) + latest
  const accessed = []
  const sessions = new Proxy({}, { get(_target, member) {
    accessed.push(member)
    throw new Error(`Unexpected session capability use: ${String(member)}`)
  } })
  const wire = await run({ command: nodeCommand(`process.stdout.write(${JSON.stringify(original)})`), output_budget_bytes: 512 }, { sessions })
  const result = parse(wire)
  assert.equal(result.exit_code, 0)
  assert.ok(original.endsWith(result.output))
  assert.ok(result.output.endsWith(latest))
  assert.ok(Buffer.byteLength(wire) <= 512)
  assert.equal(result.output_truncated, true)
  assert.deepEqual(accessed, [])
})

test('WHAT[process-execution-013] small output preserves separate stdout and stderr exactly', async () => {
  const result = parse(await run({ command: nodeCommand("process.stdout.write('hello'); process.stderr.write('warning')"), output_budget_bytes: 1024 }))
  assert.equal(result.exit_code, 0)
  assert.equal(result.stdout, 'hello')
  assert.equal(result.stderr, 'warning')
})

test('WHAT[process-execution-013] the registered plugin preserves budgets and creates no model session for small, large and line-limited output', async () => {
  const { withExecutablePlugin, acceptAuthorityRoot } = await import('../../verification-system/tests/support/plugin-fixture.mjs')
  await withExecutablePlugin(async (hooks, _directory, created, runtime) => {
    const sessionID = 'budget-registered-devops'
    const messageID = 'budget-user-root'
    await acceptAuthorityRoot(runtime, sessionID, 'devops', messageID)
    await hooks['chat.message']({ sessionID, messageID, agent: 'devops' }, {
      message: { id: messageID, role: 'user', agent: 'devops' },
      parts: [{ type: 'text', text: 'Check bounded command output.' }],
    })
    const context = { sessionID, agent: 'devops', messageID: 'budget-assistant', callID: 'budget-command' }
    const modelsBefore = created.length
    const promptsBefore = runtime.prompts.length
    for (const [budget, text] of [[0, 'never'], [512, 'ok'], [512, 'x'.repeat(2000)], [65536, '\n'.repeat(2500)]]) {
      const wire = await hooks.tool.run.execute({ command: nodeCommand(`require('node:fs').writeFileSync(1, ${JSON.stringify(text)})`), output_budget_bytes: budget }, context)
      const result = parse(wire)
      if (budget === 0) {
        assert.equal(result.executed, false)
        assert.equal(result.rejected, 'output_budget_too_small')
      } else {
        assert.equal(result.exit_code, 0)
        assert.ok(Buffer.byteLength(wire) <= budget)
        assert.ok(Buffer.byteLength(wire) <= 51200)
        assert.ok(wire.split('\n').length <= 2000)
        if (result.output_truncated) assert.ok(text.endsWith(result.output))
        else assert.equal(result.stdout, text)
      }
      assert.equal(created.length, modelsBefore)
      assert.equal(runtime.prompts.length, promptsBefore)
    }
  })
})
