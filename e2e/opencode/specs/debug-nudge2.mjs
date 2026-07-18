import { runScenario } from '../harness/scenario.js';

const exitCode = await runScenario({
  plugin: true,
  timeoutMs: 60000,
  contextLimit: 20000,
  allowSynthetic: true,
  allowTitleGen: true,
}, [
  {
    name: 'debug-nudge2',
    fn: async (t) => {
      const pad = 'x'.repeat(100);
      const sess = await t.client.createSession();
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
      for (const r of t.provider.syntheticRequests) {
        console.log('synthetic marker=', r.marker);
        console.log('text=', r.body?.messages?.[r.body.messages.length - 1]?.content?.slice(0, 200));
      }
    },
  },
]);
process.exit(exitCode);
