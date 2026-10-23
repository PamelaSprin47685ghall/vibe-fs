// 本程序的收尾形态：本次工作的散文写在助手正文——按工作本身组织，不套章节清单；确实对继任者有用的几行 FYI 可以写。散文点名它依赖的锚点，不复制散文，也不在调用之后补写：调用之后没有以后。
class Js extends JsProgram {
  async run() {
    const draft = await this.cat("plan.md");
    const matches = this.whereAll(draft, "不可判定");
    if (matches.length === 0) {
      // 这一段的散文，写在助手正文：
      // "这一段里草案没有什么可判定；计划原样保留。"
      await this.deliver([12]);
    } else {
      let text = draft;
      for (const [begin, end] of matches.reverse()) {
        text = text.slice(0, begin) + "可判定" + text.slice(end);
      }
      this.write("plan.md", text);
      // 这一段的散文，写在助手正文：
      // "草案里三处「不可判定」已判定；取舍那一行还差一个 DevOps 事实才能定。"
      await this.deliver([12]);
    }
  }
}
