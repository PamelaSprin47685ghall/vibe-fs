class Js extends JsProgram {
  async run() {
    const draft = await this.cat(PLAN_PATH);
    const [start, end] = await this.where(draft, "undecidable");
    this.write(PLAN_PATH, draft.slice(0, start) + "decidable" + draft.slice(end));
    return { changed: true };
  }
}
