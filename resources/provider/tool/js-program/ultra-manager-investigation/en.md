class Js extends JsProgram {
  async walk(dir) {
    const found = [];
    for (const entry of await this.ls(dir)) {
      const full = dir === "." ? entry : dir + "/" + entry;
      if (entry.endsWith("/")) found.push(...await this.walk(full.slice(0, -1)));
      else found.push(full);
    }
    return found;
  }
  async run() {
    const testFiles = (await this.walk("tests")).filter((path) => path.endsWith(".test.mjs"));
    const suites = [];
    for (const path of testFiles) {
      const text = await this.cat(path);
      for (const [begin, end] of this.whereAll(text, "describe(")) {
        suites.push({ path, span: [begin, end] });
      }
    }
    const packageText = await this.cat("package.json");
    return {
      testFileCount: testFiles.length,
      testSuiteCount: suites.length,
      hasScripts: packageText.includes('"scripts"'),
    };
  }
}
