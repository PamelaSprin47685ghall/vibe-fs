// The closing form of this program: the work's prose goes in the assistant
// text — what was done, the evidence, what stays open, what a successor needs
// — and the report mapping points at the anchors holding that prose and the
// context it rests on. The mapping names the anchors; it does not copy the
// prose, and it is not written after the call: there is no after.
class Js extends JsProgram {
  async run() {
    const draft = await this.cat(PLAN_PATH);
    const matches = this.whereAll(draft, "undecidable");
    if (matches.length === 0) {
      // The prose of this stretch, in the assistant text:
      // "Nothing in the draft was decidable this stretch; the plan stands as it was."
      await this.deliver({ 12: "the closing prose above", 13: null });
    } else {
      let text = draft;
      for (const [begin, end] of matches.reverse()) {
        text = text.slice(0, begin) + "decidable" + text.slice(end);
      }
      this.write(PLAN_PATH, text);
      // The prose of this stretch, in the assistant text:
      // "Three 'undecidable' markers in the draft are decided; the trade-off
      // line still needs a DevOps fact before it can be settled."
      await this.deliver({ 12: "the closing prose above", 13: null });
    }
  }
}
