import { runScenario } from '../harness/scenario.js';

const exitCode = await runScenario({
  plugin: true,
  timeoutMs: 30000,
  contextLimit: 20000,
  allowSynthetic: true,
  allowTitleGen: true,
}, [
  {
    name: 'debug-command',
    fn: async (t) => {
      const cmds = await t.client.request('GET', '/command');
      console.log('\n=== /command response ===');
      console.log(JSON.stringify(cmds.data, null, 2));
      console.log('=========================\n');
      const tools = await t.client.request('GET', '/tool');
      console.log('\n=== /tool response (first 10) ===');
      console.log(JSON.stringify(tools.data?.slice?.(0, 10) ?? tools.data, null, 2));
      console.log('=================================\n');
    },
  },
]);
process.exit(exitCode);
