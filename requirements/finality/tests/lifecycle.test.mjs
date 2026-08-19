// FINALITY lifecycle laws: plain lifecycle events enter the registered
// FinalitySurface; only JS-shaped projections leave it.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as finality from '../../../dist/Mission/Manager/FinalitySurface.js'
import {
  blessed,
  firstBirth,
  firstBirthText,
  idleEncouragementPostT1,
  idleEncouragementPreT1,
  managerSystemPrompt,
  rejected,
  reawakening as managerReawakening,
  reawakeningText,
  rest,
  workActivation,
} from '../../../dist/Mission/Finality/PromptSurface.js'

const SESSION = 'ses_a'
const LIFE = 'life-1'
const REVIEWER = 'ses-reviewer'
const REQUEST = 'req-1'
const TREE = 'tree-1'
const BARRIER = 'bar-1'

const lifeOpened = (lifeId = LIFE, cursor = 1) => ({
  kind: 'life-opened',
  sessionId: SESSION,
  lifeId,
  openingUserMessageId: `msg-open-${lifeId}`,
  openingTextRef: 'blob-1',
  openingTextDigest: 'd-1',
  openingCursorSequence: cursor,
})

const workActivated = (lifeId = LIFE) => ({
  kind: 'work-activated',
  sessionId: SESSION,
  lifeId,
  activationPromptKey: 'key-1',
  protectedPrefixEndSequence: 42,
})

const finalityRequested = (requestId = REQUEST, toolCallId = 'call-1') => ({
  kind: 'finality-requested',
  sessionId: SESSION,
  lifeId: LIFE,
  requestId,
  gitTreeHash: TREE,
  lastWordsRef: 'blob-1',
  lastWordsDigest: 'd-1',
  providerRun: 'run-1',
  toolCallId,
})

const finalityReviewerEnlisted = (requestId = REQUEST) => ({
  kind: 'finality-reviewer-enlisted',
  sessionId: SESSION,
  lifeId: LIFE,
  requestId,
  reviewerSessionId: REVIEWER,
  reviewerOrdinal: 1,
  barrierId: BARRIER,
  gitTreeHash: TREE,
  isNewReviewer: true,
})

const finalityRejected = (requestId = REQUEST) => ({
  kind: 'finality-rejected',
  sessionId: SESSION,
  lifeId: LIFE,
  requestId,
  rejectingReviewerSessionId: REVIEWER,
  barrierId: BARRIER,
  gitTreeHash: TREE,
  workRecordRef: 'blob-1',
  workRecordDigest: 'd-1',
})

const finalityBlessed = (requestId = REQUEST) => ({
  kind: 'finality-blessed',
  sessionId: SESSION,
  lifeId: LIFE,
  requestId,
  gitTreeHash: TREE,
  workRecordBundleRef: 'blob-1',
  workRecordBundleDigest: 'd-1',
})

const finalityUndecided = (requestId = REQUEST) => ({
  kind: 'finality-undecided',
  sessionId: SESSION,
  lifeId: LIFE,
  requestId,
  reviewerSessionId: REVIEWER,
  barrierId: BARRIER,
  gitTreeHash: TREE,
})

const lifeCompleted = (requestId = REQUEST) => ({
  kind: 'life-completed',
  sessionId: SESSION,
  lifeId: LIFE,
  requestId,
  terminalRef: 'blob-terminal',
  terminalDigest: 'digest-terminal',
})

const worldOf = (events) => {
  const result = finality.project(events)
  assert.equal(result.ok, true, JSON.stringify(result.error))
  return result.world
}

const base = (extra = []) => [lifeOpened(), workActivated(), ...extra]

// ── lifecycle projection ────────────────────────────────────────────────────

test('WHAT[FINALITY-021] LifeOpened opens the first life', () => {
  const life = finality.lifeView(worldOf([lifeOpened()]))
  assert.equal(life.lifeId, LIFE)
  assert.equal(life.openingCursorSequence, 1)
  assert.equal(life.protectedPrefixEnd, null)
  assert.equal(life.completed, false)
  assert.equal(life.activeFinality, null)
})

test('WHAT[FINALITY-022] a second life cannot open while one is active', () => {
  const result = finality.project([lifeOpened(), lifeOpened('life-2', 50)])
  assert.equal(result.ok, false)
  assert.match(JSON.stringify(result.error), /GLORY-012|LifeAlreadyOpen/)
})

test('WHAT[FINALITY-008] FinalityRequested is rejected while a request is open', () => {
  const result = finality.project(base([finalityRequested(), finalityRequested('req-2', 'call-2')]))
  assert.equal(result.ok, false)
  assert.match(JSON.stringify(result.error), /FinalityAlreadyActive|FinalityRequested/)
})

