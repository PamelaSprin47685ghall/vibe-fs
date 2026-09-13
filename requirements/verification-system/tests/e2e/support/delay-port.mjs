import { setTimeout as delayTimer } from 'node:timers/promises';

export const createNodeDelayPort = () => {
  const activeControllers = new Set();
  return {
    delay: (ms, opts = {}) => {
      const ac = new AbortController();
      activeControllers.add(ac);
      const onAbort = () => ac.abort();
      if (opts.signal) {
        if (opts.signal.aborted) ac.abort();
        else opts.signal.addEventListener('abort', onAbort, { once: true });
      }
      const promise = delayTimer(ms, undefined, { signal: ac.signal })
        .then(() => true)
        .catch((err) => {
          if (err?.name === 'AbortError') return false;
          throw err;
        })
        .finally(() => {
          activeControllers.delete(ac);
          if (opts.signal) opts.signal.removeEventListener('abort', onAbort);
        });
      promise.cancel = () => ac.abort();
      return promise;
    },
    dispose: () => {
      for (const ac of activeControllers) {
        ac.abort();
      }
      activeControllers.clear();
    },
  };
};

export const createImmediateDelayPort = () => ({
  delay: () => {
    const p = Promise.resolve(true);
    p.cancel = () => {};
    return p;
  },
  dispose: () => {},
});
