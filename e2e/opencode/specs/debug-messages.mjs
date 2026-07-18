import { runScenario } from '../harness/scenario.js';

const exitCode = await runScenario({
  plugin: true,
  timeoutMs: 60000,
  contextLimit: 20000,
  allowSynthetic: true,
  allowTitleGen: true,
}, [
  {
    name: 'debug-messages',
    fn: async (t) => {
      const content = (s) => s + '\n';
      const sess = await t.client.createSession();
      const sid = sess.data?.data?.data?.id || sess.data?.data?.id;
      t.provider.expectToolCall({ id: 'write-file', tool: 'write', args: { filePath: 'hello.txt', content: content('Hello World') } });
      t.provider.expectText({ id: 'write-done', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'Write hello.txt with content "Hello World\\n"');
      await turn.awaitTerminal({ timeoutMs: 60000 });
      const res = await t.client.messages(sid);
      console.log('\n=== messages ===');
      console.log(JSON.stringify(res.data, null, 2));
      console.log('================');
    },
  },
]);
process.exit(exitCode);
