import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { z } from 'zod'
import {
  createRegistered,
  description,
  argumentNames,
  argumentSchema,
} from '../../../dist/Repository/Programming/Js/OpenCode/ToolHostSurface.js'

const withTool = (check) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-049-schema-'))
  const tool = (definition) => definition
  tool.schema = z
  try {
    check(createRegistered({ tool }, 'Coder', 'en', dir, null))
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
}

test('WHAT[repository-programming-049] the public tool exposes carry and prefixCount without legacy aliases', () => {
  withTool((registered) => {
    assert.deepEqual(argumentNames(registered), [
      'program', 'name', 'unpark', 'abandon', 'prefixCount', 'carry',
    ])
  })
})

test('WHAT[repository-programming-049] program, unpark and abandon can each be sent alone', () => {
  withTool((registered) => {
    for (const name of ['program', 'unpark', 'abandon']) {
      assert.equal(argumentSchema(registered, name).safeParse(undefined).success, true)
    }
    const program = argumentSchema(registered, 'program')
    assert.equal(program.safeParse('return 1').success, true)
    assert.ok(program.description.length > 0)
    assert.equal(argumentSchema(registered, 'unpark').safeParse({ handle: 'handle-1' }).success, true)
    assert.equal(argumentSchema(registered, 'abandon').safeParse({ handle: 'handle-1' }).success, true)
  })
})

test('WHAT[repository-programming-049] unpark.value accepts an optional JSON value', () => {
  withTool((registered) => {
    const schema = argumentSchema(registered, 'unpark')
    const input = { handle: 'handle-1', value: { selected: [1, 'two', null] } }
    assert.deepEqual(schema.parse(input), input)
    assert.deepEqual(schema.parse({ handle: 'handle-1' }), { handle: 'handle-1' })
  })
})

test('WHAT[repository-programming-049] abandon declares only the handle', () => {
  withTool((registered) => {
    const schema = argumentSchema(registered, 'abandon').unwrap()
    assert.deepEqual(Object.keys(schema.shape), ['handle'])
    assert.equal(schema.shape.handle.safeParse(1).success, false)
  })
})

test('WHAT[repository-programming-049] carry has the real integer-or-string array schema', () => {
  withTool((registered) => {
    const prefix = argumentSchema(registered, 'prefixCount')
    const carry = argumentSchema(registered, 'carry')
    assert.equal(prefix.safeParse(undefined).success, true)
    assert.equal(prefix.safeParse(0).success, true)
    assert.equal(carry.safeParse(undefined).success, true)
    assert.deepEqual(carry.parse([7, '7', '', '  ', 0]), [7, '7', '', '  ', 0])
    for (const value of [{}, null, [null], [true], [-1], [1.5], [NaN], [Infinity], [[]], [{}]]) {
      assert.equal(carry.safeParse(value).success, false, String(value))
    }
  })
})

test('WHAT[repository-programming-049] the description states who owns injected JSON', () => {
  withTool((registered) => {
    const text = description(registered)
    assert.match(text, /arbitrary JSON/)
    assert.match(text, /does not pre-validate/)
  })
})