test('WHAT[FINALITY-008] a rejected request closes and a new suicide opens a new one', () => {
  const result = finality.project(
    base([finalityRequested(), finalityReviewerEnlisted(), finalityRejected(), finalityRequested('req-2', 'call-2')]),
  )
  assert.equal(result.ok, true, JSON.stringify(result.error))
  const life = finality.lifeView(result.world)
  assert.equal(life.activeFinality.requestId, 'req-2')
  assert.equal(life.activeFinality.resolution.kind, 'open')
  assert.equal(life.lastRejectedWorkRecord, 'blob-1')
})

test('WHAT[FINALITY-016] a blessing leaves the life open until the second suicide', () => {
  const life = finality.lifeView(worldOf(base([finalityRequested(), finalityReviewerEnlisted(), finalityBlessed()])))
  assert.equal(life.completed, false)
  assert.equal(life.activeFinality.resolution.kind, 'blessed')
  assert.equal(life.lastBlessing.requestId, REQUEST)
})

test('WHAT[FINALITY-017] the second suicide is the rest: LifeCompleted archives the Life', () => {
  const result = finality.project(
    base([finalityRequested(), finalityReviewerEnlisted(), finalityBlessed(), lifeCompleted()]),
  )
  assert.equal(result.ok, true, JSON.stringify(result.error))
  assert.equal(finality.lifeView(result.world), null)
  const archived = finality.archivedLivesView(result.world)
  assert.equal(archived.length, 1)
  assert.equal(archived[0].completed, true)
  assert.equal(archived[0].completedTerminal, 'blob-terminal')
  assert.equal(archived[0].activeFinality.resolution.kind, 'blessed')
})

test('WHAT[FINALITY-017] isLifeArchived true only after life completed', () => {
  const fresh = worldOf([])
  assert.equal(finality.isLifeArchived(fresh), false)

  const open = worldOf(base())
  assert.equal(finality.isLifeArchived(open), false)

  const blessed = worldOf(base([finalityRequested(), finalityReviewerEnlisted(), finalityBlessed()]))
  assert.equal(finality.isLifeArchived(blessed), false)

  const archived = worldOf(
    base([finalityRequested(), finalityReviewerEnlisted(), finalityBlessed(), lifeCompleted()]),
  )
  assert.equal(finality.isLifeArchived(archived), true)
})

test('WHAT[FINALITY-025] legacy FinalityUndecided closes the historical request without a wound record', () => {
  const life = finality.lifeView(worldOf(base([finalityRequested(), finalityUndecided()])))
  assert.equal(life.activeFinality.resolution.kind, 'undecided')
  assert.equal(life.lastRejectedWorkRecord, null)
})

test('WHAT[FINALITY-011] a revise closes finality without confirming the life', () => {
  const life = finality.lifeView(worldOf(base([finalityRequested(), finalityReviewerEnlisted(), finalityRejected()])))
  assert.equal(life.activeFinality.resolution.kind, 'rejected')
  assert.equal(life.activeFinality.resolution.rejectingReviewer, REVIEWER)
  assert.equal(life.completed, false)
})

test('WHAT[FINALITY-021] lifecycle history projection replays identically', () => {
  const history = base([
    finalityRequested(),
    finalityReviewerEnlisted(),
    finalityRejected(),
    finalityRequested('req-2', 'call-2'),
    finalityReviewerEnlisted('req-2'),
    finalityBlessed('req-2'),
    lifeCompleted('req-2'),
  ])
  const direct = finality.project(history)
  assert.equal(direct.ok, true, JSON.stringify(direct.error))

  const replay = finality.project([])
  assert.equal(replay.ok, true, JSON.stringify(replay.error))
  const replayed = finality.applyEvents(replay.world, history)
  assert.equal(replayed.ok, true, JSON.stringify(replayed.error))

  assert.deepEqual(finality.lifeView(replayed.world), finality.lifeView(direct.world))
  assert.deepEqual(finality.archivedLivesView(replayed.world), finality.archivedLivesView(direct.world))
})

// ── golden byte fixtures (provider-language contracts) ──────────────────────

