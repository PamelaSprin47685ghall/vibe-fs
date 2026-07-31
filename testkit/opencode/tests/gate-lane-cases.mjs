/**
 * Script forest contract tests (AGENTS.md KISS-N11).
 * File name kept for gate-testkit discovery; semantics are prefix/content forest.
 */
import { assertEq, assertTrue, postJson } from './gate-lib.mjs';
import { StrictMockProvider } from '../strict-mock-provider.js';
import { compileScenario } from '../scenario-schema.js';
import { ScenarioRuntime } from '../scenario-runtime.js';

// neverEnd SSE keeps stream open; read status without waiting for body.
async function postJsonNoBody(url, body, headers = {}) {
  const res = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...headers },
    body: JSON.stringify(body),
  });
  const status = res.status;
  res.body?.cancel();
  return { status, ok: status >= 200 && status < 300 };
}


function lane(role, turn = 1, session = role) {
  return { scenario: 'gate', session, role, turn, requestKind: 'chat' };
}

function chat(content, tools = [], extraMessages = []) {
  return {
    model: 'test-model',
    messages: [...extraMessages, { role: 'user', content }],
    tools: tools.map((name) => ({ type: 'function', function: { name } })),
  };
}

function bloggerChat(content) {
  return {
    model: 'test-model',
    messages: [{ role: 'user', content: 'You are the blogger of a coding agent session.\n' + content }],
    tools: [],
  };
}

function sessionHeaders(sessionId) {
  return { 'x-session-affinity': sessionId };
}

async function scenarioProvider(source, bindings) {
  const result = compileScenario(source, { name: 'gate-inline.toml' });
  assertTrue(result.ok, `fixture must compile: ${result.ok ? '' : result.problems.join(' | ')}`);

  const provider = new StrictMockProvider();
  await provider.start();
  provider.attachScenario(new ScenarioRuntime(result.scenario));
  for (const [alias, sessionIds] of Object.entries(bindings)) {
    for (const sessionId of (Array.isArray(sessionIds) ? sessionIds : [sessionIds])) {
      provider.bindSession(alias, sessionId);
    }
  }
  return provider;
}

function assistant(text) {
  return { role: 'assistant', content: text };
}

async function runForestIndependentPaths() {
  const managerTools = ['fork', 'join', 'list'];
  const provider = await scenarioProvider(`scenario = "forest-independent"
flow = [
  { prompt = { text = "write x.txt" } },
  { prompt = { text = "manager first" } },
  { prompt = { text = "manager second" } },
]

[[turn]]
id = "manager-first"
lane = "manager"
user = "manager first"
tools = ["fork", "join", "list"]

  [[turn.step]]
  respond = { type = "tool-call", tool = "fork", args = { agent = "coder", prompt = "work" } }

[[turn]]
id = "manager-second"
lane = "manager"
user = "manager second"
tools = ["fork", "join", "list"]

  [[turn.step]]
  respond = { type = "text", text = "manager done" }

[[turn]]
id = "coder-write"
lane = "coder"
user = "write x.txt"
tools = ["write"]

  [[turn.step]]
  respond = { type = "tool-call", tool = "write", args = { filePath = "x.txt", content = "ok\\n" } }
`, {
    manager: 'forest-manager',
    coder: 'forest-coder',
  });
  try {
    const coderFirst = await postJson(
      `${provider.url}/v1/chat/completions`,
      chat('write x.txt', ['write']),
      sessionHeaders('forest-coder'),
    );
    assertTrue(coderFirst.ok, 'independent coder path may run before manager');
    const managerFirst = await postJson(
      `${provider.url}/v1/chat/completions`,
      chat('manager first', managerTools),
      sessionHeaders('forest-manager'),
    );
    assertTrue(managerFirst.ok, 'manager first user matches');
    const again = await postJson(
      `${provider.url}/v1/chat/completions`,
      chat('manager first', managerTools),
      sessionHeaders('forest-manager'),
    );
    assertTrue(again.ok, 'same prefix is idempotent (no mute)');
    const managerSecond = await postJson(
      `${provider.url}/v1/chat/completions`,
      chat('manager second', managerTools, [
        { role: 'user', content: 'manager first' },
        assistant('manager first response'),
      ]),
      sessionHeaders('forest-manager'),
    );
    assertTrue(managerSecond.ok, 'manager second user matches without turn queue');
    provider.expectSatisfied();
  } finally {
    await provider.stop();
  }
}

