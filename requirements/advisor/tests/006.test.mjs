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

test('WHAT[advisor-006] advisor is capability-gated and documents the intent parameter', () => {
  const surface = generateRole('manager', 'en')
  assert.equal(surface.toolName, 'todowrite-for-manager')
  assert.equal(surface.capabilities.includes('Advisor'), true)
  assert.equal(surface.members.filter((member) => member.memberName === 'advisor').length, 1)
  assert.match(surface.description, /advisor\(intent\)/)
  assert.match(surface.baseClassSource, /async advisor\(intent\)/)
  assert.match(surface.baseClassSource, /this\._api\.advisor\(intent\)/)
  const withoutAdvisor = generate('Manager', ['Read'], 'en')
  assert.equal(withoutAdvisor.members.some((member) => member.memberName === 'advisor'), false)
  assert.doesNotMatch(withoutAdvisor.baseClassSource, /this\._api\.advisor/)
})

test('WHAT[advisor-006] advisor invocation forwards intent and returns ok and context', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-advisor-intent-'))
  const session = 'advisor-intent-member'
  const observed = []
  const unused = async () => { throw new Error('unrelated member called') }
  try {
    installDelegationHost(session, unused, unused, unused, unused, async (intent) => {
      observed.push(intent)
      return { ok: true, context: '[[session]]\nrole = "assistant"\ntext = "advice"' }
    })
    const outcome = await managerRun(dir, 'return await this.advisor("review evidence");', session)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.match(render(outcome), /context = /)
    assert.deepEqual(observed, ['review evidence'])
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

