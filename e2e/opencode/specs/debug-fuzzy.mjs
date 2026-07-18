import { runScenario } from '../harness/scenario.js';

const exitCode = await runScenario({
  plugin: true,
  timeoutMs: 60000,
  contextLimit: 20000,
  allowSynthetic: true,
  allowTitleGen: true,
}, [
  {
    name: 'debug-fuzzy',
    fn: async (t) => {
      const content = (s) => s + '\n';
      const workDir = t.host.workDir;
      const fs = await import('node:fs');
      fs.writeFileSync(workDir + '/grep_target.txt', content('unique-pattern-xyz\nhello world\nunique-pattern-xyz again'));
      const sess = await t.client.createSession();
      const sid = sess.data?.data?.data?.id || sess.data?.data?.id;
      t.provider.expectToolCall({ id: 'fuzzy-grep', tool: 'fuzzy_grep', args: { pattern: ['unique-pattern-xyz'] } });
      t.provider.expectText({ id: 'grep-done', text: 'found matches' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'search for unique-pattern-xyz');
      await turn.awaitTerminal({ timeoutMs: 60000 });
      const res = await t.client.messages(sid);
      const part = (res.data || []).map((m) => m.parts || []).flat().find((p) => p.type === 'tool' && p.tool === 'fuzzy_grep');
      console.log('\n=== fuzzy_grep tool part ===');
      console.log(JSON.stringify(part, null, 2));
      console.log('==============================');
    },
  },
]);
process.exit(exitCode);
