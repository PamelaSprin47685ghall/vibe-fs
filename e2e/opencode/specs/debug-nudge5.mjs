import { setupScenario, teardownScenario, getSessionId } from '../harness/scenario.js';
import { sleep } from './p0-canary-utils.js';

const t = await setupScenario({
  plugin: true,
  timeoutMs: 60000,
  contextLimit: 100000,
  allowSynthetic: true,
  allowTitleGen: true,
});

try {
  const pad = 'x'.repeat(1024);
  const sess = await t.client.createSession();
  const sid = getSessionId(sess);
  t.provider.expectToolCall({ id: 'nudge-todo', tool: 'todowrite', args: {
    ahaMoments: pad, changesAndReasons: pad, gotchas: pad,
    lessonsAndConventions: pad, plan: pad,
    todos: [{ content: 'pending nudge task', status: 'pending', priority: 'high' }],
    select_methodology: ['first_principles'],
  } });
  t.provider.expectText({ id: 'nudge-todo-text', text: 'continue' });
  t.provider.expectNoMoreRequests();
  const turn = await t.turn.start(sid);
  await t.client.prompt(sid, 'write a todo and say continue');
  await turn.awaitTerminal({ timeoutMs: 30000 });

  const deadline = Date.now() + 10000;
  while (!t.provider.syntheticRequests.some((r) => r.marker === 'todo-nudge')) {
    if (Date.now() > deadline) {
      console.log('timed out waiting for todo-nudge. syntheticRequests:', t.provider.syntheticRequests.map((r) => r.marker));
      break;
    }
    await sleep(200);
  }
  console.log('syntheticRequests:', t.provider.syntheticRequests.map((r) => r.marker));
} finally {
  console.log('\n=== HOST STDERR ===\n');
  console.log(t.host.stderrLog);
  console.log('\n=== HOST STDOUT ===\n');
  console.log(t.host.stdoutLog);
  await teardownScenario(t, { keepOnFailure: true });
}
