import test from 'node:test';
import assert from 'node:assert/strict';
import { PlanningSurface } from '../../../dist/Mission/Planning/Surface.js';

test('WHAT[planning-018] Delivered view idempotently recovers delivered receipt and never returns Active', () => {
  const view = {
    WorkId: 'work-rec-1',
    ActiveIncumbencyId: null,
    ActiveStage: null,
    ActivePhase: null,
    RetiredCount: 2,
    LatestRetirementOutcome: 'Delivered',
    Delivered: true,
    DeliveryDigest: 'sha256:abc123def456',
    DeliveryPath: 'plan/work-rec-1/plan.md',
    BoundDevOpsId: 'devops-1'
  };

  const readPlanFile = (path) => {
    assert.equal(path, 'plan/work-rec-1/plan.md');
    return '# Final Delivered Plan';
  };

  const pos = PlanningSurface.planRecoveryPosition(view, readPlanFile);
  assert.equal(pos.kind, 'Delivered');
  assert.equal(pos.digest, 'sha256:abc123def456');
  assert.equal(pos.path, 'plan/work-rec-1/plan.md');
  assert.ok(pos.receipt);
  assert.equal(pos.receipt.Digest, 'sha256:abc123def456');
  assert.equal(pos.receipt.Path, 'plan/work-rec-1/plan.md');
});

test('WHAT[planning-018] Active incumbency with existing plan file rebinds stage and marks planExists', () => {
  const view = {
    WorkId: 'work-rec-2',
    ActiveIncumbencyId: 'inc-rec-2',
    ActiveStage: 'S2',
    ActivePhase: 'WorkOwned',
    RetiredCount: 1,
    LatestRetirementOutcome: 'Continue',
    Delivered: false,
    DeliveryDigest: null,
    DeliveryPath: null,
    BoundDevOpsId: 'devops-1'
  };

  const readPlanFile = (path) => {
    assert.equal(path, 'plan/work-rec-2/plan.md');
    return '# Work in progress at S2';
  };

  const pos = PlanningSurface.planRecoveryPosition(view, readPlanFile);
  assert.equal(pos.kind, 'Active');
  assert.equal(pos.incumbencyId, 'inc-rec-2');
  assert.equal(pos.stage, 'S2');
  assert.equal(pos.planExists, true);
});

test('WHAT[planning-018] Active incumbency with missing plan file is continuable and not misidentified as delivered', () => {
  const view = {
    WorkId: 'work-rec-3',
    ActiveIncumbencyId: 'inc-rec-3',
    ActiveStage: 'S1',
    ActivePhase: 'WorkOwned',
    RetiredCount: 0,
    LatestRetirementOutcome: null,
    Delivered: false,
    DeliveryDigest: null,
    DeliveryPath: null,
    BoundDevOpsId: null
  };

  const readPlanFile = (path) => {
    return null; // file does not exist yet
  };

  const pos = PlanningSurface.planRecoveryPosition(view, readPlanFile);
  assert.equal(pos.kind, 'Active');
  assert.equal(pos.incumbencyId, 'inc-rec-3');
  assert.equal(pos.stage, 'S1');
  assert.equal(pos.planExists, false);
});

test('WHAT[planning-018] Empty or non-started view resolves to Nothing without guessing', () => {
  const view = {
    WorkId: 'work-rec-4',
    ActiveIncumbencyId: null,
    ActiveStage: null,
    ActivePhase: null,
    RetiredCount: 0,
    LatestRetirementOutcome: null,
    Delivered: false,
    DeliveryDigest: null,
    DeliveryPath: null,
    BoundDevOpsId: null
  };

  const readPlanFile = () => null;

  const pos = PlanningSurface.planRecoveryPosition(view, readPlanFile);
  assert.equal(pos.kind, 'Nothing');
});

test('WHAT[planning-018] Conflicting view (both Delivered and Active) fails closed with Conflict', () => {
  const view = {
    WorkId: 'work-rec-5',
    ActiveIncumbencyId: 'inc-rec-5',
    ActiveStage: 'S2',
    ActivePhase: 'WorkOwned',
    RetiredCount: 1,
    LatestRetirementOutcome: null,
    Delivered: true,
    DeliveryDigest: 'sha256:conflict',
    DeliveryPath: 'plan/work-rec-5/plan.md',
    BoundDevOpsId: null
  };

  const readPlanFile = () => '# Some plan';

  const pos = PlanningSurface.planRecoveryPosition(view, readPlanFile);
  assert.equal(pos.kind, 'Conflict');
  assert.ok(typeof pos.reason === 'string');
  assert.ok(pos.reason.includes('Conflict'));
});

test('WHAT[planning-018] Stage is determined strictly by projection and never guessed from plan content', () => {
  const view = {
    WorkId: 'work-rec-6',
    ActiveIncumbencyId: 'inc-rec-6',
    ActiveStage: 'S1', // Projection says S1
    ActivePhase: 'WorkOwned',
    RetiredCount: 0,
    LatestRetirementOutcome: null,
    Delivered: false,
    DeliveryDigest: null,
    DeliveryPath: null,
    BoundDevOpsId: null
  };

  // Even if file content misleadingly says "S3" or "Final Delivered", stage MUST remain S1!
  const readPlanFile = (path) => {
    return '# Stage S3 - Final Deliver - Finished Work';
  };

  const pos = PlanningSurface.planRecoveryPosition(view, readPlanFile);
  assert.equal(pos.kind, 'Active');
  assert.equal(pos.stage, 'S1');
  assert.equal(pos.planExists, true);
});
