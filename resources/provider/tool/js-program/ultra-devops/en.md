class Js extends JsProgram {
  async run() {
    const packageText = await this.cat("package.json");
    const syntaxCheck = await this.call("node --check scripts/build.mjs");
    return { packageText, syntaxCheck };
  }
}
