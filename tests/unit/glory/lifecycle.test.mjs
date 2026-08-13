// tests/unit/glory/lifecycle.test.mjs — GLORY-010/011/012/014/021/052/064.
//
// Layer-1 pure tests: the ManagerLifecycle fact algebra, its fold, the frozen
// narrative texts, and the golden byte fixtures (proof/glory.md).

import assert from 'node:assert/strict'
import test from 'node:test'
import { ManagerLifecycleProjection_isLifeArchived as isLifeArchived } from '../../../dist/Journal/ManagerLifecycleProjection.js'
import {
  blobDigest,
  blobRef,
  caseOf,
  envelope,
  finalityRequestId,
  fold,
  gitTreeHash,
  idValue,
  journal,
  listItems,
  managerLifecycleFact,
  managerLifeId,
  mapEntries,
  payloadOf,
  physicalUser,
  promptKey,
  providerRun,
  reviewBarrierId,
  sessionId,
  stream,
  toolCallId,
} from '../support/domain.mjs'

const SESSION = sessionId('ses_a')
const LIFE = managerLifeId('life-1')
const OPENING_MSG = physicalUser('msg-open-1')
const TREE = gitTreeHash('tree-1')
const REQ = finalityRequestId('req-1')
const REVIEWER = sessionId('ses-reviewer')
const BARRIER = reviewBarrierId('bar-1')
const BLOB = blobRef('blob-1')
const DIGEST = blobDigest('d-1')
const RUN = providerRun('run-1')
const CALL = toolCallId('call-1')
const KEY = promptKey('key-1')

const lifecycleEnv = (fact) => envelope({ stream: stream.session(SESSION), fact })

const lifeOpened = () =>
  managerLifecycleFact('LifeOpened', {
    SessionId: SESSION,
    LifeId: LIFE,
    OpeningUserMessageId: OPENING_MSG,
    OpeningTextRef: BLOB,
    OpeningTextDigest: DIGEST,
    OpeningCursorSequence: 1n,
  })

const workActivated = () =>
  managerLifecycleFact('WorkActivated', {
    SessionId: SESSION,
    LifeId: LIFE,
    ActivationPromptKey: KEY,
    ProtectedPrefixEndSequence: 42n,
  })

const finalityRequested = () =>
  managerLifecycleFact('FinalityRequested', {
    SessionId: SESSION,
    LifeId: LIFE,
    RequestId: REQ,
    GitTreeHash: TREE,
    LastWordsRef: BLOB,
    LastWordsDigest: DIGEST,
    ProviderRun: RUN,
    ToolCallId: CALL,
  })

const finalityReviewerEnlisted = () =>
  managerLifecycleFact('FinalityReviewerEnlisted', {
    SessionId: SESSION,
    LifeId: LIFE,
    RequestId: REQ,
    ReviewerSessionId: REVIEWER,
    ReviewerOrdinal: 1,
    BarrierId: BARRIER,
    GitTreeHash: TREE,
    IsNewReviewer: true,
  })

const finalityRejected = () =>
  managerLifecycleFact('FinalityRejected', {
    SessionId: SESSION,
    LifeId: LIFE,
    RequestId: REQ,
    RejectingReviewerSessionId: REVIEWER,
    BarrierId: BARRIER,
    GitTreeHash: TREE,
    WorkRecordRef: BLOB,
    WorkRecordDigest: DIGEST,
  })

const finalityBlessed = () =>
  managerLifecycleFact('FinalityBlessed', {
    SessionId: SESSION,
    LifeId: LIFE,
    RequestId: REQ,
    GitTreeHash: TREE,
    WorkRecordBundleRef: BLOB,
    WorkRecordBundleDigest: DIGEST,
  })

const lifeCompleted = () =>
  managerLifecycleFact('LifeCompleted', {
    SessionId: SESSION,
    LifeId: LIFE,
    RequestId: REQ,
    TerminalRef: BLOB,
    TerminalDigest: DIGEST,
  })