async function runIdempotentSamePrefix() {
  const provider = await scenarioProvider(`scenario = "idempotent"
prompt = { text = "idempotent ping" }

[[turn]]
id = "once"
lane = "manager"
user = "idempotent ping"
tools = ["fork", "join", "list"]

  [[turn.step]]
  respond = { type = "text", text = "hello" }
`, { manager: 'idempotent-session' });
  try {
    const body = chat('idempotent ping', ['fork', 'join', 'list']);
    const headers = sessionHeaders('idempotent-session');
    const a = await postJson(`${provider.url}/v1/chat/completions`, body, headers);
    const b = await postJson(`${provider.url}/v1/chat/completions`, body, headers);
    assertTrue(a.ok && b.ok, 'identical requests both succeed');
    // blocking edge observed → satisfied even if "pending" uses observed set
    provider.expectSatisfied();
  } finally {
    await provider.stop();
  }
}

async function runAmbiguousPrefixRejected() {
  const provider = await scenarioProvider(`scenario = "ambiguous"
flow = [
  { prompt = { text = "A" } },
  { prompt = { text = "B" } },
]

[[turn]]
id = "a"
lane = "manager"
user = ["shared marker", "A"]
tools = ["fork", "join", "list"]

  [[turn.step]]
  respond = { type = "text", text = "A" }

[[turn]]
id = "b"
lane = "manager"
user = ["shared marker", "B"]
tools = ["fork", "join", "list"]

  [[turn.step]]
  respond = { type = "text", text = "B" }
`, { manager: 'ambiguity-session' });
  try {
    const res = await postJson(
      `${provider.url}/v1/chat/completions`,
      chat('shared marker A B', ['fork', 'join', 'list']),
      sessionHeaders('ambiguity-session'),
    );
    assertEq(res.status, 500, 'ambiguous same-length prefixes must 500');
    assertTrue(
      provider.unexpectedRequests.some((u) => u.reason === 'ambiguous-turn'),
      'reason must be ambiguous-turn',
    );
  } finally {
    await provider.stop();
  }
}

async function runUserForkDisambiguates() {
  const provider = await scenarioProvider(`scenario = "user-fork"
flow = [
  { prompt = { text = "Ship job-A please" } },
  { prompt = { text = "Ship job-B please" } },
]

[[turn]]
id = "job-a"
lane = "job-a"
user = "Ship job-A"
tools = ["fork", "join", "list"]

  [[turn.step]]
  respond = { type = "text", text = "A" }

[[turn]]
id = "job-b"
lane = "job-b"
user = "Ship job-B"
tools = ["fork", "join", "list"]

  [[turn.step]]
  respond = { type = "text", text = "B" }
`, {
    'job-a': 'job-a-session',
    'job-b': 'job-b-session',
  });
  try {
    const tools = ['fork', 'join', 'list'];
    const a = await postJson(
      `${provider.url}/v1/chat/completions`,
      chat('Ship job-A please', tools),
      sessionHeaders('job-a-session'),
    );
    const b = await postJson(
      `${provider.url}/v1/chat/completions`,
      chat('Ship job-B please', tools),
      sessionHeaders('job-b-session'),
    );
    assertTrue(a.ok && b.ok, 'user-text fork disambiguates parallel paths');
    provider.expectSatisfied();
  } finally {
    await provider.stop();
  }
}

// K12.2 blocker: the schema retires `neverEnd`, and delivery-plan has no valid never-end transport.
async function runNeverEndStillIdempotent() {
  const provider = new StrictMockProvider();
  await provider.start();
  try {
    provider.expectText({
      id: 'blog',
      lane: { ...lane('blogger', 1), requestKind: 'chat' },
      neverEnd: true,
      blocking: false,
      text: 'bg',
      match: { user: 'You are the blogger' },
    });
    const r1 = await postJsonNoBody(`${provider.url}/v1/chat/completions`, bloggerChat('delta one'));
    const r2 = await postJsonNoBody(`${provider.url}/v1/chat/completions`, bloggerChat('delta two'));
    assertTrue(r1.ok && r2.ok, 'blogger template rematches without mute');
    provider.expectSatisfied();
  } finally {
    await provider.stop();
  }
}

