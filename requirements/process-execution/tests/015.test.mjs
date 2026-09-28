import assert from 'node:assert/strict'
import test from 'node:test'
import { parse } from 'smol-toml'
import { run, nodeCommand } from './support/executor.mjs'

process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'

for (const budget of [1, 3, 4, 7, 16, 64]) {
  test(`WHAT[process-execution-015] raw UTF-8 tail fits ${budget} bytes without splitting a code point`, async () => {
    const original = 'prefix-' + '中文🔥'.repeat(40)
    const wire = await run({ command: nodeCommand(`process.stdout.write(${JSON.stringify(original)})`), output_budget_bytes: budget })
    const result = parse(wire)
    const output = result.output
    assert.equal(result.exit_code, 0)
    assert.equal(result.output_truncated, true)
    assert.equal(output.includes('\uFFFD'), false)
    assert.ok(Buffer.byteLength(output, 'utf8') <= budget)
    assert.ok(original.endsWith(output))
    assert.equal(new TextDecoder('utf-8', { fatal: true }).decode(Buffer.from(output)), output)
  })
}

test('WHAT[process-execution-015] the complete visible result including metadata fits the explicit budget', { todo: 'GAP-092: the current budget applies only to raw output, not its envelope' }, async () => {
  const budget = 256
  const wire = await run({ command: nodeCommand("process.stdout.write('x'.repeat(2000))"), output_budget_bytes: budget })
  assert.equal(parse(wire).exit_code, 0)
  assert.ok(Buffer.byteLength(wire, 'utf8') <= budget, `complete result is ${Buffer.byteLength(wire)} bytes for ${budget}`)
})
