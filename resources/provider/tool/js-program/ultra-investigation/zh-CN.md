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
    const declaration = /\b(?:type|module)\s+RetryPolicy\b/;
    const hits = [];
    const evidence = [];
    for (const path of (await this.walk("src")).filter((path) => path.endsWith(".fs"))) {
      const text = await this.cat(path);
      const at = text.search(declaration);
      if (at < 0) continue;
      hits.push({ path, at });
      evidence.push({
        path,
        declarationFound: true,
        excerpt: text.slice(Math.max(0, at - 220), at + 900),
      });
    }
    return { declarations: hits, evidence, changedFiles: [], executed: false };
  }
}
