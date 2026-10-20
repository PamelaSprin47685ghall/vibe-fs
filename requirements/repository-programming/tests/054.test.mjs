import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { execute, caseName, render } from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'
import { deliveredValue, deliveredViewOf, clearDeliveredValue } from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'

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

test('WHAT[repository-programming-054] deliver ends the program with its report mapping', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const outcome = await runProgram(
      dir,
      `    await this.deliver({ 0: "final report" });
    return { unreachable: true };`,
    )
    assert.equal(caseName(outcome), 'Succeeded')
    const doc = render(outcome)
    assert.match(doc, /# ok/)
    assert.match(doc, /prefixUntil = 0/)
    assert.match(doc, /final report/)
    assert.equal(doc.includes('unreachable'), false, 'the statement after deliver never runs')
    assert.deepEqual(deliveredViewOf('sess-054'), { prefixUntil: 0, thenMap: { 0: 'final report' } }, 'the host reads the mapping as a prefixUntil=0 declaration')
  } finally {
    clearDeliveredValue('sess-054')
    cleanup()
  }
})

test('WHAT[repository-programming-054] an empty report mapping is legal and replaces nothing', async () => {
  const { dir, cleanup } = sandbox()
  try {
    clearDeliveredValue('sess-054')
    const outcome = await runProgram(dir, `    await this.deliver({});`)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.deepEqual(deliveredViewOf('sess-054'), { prefixUntil: 0, thenMap: {} })
  } finally {
    clearDeliveredValue('sess-054')
    cleanup()
  }
})

test('WHAT[repository-programming-054] a delivered mapping is recorded as a prefixUntil=0 declaration', async () => {
  const { dir, cleanup } = sandbox()
  try {
    clearDeliveredValue('sess-054')
    const outcome = await runProgram(dir, `    await this.deliver({ 0: "recorded handoff" });`)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.deepEqual(deliveredViewOf('sess-054'), { prefixUntil: 0, thenMap: { 0: 'recorded handoff' } })
  } finally {
    clearDeliveredValue('sess-054')
    cleanup()
  }
})

test('WHAT[repository-programming-054] a view wrapper handed over by hand is refused', async () => {
  const { dir, cleanup } = sandbox()
  try {
    clearDeliveredValue('sess-054')
    const outcome = await runProgram(dir, `    await this.deliver({ prefixUntil: 0, thenMap: { 0: "wrapped" } });`)
    assert.match(render(outcome), /VIEW_DECLARATION_INVALID/, 'the wrapper is refused, not delivered')
    assert.equal(deliveredValue('sess-054'), null, 'the refused wrapper never populates the session cell')
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
      `    try { await this.deliver({ 0: "cancelled" }); } catch (error) { /* knowingly cancelled */ }
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

test('WHAT[repository-programming-054] deliver without a report mapping is refused with VIEW_DECLARATION_INVALID', async () => {
  const { dir, cleanup } = sandbox()
  try {
    clearDeliveredValue('sess-054')
    const outcome = await runProgram(dir, `    return await this.deliver(undefined);`)
    const doc = render(outcome)
    assert.match(doc, /code = "VIEW_DECLARATION_INVALID"/, 'the missing report mapping is refused with the view-declaration code')
    assert.doesNotMatch(doc, /INVALID_RETURN_VALUE/, 'the refusal is not a return-value rejection')
    assert.equal(deliveredValue('sess-054'), null, 'rejected delivery never populates the session cell')
  } finally {
    clearDeliveredValue('sess-054')
    cleanup()
  }
})

test('WHAT[repository-programming-054] every generated surface carries deliver beside park', async () => {
  const { generateSurfaceForRole } = await import('../../../dist/Repository/Programming/Js/GeneratorSurface.js')
  const surface = generateSurfaceForRole('Engineer', ['Read'])
  assert.equal(surface.baseClassSource.includes('async deliver(reportMapping)'), true)
  assert.match(surface.description, /deliver\(reportMapping\)/)
})
