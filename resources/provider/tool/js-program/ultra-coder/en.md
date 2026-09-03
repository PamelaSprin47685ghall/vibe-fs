class Js extends JsProgram {
  async run() {
    const refs = await this.grep(/\boldApi\b/, "{src,tests}/**/*.{js,ts}");
    const paths = [...new Set(refs.matches.map(x => x.path))];
    this.edit("src/api.js", [
      { find: "const oldApi = buildApi();", put: "const newApi = buildApi();" },
      { find: 'registry.register("oldApi", oldApi);', put: 'registry.register("newApi", newApi);' },
      { find: "export { oldApi };", put: "export { newApi };" },
    ]);
    for (const path of paths.filter(path => path !== "src/api.js")) {
      this.edit(path, { find: /\boldApi\b/, put: "newApi", all: true });
    }
    return { migrated: `oldApi → newApi`, referencesObserved: refs.matches.length };
  }
}
