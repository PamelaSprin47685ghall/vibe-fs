import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import * as attention from '../../../dist/Interaction/Attention/Surface.js'

const read = (path) => readFileSync(path, 'utf8')

test('WHAT[ATTENTION-REGULATION-006] attention state stays a minimal deferred-work projection, not a workflow engine', () => {
  assert.deepEqual(attention.pending('ses-a', attention.empty()), [])
  const source = [
    read('src/Wanxiangshu/Interaction/Attention/Facts.fs'),
    read('src/Wanxiangshu/Interaction/Attention/Projection.fs'),
    read('src/Wanxiangshu/OpenCode/Tools/AttentionTools.fs'),
  ].join('\n')
  assert.doesNotMatch(source, /\b(Stage|Priority|Deadline|DependencyGraph|AutoResume|BackgroundExecutor)\b/)
})
