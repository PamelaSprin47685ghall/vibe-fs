class Js extends JsProgram {
  async run() {
    const testFiles = await this.glob("tests/**/*.test.mjs");
    const grepMatches = await this.grep(/describe\(/, "tests/**/*.test.mjs");
    const packageView = await this.file("package.json");
    const packageText = packageView.text();
    return {
      testFileCount: testFiles.paths.length,
      testSuiteCount: grepMatches.matches.length,
      hasScripts: packageText.includes('"scripts"'),
    };
  }
}
