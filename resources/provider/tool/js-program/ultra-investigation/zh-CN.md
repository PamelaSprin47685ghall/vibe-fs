class Js extends JsProgram {
  async run() {
    const declarations = await this.grep(/\b(?:type|module)\s+RetryPolicy\b/, "src/**/*.fs");
    const paths = [...new Set(declarations.matches.map(x => x.path))];
    const evidence = await Promise.all(paths.map(async path => {
      const file = await this.file(path);
      const text = file.text();
      const at = text.search(/\b(?:type|module)\s+RetryPolicy\b/);
      return { path, declarationFound: at >= 0,
        excerpt: at >= 0 ? text.slice(Math.max(0, at - 220), at + 900) : null };
    }));
    return { declarations: declarations.matches, evidence, changedFiles: [], executed: false };
  }
}
