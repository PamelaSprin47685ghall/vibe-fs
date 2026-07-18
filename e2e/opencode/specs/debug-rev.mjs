import fs from 'node:fs';
import { runScenario } from '../harness/scenario.js';

const exitCode = await runScenario({
  plugin: true,
  timeoutMs: 60000,
  contextLimit: 20000,
  allowSynthetic: true,
  allowTitleGen: true,
}, [
  {
    name: 'debug-rev',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = sess.data?.data?.data?.id || sess.data?.data?.id;
      t.provider.expectText({ id: 'rev-warm', text: 'ok' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'hello');
      await turn.awaitTerminal({ timeoutMs: 30000 });
      const cmdTurn = await t.turn.start(sid);
      const cmdRes = await t.client.runCommand(sid, 'loop', 'implement feature X', 10000);
      console.log('\n=== cmdRes ===');
      console.log(JSON.stringify(cmdRes, null, 2));
      await cmdTurn.awaitTerminal({ timeoutMs: 10000, requireAssistantTerminal: false });
      const ndjsonPath = t.host.workDir + '/.wanxiangshu.ndjson';
      console.log('\n=== ndjson exists', fs.existsSync(ndjsonPath));
      if (fs.existsSync(ndjsonPath)) {
        const lines = fs.readFileSync(ndjsonPath, 'utf8').split('\n').filter(Boolean);
        console.log('=== ndjson lines', lines.length);
        for (const line of lines.slice(-10)) {
          const obj = JSON.parse(line);
          console.log(`kind=${obj.kind} session=${obj.sessionID || obj.sessionId || obj.properties?.sessionID} type=${obj.properties?.type || obj.type || obj.payload?.type}`);
          if ((obj.properties?.type || obj.type || obj.payload?.type || '').includes('loop_activated') || JSON.stringify(obj).includes('loop_activated')) {
            console.log('FOUND loop_activated line:');
            console.log(JSON.stringify(obj, null, 2));
          }
        }
      }
      console.log('\n=== events with loop ===');
      for (const e of t.events.allEvents) {
        if (JSON.stringify(e).includes('loop_activated')) {
          console.log(JSON.stringify(e, null, 2));
        }
      }
    },
  },
]);
process.exit(exitCode);
