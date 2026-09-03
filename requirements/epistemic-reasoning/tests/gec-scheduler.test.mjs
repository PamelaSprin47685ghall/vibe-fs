import assert from 'node:assert/strict';
import test from 'node:test';
import { gecSurface } from '../../../dist/Sphinx/GecSurface.js';

function target(overrides = {}) {
  return {
    id: 't-base',
    dependencies: [],
    conflictKeys: [],
    cost: { compute: 1, budget: 1 },
    ...overrides,
  };
}

function shuffledCopies(list) {
  return [list.slice(), list.slice().reverse(), [list[1], list[0], list[2], list[3]].filter(Boolean)];
}

test('WHAT[EPI-022] batch_respects_dependencies_conflicts_and_budget_or_naive_scheduler_overcommits', async () => {
  const surface = gecSurface;
  const targets = [
    target({ id: 't-root', cost: { compute: 1, budget: 1 } }),
    target({ id: 't-child', dependencies: ['t-root'], cost: { compute: 1, budget: 1 } }),
    target({ id: 't-clash-a', conflictKeys: ['leaf-x'], cost: { compute: 1, budget: 1 } }),
    target({ id: 't-clash-b', conflictKeys: ['leaf-x'], cost: { compute: 1, budget: 1 } }),
  ];
  const budgeted = await surface.schedule({ targets, budget: { compute: 10, budget: 10 }, completed: [] });
  assert.equal(budgeted.ok, true);
  assert.ok(!budgeted.batch.includes('t-child') || budgeted.batch.includes('t-root'), 'a dependent target needs its dependency in the same batch or already completed');
  assert.ok(
    !(budgeted.batch.includes('t-clash-a') && budgeted.batch.includes('t-clash-b')),
    'conflicting keys must never co-occur in one batch',
  );

  const withDone = await surface.schedule({ targets, budget: { compute: 10, budget: 10 }, completed: ['t-root'] });
  assert.equal(withDone.ok, true);
  assert.ok(withDone.batch.includes('t-child'), 'a satisfied dependency must unblock the child');

  const expensive = [
    target({ id: 't-cheap', cost: { compute: 1, budget: 1 } }),
    target({ id: 't-pricey', cost: { compute: 9, budget: 9 } }),
  ];
  const tight = await surface.schedule({ targets: expensive, budget: { compute: 5, budget: 5 }, completed: [] });
  assert.equal(tight.ok, true);
  assert.ok(!tight.batch.includes('t-pricey'), 'an over-budget target must be deferred');
  assert.ok(tight.batch.includes('t-cheap'), 'the affordable target must still be scheduled');
  let total = 0;
  for (const id of tight.batch) total += expensive.find((entry) => entry.id === id).cost.budget;
  assert.ok(total <= 5, 'batched cost must stay within budget');
});

test('WHAT[EPI-022] incomparable_losses_keep_pareto_frontier_or_scalar_sum_hides_tradeoff', async () => {
  const surface = gecSurface;
  const targets = [
    target({ id: 't-alpha', loss: { currency: 'alpha-loss', value: 0.1 } }),
    target({ id: 't-beta', loss: { currency: 'beta-loss', value: 0.1 } }),
    target({ id: 't-dominated', loss: { currency: 'alpha-loss', value: 0.9 } }),
  ];
  const result = await surface.schedule({ targets, budget: { compute: 10, budget: 10 }, completed: [] });
  assert.equal(result.ok, true);
  assert.ok(result.pareto.includes('t-alpha'), 'non-dominated alpha must remain on the frontier');
  assert.ok(result.pareto.includes('t-beta'), 'incomparable beta must remain instead of collapsing to one scalar winner');
  assert.ok(!result.pareto.includes('t-dominated'), 'a strictly dominated target must leave the frontier');

  const shared = [
    target({ id: 't-one', loss: { currency: 'shared', value: 0.2 }, commonCurrency: 'shared' }),
    target({ id: 't-two', loss: { currency: 'shared', value: 0.5 }, commonCurrency: 'shared' }),
  ];
  const summed = await surface.schedule({ targets: shared, budget: { compute: 10, budget: 10 }, completed: [] });
  assert.equal(summed.ok, true);
  assert.ok(summed.batch.includes('t-one'), 'with a declared common currency the better loss must be preferred');
});

test('WHAT[EPI-022] batch_composes_by_canonical_order_not_input_sum_or_delta_addition_reorders_semantics', async () => {
  const surface = gecSurface;
  const targets = [
    target({ id: 't-zeta', loss: { currency: 'shared', value: 0.3 }, commonCurrency: 'shared' }),
    target({ id: 't-mid', loss: { currency: 'shared', value: 0.2 }, commonCurrency: 'shared' }),
    target({ id: 't-alpha', loss: { currency: 'shared', value: 0.1 }, commonCurrency: 'shared' }),
  ];
  const orders = shuffledCopies(targets);
  const seen = [];
  for (const input of orders) {
    const result = await surface.schedule({ targets: input, budget: { compute: 10, budget: 10 }, completed: [] });
    assert.equal(result.ok, true);
    assert.deepEqual(result.batch.slice().sort(), ['t-alpha', 't-mid', 't-zeta'].slice(0, result.batch.length).sort(), 'the same candidate set must be chosen regardless of input order');
    seen.push(result.order.join(','));
  }
  assert.equal(new Set(seen).size, 1, 'canonical composition order must not follow input permutation');
  assert.deepEqual(seen[0].split(','), seen[0].split(',').slice().sort(), 'canonical order must be sorted rather than summed');
  const first = await surface.schedule({ targets, budget: { compute: 10, budget: 10 }, completed: [] });
  assert.ok(!('summedDelta' in first), 'the batch must expose an order, never an additive delta sum');
});
