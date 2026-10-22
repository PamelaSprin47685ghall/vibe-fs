  async fork(payload, carry = []) {
    // Returns { ok, name, agentId, outcome }. payload is a charge string or { charge, name?, calling?, keywords? }.
    // carry adds needed history: integers reference anchors; strings supply literal text. The current turn is not sent. State the task once in charge.
    // Example: await this.fork("index the parser module", [12, "List public entries only"]);
  }

