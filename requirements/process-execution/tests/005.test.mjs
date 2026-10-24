import test from 'node:test'

{
  const {fixture,assert,caseName,existsSync} = await import('../../repository-programming/tests/support/command-program.fixture.mjs')
  test('WHAT[process-execution-005] call refuses non-string commands without a physical spawn', async t=>{
    const h=fixture(t,'proc005','DevOps')
    for(const [index,value] of ['0','-5','NaN','Infinity','null','{}'].entries()) {
      const result=await h.run('invalid-'+index,'return await this.call('+value+');')
      assert.equal(caseName(result),'Failed')
    }
    assert.equal(existsSync(h.file('spawned')),false)
  })
}

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { dirname, join } = await import("node:path");
const { default: test } = await import("node:test");
const { fileURLToPath } = await import("node:url");

const {
  command,
  commandView,
  estimate,
  estimateView,
} = await import('../../../dist/Process/Surface.js')

test('WHAT[process-execution-005] EXEC_010_process_request_carries_all_fields', () => {
  const cmd = command('sh', ['-lc', 'echo hi'], '/tmp/wx', 'input')
  const cmdView = commandView(cmd)
  const estView = estimateView(estimate(42, 65536, 'large'))

  assert.equal(cmdView.fileName, 'sh')
  assert.deepEqual(cmdView.arguments, ['-lc', 'echo hi'])
  assert.equal(cmdView.workingDirectory, '/tmp/wx')
  assert.equal(cmdView.stdin, 'input')
  assert.equal(estView.runtimeSeconds, 42)
  assert.equal(estView.outputBytes, 65536)
  assert.equal(estView.memory, 'large')
})
}

{
  const assert = (await import('node:assert/strict')).default
  const runtime = await import('../../../dist/Process/Surface.js')
  test('WHAT[process-execution-005] estimate validation rejects nonfinite or nonpositive time and negative output', () => {
    assert.equal(runtime.validateEstimate(10, 0).ok, true)
    for (const invalid of [NaN, Infinity, -Infinity, 0, -5]) {
      const result = runtime.validateEstimate(invalid, 1024)
      assert.equal(result.ok, false)
      assert.match(result.error, /finite positive number/)
    }
    const result = runtime.validateEstimate(10, -1)
    assert.equal(result.ok, false)
    assert.match(result.error, /non-negative/)
  })
}