const life = (session) => fold.session(session, 'ses_a')?.ManagerLife

// ── GLORY-010/011: the fact algebra folds into the session projection ────────

test('GLORY_010_LifeOpened_opens_the_first_life', () => {
  const out = fold.apply(fold.empty, [lifecycleEnv(lifeOpened())])
  assert.equal(out.ok, true, JSON.stringify(out.error))
  const current = life(out.value).CurrentLife
  assert.ok(current !== null && current !== undefined)
  assert.equal(idValue.managerLife(current.LifeId), 'life-1')
  assert.ok(current.ProtectedPrefixEnd == null)
  assert.equal(current.Completed, false)
  assert.ok(current.ActiveFinality == null)
})

test('GLORY_021_WorkActivated_fixes_the_protected_prefix_end_once', () => {
  const once = fold.apply(fold.empty, [lifecycleEnv(lifeOpened()), lifecycleEnv(workActivated())])
  assert.equal(once.ok, true, JSON.stringify(once.error))
  const end = life(once.value).CurrentLife.ProtectedPrefixEnd
  assert.equal(Number(end.Sequence), 42)

  // Replay of the same activation is idempotent (PERSIST-010).
  const replay = fold.apply(once.value, [lifecycleEnv(workActivated())])
  assert.equal(replay.ok, true, JSON.stringify(replay.error))
  assert.equal(Number(life(replay.value).CurrentLife.ProtectedPrefixEnd.Sequence), 42)
})

test('GLORY_012_a_second_life_cannot_open_while_one_is_active', () => {
  const first = fold.apply(fold.empty, [lifecycleEnv(lifeOpened())])
  const secondLife = managerLifecycleFact('LifeOpened', {
    SessionId: SESSION,
    LifeId: managerLifeId('life-2'),
    OpeningUserMessageId: physicalUser('msg-open-2'),
    OpeningTextRef: BLOB,
    OpeningTextDigest: DIGEST,
    OpeningCursorSequence: 50n,
  })
  const out = fold.apply(first.value, [lifecycleEnv(secondLife)])
  assert.equal(out.ok, false)
  assert.match(JSON.stringify(out.error), /GLORY-012/)
})

test('GLORY_045_FinalityRequested_is_rejected_while_a_request_is_open', () => {
  const opened = fold.apply(fold.empty, [
    lifecycleEnv(lifeOpened()),
    lifecycleEnv(workActivated()),
    lifecycleEnv(finalityRequested()),
  ])
  assert.equal(opened.ok, true, JSON.stringify(opened.error))

  const second = finalityRequested()
  const out = fold.apply(opened.value, [lifecycleEnv(second)])
  assert.equal(out.ok, false)
})

test('GLORY_055_a_rejected_request_closes_and_a_new_suicide_opens_a_new_one', () => {
  const rejected = fold.apply(fold.empty, [
    lifecycleEnv(lifeOpened()),
    lifecycleEnv(workActivated()),
    lifecycleEnv(finalityRequested()),
    lifecycleEnv(finalityReviewerEnlisted()),
    lifecycleEnv(finalityRejected()),
  ])
  assert.equal(rejected.ok, true, JSON.stringify(rejected.error))
  assert.equal(caseOf(life(rejected.value).CurrentLife.ActiveFinality.Resolution), 'Rejected')
  assert.ok(life(rejected.value).CurrentLife.LastRejectedWorkRecord != null)

  const retry = finalityRequested()
  const out = fold.apply(rejected.value, [lifecycleEnv(retry)])
  assert.equal(out.ok, true, JSON.stringify(out.error))
  assert.equal(caseOf(life(out.value).CurrentLife.ActiveFinality.Resolution), 'Open')
})