test('WHAT[FINALITY-004] first birth golden bytes: planning commitment is irreversible', async () => {
  const birth = firstBirth('Fix the retry race.')
  assert.equal(birth.parts.length, 2)
  assert.equal(birth.parts[0].text, 'Fix the retry race.')
  assert.equal(birth.parts[0].synthetic, false)
  assert.equal(birth.parts[1].synthetic, true)
  assert.ok(birth.parts[1].text.includes('# The Planning Table'))
  assert.ok(birth.parts[1].text.includes('planComplete=false'))
  assert.ok(birth.parts[1].text.includes('planning work'))
  assert.ok(birth.parts[1].text.includes('planComplete=true'))
  assert.ok(birth.parts[1].text.match(/irreversible|cannot be undone|never returns to false/i))
})

test('WHAT[FINALITY-022] reawakening golden bytes', async () => {
  const reawakening = managerReawakening('Add Windows support.')
  assert.equal(reawakening.parts.length, 3)
  assert.equal(reawakening.parts[0].synthetic, true)
  assert.ok(reawakening.parts[0].text.includes('# You awaken once more in the distant future.'))
  assert.ok(reawakening.parts[0].text.includes('prepare the road for the Manager who will'))
  assert.equal(reawakening.parts[1].text, 'Add Windows support.')
  assert.equal(reawakening.parts[1].synthetic, false)
  assert.equal(reawakening.parts[2].synthetic, true)
  assert.ok(reawakening.parts[2].text.includes('# The Planning Table'))
})

test('WHAT[FINALITY-024] activation golden bytes: planning is not completion', async () => {
  assert.equal(
    workActivation(),
    '# Now complete it yourself.\n# Carry out the work you described until the final goal is fully achieved.\n#\n# Planning is not completion.\n# Delegation is not completion.\n# A child finishing is not completion.\n# A successful command is not completion while meaningful uncertainty remains.\n# An explanation of the work is not the work itself.\n# A partial implementation is not completion merely because the remaining work is difficult.\n# As long as any useful action remains, continue.\n',
  )
})

test('WHAT[FINALITY-019] idle encouragement golden bytes', async () => {
  assert.ok(idleEncouragementPreT1().includes('# The account is not yet ready to entrust.'))
  assert.ok(idleEncouragementPreT1().includes('planComplete=false'))
  assert.ok(idleEncouragementPostT1().includes('# You have done useful work'))
})

test('WHAT[FINALITY-012] finality rejection renders work record as guidance comments', async () => {
  const record = 'Chronicle\n- defect A at src/a.ts\n- missing test for B'
  const rendered = rejected(record)
  assert.ok(rendered.startsWith('# Your ending has not accepted you.'), rendered)
  assert.ok(rendered.includes('# The work before you is finite.'), rendered)
  assert.ok(rendered.includes('# - defect A at src/a.ts'), rendered)
})

test('WHAT[FINALITY-020] rejection rendering exposes no mechanism vocabulary', async () => {
  const record = 'Chronicle\n- defect A at src/a.ts'
  const rendered = rejected(record)
  assert.ok(!rendered.includes('unfinished_work_record'), rendered)
})

test('WHAT[FINALITY-013] finality three experiences', async () => {
  const rejectedText = rejected('')
  assert.ok(rejectedText.includes('# Your ending has not accepted you.'))
  const blessedText = blessed('')
  assert.ok(blessedText.includes('# Your ending has accepted you.'))
  assert.ok(blessedText.includes('# You are not yet at rest.'))
  const restText = rest()
  assert.ok(restText.includes('# Rest in peace.'))
})

test('WHAT[PREFIX-STABILITY-007] manager system prompt stable role law', async () => {
  const prompt = managerSystemPrompt()
  assert.equal(prompt.includes('carrying one task'), false)
  assert.equal(prompt.includes('Born with a Task'), false)
  assert.ok(prompt.includes('Planning Table'))
  assert.ok(prompt.includes('The system prompt names the office'))
})

test('WHAT[FINALITY-020] manager surface has no forbidden words', async () => {
  const forbidden = [
    /\breview\b/i,
    /\breviewer\b/i,
    /\bverdict\b/i,
    /\bPERFECT\b/,
    /\bREVISE\b/,
    /\bbarrier\b/i,
    /\bwitness\b/i,
    /\bconfirmation\b/i,
  ]
  for (const re of forbidden) {
    assert.equal(re.test(managerSystemPrompt()), false, `manager prompt must not contain ${re}`)
  }
})

test('WHAT[FINALITY-020] manager role law does not name foreign tools', async () => {
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

test('WHAT[PROVIDER-LANGUAGE-005] frozen texts use lf only', async () => {
  for (const text of [
    firstBirthText('X'),
    reawakeningText('X'),
    workActivation(),
    idleEncouragementPreT1(),
    idleEncouragementPostT1(),
    rejected('record'),
  ]) {
    assert.equal(text.includes('\r'), false, 'frozen text must not contain CR')
  }
})
