import { runScenario } from '../harness/scenario.js';

const exitCode = await runScenario({
  plugin: true,
  timeoutMs: 45000,
  contextLimit: 20000,
  allowSynthetic: true,
  allowTitleGen: true,
}, [
  {
    name: 'debug-websearch2',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = sess.data?.data?.data?.id || sess.data?.data?.id;
      t.provider.expectToolCall({ id: 'web-search', tool: 'websearch', args: { query: 'E2E websearch test', what_to_summarize: 'E2E websearch result', numResults: 1 } });
      t.provider.expectText({ id: 'web-summary', text: 'Test search content for E2E.' });
      t.provider.expectText({ id: 'web-done', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'search the web for E2E websearch test');
      await turn.awaitTerminal({ timeoutMs: 30000 });
      const req2 = t.provider.requests[2];
      console.log('\n=== req2 messages ===');
      for (const m of req2.messages || []) {
        const text = typeof m.content === 'string' ? m.content : JSON.stringify(m.content);
        console.log(`\nrole=${m.role} tool=${m.tool || '-'}\ntext=${text.slice(0, 500)}`);
      }
    },
  },
]);
process.exit(exitCode);
