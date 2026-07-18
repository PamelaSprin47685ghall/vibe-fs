import { runScenario } from '../harness/scenario.js';

const exitCode = await runScenario({
  plugin: true,
  timeoutMs: 60000,
  contextLimit: 20000,
  allowSynthetic: true,
  allowTitleGen: true,
}, [
  {
    name: 'debug-cb',
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
      t.provider.expectNoMoreRequests();
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'commit a detailed report via todowrite and say continue');
      await turn.awaitTerminal({ timeoutMs: 30000 });
      const s = await t.client.sessionStatus(sid);
      const tokens = s.data?.data?.tokens || s.data?.tokens || {};
      const nudges = t.provider.syntheticRequests.filter((r) => r.marker === 'budget-nudge');
      console.log('\n=== nudges', nudges.length);
      const nudge = nudges[0];
      const nudgeMsg = nudge?.body?.messages?.[nudge?.body?.messages.length - 1];
      console.log('nudge text:', nudgeMsg?.content);
      console.log('tokens input:', tokens.input);
      console.log('remaining expectations:', t.provider.remainingExpectations);
    },
  },
]);
process.exit(exitCode);