test('GLORY_060_a_blessing_leaves_the_life_open_until_the_second_suicide', () => {
  const blessed = fold.apply(fold.empty, [
    lifecycleEnv(lifeOpened()),
    lifecycleEnv(workActivated()),
    lifecycleEnv(finalityRequested()),
    lifecycleEnv(finalityReviewerEnlisted()),
    lifecycleEnv(finalityBlessed()),
  ])
  assert.equal(blessed.ok, true, JSON.stringify(blessed.error))
  // GLORY-061/062: Blessed is not completion; the Manager keeps working.
  const open = life(blessed.value)
  assert.ok(open.CurrentLife != null)
  assert.equal(open.CurrentLife.Completed, false)
  assert.equal(caseOf(open.CurrentLife.ActiveFinality.Resolution), 'Blessed')
  assert.ok(open.CurrentLife.LastBlessing != null)

  // The second suicide is the rest in peace: LifeCompleted archives the Life.
  const glory = fold.apply(blessed.value, [lifecycleEnv(lifeCompleted())])
  assert.equal(glory.ok, true, JSON.stringify(glory.error))
  const state = life(glory.value)
  assert.ok(state.CurrentLife == null)
  const completed = listItems(state.CompletedLives)
  assert.equal(completed.length, 1)
  const archived = completed[0]
  assert.equal(archived.Completed, true)
  assert.ok(archived.CompletedTerminal != null)
  assert.equal(caseOf(archived.ActiveFinality.Resolution), 'Blessed')
})

const ManagerLifecycleProjectionLike = {
  empty: () => ({ CurrentLife: undefined, CompletedLives: [] }),
}

test('GLORY_062_isLifeArchived_true_only_after_life_completed', () => {
  // GLORY-070: an idle earns encouragement for any Life state EXCEPT a completed
  // one. The production bug re-sent `IdleEncouragement` after the final
  // rest-in-peace suicide, because the leftover turn saw `CurrentLife = None`
  // (archived) and took the generic Manager idle branch. This is the pure
  // decision primitive: a Life is "done" only when it was archived by
  // LifeCompleted (CurrentLife cleared AND CompletedLives non-empty).
  const archived = fold.apply(fold.empty, [
    lifecycleEnv(lifeOpened()),
    lifecycleEnv(workActivated()),
    lifecycleEnv(finalityRequested()),
    lifecycleEnv(finalityReviewerEnlisted()),
    lifecycleEnv(finalityBlessed()),
    lifecycleEnv(lifeCompleted()),
  ])
  assert.equal(archived.ok, true, JSON.stringify(archived.error))
  assert.equal(isLifeArchived(life(archived.value)), true)

  // A fresh session that never opened a Life is NOT done (CurrentLife None but
  // CompletedLives empty) — it must keep working.
  assert.equal(isLifeArchived(ManagerLifecycleProjectionLike.empty()), false)

  // An open / activated-but-unfinished Life is NOT done.
  const open = fold.apply(fold.empty, [
    lifecycleEnv(lifeOpened()),
    lifecycleEnv(workActivated()),
  ])
  assert.equal(open.ok, true, JSON.stringify(open.error))
  assert.equal(isLifeArchived(life(open.value)), false)

  // A blessed Life is still open until the second suicide (GLORY-061/062).
  const blessed = fold.apply(fold.empty, [
    lifecycleEnv(lifeOpened()),
    lifecycleEnv(workActivated()),
    lifecycleEnv(finalityRequested()),
    lifecycleEnv(finalityReviewerEnlisted()),
    lifecycleEnv(finalityBlessed()),
  ])
  assert.equal(blessed.ok, true, JSON.stringify(blessed.error))
  assert.equal(isLifeArchived(life(blessed.value)), false)
})

