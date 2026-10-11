import test from 'node:test';
import assert from 'node:assert/strict';
import { PlanningSurface } from '../../../dist/Mission/Planning/Surface.js';

test('WHAT[planning-018] evaluateRecoveryEffects returns DeliveredIdempotent for delivered view', () => {
  const deliveredView = {
    WorkId: 'work-rec-deliv',
    ActiveIncumbencyId: null,
    ActiveStage: null,
    ActivePhase: null,
    RetiredCount: 2,
    LatestRetirementOutcome: 'Delivered',
    Delivered: true,
    DeliveryDigest: 'sha256:abc123def456',
    DeliveryPath: 'plan/work-rec-deliv/plan.md',
    BoundDevOpsId: 'devops-1',
    PendingAskQuestion: null,
    PendingAskIncumbencyId: null,
    PendingAskCursor: null
  };

  const readPlanFile = (path) => {
    assert.equal(path, 'plan/work-rec-deliv/plan.md');
    return '# Delivered Content';
  };

  const effects = PlanningSurface.evaluateRecoveryEffects([deliveredView], readPlanFile);
  assert.equal(effects.length, 1);
  assert.equal(effects[0].kind, 'DeliveredIdempotent');
  assert.equal(effects[0].workId, 'work-rec-deliv');
  assert.equal(effects[0].path, 'plan/work-rec-deliv/plan.md');
});

test('WHAT[planning-018] evaluateRecoveryEffects returns ActiveRebound with planExists true when plan file exists', () => {
  const activeView = {
    WorkId: 'work-rec-act-exist',
    ActiveIncumbencyId: 'inc-act-1',
    ActiveStage: 'S2',
    ActivePhase: 'WorkOwned',
    RetiredCount: 1,
    LatestRetirementOutcome: 'Continue',
    Delivered: false,
    DeliveryDigest: null,
    DeliveryPath: 'plan/work-rec-act-exist/plan.md',
    BoundDevOpsId: 'devops-1',
    PendingAskQuestion: null,
    PendingAskIncumbencyId: null,
    PendingAskCursor: null
  };

  const readPlanFile = (path) => {
    if (path === 'plan/work-rec-act-exist/plan.md') {
      return '# Plan content at S2';
    }
    return null;
  };

  const effects = PlanningSurface.evaluateRecoveryEffects([activeView], readPlanFile);
  assert.equal(effects.length, 1);
  assert.equal(effects[0].kind, 'ActiveRebound');
  assert.equal(effects[0].workId, 'work-rec-act-exist');
  assert.equal(effects[0].incumbencyId, 'inc-act-1');
  assert.equal(effects[0].stage, 'S2');
  assert.equal(effects[0].planExists, true);
});

test('WHAT[planning-018] evaluateRecoveryEffects returns ActiveRebound with planExists false when plan file is missing', () => {
  const activeMissingView = {
    WorkId: 'work-rec-act-missing',
    ActiveIncumbencyId: 'inc-act-2',
    ActiveStage: 'S1',
    ActivePhase: 'WorkOwned',
    RetiredCount: 0,
    LatestRetirementOutcome: null,
    Delivered: false,
    DeliveryDigest: null,
    DeliveryPath: null,
    BoundDevOpsId: 'devops-1',
    PendingAskQuestion: null,
    PendingAskIncumbencyId: null,
    PendingAskCursor: null
  };

  const readPlanFile = () => null;

  const effects = PlanningSurface.evaluateRecoveryEffects([activeMissingView], readPlanFile);
  assert.equal(effects.length, 1);
  assert.equal(effects[0].kind, 'ActiveRebound');
  assert.equal(effects[0].workId, 'work-rec-act-missing');
  assert.equal(effects[0].incumbencyId, 'inc-act-2');
  assert.equal(effects[0].stage, 'S1');
  assert.equal(effects[0].planExists, false);
});

test('WHAT[planning-018] evaluateRecoveryEffects returns Conflict for conflicting projection view', () => {
  const conflictView = {
    WorkId: 'work-rec-conflict',
    ActiveIncumbencyId: 'inc-act-3',
    ActiveStage: 'S2',
    ActivePhase: 'WorkOwned',
    RetiredCount: 1,
    LatestRetirementOutcome: 'Delivered',
    Delivered: true,
    DeliveryDigest: 'sha256:conflicthash',
    DeliveryPath: 'plan/work-rec-conflict/plan.md',
    BoundDevOpsId: 'devops-1',
    PendingAskQuestion: null,
    PendingAskIncumbencyId: null,
    PendingAskCursor: null
  };

  const readPlanFile = () => null;

  const effects = PlanningSurface.evaluateRecoveryEffects([conflictView], readPlanFile);
  assert.equal(effects.length, 1);
  assert.equal(effects[0].kind, 'Conflict');
  assert.equal(effects[0].workId, 'work-rec-conflict');
  assert.match(effects[0].reason, /Conflict: Plan work view is both Delivered and has an Active incumbency/);
});

