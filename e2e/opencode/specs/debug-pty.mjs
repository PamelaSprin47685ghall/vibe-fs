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
    name: 'debug-pty',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = sess.data?.data?.data?.id || sess.data?.data?.id;
      t.provider.expectToolCall({ id: 'pty-spawn', tool: 'pty_spawn', args: { command: 'echo', args: ['hello-pty'], description: 'test pty spawn' } });
      t.provider.expectText({ id: 'pty-done', text: 'spawned' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'run "echo hello-pty" in a PTY session and confirm it started');
      await turn.awaitTerminal({ timeoutMs: 30000 });
      const messages = (await t.client.messages(sid)).data || [];
      const part = findToolPart(messages, 'pty_spawn');
      console.log('\n=== pty_spawn tool part ===');
      console.log(JSON.stringify(part, null, 2));
      console.log('\n=== messages ===');
      for (const m of messages) {
        const texts = (m.parts || []).filter((p) => p.type === 'text' || p.type === 'tool').map((p) => ({ type: p.type, tool: p.tool, text: p.text, state: p.state }));
        if (texts.length) console.log(JSON.stringify({ role: m.info?.role, parts: texts }, null, 2));
      }
    },
  },
]);
process.exit(exitCode);
