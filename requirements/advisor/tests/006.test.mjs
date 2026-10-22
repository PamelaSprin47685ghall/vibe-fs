import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { generate, generateRole } from '../../../dist/Repository/Programming/Js/GeneratorSurface.js'
import { execute, caseName, render } from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'
import { installDelegationHost, clearDelegationHost } from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'

const program = (body) => 'class Js extends JsProgram { async run() { ' + body + ' } }'
const managerRun = (dir, body, sessionId) =>
  execute(dir, 'Manager', 'en', sessionId, { name: 'advisor contract probe', program: program(body) }, 3000, Date.now() + 60_000, 1 << 20, null)

test('WHAT[advisor-006] advisor is capability-gated and documents the optional carry array', () => {
  const surface = generateRole('manager', 'en')
  assert.equal(surface.toolName, 'todowrite-for-manager')
  assert.equal(surface.capabilities.includes('Advisor'), true)
  assert.equal(surface.members.filter((member) => member.memberName === 'advisor').length, 1)
  assert.match(surface.description, /advisor\(intent, carry/)
  assert.match(surface.baseClassSource, /async advisor\(intent, carry = \[\]\)/)
  assert.match(surface.baseClassSource, /this\._api\.advisor\(intent, carry\)/)
  const withoutAdvisor = generate('Manager', ['Read'], 'en')
  assert.equal(withoutAdvisor.members.some((member) => member.memberName === 'advisor'), false)
  assert.doesNotMatch(withoutAdvisor.baseClassSource, /this\._api\.advisor/)
})

test('WHAT[advisor-006] carry crosses as an ordered array, and omitted carry is empty', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-advisor-carry-'))
  const session = 'advisor-carry-member'
  const observed = []
  const unused = async () => { throw new Error('unrelated member called') }
  try {
    installDelegationHost(session, unused, unused, unused, unused, async (intent, carry) => {
      observed.push({ intent, carry: Array.from(carry) })
      return { ok: true, advice: 'host-advice-verbatim' }
    })
    for (const body of [
      'return await this.advisor("review evidence", [3, "literal", 3, "", "  "]);',
      'return await this.advisor("review only this intent");',
    ]) {
      const outcome = await managerRun(dir, body, session)
      assert.equal(caseName(outcome), 'Succeeded')
      assert.match(render(outcome), /advice = "host-advice-verbatim"/)
    }
    assert.deepEqual(observed, [
      { intent: 'review evidence', carry: [3, 'literal', 3, '', '  '] },
      { intent: 'review only this intent', carry: [] },
    ])
  } finally {
    clearDelegationHost(session)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[advisor-006] malformed carry is a data refusal and never reaches the host', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-advisor-invalid-'))
  const session = 'advisor-invalid-member'
  let calls = 0
  const unused = async () => { throw new Error('unrelated member called') }
  try {
    installDelegationHost(session, unused, unused, unused, unused, async () => { calls += 1; return { ok: true } })
    for (const invalid of ['{}', 'null', '[true]', '[-1]', '[1.5]', '[[1]]', '[{toJSON(){return "fake"}}]']) {
      const outcome = await managerRun(dir, 'return await this.advisor("review", ' + invalid + ');', session)
      assert.equal(caseName(outcome), 'Succeeded')
      assert.match(render(outcome), /code = "VIEW_DECLARATION_INVALID"/)
    }
    assert.equal(calls, 0)
  } finally {
    clearDelegationHost(session)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[advisor-006] absent host returns DELEGATION_UNAVAILABLE', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-advisor-nohost-'))
  try {
    clearDelegationHost('advisor-nohost')
    const outcome = await managerRun(dir, 'return await this.advisor("review");', 'advisor-nohost')
    assert.equal(caseName(outcome), 'Succeeded')
    assert.match(render(outcome), /code = "DELEGATION_UNAVAILABLE"/)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

