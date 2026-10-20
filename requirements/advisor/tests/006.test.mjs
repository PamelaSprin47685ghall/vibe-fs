import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { generate, generateRole } from '../../../dist/Repository/Programming/Js/GeneratorSurface.js'
import { execute, caseName, render } from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'
import {
  installDelegationHost,
  clearDelegationHost,
} from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'

// advisor-[006]: `js.advisor(intent, view)` is an ordinary delegation member — its value crosses as
// data, never as rendered prose, and a surface without the capability never carries it.
const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
  }
}`

const managerRun = (dir, body, sessionId) =>
  execute(dir, 'Manager', 'en', sessionId, { program: program(body) }, 3000, Date.now() + 60_000, 1 << 20, null)

const sandbox = () => mkdtempSync(join(tmpdir(), 'wxs-advisor-member-'))

test('WHAT[advisor-006] todowrite-for-manager carries the advisor member in description, base class and binding', () => {
  const surface = generateRole('manager', 'en')
  assert.equal(surface.toolName, 'todowrite-for-manager')
  assert.equal(surface.capabilities.includes('Advisor'), true)

  const fragment = surface.members.find((member) => member.memberName === 'advisor')
  assert.ok(fragment, 'the manager surface carries the advisor member')
  assert.equal(fragment.capability, 'Advisor')
  assert.equal(fragment.signature, 'async advisor(intent, view)')
  assert.equal(fragment.runtimeBindingKey, 'js.advisor')

  assert.equal(surface.description.includes('advisor(intent, view)'), true)
  assert.equal(surface.baseClassSource.includes('this._api.js.advisor(intent, view)'), true)
  assert.equal(
    surface.runtimeBindings.some((entry) => entry.memberName === 'advisor' && entry.binding === 'js.advisor'),
    true,
  )

  // The member is capability-gated: the same role without ToolPermission.Advisor loses it in all layers.
  const withoutAdvisor = generate('Manager', ['Read'], 'en')
  assert.equal(withoutAdvisor.members.some((member) => member.memberName === 'advisor'), false)
  assert.equal(withoutAdvisor.description.includes('advisor(intent, view)'), false)
  assert.equal(withoutAdvisor.baseClassSource.includes('js.advisor'), false)
})

test('WHAT[advisor-006] js.advisor returns the installed host closure value back verbatim as data', async () => {
  const dir = sandbox()
  const sessionId = `sess-advisor-${Math.random().toString(36).slice(2, 8)}`
  const observed = []
  const notExercised = async () => {
    throw new Error('the advisor member must not reach fork/join/fission/horizon')
  }

  try {
    installDelegationHost(sessionId, notExercised, notExercised, notExercised, notExercised, async (intent, view) => {
      observed.push({ intent, view })
      return { ok: true, advice: 'host-advice-verbatim', witness: 'host-value-untouched' }
    })

    const outcome = await managerRun(
      dir,
      '    return await this.advisor("review this delta", { prefixUntil: 3, thenMap: {} });',
      sessionId,
    )
    assert.equal(caseName(outcome), 'Succeeded')

    const doc = render(outcome)
    assert.equal(doc.includes('advice = "host-advice-verbatim"'), true, 'the closure envelope reaches the program as data')
    assert.equal(doc.includes('witness = "host-value-untouched"'), true)
    assert.equal(doc.includes('DELEGATION_UNAVAILABLE'), false)

    assert.equal(observed.length, 1, 'the closure is consulted exactly once')
    assert.equal(observed[0].intent, 'review this delta')
    assert.equal(observed[0].view.prefixUntil, 3)
  } finally {
    clearDelegationHost(sessionId)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[advisor-006] an absent delegation host refuses with DELEGATION_UNAVAILABLE', async () => {
  const dir = sandbox()
  const sessionId = `sess-advisor-nohost-${Math.random().toString(36).slice(2, 8)}`

  try {
    clearDelegationHost(sessionId)
    const outcome = await managerRun(
      dir,
      '    return await this.advisor("review this delta", { prefixUntil: 3, thenMap: {} });',
      sessionId,
    )
    assert.equal(caseName(outcome), 'Succeeded', 'the refusal is data, not a program failure')
    assert.match(render(outcome), /code = "DELEGATION_UNAVAILABLE"/)
  } finally {
    clearDelegationHost(sessionId)
    rmSync(dir, { recursive: true, force: true })
  }
})
