import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { parse as parseToml } from 'smol-toml'
import { createRegistered, execute } from '../../../dist/Repository/Programming/Js/OpenCode/ToolHostSurface.js'
import { takeView, clearAll } from '../../../dist/Repository/Programming/Js/Anchor/AnchorSurface.js'

// 顶层上下文整理参数：工具调用面用扁平的 prefixUntil + thenMap，
// 旧的 `view` 包装对象已退役，非法形状以 VIEW_DECLARATION_INVALID 拒绝。
const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-063-view-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

// 顶层工具的宿主 schema 形状：prefixUntil 是 number，thenMap 是 record。
// 每个 builder 都能标记 optional，与真实宿主 schema 一致。
const schemaOf = (state) => ({
  ...state,
  optional: () => schemaOf({ ...state, optional: true }),
  nullable: () => schemaOf({ ...state, nullable: true }),
  describe: (text) => schemaOf({ ...state, description: text }),
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

const program = `class Js extends JsProgram {
  async run() {
    return { ran: true };
  }
}`

const register = (dir) => createRegistered(toolModule(), 'Coder', 'en', dir, null)

const failureOf = (result) => {
  assert.equal(result.startsWith('# failed'), true, `expected a refusal, got: ${result}`)
  const doc = parseToml(result)
  assert.equal(typeof doc.reason, 'string')
  assert.notEqual(doc.reason.length, 0)
  return doc
}

test.beforeEach(() => clearAll())

test('WHAT[repository-programming-063] flat prefixUntil and thenMap are accepted and captured as the pending view', async () => {
  const { dir, cleanup } = sandbox()
  const session = 'ses-063-flat'
  try {
    const registered = register(dir)
    const result = await execute(
      registered,
      { program, prefixUntil: 0, thenMap: { 0: 'flat summary' } },
      { sessionID: session },
    )

    assert.equal(result.startsWith('# ok'), true, result)
    assert.deepEqual(
      takeView(session),
      { prefixUntil: 0, thenMap: { 0: 'flat summary' } },
      'the flat declaration is captured for the next request',
    )
  } finally {
    clearAll()
    cleanup()
  }
})

test('WHAT[repository-programming-063] the retired view wrapper is refused and points at the flat parameters', async () => {
  const { dir, cleanup } = sandbox()
  const session = 'ses-063-view-wrapper'
  try {
    const registered = register(dir)
    const result = await execute(
      registered,
      { program, view: { prefixUntil: 0, thenMap: { 0: 'wrapped summary' } } },
      { sessionID: session },
    )

    const doc = failureOf(result)
    assert.equal(doc.code, 'VIEW_DECLARATION_INVALID')
    assert.match(doc.reason, /prefixUntil/)
    assert.match(doc.reason, /thenMap/)
    assert.equal(
      takeView(session),
      null,
      'a refused view wrapper must not leave a pending view behind',
    )
  } finally {
    clearAll()
    cleanup()
  }
})

test('WHAT[repository-programming-063] a call without prefixUntil and thenMap sets no view at all', async () => {
  const { dir, cleanup } = sandbox()
  const session = 'ses-063-no-args'
  try {
    const registered = register(dir)
    const result = await execute(registered, { program }, { sessionID: session })

    assert.equal(result.startsWith('# ok'), true, result)
    assert.equal(takeView(session), null, 'no declaration means no pending view')
  } finally {
    clearAll()
    cleanup()
  }
})

test('WHAT[repository-programming-063] a malformed prefixUntil or thenMap is refused with VIEW_DECLARATION_INVALID', async () => {
  const { dir, cleanup } = sandbox()
  const session = 'ses-063-invalid'
  try {
    const registered = register(dir)

    const cases = [
      { label: 'negative prefixUntil', args: { prefixUntil: -1, thenMap: {} } },
      { label: 'fractional prefixUntil', args: { prefixUntil: 1.5, thenMap: {} } },
      { label: 'non-numeric thenMap value', args: { prefixUntil: 0, thenMap: { 0: 42 } } },
      { label: 'blank thenMap value', args: { prefixUntil: 0, thenMap: { 0: '   ' } } },
      { label: 'negative thenMap key', args: { prefixUntil: 0, thenMap: { '-1': 'summary' } } },
    ]

    for (const { label, args } of cases) {
      const result = await execute(registered, { program, ...args }, { sessionID: session })
      const doc = failureOf(result)
      assert.equal(doc.code, 'VIEW_DECLARATION_INVALID', `${label}: ${result}`)
      assert.equal(
        takeView(session),
        null,
        `${label}: a refused declaration must not leave a pending view behind`,
      )
    }
  } finally {
    clearAll()
    cleanup()
  }
})