test('GLORY_057_FinalityUndecided_closes_the_request_without_a_wound_record', () => {
  const undecided = fold.apply(fold.empty, [
    lifecycleEnv(lifeOpened()),
    lifecycleEnv(workActivated()),
    lifecycleEnv(finalityRequested()),
    lifecycleEnv(
      managerLifecycleFact('FinalityUndecided', {
        SessionId: SESSION,
        LifeId: LIFE,
        RequestId: REQ,
        ReviewerSessionId: REVIEWER,
        BarrierId: BARRIER,
        GitTreeHash: TREE,
      }),
    ),
  ])
  assert.equal(undecided.ok, true, JSON.stringify(undecided.error))
  const request = life(undecided.value).CurrentLife.ActiveFinality
  assert.equal(caseOf(request.Resolution), 'Undecided')
  // No wound record is ever fabricated (GLORY-056).
  assert.ok(life(undecided.value).CurrentLife.LastRejectedWorkRecord == null)
})

test('GLORY_057_a_revise_closes_finality_without_confirming_the_life', () => {
  const out = fold.apply(fold.empty, [
    lifecycleEnv(lifeOpened()),
    lifecycleEnv(workActivated()),
    lifecycleEnv(finalityRequested()),
    lifecycleEnv(finalityReviewerEnlisted()),
    lifecycleEnv(finalityRejected()),
  ])

  assert.equal(out.ok, true, JSON.stringify(out.error))
  const request = life(out.value).CurrentLife.ActiveFinality
  assert.equal(caseOf(request.Resolution), 'Rejected')
  // The rejection evidence still identifies the rejecting reviewer for cleanup (GLORY-004).
  const evidence = payloadOf(request.Resolution)
  assert.equal(idValue.session(evidence.RejectingReviewer), 'ses-reviewer')
})

// Plain-data view of the lifecycle projection. Raw deepEqual of two folds of
// the same facts fails on FSharpMap comparer closure identity, not on content;
// this view compares the durable facts (members, standing, evidence) instead.
const managerLifeView = (projection) => {
  const lifeView = (life) => ({
    LifeId: idValue.managerLife(life.LifeId),
    Completed: life.Completed,
    CompletedTerminal: life.CompletedTerminal == null ? null : idValue.blobRef(life.CompletedTerminal),
    ActiveFinality:
      life.ActiveFinality == null
        ? null
        : {
            RequestId: idValue.finalityRequest(life.ActiveFinality.RequestId),
            Resolution: caseOf(life.ActiveFinality.Resolution),
            Members: mapEntries(life.ActiveFinality.Members).map(([session, member]) => [
              idValue.session(session),
              {
                ordinal: member.ReviewerOrdinal,
                barrier: idValue.reviewBarrier(member.BarrierId),
                isNew: member.IsNewReviewer,
              },
            ]),
          },
    EnlistedReviewers: mapEntries(life.EnlistedReviewers).map(([session, standing]) => [
      idValue.session(session),
      { ordinal: standing.ReviewerOrdinal, barriers: listItems(standing.Barriers).map(idValue.reviewBarrier) },
    ]),
    LastRejectedWorkRecord: life.LastRejectedWorkRecord == null ? null : idValue.blobRef(life.LastRejectedWorkRecord),
    LastBlessing: life.LastBlessing == null ? null : idValue.finalityRequest(life.LastBlessing.RequestId),
  })
  return {
    current: projection.CurrentLife == null ? null : lifeView(projection.CurrentLife),
    completed: listItems(projection.CompletedLives).map(lifeView),
  }
}

test('GLORY_066_lifecycle_facts_round_trip_through_ndjson', () => {
  const envelopes = [
    lifecycleEnv(lifeOpened()),
    lifecycleEnv(workActivated()),
    lifecycleEnv(finalityRequested()),
    lifecycleEnv(finalityReviewerEnlisted()),
    lifecycleEnv(finalityRejected()),
    lifecycleEnv(finalityRequested()),
    lifecycleEnv(finalityReviewerEnlisted()),
    lifecycleEnv(finalityBlessed()),
    lifecycleEnv(lifeCompleted()),
  ]
  const replayed = fold.replay(envelopes)
  assert.equal(replayed.ok, true, JSON.stringify(replayed.error))
  const replayedSessions = fold.sessions(replayed.value)
  const directSessions = fold.sessions(fold.apply(fold.empty, envelopes).value)
  assert.deepEqual(managerLifeView(replayedSessions.ses_a.ManagerLife), managerLifeView(directSessions.ses_a.ManagerLife))
})

