class Js extends JsProgram {
  async run() {
    const candidateTests = await this.glob("tests/**/*.test.mjs");
    const file = await this.file("package.json", [["^", "$", "scripts"]]);
    const oldScript = "node scripts/retired-entry.mjs";
    const newScript = "node scripts/run.mjs";
    await this.file("scripts/test.mjs");
    this.edit("package.json", { find: JSON.stringify(oldScript), put: JSON.stringify(newScript) });
    return { candidateTests, repaired: true, verificationStillRequired: true };
  }
}
