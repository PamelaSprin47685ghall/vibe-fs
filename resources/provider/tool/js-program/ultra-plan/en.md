class Js extends JsProgram {
  async run() {
    const draft = await this.cat("plan.md");
    const matches = this.whereAll(draft, "undecidable");
    if (matches.length === 0) {
      return;
    }
    let text = draft;
    for (const [begin, end] of matches.reverse()) {
      text = text.slice(0, begin) + "decidable" + text.slice(end);
    }
    this.write("plan.md", text);
  }
}
