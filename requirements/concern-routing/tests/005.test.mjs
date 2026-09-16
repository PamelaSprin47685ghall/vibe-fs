import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const read = (path) => readFileSync(path, 'utf8')

test('WHAT[CONCERN-ROUTING-005] peer routing carries no authority or obligation vocabulary', () => {
  const source = [
    read('src/Wanxiangshu/Interaction/Concern/Facts.fs'),
    read('src/Wanxiangshu/Interaction/Concern/Projection.fs'),
    read('src/Wanxiangshu/OpenCode/Tools/ConcernTools.fs'),
  ].join('\n')
  assert.doesNotMatch(source, /PromptAuthority|AuthorityRoot|MagicTodo|Obligation/)
})
