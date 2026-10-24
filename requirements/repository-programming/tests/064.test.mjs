import assert from 'node:assert/strict'
import test from 'node:test'
import { existsSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { parse as parseToml } from 'smol-toml'
import { createRegistered, execute, argumentNames, argumentSchema } from '../../../dist/Repository/Programming/Js/OpenCode/ToolHostSurface.js'
import { handlesForSession, clearAll } from '../../../dist/Repository/Programming/Js/ParkedSites.js'
import * as language from '../../../dist/Participant/Provider/LanguageSurface.js'

// The public Host tool has exactly one required source argument. The private
// workflow's parked-state transport must never leak into the LLM tool schema.
const schemaOf = (state) => ({
  ...state,
  optional: () => schemaOf({ ...state, optional: true }),
  nullable: () => schemaOf({ ...state, nullable: true }),
  describe: text => schemaOf({ ...state, description: text }),
  int: () => schemaOf({ ...state, integer: true }),
  nonnegative: () => schemaOf({ ...state, nonnegative: true }),
  min: bound => schemaOf({ ...state, min: bound }),
  max: bound => schemaOf({ ...state, max: bound }),
})

const toolModule = () => {
  const tool = definition => definition
  tool.schema = {
    string: () => schemaOf({ type: 'string' }),
    any: () => schemaOf({ type: 'any' }),
    number: () => schemaOf({ type: 'number' }),
    array: item => schemaOf({ type: 'array', item }),
    record: (key, value) => schemaOf({ type: 'record', key, value }),
    object: shape => schemaOf({ type: 'object', shape }),
  }
  return { tool }
}

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-064-program-only-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const register = dir => createRegistered(toolModule(), 'Engineer', 'en', dir, null)

const parkProgram = `class Js extends JsProgram {
  async run() {
    const decision = await this.park({ stage: 'single-program-host-tool' });
    return { decided: decision };
  }
}`

const writeProgram = `class Js extends JsProgram {
  async run() {
    await this.write('program-only-probe.txt', 'the program ran');
    return { wrote: true };
  }
}`

const PROBE = 'program-only-probe.txt'
let previousLanguage

test.beforeEach(() => {
  previousLanguage = process.env.WANXIANGSHU_PROVIDER_LANGUAGE
  process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'
  language.refreshGlobalLanguage()
  clearAll()
})

test.afterEach(() => {
  if (previousLanguage === undefined) delete process.env.WANXIANGSHU_PROVIDER_LANGUAGE
  else process.env.WANXIANGSHU_PROVIDER_LANGUAGE = previousLanguage
  language.refreshGlobalLanguage()
  clearAll()
})

const parkOnce = async (registered, session) => {
  const result = await execute(registered, { program: parkProgram }, { sessionID: session })
  assert.equal(result.startsWith('# parked'), true, `expected real parked result, got: ${result}`)
  const payload = parseToml(result)
  assert.equal(typeof payload.handle, 'string')
  assert.ok(payload.handle.length)
  return payload.handle
}

test('WHAT[repository-programming-064] the physical Host ToolSpec declares program only and required', () => {
  const { dir, cleanup } = sandbox()
  try {
    const registered = register(dir)
    assert.deepEqual(argumentNames(registered), ['program'])
    const schema = argumentSchema(registered, 'program')
    assert.equal(schema.type, 'string')
    assert.notEqual(schema.optional, true)
    for (const former of ['name', 'planB', 'unpark', 'abandon'])
      assert.equal(argumentSchema(registered, former), null, former + ' is not an argument')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-064] the only accepted public state, program, executes with no name supplied', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const result = await execute(register(dir), { program: writeProgram }, { sessionID: 'ses-064-program' })
    assert.equal(result.startsWith('# ok'), true, result)
    assert.equal(parseToml(result).data.wrote, true)
    assert.equal(existsSync(join(dir, PROBE)), true)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-064] empty arguments cannot execute a program', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const result = await execute(register(dir), {}, { sessionID: 'ses-064-missing' })
    assert.match(result, /missing 'program' argument/)
    assert.equal(existsSync(join(dir, PROBE)), false)
  } finally {
    cleanup()
  }
})

for (const [former, input] of [
  ['unpark', handle => ({ unpark: { handle, value: 'forged' } })],
  ['abandon', handle => ({ abandon: { handle } })],
]) {
  test(`WHAT[repository-programming-064] retired top-level ${former} is not a substitute for program and cannot consume a handle`, async () => {
    const { dir, cleanup } = sandbox()
    const session = 'ses-064-old-' + former
    try {
      const registered = register(dir)
      const handle = await parkOnce(registered, session)
      const result = await execute(registered, input(handle), { sessionID: session })
      assert.match(result, /missing 'program' argument/)
      assert.deepEqual(Array.from(handlesForSession(session)), [handle])
      assert.equal(existsSync(join(dir, PROBE)), false)
    } finally {
      cleanup()
    }
  })
}
