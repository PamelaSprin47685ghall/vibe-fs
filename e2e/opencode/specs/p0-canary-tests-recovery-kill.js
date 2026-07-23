/**
 * p0-canary-tests-recovery-kill.js — SIGKILL restart preserves NDJSON integrity.
 */

import { getSessionId } from '../../../testkit/opencode/scenario.js';
import { waitForLoopCommand, validateNdjson } from './p0-canary-tests-recovery-helpers.js';

const testSigkillPreservesNdjson = {
  name: 'OC-ES-015 SIGKILL host preserves NDJSON integrity on restart',
  fn: async (t) => {
    const sid = getSessionId(await t.client.createSession());
    const hasPlugin = t.host._startOpts.pluginPaths && t.host._startOpts.pluginPaths.length > 0;
    if (hasPlugin) await waitForLoopCommand(t.client);

    t.provider.expectText({ id: 'warm', text: 'ok' });
    const turn = await t.turn.start(sid);
    await t.client.prompt(sid, 'hello');

    await t.events.close();
    t.host._child.kill('SIGKILL');

    await t.host.stop({ assert: false });
    await t.host.start(t.host._startOpts);
    t.client._baseUrl = t.host.baseUrl;
    t.events._baseUrl = t.host.baseUrl;
    await t.events.connect();

    validateNdjson(t.host.workDir, [sid]);
  },
};

export default [testSigkillPreservesNdjson];
