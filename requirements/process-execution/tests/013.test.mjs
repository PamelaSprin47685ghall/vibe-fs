import assert from 'node:assert/strict'
import test from 'node:test'
import { parse } from 'smol-toml'
import { run, nodeCommand } from './support/executor.mjs'

process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'

test('WHAT[process-execution-013] real large output retains the exact raw suffix without calling the injected session capability', async () => {
  const original = 'EARLY_ERROR_' + 'x'.repeat(500) + 'LATEST_RAW_OUTPUT'
  const accessed = []
  const sessions = new Proxy({}, { get(_target, member) {
    accessed.push(member)
    throw new Error(`Unexpected session capability use: ${String(member)}`)
  } })
  const result = parse(await run({ command: nodeCommand(`process.stdout.write(${JSON.stringify(original)})`), output_budget_bytes: 64 }, { sessions }))
  assert.equal(result.exit_code, 0)
  assert.equal(result.output, original.slice(-64))
  assert.equal(result.output_truncated, true)
  assert.deepEqual(accessed, [])
})

test('WHAT[process-execution-013] small output preserves separate stdout and stderr exactly', async () => {
  const result = parse(await run({ command: nodeCommand("process.stdout.write('hello'); process.stderr.write('warning')"), output_budget_bytes: 1024 }))
  assert.equal(result.exit_code, 0)
  assert.equal(result.stdout, 'hello')
  assert.equal(result.stderr, 'warning')
})

test.todo('WHAT[process-execution-013] the registered plugin path creates no model session for any output size')
