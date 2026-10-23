  async resume(payload, carry) {
    // Returns { ok, name, agentId, outcome }. payload requires { charge, name }. Continues existing work and does not accept calling.
    // carry adds needed history: integers reference anchors; strings supply literal text. The current turn is not sent. State the task once in charge.
    // Example: await this.resume({ name: "audit-road", charge: "continue the audit" }, [12]);
  }

