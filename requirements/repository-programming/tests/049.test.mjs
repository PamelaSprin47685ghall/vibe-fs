import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
  createRegistered,
  description,
  argumentNames,
  argumentSchema,
} from '../../../dist/Repository/Programming/Js/OpenCode/ToolHostSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-unpark-schema-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

// The fake host schema builders mirror the real ones: every builder can be
// marked optional, so the registered schema can say "this argument may be
// absent" the way the zod-based host schema does. Optionality is what lets a
// model send one state (program / unpark / abandon) and nothing else.
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

test('WHAT[repository-programming-049] the tool declares the three state arguments and the flat view parameters', () => {
  const { dir, cleanup } = sandbox()
  try {
    const registered = createRegistered(toolModule(), 'Coder', 'en', dir, null)
    assert.deepEqual(
      argumentNames(registered),
      ['program', 'unpark', 'abandon', 'prefixUntil', 'thenMap'],
      'the retired view wrapper must not be an argument any more',
    )
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-049] program, unpark and abandon are all optional so one state can be sent alone', () => {
  const { dir, cleanup } = sandbox()
  try {
    const registered = createRegistered(toolModule(), 'Coder', 'en', dir, null)

    const program = argumentSchema(registered, 'program')
    assert.equal(program.type, 'string')
    assert.equal(program.optional, true, 'program must be optional: a resume or abandon call carries no program')
    assert.equal(typeof program.description, 'string')
    assert.ok(program.description.length > 0, 'the program argument keeps its teaching description')

    const unpark = argumentSchema(registered, 'unpark')
    assert.equal(unpark.type, 'object')
    assert.equal(unpark.optional, true, 'unpark must be optional: a program call carries no unpark')

    const abandon = argumentSchema(registered, 'abandon')
    assert.equal(abandon.type, 'object')
    assert.equal(abandon.optional, true, 'abandon must be optional: a program call carries no abandon')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-049] unpark.value is declared as an optional free-form JSON value', () => {
  const { dir, cleanup } = sandbox()
  try {
    const registered = createRegistered(toolModule(), 'Coder', 'en', dir, null)
    const unpark = argumentSchema(registered, 'unpark')
    assert.equal(unpark.shape.handle.type, 'string')
    assert.equal(unpark.shape.value.type, 'any')
    assert.equal(unpark.shape.value.optional, true)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-049] abandon carries nothing but the handle', () => {
  const { dir, cleanup } = sandbox()
  try {
    const registered = createRegistered(toolModule(), 'Coder', 'en', dir, null)
    const abandon = argumentSchema(registered, 'abandon')
    assert.deepEqual(Object.keys(abandon.shape), ['handle'])
    assert.equal(abandon.shape.handle.type, 'string')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-049] the flat view parameters stay optional top-level arguments', () => {
  const { dir, cleanup } = sandbox()
  try {
    const registered = createRegistered(toolModule(), 'Coder', 'en', dir, null)

    const prefixUntil = argumentSchema(registered, 'prefixUntil')
    assert.equal(prefixUntil.type, 'number')
    assert.equal(prefixUntil.optional, true, 'prefixUntil must be optional: a call may declare no view at all')

    const thenMap = argumentSchema(registered, 'thenMap')
    assert.equal(thenMap.type, 'record')
    assert.equal(thenMap.optional, true, 'thenMap must be optional: a call may declare no view at all')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-049] the tool description teaches the free-form injection responsibility', () => {
  const { dir, cleanup } = sandbox()
  try {
    const registered = createRegistered(toolModule(), 'Coder', 'en', dir, null)
    const text = description(registered)
    assert.match(text, /arbitrary JSON/)
    assert.match(text, /does not pre-validate/)
  } finally {
    cleanup()
  }
})
