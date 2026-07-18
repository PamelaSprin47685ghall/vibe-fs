import { runScenario } from '../harness/scenario.js';
import { findToolPart } from './p0-canary-utils.js';

const exitCode = await runScenario({
  plugin: true,
  timeoutMs: 60000,
  contextLimit: 20000,
  allowSynthetic: true,
  allowTitleGen: true,
}, [
  {
    name: 'debug-nudge5',
    fn: async (t) => {
      const sess = await t.client.createSession({
        id: 'test-model', providerID: 'test', limit: { input: 100000, context: 100000 },
      });
      const sid = sess.data?.data?.data?.id || sess.data?.data?.id;
      t.provider.expectToolCall({ id: 'nudge-todo', tool: 'todowrite', args: {
        todos: [{ content: 'pending nudge task', status: 'pending', priority: 'high' }],
        select_methodology: ['first_principles'],
      } });
      t.provider.expectNoMoreRequests();
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'write a todo and say continue');
      await turn.awaitTerminal({ timeoutMs: 30000 });
      const messages = (await t.client.messages(sid)).data || [];
      const part = findToolPart(messages, 'todowrite');
      console.log('todowrite output length:', (part?.state?.output || '').length);
      console.log(part?.state?.output?.slice(0, 500));
      console.log('synthetic markers:', t.provider.syntheticRequests.map((r) => r.marker));
    },
  },
]);
process.exit(exitCode);
