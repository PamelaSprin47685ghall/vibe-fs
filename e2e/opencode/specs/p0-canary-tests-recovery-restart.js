/**
 * p0-canary-tests-recovery-restart.js — Recovery from corrupt NDJSON / orphan lock files.
 */

import { getSessionId } from '../harness/scenario.js';
import { validateNdjson } from './p0-canary-tests-recovery-helpers.js';
import { readNdjsonLines, TIMEOUTS } from './p0-canary-utils.js';
import fs from 'node:fs';

const testCorruptAndOrphanRecovery = {
  name: 'OC-ES-004 OC-ES-005 OC-ES-014 recovery from truncated, corrupt NDJSON and orphan lock files',
  fn: async (t) => {
    const sid = getSessionId(await t.client.createSession());
    t.provider.expectText({ id: 'warm', text: 'ok' });
    const turn = await t.turn.start(sid);
    await t.client.prompt(sid, 'hello');
    await turn.awaitTerminal({ timeoutMs: TIMEOUTS.quick, requireAssistantTerminal: false });

    const p = t.host.workDir + '/.wanxiangshu.ndjson';
    const lockPath = t.host.workDir + '/.wanxiangshu.ndjson.lock';
    fs.writeFileSync(lockPath, 'lock-content', 'utf8');
    fs.appendFileSync(p, '{"V":1,"Session":"' + sid + '","Kind":"human_turn_started","At":"9999999999999","Payload":{}', 'utf8');
    fs.appendFileSync(p, '\n{invalid-json-content}\n', 'utf8');
    fs.appendFileSync(p, '{"V":1,"Session":"' + sid + '","Kind":"nudge_dedup_cleared","At":"9999999999998","Payload":{}}\n', 'utf8');

    await t.restart();

    const status = await t.client.sessionStatus(sid);
    if (status.status !== 200) throw new Error(`Failed to recover session status after corrupt NDJSON restart: ${status.status}`);

    if (fs.existsSync(lockPath)) throw new Error('Orphan lock file still exists after restart');

    const newSid = getSessionId(await t.client.createSession());
    t.provider.expectText({ id: 'new-warm', text: 'ok-new' });
    const newTurn = await t.turn.start(newSid);
    await t.client.prompt(newSid, 'hello new');
    await newTurn.awaitTerminal({ timeoutMs: TIMEOUTS.quick, requireAssistantTerminal: false });

    const afterEvents = readNdjsonLines(t.host.workDir);
    if (!afterEvents.some(e => e.Session === newSid && e.Kind === 'human_turn_started')) {
      throw new Error('New events not written to NDJSON after corrupt restart');
    }
  },
};

export default [testCorruptAndOrphanRecovery];
