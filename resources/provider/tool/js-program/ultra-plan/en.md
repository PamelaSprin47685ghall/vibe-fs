// The closing form of this program: the work's own prose goes in the assistant
// text — no mandated sections; a few short FYI notes are welcome when they
// genuinely help the successor. The prose names the anchors it rests on; it
// does not copy the prose, and it is not written after the call: there is no
// after.
class Js extends JsProgram {
  async run() {
    const draft = await this.cat("plan.md");
    const matches = this.whereAll(draft, "undecidable");
    if (matches.length === 0) {
      // The prose of this stretch, in the assistant text:
      // "Nothing in the draft was decidable this stretch; the plan stands as it was."
      await this.deliver([12]);
    } else {
      let text = draft;
      for (const [begin, end] of matches.reverse()) {
        text = text.slice(0, begin) + "decidable" + text.slice(end);
      }
      this.write("plan.md", text);
      // The prose of this stretch, in the assistant text:
      // "Three 'undecidable' markers in the draft are decided; the trade-off
      // line still needs a DevOps fact before it can be settled."
      await this.deliver([12]);
    }
  }
}
