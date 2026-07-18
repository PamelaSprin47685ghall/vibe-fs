import { runScenario } from '../harness/scenario.js';

const exitCode = await runScenario({
  plugin: true,
  timeoutMs: 60000,
  contextLimit: 20000,
  allowSynthetic: true,
  allowTitleGen: true,
}, [
  {
    name: 'debug-cb2',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = sess.data?.data?.data?.id || sess.data?.data?.id;
      const pad = 'x'.repeat(1200);
      t.provider.expectToolCall({ id: 'cb-todo', tool: 'todowrite', args: {
        ahaMoments: pad, changesAndReasons: pad, gotchas: pad,
        lessonsAndConventions: pad, plan: pad,
        todos: [{ content: 'budget test', status: 'completed', priority: 'high' }],
        select_methodology: ['first_principles'],
      } });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'commit a detailed report via todowrite and say continue');
      await turn.awaitTerminal({ timeoutMs: 30000 });
      console.log('\n=== events with nudge text ===');
      for (const e of t.events.allEvents) {
        if (JSON.stringify(e).includes('system context is about to be suspended')) {
          console.log(JSON.stringify(e, null, 2).slice(0, 500));
        }
      }
    },
  },
]);
process.exit(exitCode);
