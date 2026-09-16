import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as surface from '../../../dist/OpenCode/Host/RequirementGroundingRepositorySurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wanxiang-grounding-js-'))
  for (const name of ['alpha', 'beta']) mkdirSync(join(dir, 'requirements', name), { recursive: true })
  mkdirSync(join(dir, 'src', 'alpha'), { recursive: true })
  mkdirSync(join(dir, 'src', 'beta'), { recursive: true })
  writeFileSync(join(dir, 'requirements', 'alpha', 'WHAT.md'), 'alpha\n', 'utf8')
  writeFileSync(join(dir, 'requirements', 'alpha', 'WHY.md'), 'alpha why\n', 'utf8')
  writeFileSync(join(dir, 'requirements', 'alpha', 'APPLIES-TO'), '/src/alpha/**\n', 'utf8')
  writeFileSync(join(dir, 'requirements', 'beta', 'WHAT.md'), 'beta\n', 'utf8')
  writeFileSync(join(dir, 'requirements', 'beta', 'APPLIES-TO'), '/src/beta/**\n', 'utf8')
  writeFileSync(join(dir, 'src', 'alpha', 'a.txt'), 'a0', 'utf8')
  writeFileSync(join(dir, 'src', 'beta', 'b.txt'), 'b0', 'utf8')
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const program = `class Js extends JsProgram {
  async run() {
    await this.write('src/alpha/new.txt', 'a1');
    await this.write('src/beta/new.txt', 'b1');
    return { ok: true };
  }
}`

test('WHAT[REQUIREMENT-GROUNDING-009] js-* mutations commit normally while grounding observes the full effect set without admission', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const result = await surface.runWithObservationFailure(dir, 'js-union', program)
    assert.equal(result.caseName, 'Succeeded')
    assert.equal(result.failureCode, null)
    assert.deepEqual(result.pendingPackages, ['alpha', 'beta'])
    assert.deepEqual(result.created.sort(), ['src/alpha/new.txt', 'src/beta/new.txt'])
    assert.equal(readFileSync(join(dir, 'src', 'alpha', 'new.txt'), 'utf8'), 'a1')
    assert.equal(readFileSync(join(dir, 'src', 'beta', 'new.txt'), 'utf8'), 'b1')
    assert.equal(readFileSync(join(dir, 'src', 'alpha', 'a.txt'), 'utf8'), 'a0')
    assert.equal(readFileSync(join(dir, 'src', 'beta', 'b.txt'), 'utf8'), 'b0')

    surface.dispose(result.runtime)
  } finally { cleanup() }
})

test('WHAT[REQUIREMENT-GROUNDING-010] js-* read is a real read: covered code triggers grounding and already-read Markdown is deduplicated', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const readProgram = `class Js extends JsProgram {
      async run() {
        const spec = await this.file('requirements/alpha/WHAT.md');
        const code = await this.file('src/alpha/a.txt');
        return { spec: spec.text(), code: code.text() };
      }
    }`
    const result = await surface.runFirstAttempt(dir, 'js-read', readProgram)
    assert.equal(result.caseName, 'Succeeded')
    assert.deepEqual(result.pendingPackages, ['alpha'])
    assert.deepEqual(result.pendingMaterials, ['requirements/alpha/WHY.md'])
    surface.dispose(result.runtime)
  } finally { cleanup() }
})
