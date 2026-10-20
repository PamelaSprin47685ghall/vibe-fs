  async advisor(intent, view) {
    // Returns { ok, advice }: consults this session's own advisor seat and takes the prose answer back as data, never as a rendering.
    // Example: const advice = await this.advisor("is the retry path sound?", { prefixUntil: 7, thenMap: {} });
  }
