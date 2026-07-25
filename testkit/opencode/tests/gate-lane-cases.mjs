import { assertEq, assertTrue, postJson } from './gate-lib.mjs';
import { StrictMockProvider } from '../strict-mock-provider.js';

function lane(role, turn, session = role) {
  return { scenario: 'gate', session, role, turn, requestKind: 'chat' };
}

function chat(content, tools = []) {
  return {
    model: 'test-model',
    messages: [{ role: 'user', content }],
    tools: tools.map((name) => ({ type: 'function', function: { name } })),
  };
}

async function runStrictMockLanes() {
  const provider = new StrictMockProvider();
  provider.strict = true;
  assertEq(typeof provider.allowOutOfOrder, 'undefined', 'global out-of-order bypass must not exist');
  assertEq(typeof provider.allowBloggerRequests, 'undefined', 'Blogger bypass must not exist');
  assertEq(typeof provider.allowTitleGeneration, 'undefined', 'title bypass must not exist');
  assertEq(typeof provider.allowSyntheticContinuations, 'undefined', 'synthetic continuation bypass must not exist');
  const consumed = [];
  provider.onExpectationConsumed = ({ id, lane: consumedLane }) => consumed.push(`${id}:${consumedLane.role}:${consumedLane.turn}`);
  await provider.start();

  try {
    provider.expectToolCall({
      id: 'manager-first',
      lane: lane('manager', 1),
      tool: 'fork',
      args: { agent: 'coder', prompt: 'work' },
      match: { requiredTools: ['fork', 'join', 'list'], containsText: ['manager first'] },
    });
    provider.expectText({
      id: 'manager-second',
      lane: lane('manager', 2),
      text: 'manager done',
      match: { requiredTools: ['fork', 'join', 'list'], containsText: ['manager second'] },
    });
    provider.expectToolCall({
      id: 'coder-write',
      lane: lane('coder', 1),
      tool: 'write',
      args: { filePath: 'x.txt', content: 'ok\n' },
      match: { requiredTools: ['write'] },
    });

    const coderFirst = await postJson(`${provider.url}/v1/chat/completions`, chat('write x.txt', ['write']));
    assertTrue(coderFirst.ok, 'independent coder lane may run before manager lane');
    const managerFirst = await postJson(`${provider.url}/v1/chat/completions`, chat('manager first', ['fork', 'join', 'list']));
    assertTrue(managerFirst.ok, 'manager lane head should match after coder interleaving');
    const managerSecond = await postJson(`${provider.url}/v1/chat/completions`, chat('manager second', ['fork', 'join', 'list']));
    assertTrue(managerSecond.ok, 'manager lane second turn should follow its first turn');
    assertEq(provider.remainingExpectations, 0, 'all lane expectations should be consumed');
    assertEq(consumed.join(','), 'coder-write:coder:1,manager-first:manager:1,manager-second:manager:2', 'consumption records causal lane order');
    provider.expectSatisfied();

    const noExpRes = await postJson(`${provider.url}/v1/chat/completions`, chat('unexpected blogger', []));
    assertEq(noExpRes.status, 500, 'empty queue should return 500');
    assertEq(provider.unexpectedRequests.length, 1, 'empty queue should record unexpected');

    let satisfiedThrew = false;
    try { provider.expectSatisfied(); } catch (err) {
      satisfiedThrew = true;
      assertTrue(err.message.includes('unexpected'), 'expectSatisfied should fail on unexpected request');
    }
    assertTrue(satisfiedThrew, 'expectSatisfied must throw');

    const order = new StrictMockProvider();
    order.strict = true;
    await order.start();
    try {
      order.expectText({
        id: 'order-first',
        lane: lane('manager', 1, 'order'),
        text: 'first',
        match: { requiredTools: ['fork', 'join', 'list'], containsText: ['manager first'] },
      });
      order.expectText({
        id: 'order-second',
        lane: lane('manager', 2, 'order'),
        text: 'second',
        match: { requiredTools: ['fork', 'join', 'list'], containsText: ['manager second'] },
      });
      const mismatchRes = await postJson(`${order.url}/v1/chat/completions`, chat('manager second', ['fork', 'join', 'list']));
      assertEq(mismatchRes.status, 500, 'later turn must not bypass its lane head');
      assertEq(order.remainingExpectations, 2, 'lane-order mismatch must not consume expectation');
    } finally {
      await order.stop();
    }

    const missing = new StrictMockProvider();
    missing.strict = true;
    await missing.start();
    try {
      missing.expectText({
        id: 'missing-lane',
        lane: lane('manager', 1, 'missing'),
        text: 'missing',
        match: { requiredTools: ['fork', 'join', 'list'] },
      });
      let missingThrew = false;
      try { missing.expectSatisfied(); } catch (err) {
        missingThrew = true;
        assertTrue(err.message.includes('remaining expectations'), 'missing expectation should report remaining lane head');
      }
      assertTrue(missingThrew, 'missing expectation must fail expectSatisfied');
    } finally {
      await missing.stop();
    }
  } finally {
    await provider.stop();
  }
}

async function runSessionBoundLanes() {
  const provider = new StrictMockProvider();
  await provider.start();
  try {
    provider.bindSession('parent', 'parent-1');
    provider.expectText({
      id: 'child-first',
      lane: { ...lane('coder', 1, 'child'), parentSession: 'parent' },
      text: 'first',
      match: { requiredTools: ['write'], containsText: ['first child turn'] },
    });
    provider.afterExpectation('child-first', () => {
      provider.expectText({
        id: 'child-second',
        lane: { ...lane('coder', 2, 'child'), parentSession: 'parent' },
        text: 'second',
        match: { requiredTools: ['write'], containsText: ['second child turn'] },
      });
    });

    const headers = {
      'x-session-affinity': 'child-1',
      'x-parent-session-id': 'parent-1',
    };
    const first = await postJson(`${provider.url}/v1/chat/completions`, chat('first child turn', ['write']), headers);
    assertTrue(first.ok, 'parent-bound child lane must accept its first request');
    const second = await postJson(`${provider.url}/v1/chat/completions`, chat('second child turn', ['write']), headers);
    assertTrue(second.ok, 'afterExpectation must register the causal successor atomically');
    provider.expectSatisfied();
  } finally {
    await provider.stop();
  }

  const wrongParent = new StrictMockProvider();
  await wrongParent.start();
  try {
    wrongParent.bindSession('parent', 'parent-1');
    wrongParent.expectText({
      id: 'child',
      lane: { ...lane('coder', 1, 'child'), parentSession: 'parent' },
      text: 'never',
      match: { requiredTools: ['write'] },
    });
    const rejected = await postJson(
      `${wrongParent.url}/v1/chat/completions`,
      chat('wrong parent', ['write']),
      { 'x-session-affinity': 'child-1', 'x-parent-session-id': 'other-parent' },
    );
    assertEq(rejected.status, 500, 'lane must reject a child with another parent session');
  } finally {
    await wrongParent.stop();
  }
}

export const laneCases = [
  { name: 'strict mock lanes and unexpected requests', fn: runStrictMockLanes },
  { name: 'session-bound lanes and causal successors', fn: runSessionBoundLanes },
];
