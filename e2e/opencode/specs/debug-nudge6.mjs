import { runScenario } from '../harness/scenario.js';

const exitCode = await runScenario({
  plugin: true,
  timeoutMs: 60000,
  contextLimit: 1000000,
  allowSynthetic: true,
  allowTitleGen: true,
}, [
  {
    name: 'debug-nudge6',
    fn: async (t) => {
      const pad = 'x'.repeat(1024);
      const sess = await t.client.createSession({
        id: 'test-model', providerID: 'test', limit: { input: 100000, context: 100000 },
      });
      const sid = sess.data?.data?.data?.id || sess.data?.data?.id;
      t.provider.expectToolCall({ id: 'nudge-todo', tool: 'todowrite', args: {
        ahaMoments: pad, changesAndReasons: pad, gotchas: pad,
        lessonsAndConventions: pad, plan: pad,
        todos: [{ content: 'pending nudge task', status: 'pending', priority: 'high' }],
        select_methodology: ['first_principles'],
      } });
      t.provider.expectNoMoreRequests();
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'write a todo and say continue');
      await turn.awaitTerminal({ timeoutMs: 30000 });
      const deadline = Date.now() + 30000;
      while (!t.provider.syntheticRequests.some((r) => r.marker === 'todo-nudge')) {
        if (Date.now() > deadline) {
          console.log('no todo-nudge. markers:', t.provider.syntheticRequests.map((r) => r.marker));
          break;
        }
        await new Promise((r) => setTimeout(r, 200));
      }
      console.log('markers:', t.provider.syntheticRequests.map((r) => r.marker));
    },
  },
]);
process.exit(exitCode);
