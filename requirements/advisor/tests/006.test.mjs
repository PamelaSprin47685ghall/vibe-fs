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

// advisor-[006]: `this.advisor(intent, reportMapping)` is an ordinary delegation member — its value
// crosses as data, never as rendered prose, and a surface without the capability never carries it.
// The second argument is the report mapping itself (Record<string, null|string>, no `prefixUntil`),
// and it is mandatory: a missing one refuses with VIEW_DECLARATION_INVALID.
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
  assert.equal(fragment.memberName, 'advisor')

  assert.equal(surface.description.includes('advisor(intent, reportMapping)'), true)
  assert.equal(surface.baseClassSource.includes('this._api.advisor(intent, reportMapping)'), true)
  assert.equal(surface.members.filter((member) => member.memberName === 'advisor').length, 1)

  // The member is capability-gated: the same role without ToolPermission.Advisor loses it in all layers.
  const withoutAdvisor = generate('Manager', ['Read'], 'en')
  assert.equal(withoutAdvisor.members.some((member) => member.memberName === 'advisor'), false)
  assert.equal(withoutAdvisor.description.includes('advisor(intent, reportMapping)'), false)
  assert.equal(withoutAdvisor.baseClassSource.includes('this._api.advisor'), false)
})

test('WHAT[advisor-006] this.advisor returns the installed host closure value back verbatim as data', async () => {
  const dir = sandbox()
  const sessionId = `sess-advisor-${Math.random().toString(36).slice(2, 8)}`
  const observed = []
  const notExercised = async () => {
    throw new Error('the advisor member must not reach fork/join/fission/horizon')
  }

  try {
    installDelegationHost(sessionId, notExercised, notExercised, notExercised, notExercised, async (intent, reportMapping) => {
      observed.push({ intent, reportMapping })
      return { ok: true, advice: 'host-advice-verbatim', witness: 'host-value-untouched' }
    })

    const outcome = await managerRun(
      dir,
      '    return await this.advisor("review this delta", { 1: null });',
      sessionId,
    )
    assert.equal(caseName(outcome), 'Succeeded')

    const doc = render(outcome)
    assert.equal(doc.includes('advice = "host-advice-verbatim"'), true, 'the closure envelope reaches the program as data')
    assert.equal(doc.includes('witness = "host-value-untouched"'), true)
    assert.equal(doc.includes('DELEGATION_UNAVAILABLE'), false)

    assert.equal(observed.length, 1, 'the closure is consulted exactly once')
    assert.equal(observed[0].intent, 'review this delta')

    // The second argument crosses as the report mapping itself — no `prefixUntil` wrapper.
    assert.equal('prefixUntil' in observed[0].reportMapping, false, 'the report mapping carries no prefixUntil')
    assert.deepEqual(Object.keys(observed[0].reportMapping), ['1'])
    // The program runs in its own `node:vm` realm, so the mapping arrives with
    // that realm's Object.prototype; compare the contents, not the prototype.
    assert.deepEqual({ ...observed[0].reportMapping }, { 1: null })
  } finally {
    clearDelegationHost(sessionId)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[advisor-006] a missing report mapping refuses with VIEW_DECLARATION_INVALID without failing the program', async () => {
  const dir = sandbox()
  const sessionId = `sess-advisor-nomapping-${Math.random().toString(36).slice(2, 8)}`
  const observed = []
  const notExercised = async () => {
    throw new Error('the advisor member must not reach fork/join/fission/horizon')
  }

  try {
    installDelegationHost(sessionId, notExercised, notExercised, notExercised, notExercised, async (intent, reportMapping) => {
      observed.push({ intent, reportMapping })
      return { ok: true, advice: 'host-advice-verbatim' }
    })

    const outcome = await managerRun(
      dir,
      '    return await this.advisor("review this delta");',
      sessionId,
    )
    assert.equal(caseName(outcome), 'Succeeded', 'the refusal is data, not a program failure')

    const doc = render(outcome)
    assert.match(doc, /code = "VIEW_DECLARATION_INVALID"/)
    assert.equal(doc.includes('DELEGATION_UNAVAILABLE'), false, 'the host was installed — this is not an unavailable host')
    assert.equal(observed.length, 0, 'an invalid declaration never reaches the advisor seat')
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
      '    return await this.advisor("review this delta", { 1: null });',
      sessionId,
    )
    assert.equal(caseName(outcome), 'Succeeded', 'the refusal is data, not a program failure')
    assert.match(render(outcome), /code = "DELEGATION_UNAVAILABLE"/)
  } finally {
    clearDelegationHost(sessionId)
    rmSync(dir, { recursive: true, force: true })
  }
})
