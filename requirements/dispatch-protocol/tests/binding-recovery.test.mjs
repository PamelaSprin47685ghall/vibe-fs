import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

test('WHAT[DISPATCH-PROTOCOL-012] recovered turn binding restores durable canonical role when process-local role is absent', () => {
  const source = readFileSync(
    resolve(import.meta.dirname, '../../../src/Wanxiangshu/Composition/Turn/Binding.fs'),
    'utf8',
  )

  assert.match(source, /let mergeProjectedBinding/)
  assert.match(source, /Role = binding\.Role \|> Option\.orElse projected\.Role/)
  assert.match(source, /\| Some binding, Some projected -> Some\(mergeProjectedBinding binding projected\)/)
})
