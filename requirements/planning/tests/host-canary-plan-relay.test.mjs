import test from 'node:test';
import assert from 'node:assert/strict';
import { PlanningSurface, decideAction } from '../../../dist/Mission/Planning/Surface.js';
PlanningSurface.decideAction = decideAction;

// Physical Host Canary: continuous tenure relay in a single physical session
// under the new planning contract.
// Scenario: S1 -> review(Promoted) -> S2 -> deliver(relay) -> S2 -> review(Promoted) -> S3
//           -> deliver(relay) -> S3 -> review(Promoted, authorized) -> deliver -> PlanDelivered
//           -> reprompt -> PlanReopened -> deliver again.
// Note: pure contract assertions below verify the exact invariant ladder on
// PlanningSurface. Live daemon execution requires a physical OpenCode host.

test('WHAT[planning-canary] review-driven relay and delivery across a single session contract verification', async () => {
  const workId = 'work-canary-relay-1';
  const root = '/workspace/test';
  const digest = 'sha256:7f83b1657ff1fc53b92dc18148a1d65dfc2d4b1fa3d677284addd200126d9069';
  const deliveryPath = 'plan/work-canary-relay-1/plan.md';
  const execSessionId = 'exec-canary-1';

  // --- TENURE 1: Stage S1 ---
  let state = PlanningSurface.empty();
  state = PlanningSurface.applyWorkEvent(PlanningSurface.workOpened(workId, root), state).state;
  state = PlanningSurface.applyWorkEvent(PlanningSurface.incumbencyOpened(workId, 'inc-s1', 'S1', 10n), state).state;

  // 1. The four tools are uniformly permitted on an active tenure; retired tools are rejected
  for (const action of ['js-plan', 'ask', 'review', 'deliver']) {
    const decision = PlanningSurface.decideAction('S1', action, false, false);
    assert.equal(decision.ok, true, `S1 must permit ${action}`);
  }
  const retiredHandoff = PlanningSurface.decideAction('S1', 'handoff', false, false);
  assert.equal(retiredHandoff.ok, false);
  assert.equal(retiredHandoff.rejection, 'HandoffRetired');
  const retiredResume = PlanningSurface.decideAction('S1', 'resume', false, false);
  assert.equal(retiredResume.ok, false);
  assert.equal(retiredResume.rejection, 'ResumeRetired');

  // 2. S1 tenure isolation: first request triggers prefix reanchor
  const s1Messages = [
    { role: 'user', content: 'Create a plan for caching architecture', id: 'u-1', cursor: 5n },
    { role: 'assistant', content: 'Analyzing existing facts in S1...', id: 'a-1', cursor: 12n },
  ];
  const s1TenureObj = {
    workId: workId,
    incumbencyId: 'inc-s1',
    stage: 'S1',
    openingCursor: 10n,
    previousRange: null,
    isFreshHandover: true,
  };
  const s1Assembled = PlanningSurface.assembleTenureMessages(s1Messages, s1TenureObj, () => '');
  assert.equal(s1Assembled.reanchorRequested, true);
  assert.equal(s1Assembled.messages.length, 2);

  // 3. The S1 review promotes the tenure to S2 in place
  state = PlanningSurface.applyWorkEvent(PlanningSurface.reviewed('inc-s1', 'S1', 'Promoted'), state).state;
  assert.equal(state.Active.Stage.name, 'S2');

  // 4. Unauthorized deliver relays: retire Relayed and open the successor at the same stage
  state = PlanningSurface.applyWorkEvent(PlanningSurface.incumbencyRetired('inc-s1', 'Relayed', 20n), state).state;
  state = PlanningSurface.applyWorkEvent(PlanningSurface.incumbencyOpened(workId, 'inc-s2', 'S2', 21n), state).state;

  // --- TENURE 2: Stage S2 ---
  const s2Messages = [
    { role: 'user', content: 'Create a plan for caching architecture', id: 'u-1', cursor: 5n },
    { role: 'assistant', content: 'S1 old assistant message', id: 'a-old-1', cursor: 15n },
    { role: 'tool', name: 'js-plan', content: 'S1 old draft', id: 't-old-1', cursor: 18n },
  ];
  const s2TenureObj = {
    workId: workId,
    incumbencyId: 'inc-s2',
    stage: 'S2',
    openingCursor: 21n,
    previousRange: [10n, 20n],
    isFreshHandover: true,
  };
  const s2Assembled = PlanningSurface.assembleTenureMessages(s2Messages, s2TenureObj, (r) => `Summary of S1 (${r.start}-${r.end})`);
  assert.equal(s2Assembled.reanchorRequested, true);
  // Prior S1 assistant & tool messages must be stripped completely
  const s2Roles = s2Assembled.messages.map(m => m.role);
  assert.equal(s2Roles.includes('assistant'), false);
  assert.equal(s2Roles.includes('tool'), false);
  // Synthetic LWR_prev inserted after the original U message
  assert.equal(s2Assembled.messages[0].content, 'Create a plan for caching architecture');
  assert.equal(s2Assembled.messages[1].id, 'lwr-prev-inc-s2');
  assert.equal(s2Assembled.messages[1].content, 'Summary of S1 (10-20)');

  // The S2 review promotes to S3, then a relay brings the third tenure
  state = PlanningSurface.applyWorkEvent(PlanningSurface.reviewed('inc-s2', 'S2', 'Promoted'), state).state;
  assert.equal(state.Active.Stage.name, 'S3');
  state = PlanningSurface.applyWorkEvent(PlanningSurface.incumbencyRetired('inc-s2', 'Relayed', 30n), state).state;
  state = PlanningSurface.applyWorkEvent(PlanningSurface.incumbencyOpened(workId, 'inc-s3', 'S3', 31n), state).state;

  // --- TENURE 3: Stage S3, review grants authorization ---
  state = PlanningSurface.applyWorkEvent(PlanningSurface.reviewed('inc-s3', 'S3', 'Promoted'), state).state;
  assert.equal(state.Active.Authorized, true, 'the S3 review must grant delivery authorization');
  assert.equal(state.Active.Stage.name, 'S3', 'S3 promotion grants authorization, not a fourth stage');

  // Authorized delivery retires Delivered and records the exec session
  state = PlanningSurface.applyWorkEvent(PlanningSurface.incumbencyRetired('inc-s3', PlanningSurface.PlanRetirementOutcome.Delivered, 40n), state).state;
  state = PlanningSurface.applyWorkEvent(PlanningSurface.delivered('inc-s3', workId, digest, deliveryPath, execSessionId), state).state;

  // PlanDelivered recorded with digest, path and execSessionId
  assert.ok(state.Delivered != null);
  assert.equal(state.Delivered.Digest, digest);
  assert.equal(state.Delivered.Path, deliveryPath);
  assert.equal(state.Delivered.ExecSessionId, execSessionId);

  // Post-delivery lockdown: ask and deliver are rejected until reopened
  const postDeliver = PlanningSurface.decideAction('S3', 'deliver', true, false);
  assert.equal(postDeliver.ok, false);
  const postAsk = PlanningSurface.decideAction('S3', 'ask', true, false);
  assert.equal(postAsk.ok, false);

  // 5. Reprompt reopens the work and restores the original tenure
  state = PlanningSurface.applyWorkEvent(PlanningSurface.reopened('inc-s3'), state).state;
  assert.equal(PlanningSurface.isDelivered(state), false);
  assert.equal(state.Active.Id, 'inc-s3', 'reopened work must reuse the original incumbency');
  assert.equal(state.Active.Stage.name, 'S3');
  assert.equal(state.Active.Authorized, true, 'reopened tenure keeps its authorization');
  // After reopen, the mutation tools are permitted again
  const reopenedDeliver = PlanningSurface.decideAction('S3', 'deliver', false, false);
  assert.equal(reopenedDeliver.ok, true);

  // 6. Crash recovery idempotency: the journal replays the full durable chain,
  // so fold-level idempotence means replaying the same event sequence from
  // empty converges on the delivered state of the live run — never guessing
  // the stage from text.
  const replayEvents = [
    PlanningSurface.workOpened(workId, root),
    PlanningSurface.incumbencyOpened(workId, 'inc-s1', 'S1', 10n),
    PlanningSurface.reviewed('inc-s1', 'S1', 'Promoted'),
    PlanningSurface.incumbencyRetired('inc-s1', 'Relayed', 20n),
    PlanningSurface.incumbencyOpened(workId, 'inc-s2', 'S2', 21n),
    PlanningSurface.reviewed('inc-s2', 'S2', 'Promoted'),
    PlanningSurface.incumbencyRetired('inc-s2', 'Relayed', 30n),
    PlanningSurface.incumbencyOpened(workId, 'inc-s3', 'S3', 31n),
    PlanningSurface.reviewed('inc-s3', 'S3', 'Promoted'),
    PlanningSurface.incumbencyRetired('inc-s3', PlanningSurface.PlanRetirementOutcome.Delivered, 40n),
    PlanningSurface.delivered('inc-s3', workId, digest, deliveryPath, execSessionId),
  ];

  let deliveredState = PlanningSurface.empty();
  for (const event of replayEvents) {
    const step = PlanningSurface.applyWorkEvent(event, deliveredState);
    assert.equal(step.ok, true, 'replay must fold every event of the durable chain cleanly');
    deliveredState = step.state;
  }

  assert.equal(PlanningSurface.isDelivered(deliveredState), true);
  // The replay converges on the delivered state the live run held before the
  // step-5 reopen: same receipt, and no active incumbency because the
  // Delivered retirement cleared it.
  assert.equal(deliveredState.Delivered.Digest, digest);
  assert.equal(deliveredState.Delivered.ExecSessionId, execSessionId);
  assert.equal(deliveredState.Active, undefined);
  const view = {
    WorkId: workId,
    ActiveIncumbencyId: null,
    ActiveStage: null,
    ActivePhase: null,
    RetiredCount: 1,
    LatestRetirementOutcome: 'Delivered',
    Delivered: true,
    DeliveryDigest: digest,
    DeliveryPath: deliveryPath,
    BoundDevOpsId: null,
  };
  const recoveryPos = PlanningSurface.planRecoveryPosition(view, () => '# Text containing misleading S1 S2 S3 keywords');
  assert.equal(recoveryPos.kind, 'Delivered');
  assert.equal(recoveryPos.digest, digest);
  assert.equal(recoveryPos.path, deliveryPath);
  assert.ok(recoveryPos.receipt);
});

// Physical OpenCode daemon integration requirement mark:
test.todo('WHAT[planning-canary] physical OpenCode host canary live execution of review-driven relay in a single session');