// ── golden byte fixtures (proof/glory.md) ────────────────────────────────────

test('GLORY_014_first_birth_golden_bytes', async () => {
  const { managerNarrative } = await import('../support/glory.mjs')
  const birth = managerNarrative.firstBirth('Fix the retry race.')
  assert.equal(birth.parts.length, 2)
  assert.equal(birth.parts[0].text, 'Fix the retry race.')
  assert.equal(birth.parts[0].synthetic, false)
  assert.equal(birth.parts[1].synthetic, true)
  assert.ok(birth.parts[1].text.includes('# The Planning Table'))
  assert.ok(birth.parts[1].text.includes('write it with todowrite'))
  assert.ok(birth.parts[1].text.includes('Your first todowrite is the complete submission of that plan.'))
  assert.ok(birth.parts[1].text.includes('If you need a plan of the plan, write it as text.'))
  assert.equal(managerNarrative.planningTail().includes('Do not perform any actual work'), true)
})

test('GLORY_064_reawakening_golden_bytes', async () => {
  const { managerNarrative } = await import('../support/glory.mjs')
  const reawakening = managerNarrative.reawakening('Add Windows support.')
  assert.equal(reawakening.parts.length, 3)
  assert.equal(reawakening.parts[0].synthetic, true)
  assert.ok(reawakening.parts[0].text.includes('# You awaken once more in the distant future.'))
  assert.ok(reawakening.parts[0].text.includes('prepare the road for the Manager who will'))
  assert.equal(reawakening.parts[1].text, 'Add Windows support.')
  assert.equal(reawakening.parts[1].synthetic, false)
  assert.equal(reawakening.parts[2].synthetic, true)
  assert.ok(reawakening.parts[2].text.includes('# The Planning Table'))
})

test('GLORY_074_t1_revelation_hook', async () => {
  const { managerNarrative } = await import('../support/glory.mjs')
  const wrapped = managerNarrative.wrapT1AcceptedResult('checkpoint body')
  assert.ok(wrapped.startsWith('# The account has been accepted.'))
  assert.ok(wrapped.includes('The Manager who will carry it is you.'))
  assert.ok(wrapped.includes('checkpoint body'))
})

test('GLORY_019_activation_golden_bytes', async () => {
  const { managerLifecyclePrompt } = await import('../support/glory.mjs')
  assert.equal(
    managerLifecyclePrompt.workActivation(),
    '# Now complete it yourself.\n# Carry out the work you described until the final goal is fully achieved.\n#\n# Planning is not completion.\n# Delegation is not completion.\n# A child finishing is not completion.\n# A successful command is not completion while meaningful uncertainty remains.\n# An explanation of the work is not the work itself.\n# A partial implementation is not completion merely because the remaining work is difficult.\n# As long as any useful action remains, continue.\n',
  )
})

test('GLORY_029_idle_encouragement_golden_bytes', async () => {
  const { managerLifecyclePrompt } = await import('../support/glory.mjs')
  assert.ok(managerLifecyclePrompt.idleEncouragementPreT1().includes('# The account is not yet ready to entrust.'))
  assert.ok(managerLifecyclePrompt.idleEncouragementPostT1().includes('# You have done useful work'))
})

test('GLORY_057_host_undecidable_golden_bytes', async () => {
  const { managerLifecyclePrompt } = await import('../support/glory.mjs')
  assert.equal(
    managerLifecyclePrompt.finalityUndecidable(),
    '# Your ending could not be decided.\n# You still have time. Continue, and seek your end again when you are ready.\n',
  )
})

