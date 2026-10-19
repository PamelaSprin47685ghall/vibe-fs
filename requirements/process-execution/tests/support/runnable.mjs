export const oneCommand = (command, { maxDuration = 5000, onExit = '', fields = '' } = {}) => `class extends js.Runnable {
  maxDuration = ${maxDuration};
  interval = 25;
  ${fields}
  next(ctx) { return ctx.step === 0 ? ${JSON.stringify(command)} : null; }
  watch(ctx) { return ctx.remainingMs > 0; }
  sanity() {
    return {
      nextDone: () => this.next({ step: 1 }),
      watchVeto: () => this.watch({ remainingMs: 0 }),
    };
  }
  ${onExit}
}`