async function runPrefixCacheInvalidation() {
  const provider = await scenarioProvider(`scenario = "prefix-invalidation"
flow = [
  { prompt = { text = "stable system prefix first user" } },
  { prompt = { text = "appended user turn" } },
]

[[turn]]
id = "turn-1"
lane = "manager"
user = "stable system prefix"

  [[turn.step]]
  respond = { type = "text", text = "ok1" }

[[turn]]
id = "turn-2"
lane = "manager"
user = "appended user turn"

  [[turn.step]]
  respond = { type = "text", text = "ok2" }
`, { manager: 'prefix-session-1' });
  try {
    const tools = [
      { type: 'function', function: { name: 'fork' } },
      { type: 'function', function: { name: 'join' } },
      { type: 'function', function: { name: 'list' } },
    ];
    const body1 = {
      model: 'test-model',
      tools,
      messages: [
        { role: 'system', content: 'stable system prefix' },
        { role: 'user', content: 'stable system prefix first user' },
      ],
    };
    const first = await postJson(`${provider.url}/v1/chat/completions`, body1, {
      'x-session-affinity': 'prefix-session-1',
    });
    assertTrue(first.ok, 'prefix: first request matches');

    const body2 = {
      model: 'test-model',
      tools,
      messages: [
        { role: 'system', content: 'MUTATED system prefix' },
        { role: 'user', content: 'stable system prefix first user' },
        { role: 'user', content: 'appended user turn' },
      ],
    };
    const second = await postJson(`${provider.url}/v1/chat/completions`, body2, {
      'x-session-affinity': 'prefix-session-1',
    });
    assertEq(second.status, 500, 'prefix: mutated sealed prefix must 500');
    assertTrue(provider.unexpectedRequests.some((u) => u.reason === 'seal-undeclared'), 'prefix: reason must be seal-undeclared');
  } finally {
    await provider.stop();
  }
}

async function runPrefixCacheAppendOk() {
  const provider = await scenarioProvider(`scenario = "prefix-append"
flow = [
  { prompt = { text = "append-ok first" } },
  { prompt = { text = "append-ok second" } },
]

[[turn]]
id = "turn-1"
lane = "manager"
user = "append-ok first"

  [[turn.step]]
  respond = { type = "text", text = "ok1" }

[[turn]]
id = "turn-2"
lane = "manager"
user = "append-ok second"

  [[turn.step]]
  respond = { type = "text", text = "ok2" }
`, { manager: 'prefix-session-2' });
  try {
    const tools = [
      { type: 'function', function: { name: 'fork' } },
      { type: 'function', function: { name: 'join' } },
      { type: 'function', function: { name: 'list' } },
    ];
    const body1 = {
      model: 'test-model',
      tools,
      messages: [
        { role: 'system', content: 'append-ok system' },
        { role: 'user', content: 'append-ok first' },
      ],
    };
    const first = await postJson(`${provider.url}/v1/chat/completions`, body1, {
      'x-session-affinity': 'prefix-session-2',
    });
    assertTrue(first.ok, 'prefix append: first ok');
    const body2 = {
      model: 'test-model',
      tools,
      messages: [
        { role: 'system', content: 'append-ok system' },
        { role: 'user', content: 'append-ok first' },
        { role: 'assistant', content: 'ok1' },
        { role: 'user', content: 'append-ok second' },
      ],
    };
    const second = await postJson(`${provider.url}/v1/chat/completions`, body2, {
      'x-session-affinity': 'prefix-session-2',
    });
    assertTrue(second.ok, 'prefix append: second ok when sealed prefix preserved');
    provider.expectSatisfied();
  } finally {
    await provider.stop();
  }
}

