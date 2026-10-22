import assert from 'node:assert/strict'
import { writeFileSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { sandbox, program, nodeCommand, caseName, parseData, render, execute } from './executor.mjs'

assert.ok(process.argv[2] === 'true' || process.argv[2] === 'false')
const exhaustAfterRun = process.argv[2] === 'true'
const { dir, cleanup } = sandbox('wxs-run-deadline-lease-')
try {
  writeFileSync(join(dir, 'probe'), 'data')
  const command = nodeCommand('setTimeout(() => {}, 350)')
  const outcome = await execute(dir, 'DevOps', 'en', 'deadline-lease', { program: program(`
    const host = this;
    let observedLate = false;
    const runnable = (command) => {
      return class extends this.Callable {
        maxDuration = 1500;
        interval = 25;
        next(ctx) { return ctx.step === 0 ? command : null; }
        async watch(ctx) {
          if (ctx.remainingMs === 0) return false;
          const observed = await host.stat('probe');
          if (ctx.elapsedMs > 200 && observed.exists) observedLate = true;
          return true;
        }
        sanity() { return { nextDone: () => [{ step: 1 }], watchVeto: () => [{ remainingMs: 0 }] }; }
      };
    }
    const results = await Promise.all([this.call(runnable('sleep 0.1')), this.call(runnable(${JSON.stringify(command)}))]);
    await host.write('runs-finished', JSON.stringify({ observedLate, statuses: results.map(result => result.status) }));
    ${exhaustAfterRun ? 'const until = Date.now() + 250; while (Date.now() < until) {}' : ''}
    const after = await host.stat('probe');
    return { observedLate, afterExists: after.exists, statuses: results.map(result => result.status) };
  `) }, 1000, Date.now() + 150, 4096, null)
  assert.deepEqual(JSON.parse(readFileSync(join(dir, 'runs-finished'), 'utf8')), { observedLate: true, statuses: ['completed', 'completed'] })
  if (exhaustAfterRun) {
    assert.equal(caseName(outcome), 'Failed')
    assert.match(render(outcome), /PROGRAM_TIMEOUT/)
  } else {
    assert.equal(caseName(outcome), 'Succeeded', render(outcome))
    assert.deepEqual(parseData(outcome), { observedLate: true, afterExists: true, statuses: ['completed', 'completed'] })
  }
} finally {
  cleanup()
}