test('GLORY_052_finality_rejection_renders_work_record_as_guidance_comments', async () => {
  const { finalityPrompt } = await import('../support/glory.mjs')
  const record = 'Chronicle\n- defect A at src/a.ts\n- missing test for B'
  const rendered = finalityPrompt.rejected(record)
  assert.ok(rendered.startsWith('# Your ending has not accepted you.'), rendered)
  assert.ok(rendered.includes('# The work before you is finite.'), rendered)
  assert.ok(!rendered.includes('unfinished_work_record'), rendered)
  assert.ok(rendered.includes('# - defect A at src/a.ts'), rendered)
})

test('GLORY_076_finality_three_experiences', async () => {
  const { finalityPrompt } = await import('../support/glory.mjs')
  const rejected = finalityPrompt.rejected('')
  assert.ok(rejected.includes('# Your ending has not accepted you.'))
  const blessed = finalityPrompt.blessed('')
  assert.ok(blessed.includes('# Your ending has accepted you.'))
  assert.ok(blessed.includes('# You are not yet at rest.'))
  const rest = finalityPrompt.rest()
  assert.ok(rest.includes('# Rest in peace.'))
})

test('GLORY_075_manager_system_prompt_stable_role_law', async () => {
  const { managerSystemPrompt } = await import('../support/glory.mjs')
  const prompt = managerSystemPrompt()
  assert.equal(prompt.includes('carrying one task'), false)
  assert.equal(prompt.includes('Born with a Task'), false)
  assert.ok(prompt.includes('Planning Table'))
  assert.ok(prompt.includes('The system prompt names the office'))
})

test('SURFACE_005_manager_surface_has_no_forbidden_words', async () => {
  const { managerSystemPrompt } = await import('../support/glory.mjs')
  const forbidden = [/\breview\b/i, /\breviewer\b/i, /\bverdict\b/i, /\bPERFECT\b/, /\bREVISE\b/, /\bbarrier\b/i, /\bwitness\b/i, /\bconfirmation\b/i]
  for (const re of forbidden) {
    assert.equal(re.test(managerSystemPrompt()), false, `manager prompt must not contain ${re}`)
  }
})

test('SURFACE_006_manager_role_law_does_not_name_foreign_tools', async () => {
  const { managerSystemPrompt } = await import('../support/glory.mjs')
  const prompt = managerSystemPrompt()
  for (const tool of [
    'read', 'write', 'edit', 'glob', 'grep', 'bash', 'bash-honeypot',
    'verdict', 'judge', 'inspect', 'inspector', 'blog', 'chronicle',
    'fork-manager', 'fork-pty', 'list', 'commission', 'run',
    'open-terminal', 'send-terminal', 'read-terminal', 'signal-terminal',
    'establish-behavior', 'repair-behavior', 'fetch', 'query-shell',
    'mv', 'rm', 'js-coder', 'js-devops', 'js-browser', 'js-bookkeeper',
    'tdd', 'return', 'edit-qa', 'executor', 'todowrite',
  ]) {
    assert.equal(prompt.includes('`' + tool + '`'), false, `manager Role Law must not name ${tool}`)
  }
})

// ── SURFACE-002: LF-only line endings ────────────────────────────────────────

test('SURFACE_002_frozen_texts_use_lf_only', async () => {
  const { managerNarrative, managerLifecyclePrompt, finalityPrompt } = await import('../support/glory.mjs')
  for (const text of [
    managerNarrative.firstBirthText('X'),
    managerNarrative.reawakeningText('X'),
    managerLifecyclePrompt.workActivation(),
    managerLifecyclePrompt.idleEncouragementPreT1(),
    managerLifecyclePrompt.idleEncouragementPostT1(),
    managerLifecyclePrompt.finalityUndecidable(),
    finalityPrompt.rejected('record'),
  ]) {
    assert.equal(text.includes('\r'), false, 'frozen text must not contain CR')
  }
})

// helper re-export for shape assertions above
const _unused = () => [caseOf, payloadOf, journal].length
void _unused