async function runMissingEdgeFailsSatisfied() {
  const provider = await scenarioProvider(`scenario = "missing-edge"
prompt = { text = "never sent" }

[[turn]]
id = "never-hit"
lane = "manager"
user = "never sent"
tools = ["fork", "join", "list"]

  [[turn.step]]
  respond = { type = "text", text = "x" }
`, { manager: 'missing-edge-session' });
  try {
    let threw = false;
    try {
      provider.expectSatisfied();
    } catch (err) {
      threw = true;
      assertTrue(err.message.includes('declared but never reached'), 'must report unanswered scenario turns');
      assertTrue(err.message.includes('[never-hit]'), 'must report the unanswered turn id');
    }
    assertTrue(threw, 'unobserved blocking edge fails satisfied');
  } finally {
    await provider.stop();
  }
}

async function runHistorySplitsSameLastUser() {
  // Same lastUser phrase, different full history → different seals; both use same template edge (idempotent template).
  const provider = await scenarioProvider(`scenario = "history-splits"
flow = [
  { prompt = { text = "Review the current worktree for correctness." } },
  { prompt = { text = "Nope, let's re-evaluate: does it really fully satisfy the original task without cutting corners?" } },
]

[[turn]]
id = "review-first"
lane = "reviewer"
user = "Review the current worktree"
tools = ["verdict"]

  [[turn.step]]
  respond = { type = "tool-call", tool = "verdict", args = { verdict = "PERFECT" } }

[[turn]]
id = "review-confirm"
lane = "reviewer"
user = "Nope, let's re-evaluate: does it really fully satisfy the original task without cutting corners?"
tools = ["verdict"]

  [[turn.step]]
  respond = { type = "tool-call", tool = "verdict", args = { verdict = "PERFECT" } }
`, {
    reviewer: ['rev-1', 'rev-2'],
  });
  try {
    const tools = [{ type: 'function', function: { name: 'verdict' } }];
    const first = await postJson(`${provider.url}/v1/chat/completions`, {
      model: 'test-model',
      tools,
      messages: [{ role: 'user', content: 'Review the current worktree for correctness.' }],
    }, sessionHeaders('rev-1'));
    assertTrue(first.ok, 'first review matches');
    const confirm = await postJson(`${provider.url}/v1/chat/completions`, {
      model: 'test-model',
      tools,
      messages: [
        { role: 'user', content: 'Review the current worktree for correctness.' },
        { role: 'assistant', content: null, tool_calls: [{ id: 'c1', type: 'function', function: { name: 'verdict', arguments: '{}' } }] },
        { role: 'tool', tool_call_id: 'c1', content: "Nope, let's re-evaluate: does it really fully satisfy the original task without cutting corners?" },
        { role: 'user', content: "Nope, let's re-evaluate: does it really fully satisfy the original task without cutting corners?" },
      ],
    }, sessionHeaders('rev-1'));
    assertTrue(confirm.ok, 'confirm user matches different edge');
    // Same first-user request shape on another session (post-rebase style) — same template, ok
    const again = await postJson(`${provider.url}/v1/chat/completions`, {
      model: 'test-model',
      tools,
      messages: [{ role: 'user', content: 'Review the current worktree for correctness.' }],
    }, sessionHeaders('rev-2'));
    assertTrue(again.ok, 'identical first-user template is reusable across sessions');
    provider.expectSatisfied();
  } finally {
    await provider.stop();
  }
}

export const laneCases = [
  { name: 'forest independent content paths', fn: runForestIndependentPaths },
  { name: 'identical prefix is idempotent', fn: runIdempotentSamePrefix },
  { name: 'ambiguous equal templates rejected', fn: runAmbiguousPrefixRejected },
  { name: 'user-text fork disambiguates parallel jobs', fn: runUserForkDisambiguates },
  { name: 'blogger template rematches', fn: runNeverEndStillIdempotent },
  { name: 'prefix cache invalidation fails closed', fn: runPrefixCacheInvalidation },
  { name: 'prefix cache append-only succeeds', fn: runPrefixCacheAppendOk },
  { name: 'unobserved blocking edge fails satisfied', fn: runMissingEdgeFailsSatisfied },
  { name: 'history splits first vs confirm review', fn: runHistorySplitsSameLastUser },
];
