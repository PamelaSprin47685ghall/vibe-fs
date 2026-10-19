import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, mkdirSync, writeFileSync, existsSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { run, caseName, failureCode, failureReason } from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-rmmv-codes-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
    return { done: true };
  }
}`

const execute = (dir, body) =>
  run(dir, 'Engineer', 'en', program(body), 2000, Date.now() + 60_000, 1 << 20, null)

test('WHAT[repository-programming-044] runtime rm/mv failures report code and reason together', async () => {
  const { dir, cleanup } = sandbox()
  try {
    mkdirSync(join(dir, 'full'))
    writeFileSync(join(dir, 'full', 'a.txt'), 'a', 'utf8')

    const nonEmpty = await execute(dir, `    this.rm('full');`)
    assert.equal(caseName(nonEmpty), 'Failed')
    assert.equal(failureCode(nonEmpty), 'DIRECTORY_NOT_EMPTY')
    assert.ok(failureReason(nonEmpty).includes('full'), 'the failure identifies the directory')
    assert.equal(existsSync(join(dir, 'full', 'a.txt')), true)

    const missing = await execute(dir, `    this.rm('absent.tmp');`)
    assert.equal(caseName(missing), 'Failed')
    assert.equal(failureCode(missing), 'FILE_NOT_FOUND')
    assert.ok(failureReason(missing).includes('absent.tmp'), 'the failure identifies the missing target')

    writeFileSync(join(dir, 'src.tmp'), 's', 'utf8')
    const outside = await execute(dir, `    this.mv('src.tmp', '../escape.txt');`)
    assert.equal(caseName(outside), 'Failed')
    assert.equal(failureCode(outside), 'PATH_DENIED')
    // WHAT[040]: on PATH_DENIED the source survives.
    assert.equal(existsSync(join(dir, 'src.tmp')), true)
  } finally {
    cleanup()
  }
})
