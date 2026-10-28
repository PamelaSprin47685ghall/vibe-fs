class Js extends JsProgram {
  async run() {
    const draft = await this.cat("plan.md");
    const matches = this.whereAll(draft, "不可判定");
    if (matches.length === 0) {
      return;
    }
    let text = draft;
    for (const [begin, end] of matches.reverse()) {
      text = text.slice(0, begin) + "可判定" + text.slice(end);
    }
    this.write("plan.md", text);
  }
}
