import { runScenario } from '../harness/scenario.js';

const exitCode = await runScenario({
  plugin: true,
  timeoutMs: 30000,
  contextLimit: 20000,
  allowSynthetic: true,
  allowTitleGen: true,
}, [
  {
    name: 'debug-tools',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = sess.data?.data?.data?.id || sess.data?.data?.id;
      t.provider.expectText({ id: 'schema-warm', text: 'ok' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'list your tools');
      await turn.awaitTerminal({ timeoutMs: 30000 });
      const tools = t.provider.requests[0]?.tools || [];
      console.log('\n=== tools ===');
      console.log(JSON.stringify(tools, null, 2));
      console.log('=============');
    },
  },
]);
process.exit(exitCode);
