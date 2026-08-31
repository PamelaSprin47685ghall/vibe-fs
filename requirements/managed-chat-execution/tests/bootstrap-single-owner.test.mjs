import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = fileURLToPath(new URL('../../..', import.meta.url))
const bootstrap = readFileSync(join(ROOT, 'src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs'), 'utf8')

const occurrences = (pattern) => bootstrap.match(pattern)?.length ?? 0

test('WHAT[CHATEXEC-003] managed path calls one admission transaction', () => {
  assert.equal(occurrences(/ChatAdmissionTransaction\.production/g), 1)
  assert.equal(occurrences(/ChatAdmissionTransaction\.execute/g), 1)
  assert.match(
    bootstrap,
    /let\s+admissionTransaction\s*=\s*\n\s*journal\s*\n\s*\|> Option\.map\s*\(fun durable ->\s*\n\s*let runtime = PromptDispatcher\.forJournal durable\s*\n\s*ChatAdmissionTransaction\.production durable runtime\.AcceptManagedChatIntent\)/,
  )
  assert.match(
    bootstrap,
    /let ports = createTransaction \(ModelRouting\.projectHostModel output\)[\s\S]*?ChatAdmissionTransaction\.execute\s*\n\s*ports/,
  )

  const construction = bootstrap.indexOf('ChatAdmissionTransaction.production')
  const hook = bootstrap.indexOf('let chatMessageHook')
  assert.ok(construction >= 0 && construction < hook, 'the transaction factory must be composed before the callback')
})

test('WHAT[CHATEXEC-003] only Settled crosses the managed provider boundary', () => {
  assert.equal(occurrences(/continueManagedChatMessage/g), 2, 'one declaration and one invocation')
  const admission = bootstrap.slice(
    bootstrap.indexOf('let admitManagedChatMessage'),
    bootstrap.indexOf('let rejectedChatMessage'),
  )

  assert.match(admission, /Ok\(ChatAdmissionTransactionOutcome\.Settled _\) ->\s+continueManagedChatMessage intent output/)
  assert.match(admission, /Ok outcome ->\s+raise \(ChatAdmissionHookException\(TransactionStopped outcome, executionKey intent\)\)/)
  assert.doesNotMatch(admission, /ChatAdmissionTransactionOutcome\.Superseded _\) -> \(\)/)
  assert.equal(occurrences(/TransactionStopped outcome/g), 1)
})

test('WHAT[CHATEXEC-003] acceptance uncertainty, acquire, bind, and Host projection failures stop before provider', () => {
  const continuation = bootstrap.slice(
    bootstrap.indexOf('let continueManagedChatMessage'),
    bootstrap.indexOf('let currentExecution'),
  )
  const admission = bootstrap.slice(
    bootstrap.indexOf('let admitManagedChatMessage'),
    bootstrap.indexOf('let rejectedChatMessage'),
  )

  assert.match(
    admission,
    /Error error ->\s+raise \(ChatAdmissionHookException\(TransactionFailed error, executionKey intent\)\)/,
  )
  assert.doesNotMatch(continuation, /ChatAdmissionTransaction|TransactionFailed|Error error/)
  assert.doesNotMatch(admission, /Error error[\s\S]*continueManagedChatMessage/)
})

test('WHAT[CHATEXEC-003] unmanaged and HostInternal preserve the physical continuation without admission', () => {
  assert.match(
    bootstrap,
    /Decision\.NoManagedExecution _[\s\S]*?Decision\.HostInternal _[\s\S]*?continueUnmanagedChatMessage intent/,
  )
})

test('WHAT[CHATEXEC-003] Reject remains typed at the Host hook boundary', () => {
  assert.match(
    bootstrap,
    /Decision\.Reject rejection, _, _ ->\s*rejectedChatMessage \(IntentRejected rejection\)/,
  )
})

test('WHAT[CHATEXEC-003] bootstrap contains no fragmented admission owner', () => {
  for (const forbidden of [
    /PromptIngress\.createDecisionHook/,
    /PromptIngress\.createHook/,
    /ModelRouting\.routeChatExecution/,
    /ModelRouting\.AcquireAndCommitRoutedExecution/,
    /SessionExecutionBinding\.acceptRoutedExecution/,
    /SessionExecutionBinding\.acceptExternalExecution/,
    /SessionExecutionBinding\.acceptPromptExecution/,
    /ModelRouting\.projectRoutedModel/,
  ]) {
    assert.doesNotMatch(bootstrap, forbidden)
  }
})