test('WHAT[planning-018] evaluateRecoveryEffects returns empty list for Nothing view', () => {
  const nothingView = {
    WorkId: 'work-rec-nothing',
    ActiveIncumbencyId: null,
    ActiveStage: null,
    ActivePhase: null,
    RetiredCount: 0,
    LatestRetirementOutcome: null,
    Delivered: false,
    DeliveryDigest: null,
    DeliveryPath: null,
    BoundDevOpsId: null,
    PendingAskQuestion: null,
    PendingAskIncumbencyId: null,
    PendingAskCursor: null
  };

  const readPlanFile = () => null;

  const effects = PlanningSurface.evaluateRecoveryEffects([nothingView], readPlanFile);
  assert.equal(effects.length, 0);
});

test('WHAT[planning-018] evaluateRecoveryEffects returns Conflict when WorkId is missing', () => {
  const missingWorkIdView = {
    WorkId: null,
    ActiveIncumbencyId: 'inc-act-4',
    ActiveStage: 'S1',
    ActivePhase: 'WorkOwned',
    RetiredCount: 0,
    LatestRetirementOutcome: null,
    Delivered: false,
    DeliveryDigest: null,
    DeliveryPath: null,
    BoundDevOpsId: null,
    PendingAskQuestion: null,
    PendingAskIncumbencyId: null,
    PendingAskCursor: null
  };

  const readPlanFile = () => null;

  const effects = PlanningSurface.evaluateRecoveryEffects([missingWorkIdView], readPlanFile);
  assert.equal(effects.length, 1);
  assert.equal(effects[0].kind, 'Conflict');
  assert.equal(effects[0].workId, '');
  assert.equal(effects[0].reason, '视图缺少 WorkId');
});

test('WHAT[planning-018] evaluateRecoveryEffects processes heterogeneous batch preserving order and omitting Nothing', () => {
  const views = [
    {
      WorkId: 'work-batch-deliv',
      ActiveIncumbencyId: null,
      ActiveStage: null,
      ActivePhase: null,
      RetiredCount: 1,
      LatestRetirementOutcome: 'Delivered',
      Delivered: true,
      DeliveryDigest: 'sha256:d1',
      DeliveryPath: 'plan/work-batch-deliv/plan.md',
      BoundDevOpsId: 'devops-1'
    },
    {
      WorkId: 'work-batch-nothing',
      ActiveIncumbencyId: null,
      ActiveStage: null,
      ActivePhase: null,
      RetiredCount: 0,
      LatestRetirementOutcome: null,
      Delivered: false,
      DeliveryDigest: null,
      DeliveryPath: null,
      BoundDevOpsId: null
    },
    {
      WorkId: 'work-batch-active',
      ActiveIncumbencyId: 'inc-batch-1',
      ActiveStage: 'S3',
      ActivePhase: 'WorkOwned',
      RetiredCount: 2,
      LatestRetirementOutcome: 'Continue',
      Delivered: false,
      DeliveryDigest: null,
      DeliveryPath: 'plan/work-batch-active/plan.md',
      BoundDevOpsId: 'devops-1'
    },
    {
      WorkId: null,
      ActiveIncumbencyId: null,
      ActiveStage: null,
      ActivePhase: null,
      RetiredCount: 0,
      LatestRetirementOutcome: null,
      Delivered: false,
      DeliveryDigest: null,
      DeliveryPath: null,
      BoundDevOpsId: null
    }
  ];

  const readPlanFile = (path) => {
    if (path === 'plan/work-batch-deliv/plan.md') return '# Delivered';
    if (path === 'plan/work-batch-active/plan.md') return '# Active plan S3';
    return null;
  };

  const effects = PlanningSurface.evaluateRecoveryEffects(views, readPlanFile);
  assert.equal(effects.length, 3);
  assert.equal(effects[0].kind, 'DeliveredIdempotent');
  assert.equal(effects[0].workId, 'work-batch-deliv');
  assert.equal(effects[1].kind, 'ActiveRebound');
  assert.equal(effects[1].workId, 'work-batch-active');
  assert.equal(effects[1].planExists, true);
  assert.equal(effects[2].kind, 'Conflict');
  assert.equal(effects[2].reason, '视图缺少 WorkId');
});
