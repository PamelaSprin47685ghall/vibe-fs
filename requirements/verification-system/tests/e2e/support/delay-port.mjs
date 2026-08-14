import { setTimeout as delayTimer } from 'node:timers/promises';

export const createNodeDelayPort = () => ({
  delay: (ms) => delayTimer(ms),
  dispose: () => {},
});

export const createImmediateDelayPort = () => ({
  delay: () => Promise.resolve(),
  dispose: () => {},
});
