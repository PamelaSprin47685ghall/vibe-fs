  async resume(payload) {
    // 返回 { ok, name, agentId, outcome }。payload 必须带 { charge, name }，继续已有工作，不接受 calling。
    // 例如: await this.resume({ name: "audit-road", charge: "继续那项审计" });
  }
