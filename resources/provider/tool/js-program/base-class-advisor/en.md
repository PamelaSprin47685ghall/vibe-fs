  async advisor(intent, reportMapping) {
    // Returns { ok, advice }: consults this session's own advisor seat and takes the prose answer back as data, never as a rendering. reportMapping is required and is a Record<string, null|string> mapping anchor numbers to summaries (null leaves an anchor unmapped); the host reads it as { prefixUntil: 0, thenMap: reportMapping }. A missing or invalid reportMapping is refused with { ok: false, code: "VIEW_DECLARATION_INVALID", reason } and is never treated as an empty mapping; an unavailable seat is refused with { ok: false, code: "advisor-unavailable", reason }.
    // Example: const advice = await this.advisor("is the retry path sound?", { 1: "setup summary", 2: null });
  }
