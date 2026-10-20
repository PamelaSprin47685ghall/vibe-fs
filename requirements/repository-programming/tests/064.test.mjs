import assert from 'node:assert/strict'
import test from 'node:test'
import { existsSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { parse as parseToml } from 'smol-toml'
import {
  createRegistered,
  execute,
} from '../../../dist/Repository/Programming/Js/OpenCode/ToolHostSurface.js'
import {
  handlesForSession,
  clearAll,
} from '../../../dist/Repository/Programming/Js/OpenCode/ParkedSites.js'
import {
  takeView,
  clearAll as clearViews,
} from '../../../dist/Repository/Programming/Js/Anchor/AnchorSurface.js'
import * as language from '../../../dist/Participant/Provider/LanguageSurface.js'

// 调用面的三态互斥：一次编程调用只携带 program / unpark / abandon 之一。
// 这里走真实的工具调用面（ToolHostSurface 装配出来的注册工具）与真实的
// 现场表（JsParkedSites）：断言的是真结果、真 handle 与真文件效果，
// 不是源码形状。

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-064-exclusive-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

// The refusal messages come from the bilingual resource, and the language is
// a global preference. Pin it to English for this file so the assertions
// read a known wording, and restore the caller's preference afterwards.
let previousLanguage

test.beforeEach(() => {
  previousLanguage = process.env.WANXIANGSHU_PROVIDER_LANGUAGE
  process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'
  language.refreshGlobalLanguage()
  clearAll()
  clearViews()
})

test.afterEach(() => {
  if (previousLanguage === undefined) delete process.env.WANXIANGSHU_PROVIDER_LANGUAGE
  else process.env.WANXIANGSHU_PROVIDER_LANGUAGE = previousLanguage
  language.refreshGlobalLanguage()
})

// 假宿主 schema 构造器：optional() 把 inner 摊平并标出 optional，
// 于是 type/shape 仍然可读，可选性也可读。
const schemaOf = (state) => ({
  ...state,
  optional: () => schemaOf({ ...state, optional: true }),
  nullable: () => schemaOf({ ...state, nullable: true }),
  describe: (text) => schemaOf({ ...state, description: text }),
  int: () => schemaOf({ ...state, integer: true }),
  nonnegative: () => schemaOf({ ...state, nonnegative: true }),
  min: (bound) => schemaOf({ ...state, min: bound }),
  max: (bound) => schemaOf({ ...state, max: bound }),
})

const toolModule = () => {
  const tool = (definition) => definition
  tool.schema = {
    string: () => schemaOf({ type: 'string' }),
    any: () => schemaOf({ type: 'any' }),
    number: () => schemaOf({ type: 'number' }),
    array: (item) => schemaOf({ type: 'array', item }),
    record: (key, value) => schemaOf({ type: 'record', key, value }),
    object: (shape) => schemaOf({ type: 'object', shape }),
  }
  return { tool }
}

// Engineer 携带 Write：探针程序真的写盘，"程序没有跑"才有文件层面的证据。
const register = (dir) => createRegistered(toolModule(), 'Engineer', 'en', dir, null)

const parkProgram = `class Js extends JsProgram {
  async run() {
    const decision = await this.park({ stage: 'exclusivity' });
    return { decided: decision };
  }
}`

const probeProgram = `class Js extends JsProgram {
  async run() {
    await this.write('ambiguous-probe.txt', 'the program ran');
    return { wrote: true };
  }
}`

const PROBE = 'ambiguous-probe.txt'

const parkOnce = async (registered, session) => {
  const result = await execute(registered, { program: parkProgram }, { sessionID: session })
  assert.equal(result.startsWith('# parked'), true, `expected a parked payload, got: ${result}`)
  const doc = parseToml(result)
  assert.equal(typeof doc.handle, 'string', `the parked payload must carry a handle: ${result}`)
  assert.ok(doc.handle.length > 0, 'the parked handle must not be empty')
  return doc.handle
}

test('WHAT[repository-programming-064] a call carrying two states is refused with INVALID_ARGUMENT and runs nothing', async () => {
  const { dir, cleanup } = sandbox()
  const session = 'ses-064-two-states'
  try {
    const registered = register(dir)
    const handle = await parkOnce(registered, session)

    const result = await execute(
      registered,
      { program: probeProgram, abandon: { handle } },
      { sessionID: session },
    )

    assert.equal(result.startsWith('# failed'), true, `the ambiguous call must fail: ${result}`)
    const doc = parseToml(result)
    assert.equal(doc.code, 'INVALID_ARGUMENT', `the refusal must carry the stable code: ${result}`)
    assert.match(doc.reason, /program/, `the refusal must name the program field: ${result}`)
    assert.match(doc.reason, /abandon/, `the refusal must name the abandon field: ${result}`)

    assert.equal(
      existsSync(join(dir, PROBE)),
      false,
      'the program carried by an ambiguous call must not run',
    )
    assert.deepEqual(
      Array.from(handlesForSession(session)),
      [ handle ],
      'a refused call must not consume the handle it named',
    )
    assert.equal(
      takeView(session),
      null,
      'a refused call must not leave a view declaration behind for the next request',
    )
  } finally {
    clearAll()
    clearViews()
    cleanup()
  }
})

test('WHAT[repository-programming-064] abandon alone still clears the handle', async () => {
  const { dir, cleanup } = sandbox()
  const session = 'ses-064-abandon-alone'
  try {
    const registered = register(dir)
    const handle = await parkOnce(registered, session)

    const result = await execute(registered, { abandon: { handle } }, { sessionID: session })

    assert.match(result, /ABANDONED/, `the abandon call must settle the site: ${result}`)
    assert.deepEqual(
      Array.from(handlesForSession(session)),
      [],
      'abandon alone must consume the handle',
    )
  } finally {
    clearAll()
    clearViews()
    cleanup()
  }
})

test('WHAT[repository-programming-064] unpark alone still resumes the parked program', async () => {
  const { dir, cleanup } = sandbox()
  const session = 'ses-064-unpark-alone'
  try {
    const registered = register(dir)
    const handle = await parkOnce(registered, session)

    const result = await execute(
      registered,
      { unpark: { handle, value: 'resumed' } },
      { sessionID: session },
    )

    assert.equal(result.startsWith('# ok'), true, `the resume must continue the program: ${result}`)
    const doc = parseToml(result)
    assert.equal(
      doc.data.decided,
      'resumed',
      'the injected value must reach the parked await',
    )
    assert.deepEqual(
      Array.from(handlesForSession(session)),
      [],
      'a resumed site must leave the table',
    )
  } finally {
    clearAll()
    clearViews()
    cleanup()
  }
})

test('WHAT[repository-programming-064] program alone still runs the program', async () => {
  const { dir, cleanup } = sandbox()
  const session = 'ses-064-program-alone'
  try {
    const registered = register(dir)

    const result = await execute(registered, { program: probeProgram }, { sessionID: session })

    assert.equal(result.startsWith('# ok'), true, `the single-state program call must run: ${result}`)
    assert.equal(parseToml(result).data.wrote, true, 'the program return value must come back')
    assert.equal(
      existsSync(join(dir, PROBE)),
      true,
      'the single-state program call must reach the filesystem',
    )
  } finally {
    clearAll()
    clearViews()
    cleanup()
  }
})

test('WHAT[repository-programming-064] a call with no state at all is the missing-program error', async () => {
  const { dir, cleanup } = sandbox()
  const session = 'ses-064-no-state'
  try {
    const registered = register(dir)

    const result = await execute(registered, {}, { sessionID: session })

    assert.notEqual(result.startsWith('# ok'), true, `a stateless call must not succeed: ${result}`)
    assert.match(
      result,
      /missing 'program' argument/,
      `a stateless call must report the missing program: ${result}`,
    )
    assert.equal(
      existsSync(join(dir, PROBE)),
      false,
      'a stateless call must not run anything',
    )
  } finally {
    clearAll()
    clearViews()
    cleanup()
  }
})
