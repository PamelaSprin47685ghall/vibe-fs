import { runScenario } from '../harness/scenario.js';

const exitCode = await runScenario({
  plugin: true,
  timeoutMs: 60000,
  contextLimit: 20000,
  allowSynthetic: true,
  allowTitleGen: true,
}, [
  {
    name: 'debug-rawevent68',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = sess.data?.data?.data?.id || sess.data?.data?.id;
      t.provider.expectToolCall({ id: 'write-file', tool: 'write', args: { filePath: 'hello.txt', content: 'Hello World\n' } });
      t.provider.expectText({ id: 'write-done', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'Write hello.txt with content "Hello World\\n"');
      await turn.awaitTerminal({ timeoutMs: 60000, requireAssistantTerminal: false });
      const events = t.events.allEvents;
      const e68 = events.find((e) => e.seq === 68);
      console.log('\n=== event 68 ===');
      console.log(JSON.stringify(JSON.parse(e68.raw), null, 2));
      console.log('================');
    },
  },
]);
process.exit(exitCode);
