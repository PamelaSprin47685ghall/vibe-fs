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

const toolModule = () => {
  const tool = (definition) => definition
  tool.schema = {
    string: () => ({
      type: 'string',
      nullable: () => ({ type: 'string', nullable: true }),
      describe: (text) => ({ type: 'string', description: text }),
    }),
    any: () => ({
      type: 'any',
      optional: () => ({ type: 'any', optional: true }),
    }),
    number: () => ({ type: 'number', optional: () => ({ type: 'number', optional: true }) }),
    array: (item) => ({
      type: 'array',
      item,
      describe: (description) => ({ type: 'array', item, description }),
    }),
    record: (key, value) => ({
      type: 'record',
      key,
      value,
      describe: (description) => ({ type: 'record', key, value, description }),
    }),
    object: (shape) => ({ type: 'object', shape }),
  }
  return { tool }
}

test('WHAT[repository-programming-049] unpark.value is declared as an optional free-form JSON value', () => {
  const { dir, cleanup } = sandbox()
  try {
    const registered = createRegistered(toolModule(), 'Coder', 'en', dir, null)
    assert.deepEqual(argumentNames(registered), ['program', 'unpark', 'abandon', 'view'])
    const unpark = argumentSchema(registered, 'unpark')
    assert.equal(unpark.type, 'object')
    assert.equal(unpark.shape.handle.type, 'string')
    assert.equal(unpark.shape.value.type, 'any')
    assert.equal(unpark.shape.value.optional, true)
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
