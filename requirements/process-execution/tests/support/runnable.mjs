export const oneCommand = (command, { maxDuration = 5000, onExit = '', fields = '' } = {}) => `class extends this.Callable {
  maxDuration = ${maxDuration};
  interval = 25;
  ${fields}
  next(ctx) { return ctx.step === 0 ? ${JSON.stringify(command)} : null; }
  watch(ctx) { return ctx.remainingMs > 0; }
  sanity() {
    return {
      nextDone: () => [{ step: 1 }],
      watchVeto: () => [{ remainingMs: 0 }],
    };
  }
  ${onExit}
}`
