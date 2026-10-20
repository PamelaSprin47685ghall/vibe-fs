import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { execute, caseName, render } from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'
import { deliveredValue, deliveredViewOf, deliveredFenceOf, clearDeliveredValue } from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-deliver-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
  }
}`

const runProgram = (dir, body) =>
  execute(dir, 'Coder', 'en', 'sess-054', { program: program(body) }, 3000, Date.now() + 60_000, 1 << 20, null)

test('WHAT[repository-programming-054] deliver ends the program with its view declaration', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const outcome = await runProgram(
      dir,
      `    await this.deliver({ prefixUntil: 0, thenMap: { 0: "final report" } });
    return { unreachable: true };`,
    )
    assert.equal(caseName(outcome), 'Succeeded')
    const doc = render(outcome)
    assert.match(doc, /# ok/)
    assert.match(doc, /prefixUntil = 0/)
    assert.match(doc, /final report/)
    assert.equal(doc.includes('unreachable'), false, 'the statement after deliver never runs')
    assert.deepEqual(deliveredViewOf('sess-054'), { prefixUntil: 0, thenMap: { 0: 'final report' } }, 'the view stays in the cell until a consumer takes it')
  } finally {
    clearDeliveredValue('sess-054')
    cleanup()
  }
})

test('WHAT[repository-programming-054] a delivered view carries the exact call fence', async () => {
  const { dir, cleanup } = sandbox()
  try {
    clearDeliveredValue('sess-054')
    const outcome = await runProgram(dir, `    await this.deliver({ prefixUntil: 0, thenMap: { 0: "fenced handoff" } });`)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.deepEqual(deliveredViewOf('sess-054'), { prefixUntil: 0, thenMap: { 0: 'fenced handoff' } })
  } finally {
    clearDeliveredValue('sess-054')
    cleanup()
  }
})

test('WHAT[repository-programming-054] catching the delivery sentinel keeps the program alive', async () => {
  const { dir, cleanup } = sandbox()
  try {
    clearDeliveredValue('sess-054')
    const outcome = await runProgram(
      dir,
      `    try { await this.deliver({ prefixUntil: 0, thenMap: { 0: "cancelled" } }); } catch (error) { /* knowingly cancelled */ }
    return { after: true };`,
    )
    assert.equal(caseName(outcome), 'Succeeded')
    const doc = render(outcome)
    assert.match(doc, /after = true/)
    assert.equal(doc.includes('cancelled'), false)
    assert.equal(deliveredValue('sess-054'), null, 'cancelled delivery leaves the session cell empty')
  } finally {
    clearDeliveredValue('sess-054')
    cleanup()
  }
})

test('WHAT[repository-programming-054] deliver without a view fails as an invalid return', async () => {
  const { dir, cleanup } = sandbox()
  try {
    clearDeliveredValue('sess-054')
    const outcome = await runProgram(dir, `    await this.deliver(undefined);`)
    assert.equal(caseName(outcome), 'Failed')
    assert.match(render(outcome), /# failed/)
    assert.match(render(outcome), /INVALID_RETURN_VALUE/)
    assert.equal(deliveredValue('sess-054'), null, 'rejected delivery never populates the session cell')
  } finally {
    clearDeliveredValue('sess-054')
    cleanup()
  }
})

test('WHAT[repository-programming-054] every generated surface carries deliver beside park', async () => {
  const { generateSurfaceForRole } = await import('../../../dist/Repository/Programming/Js/GeneratorSurface.js')
  const surface = generateSurfaceForRole('Engineer', ['Read'])
  assert.equal(surface.baseClassSource.includes('async deliver(view)'), true)
  assert.match(surface.description, /deliver\(view\)/)
})
