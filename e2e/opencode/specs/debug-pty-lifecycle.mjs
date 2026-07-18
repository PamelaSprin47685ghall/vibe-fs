import { runScenario } from '../harness/scenario.js';
import { findToolPart, findPtySessionId } from './p0-canary-utils.js';

const exitCode = await runScenario({
  plugin: true,
  timeoutMs: 90000,
  contextLimit: 20000,
  allowSynthetic: true,
  allowTitleGen: true,
}, [
  {
    name: 'debug-pty-lifecycle',
    fn: async (t) => {
      const { isPidAlive } = await import('../harness/process-host-checks.js');
      const sess = await t.client.createSession();
      const sid = sess.data?.data?.data?.id || sess.data?.data?.id;
      t.provider.expectToolCall({ id: 'pty-spawn', tool: 'pty_spawn', args: { command: 'sleep', args: ['5'], description: 'test pty lifecycle' } });
      t.provider.expectToolCall({ id: 'pty-list', tool: 'pty_list', args: {} });
      t.provider.expectToolCall({ id: 'pty-read', tool: 'pty_read', args: (reqBody) => ({ id: findPtySessionId(reqBody), offset: 0, limit: 50 }) });
      t.provider.expectToolCall({ id: 'pty-kill', tool: 'pty_kill', args: (reqBody) => ({ id: findPtySessionId(reqBody), cleanup: true }) });
      t.provider.expectText({ id: 'pty-done', text: 'done' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'run "sleep 5" in a PTY, list it, read it, then kill it');
      await turn.awaitTerminal({ timeoutMs: 60000 });
      const messages = (await t.client.messages(sid)).data || [];
      console.log('\n=== pty tool parts ===');
      for (const name of ['pty_spawn', 'pty_list', 'pty_read', 'pty_kill']) {
        const part = findToolPart(messages, name);
        console.log(`\n${name}:`);
        console.log(JSON.stringify(part?.state || 'not found', null, 2));
      }
      console.log('\n=== provider requests ===');
      t.provider.requests.forEach((r, i) => {
        console.log(`req ${i}: tools=${r.tools?.map((t) => t.function?.name || t.name).join(',')} last=${JSON.stringify(r.messages?.slice(-1)[0]?.content).slice(0, 120)}`);
      });
    },
  },
]);
process.exit(exitCode);
