import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import test from 'node:test'

test('WHAT[PROC-007] short PTY exit race owns enough physical lifetime to settle', () => {
  const surfaceUrl = new URL('../../../dist/Process/Surface.js', import.meta.url).href
  const program = `
    const { ptyRaceExit } = await import(${JSON.stringify(surfaceUrl)});
    const result = await ptyRaceExit(new Promise(() => {}), 0);
    process.stdout.write(String(result));
  `
  const child = spawnSync(process.execPath, ['--input-type=module', '--eval', program], { encoding: 'utf8' })

  assert.equal(child.status, 0, child.stderr)
  assert.equal(child.stdout, 'false')
})
