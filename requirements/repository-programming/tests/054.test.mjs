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
  execute(dir, 'Coder', 'en', 'sess-054', { name: 'deliver-contract', program: program(body) }, 3000, Date.now() + 60_000, 1 << 20, null)

test('WHAT[repository-programming-054] deliver stores carry without echoing it or running later statements', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const outcome = await runProgram(
      dir,
      `    await this.deliver(["final report"]);
    return { unreachable: true };`,
    )
    assert.equal(caseName(outcome), 'Succeeded')
    const doc = render(outcome)
    assert.match(doc, /# ok/)
    assert.match(doc, /delivered = true/)
    assert.doesNotMatch(doc, /final report/)
    assert.equal(doc.includes('unreachable'), false, 'the statement after deliver never runs')
    assert.deepEqual(deliveredViewOf('sess-054'), ['final report'])
  } finally {
    clearDeliveredValue('sess-054')
    cleanup()
  }
})

test('WHAT[repository-programming-054] deliver without an argument is refused', async () => {
  const { dir, cleanup } = sandbox()
  try {
    clearDeliveredValue('sess-054')
    const outcome = await runProgram(dir, `    return await this.deliver();`)
    assert.match(render(outcome), /code = "VIEW_DECLARATION_INVALID"/)
    assert.equal(deliveredValue('sess-054'), null)
  } finally {
    clearDeliveredValue('sess-054')
    cleanup()
  }
})

test('WHAT[repository-programming-054] an explicit empty carry array establishes delivery', async () => {
  const { dir, cleanup } = sandbox()
  try {
    clearDeliveredValue('sess-054')
    const outcome = await runProgram(dir, `    await this.deliver([]);`)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.deepEqual(deliveredViewOf('sess-054'), [])
  } finally {
    clearDeliveredValue('sess-054')
    cleanup()
  }
})

test('WHAT[repository-programming-054] a view wrapper handed over by hand is refused', async () => {
  const { dir, cleanup } = sandbox()
  try {
    clearDeliveredValue('sess-054')
    const outcome = await runProgram(dir, `    return await this.deliver({ prefixUntil: 0, thenMap: { 0: "wrapped" } });`)
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
      `    try { await this.deliver(["cancelled"]); } catch (error) { /* knowingly cancelled */ }
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

test('WHAT[repository-programming-054] deliver rejects objects before JSON conversion can disguise them', async () => {
  const { dir, cleanup } = sandbox()
  try {
    clearDeliveredValue('sess-054')
    const outcome = await runProgram(dir, `    return await this.deliver([{ toJSON() { return "disguised"; } }]);`)
    const doc = render(outcome)
    assert.match(doc, /code = "VIEW_DECLARATION_INVALID"/)
    assert.doesNotMatch(doc, /INVALID_RETURN_VALUE/, 'the refusal is not a return-value rejection')
    assert.equal(deliveredValue('sess-054'), null, 'rejected delivery never populates the session cell')
  } finally {
    clearDeliveredValue('sess-054')
    cleanup()
  }
})

test('WHAT[repository-programming-054] array serialization hooks cannot replace validated carry', async () => {
  const { dir, cleanup } = sandbox()
  try {
    clearDeliveredValue('sess-054')
    const outcome = await runProgram(dir, `    const carry = ["real material"];
    carry.toJSON = () => ["replaced material"];
    await this.deliver(carry);`)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.deepEqual(deliveredViewOf('sess-054'), ['real material'])
  } finally {
    clearDeliveredValue('sess-054')
    cleanup()
  }
})

test('WHAT[repository-programming-054] every generated surface carries deliver beside park', async () => {
  const { generateSurfaceForRole } = await import('../../../dist/Repository/Programming/Js/GeneratorSurface.js')
  const surface = generateSurfaceForRole('Engineer', ['Read'])
  assert.equal(surface.baseClassSource.includes('async deliver(carry)'), true)
  assert.match(surface.description, /deliver\(carry/)
})

test('WHAT[repository-programming-054] malformed carry and extra arguments do not establish delivery', async () => {
  const { dir, cleanup } = sandbox()
  try {
    for (const argument of ['null', '{}', '[null]', '[true]', '[1.5]', '[-1]', '[NaN]', '[Infinity]', '[9007199254740992]', '[[1]]', '[], []']) {
      clearDeliveredValue('sess-054')
      const outcome = await runProgram(dir, `    return await this.deliver(${argument});`)
      assert.match(render(outcome), /VIEW_DECLARATION_INVALID/, argument)
      assert.equal(deliveredValue('sess-054'), null, argument)
    }
  } finally {
    clearDeliveredValue('sess-054')
    cleanup()
  }
})

test('WHAT[repository-programming-054] an unknown anchor fails before a delivery cell is recorded', async () => {
  const { dir, cleanup } = sandbox()
  try {
    clearDeliveredValue('sess-054')
    const outcome = await runProgram(dir, `    await this.deliver([999999]);`)
    assert.equal(caseName(outcome), 'Failed')
    assert.match(render(outcome), /VIEW_DECLARATION_INVALID/)
    assert.equal(deliveredValue('sess-054'), null)
  } finally {
    clearDeliveredValue('sess-054')
    cleanup()
  }
})

