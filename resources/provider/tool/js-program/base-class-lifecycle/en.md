  async park(payload) {
    // Suspends this program at this await point and returns { handle, payload }. The public tool input remains exactly { program: string }; resume/abandon belong to the separate lifecycle interface, not to Host tool arguments.
    // Example: const decision = await this.park({ stage: "review" });
  }

  async run() {
    // The program entry: your class implements it, and its return value is this program's result (it must be JSON-compatible).
    // Example: async run() { return { ok: true }; }
  }

