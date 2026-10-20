class Js extends JsProgram {
  async run() {
    const draft = await this.cat(PLAN_PATH);
    const matches = this.whereAll(draft, "不可判定");
    if (matches.length === 0) return { changed: false };
    let text = draft;
    for (const [begin, end] of matches.reverse()) {
      text = text.slice(0, begin) + "可判定" + text.slice(end);
    }
    this.write(PLAN_PATH, text);
    return { changed: true };
  }
}
