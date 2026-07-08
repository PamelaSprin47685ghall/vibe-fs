import { startInProcess } from './wanxiangzhen-harness/server.js';
import { startOpencode } from './wanxiangzhen-harness/serve.js';
import { releaseLock } from './wanxiangzhen-harness/lock.js';

export async function start(opts = {}) {
  try {
    if (opts.inProcess || process.env.WANXIANGZHEN_E2E_INPROCESS === '1') {
      return await startInProcess(opts);
    }
    return await startOpencode(opts);
  } catch (e) {
    releaseLock();
    return { error: e.message, stack: e.stack };
  }
}