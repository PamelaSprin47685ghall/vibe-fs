  async resume(payload, carry) {
    // 返回 { ok, name, agentId, outcome }。payload 必须带 { charge, name }，继续已有工作，不接受 calling。
    // carry 只补充所需历史：整数引用锚点，字符串是字面文本。本回合不传，任务只写在 charge 中。
    // 例如: await this.resume({ name: "audit-road", charge: "继续那项审计" }, [12]);
  }

