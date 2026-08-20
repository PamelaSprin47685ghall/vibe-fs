import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import * as attention from '../../../dist/Interaction/Attention/Surface.js'

const read = (path) => readFileSync(path, 'utf8')

test('WHAT[ATTENTION-REGULATION-001] enough is a pure cognitive stop with no durable authority state', () => {
  const tools = read('src/Wanxiangshu/OpenCode/Tools/AttentionTools.fs')
  assert.match(tools, /"enough"[\s\S]*?simpleExecute "decision"/)
  const enoughBody = tools.match(/let private simpleExecute([\s\S]*?)let private occurrenceId/)?.[1] ?? ''
  assert.doesNotMatch(enoughBody, /AgentJournal|PromptAuthority|MagicTodo|appendAgent/)
})

test('WHAT[ATTENTION-REGULATION-002] abandon releases only cognitive attention and never mutates obligations or authority', () => {
  const tools = read('src/Wanxiangshu/OpenCode/Tools/AttentionTools.fs')
  assert.match(tools, /"abandon"[\s\S]*?simpleExecute "commitment"/)
  const simple = tools.match(/let private simpleExecute([\s\S]*?)let private occurrenceId/)?.[1] ?? ''
  assert.doesNotMatch(simple, /AgentJournal|Obligation|PromptAuthority|AbortSession|rmSync/)
})

test('WHAT[ATTENTION-REGULATION-003] defer creates pending work without creating execution or obligation state', () => {
  let state = attention.empty()
  state = attention.record('ses-a', 'call-1', 'investigate later', state)
  assert.deepEqual(attention.pending('ses-a', state), [{ occurrence: 'call-1', text: 'investigate later' }])

  const facts = read('src/Wanxiangshu/Interaction/Attention/Facts.fs')
  assert.match(facts, /DeferredWorkRecorded/)
  assert.doesNotMatch(facts, /Obligation|Background|Execute|Authority/)
})

test('WHAT[ATTENTION-REGULATION-004] deferred work is occurrence-idempotent and participant-life isolated', () => {
  let state = attention.empty()
  state = attention.record('ses-a', 'call-1', 'one', state)
  state = attention.record('ses-a', 'call-1', 'one', state)
  state = attention.record('ses-b', 'call-2', 'two', state)
  assert.deepEqual(attention.pending('ses-a', state), [{ occurrence: 'call-1', text: 'one' }])
  assert.deepEqual(attention.pending('ses-b', state), [{ occurrence: 'call-2', text: 'two' }])
})

test('WHAT[ATTENTION-REGULATION-005] resurfacing consumes deferred visibility once without activating work', () => {
  let state = attention.empty()
  state = attention.record('ses-a', 'call-1', 'one', state)
  state = attention.record('ses-a', 'call-2', 'two', state)
  state = attention.resurface('ses-a', 'learn-1', ['call-1', 'call-2'], state)
  state = attention.resurface('ses-a', 'learn-1', ['call-1', 'call-2'], state)
  assert.deepEqual(attention.pending('ses-a', state), [])

  const projection = read('src/Wanxiangshu/Interaction/Attention/Projection.fs')
  assert.doesNotMatch(projection, /StartWork|Activate|Delegate|Background/)
})

test('WHAT[ATTENTION-REGULATION-006] attention state stays a minimal deferred-work projection, not a workflow engine', () => {
  const source = [
    read('src/Wanxiangshu/Interaction/Attention/Facts.fs'),
    read('src/Wanxiangshu/Interaction/Attention/Projection.fs'),
    read('src/Wanxiangshu/OpenCode/Tools/AttentionTools.fs'),
  ].join('\n')
  assert.doesNotMatch(source, /\b(Stage|Priority|Deadline|DependencyGraph|AutoResume|BackgroundExecutor)\b/)
})

