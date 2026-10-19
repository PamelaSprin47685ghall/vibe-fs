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
    const paths = (await this.walk("src")).concat(await this.walk("tests"))
      .filter((path) => path.endsWith(".js") || path.endsWith(".ts"));
    let rewritten = 0;
    for (const path of paths) {
      const text = await this.cat(path);
      const spans = this.whereAll(text, "oldApi");
      if (spans.length === 0) continue;
      let next = text;
      for (const [begin, end] of [...spans].reverse()) {
        next = next.slice(0, begin) + "newApi" + next.slice(end);
      }
      this.write(path, next);
      rewritten += spans.length;
    }
    const api = await this.cat("src/api.js");
    const [registryAt] = await this.where(api, 'registry.register("oldApi", oldApi);');
    return { migrated: "oldApi -> newApi", rewritten, registryAt };
  }
}
