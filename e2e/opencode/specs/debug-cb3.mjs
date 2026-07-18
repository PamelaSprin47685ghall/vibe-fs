import { runScenario } from '../harness/scenario.js';

const exitCode = await runScenario({
  plugin: true,
  timeoutMs: 60000,
  contextLimit: 20000,
  allowSynthetic: true,
  allowTitleGen: true,
}, [
  {
    name: 'debug-cb3',
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
      const n = t.provider.syntheticRequests[0];
      console.log('\n=== synthetic request body keys ===');
      console.log(Object.keys(n.body || {}).join(', '));
      console.log('sessionId=', n.body?.sessionId);
      console.log('session=', n.body?.session);
      console.log('id=', n.body?.id);
      console.log(JSON.stringify(n.body, null, 2).slice(0, 1000));
    },
  },
]);
process.exit(exitCode);
