import { setupScenario, teardownScenario } from '../harness/scenario.js';

const scenario = await setupScenario({
  plugin: true,
  timeoutMs: 60000,
  contextLimit: 20000,
  allowSynthetic: true,
  allowTitleGen: true,
});
try {
  const sess = await scenario.client.createSession({
    id: 'test-model', providerID: 'test', limit: { input: 100000, context: 100000 },
  });
  const sid = sess.data?.data?.data?.id || sess.data?.data?.id;
  scenario.provider.expectToolCall({ id: 'nudge-todo', tool: 'todowrite', args: {
    ahaMoments: 'x'.repeat(1024), changesAndReasons: 'x'.repeat(1024), gotchas: 'x'.repeat(1024),
    lessonsAndConventions: 'x'.repeat(1024), plan: 'x'.repeat(1024),
    todos: [{ content: 'pending nudge task', status: 'pending', priority: 'high' }],
    select_methodology: ['first_principles'],
  } });
  const turn = await scenario.turn.start(sid);
  await scenario.client.prompt(sid, 'write a todo and say continue');
  await turn.awaitTerminal({ timeoutMs: 30000 });
} finally {
  console.log('--- STDOUT ---');
  console.log(scenario.host.stdoutLog);
  console.log('--- STDERR ---');
  console.log(scenario.host.stderrLog);
  try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
}
