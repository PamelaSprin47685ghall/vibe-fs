import { runScenario } from '../harness/scenario.js';

const exitCode = await runScenario({
  plugin: true,
  timeoutMs: 60000,
  contextLimit: 20000,
  allowSynthetic: true,
  allowTitleGen: true,
}, [
  {
    name: 'debug-session-model',
    fn: async (t) => {
      const sess = await t.client.createSession({
        id: 'test-model', providerID: 'test', limit: { input: 100000, context: 100000 },
      });
      const sid = sess.data?.data?.data?.id || sess.data?.data?.id;
      console.log('createSession response model:', JSON.stringify(sess.data?.data?.data?.model || sess.data?.data?.model));
      const getRes = await t.client.request('GET', `/api/session/${sid}`);
      console.log('GET session status:', getRes.status);
      console.log('GET session model:', JSON.stringify(getRes.data?.data?.model || getRes.data?.model));
    },
  },
]);
process.exit(exitCode);
