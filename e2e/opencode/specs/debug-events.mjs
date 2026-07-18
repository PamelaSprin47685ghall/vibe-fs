import { runScenario } from '../harness/scenario.js';

const exitCode = await runScenario({
  plugin: true,
  timeoutMs: 60000,
  contextLimit: 20000,
  allowSynthetic: true,
  allowTitleGen: true,
}, [
  {
    name: 'debug-events',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = sess.data?.data?.data?.id || sess.data?.data?.id;
      t.provider.expectToolCall({ id: 'write-file', tool: 'write', args: { filePath: 'hello.txt', content: 'Hello World\n' } });
      t.provider.expectText({ id: 'write-done', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'Write hello.txt with content "Hello World\\n"');
      await turn.awaitTerminal({ timeoutMs: 60000, requireAssistantTerminal: false });
      const events = t.events.allEvents;
      console.log('\n=== events ===');
      for (const e of events.filter((e) => e.type.startsWith('message') || e.type.startsWith('session') || e.finishReason)) {
        console.log(`seq=${e.seq} type=${e.type} finishReason=${e.finishReason} sessionID=${e.sessionID}`);
      }
      console.log('==============');
    },
  },
]);
process.exit(exitCode);
