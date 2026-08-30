import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import * as todo from '../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js'

const here = dirname(fileURLToPath(import.meta.url))
const membraneSource = join(
  here,
  '../../../src/Wanxiangshu/Mission/Obligation/Todo/MagicTodoMembrane.fs',
)

test('WHAT[OBLIGATION-LEDGER-020] quality assurance is consolidated to Finality Review without dedicated process reviewers', () => {
  const source = readFileSync(membraneSource, 'utf8')
  assert.doesNotMatch(source, /NeedsDedicatedEnlist/, 'checkpoints must not create dedicated process reviewers')
  assert.doesNotMatch(source, /NeedsEnsureReview/, 'checkpoints must not derive process review duties')
})

test('WHAT[OBLIGATION-LEDGER-025] deferred prepare synchronizes the Host snapshot before freezing ReviewFrontier', () => {
  const source = readFileSync(membraneSource, 'utf8')
  const locate = source.indexOf('SessionSnapshotPort.locateToolCall callId messages')
  const prefix = source.indexOf('messages |> List.takeWhile (fun message -> message.Id <> currentRunId)')
  const capture = source.indexOf('XTraceCapture.captureSessionMessagesWithReceipt (Some durable) sessionId priorMessages')
  const resolve = source.indexOf('MagicTodoLocality.resolve sessionId messages (AgentJournal.snapshot durable) callId')
  assert.ok(locate > 0, 'deferred prepare must identify the exact current provider run from the Host snapshot')
  assert.ok(prefix > locate, 'only the complete transcript before the current provider run may be synchronized')
  assert.ok(capture > prefix, 'the prior transcript must be synchronized into XTrace')
  assert.ok(resolve > capture, 'ReviewFrontier must be localized only after the synchronized XTrace snapshot is current')
  assert.match(source, /\| Error error ->/, 'typed capture failures must remain explicit')
  assert.match(source, /\| Ok _ -> \(\)/, 'the receipt may be discarded only after successful capture')
  assert.doesNotMatch(
    source,
    /captureSessionMessages(?:WithReceipt)? \(Some durable\) sessionId messages/,
    'the current pending tool message must not be durably captured before its input materializes',
  )
})

test('WHAT[OBLIGATION-LEDGER-026] after hook accepts checkpoint durably and enriches T1 revelation', () => {
  const source = readFileSync(membraneSource, 'utf8')
  assert.match(source, /MagicTodoFact\.TodoWriteAccepted accepted/, 'after hook writes TodoWriteAccepted fact')
  assert.match(source, /enrichAcceptedResult/, 'after hook enriches accepted result')
})
