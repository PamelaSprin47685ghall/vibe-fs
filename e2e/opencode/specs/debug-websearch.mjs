import { runScenario } from '../harness/scenario.js';
import { findToolPart } from './p0-canary-utils.js';

const exitCode = await runScenario({
  plugin: true,
  timeoutMs: 45000,
  contextLimit: 20000,
  allowSynthetic: true,
  allowTitleGen: true,
}, [
  {
    name: 'debug-websearch',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = sess.data?.data?.data?.id || sess.data?.data?.id;
      t.provider.expectToolCall({ id: 'web-search', tool: 'websearch', args: { query: 'E2E websearch test', what_to_summarize: 'E2E websearch result', numResults: 1 } });
      t.provider.expectText({ id: 'web-summary', text: 'Test search content for E2E.' });
      t.provider.expectText({ id: 'web-done', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'search the web for E2E websearch test');
      await turn.awaitTerminal({ timeoutMs: 30000 });
      const res = await t.client.messages(sid);
      const messages = res.data || [];
      const part = findToolPart(messages, 'websearch');
      console.log('\n=== websearch tool part ===');
      console.log(JSON.stringify(part, null, 2));
      console.log('\n=== requests ===');
      t.provider.requests.forEach((r, i) => {
        console.log(`\n--- req ${i}`);
        console.log(JSON.stringify({ tools: r.tools?.map((t) => t.function?.name || t.name), last: r.messages?.slice(-1)[0] }, null, 2));
      });
    },
  },
]);
process.exit(exitCode);
