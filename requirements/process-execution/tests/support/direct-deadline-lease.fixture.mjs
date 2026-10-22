import assert from 'node:assert/strict'
import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { sandbox, program, nodeCommand, caseName, parseData, render, execute } from './executor.mjs'

assert.ok(process.argv[2] === 'true' || process.argv[2] === 'false')
const exhaustAfterRun = process.argv[2] === 'true'
const { dir, cleanup } = sandbox('wxs-direct-deadline-lease-')
try {
  writeFileSync(join(dir, 'probe'), 'data')
  const firstMarker = join(dir, 'first-physical-finished')
  const secondMarker = join(dir, 'second-physical-finished')
  const command = (marker, milliseconds) => `exec ${nodeCommand(`setTimeout(() => { require('node:fs').writeFileSync(${JSON.stringify(marker)}, 'physical-finished'); process.stdout.write('PHYSICAL-DONE'); }, ${milliseconds})`)}`
  const planB = 'Inspect the actual running command before choosing another action. '.repeat(20)
  const outcome = await execute(dir, 'DevOps', 'en', 'direct-deadline-lease', { program: program(`
    const results = await Promise.all([
      this.call(${JSON.stringify(command(firstMarker, 200))}, ${JSON.stringify(planB)}),
      this.call(${JSON.stringify(command(secondMarker, 350))}, ${JSON.stringify(planB)})
    ]);
    await this.write('runs-finished', JSON.stringify(results.map(result => ({ status: result.status, closed: result.scopeClosed }))));
    ${exhaustAfterRun ? 'const until = Date.now() + 250; while (Date.now() < until) {}' : ''}
    const after = await this.stat('probe');
    return { afterExists: after.exists, stdout: results.map(result => result.stdout) };
  `) }, 1000, Date.now() + 150, 4096, null)
  assert.equal(existsSync(firstMarker), true, render(outcome))
  assert.equal(existsSync(secondMarker), true, render(outcome))
  assert.equal(readFileSync(firstMarker, 'utf8'), 'physical-finished')
  assert.equal(readFileSync(secondMarker, 'utf8'), 'physical-finished')
  assert.deepEqual(JSON.parse(readFileSync(join(dir, 'runs-finished'), 'utf8')), [
    { status: 'completed', closed: true },
    { status: 'completed', closed: true },
  ])
  if (exhaustAfterRun) {
    assert.equal(caseName(outcome), 'Failed')
    assert.match(render(outcome), /PROGRAM_TIMEOUT/)
  } else {
    assert.equal(caseName(outcome), 'Succeeded', render(outcome))
    assert.deepEqual(parseData(outcome), { afterExists: true, stdout: ['PHYSICAL-DONE', 'PHYSICAL-DONE'] })
  }
} finally {
  cleanup()
}
