namespace Wanxiangshu.Domain

/// Canonical example with capability requirements (proposal §44).
type JsExample =
    { Requires: Set<JsCapability>
      Source: string }

/// Proposal §12/§16/§31–§47: LLM-visible description is header + exact public
/// base class + capability rules + filtered examples + footer. Runtime proxy
/// class stays out of the description.
module JsCanonicalDescription =

    let header =
        "This is the programmable filesystem tool for the current agent.\n"
        + "\n"
        + "The base class below is generated from the capabilities actually available in\n"
        + "this request. If a method is present, you may use it. If a method is absent,\n"
        + "that capability is not available.\n"
        + "\n"
        + "Define exactly one class named Js that extends JsProgram and implement\n"
        + "async run()."

    let footer =
        "Use the generated API directly. Do not reimplement Host filesystem,\n"
        + "permission, anchor, snapshot, or transaction logic.\n"
        + "\n"
        + "Anchors locate. JavaScript transforms. Mutations are staged and committed by\n"
        + "the Host as one transaction."

    /// Proposal §16 canonical file() body (description uses HOST_READ).
    /// `raiseFailure` closes over `code` and `reason`.
    let fileAlgorithm (readSourceLine: string) (raiseFailure: string) =
        "  async file(path, matches = []) {\n"
        + "    const fail = (code, reason) => {\n"
        + "      "
        + raiseFailure
        + "\n"
        + "    };\n"
        + "    "
        + readSourceLine
        + "\n"
        + """
    const anchors = new Map([
      ["^", 0],
      ["$", source.length],
    ]);

    let cursor = 0;

    const preview = pattern => {
      if (typeof pattern === "string") {
        return pattern.length > 80
          ? JSON.stringify(pattern.slice(0, 80)) + "…"
          : JSON.stringify(pattern);
      }

      if (pattern instanceof RegExp) {
        const shown = "/" + pattern.source + "/";
        return shown.length > 80 ? shown.slice(0, 80) + "…" : shown;
      }

      return String(pattern);
    };

    const locatePattern = pattern => {
      if (typeof pattern === "string") {
        if (pattern.length === 0)
          fail("EMPTY_ANCHOR_CONTENT", "string anchor patterns must be non-empty");

        const start = source.indexOf(pattern, cursor);

        if (start < 0)
          return null;

        return {
          start,
          end: start + pattern.length,
        };
      }

      if (pattern instanceof RegExp) {
        // Anchor matching defines its own forward-search semantics.
        // Caller g/y state and lastIndex are ignored.
        const flags =
          [...new Set(
            pattern.flags.replace(/[gy]/g, "") + "g"
          )].join("");

        let regexp;
        try {
          regexp = new RegExp(pattern.source, flags);
        } catch {
          fail("INVALID_ANCHOR_PATTERN", "anchor RegExp is invalid");
        }

        regexp.lastIndex = cursor;

        const match = regexp.exec(source);

        if (!match)
          return null;

        return {
          start: match.index,
          end: match.index + match[0].length,
        };
      }

      fail("INVALID_ANCHOR_PATTERN", "anchor pattern must be a string or RegExp");
    };

    let declaration = 0;

    for (const [begin, end, pattern] of matches) {
      declaration += 1;

      if (!begin || !end)
        fail("EMPTY_ANCHOR_CONTENT", `anchor ${declaration}: names must be non-empty`);

      if (
        begin === "^" || begin === "$" ||
        end === "^" || end === "$"
      )
        fail("INVALID_ANCHOR_PATTERN", `anchor ${declaration}: ^ and $ are reserved`);

      if (begin === end)
        fail(
          "INVALID_ANCHOR_PATTERN",
          `anchor ${declaration}: begin and end names must differ`
        );

      if (anchors.has(begin) || anchors.has(end))
        fail("INVALID_ANCHOR_PATTERN", `anchor ${declaration}: names must be unique`);

      const match = locatePattern(pattern);

      if (!match)
        fail(
          "ANCHOR_NOT_FOUND",
          `anchor ${declaration} not found in ${path} (forward from cursor ${cursor}); pattern: ${preview(pattern)}`
        );

      anchors.set(begin, match.start);
      anchors.set(end, match.end);

      cursor = match.end;
    }

    const clip = n => Math.min(Math.max(0, n), source.length);

    const offset = name => {
      if (anchors.has(name))
        return anchors.get(name);

      const shifted = /^(.*)([+-]\d+)$/.exec(name);

      if (!shifted || shifted[1].length === 0)
        fail("ANCHOR_NOT_FOUND", `unknown anchor: ${name}`);

      return clip(offset(shifted[1]) + Number(shifted[2]));
    };

    return Object.freeze({
      text(from = "^", to = "$") {
        const start = offset(from);
        const end = offset(to);

        if (start > end)
          fail("INVALID_ANCHOR_PATTERN", `invalid slice: ${from} is after ${to}`);

        return source.slice(start, end);
      },
    });
  }"""

    let globStub = "  async glob(pattern) {\n    // Host capability\n  }"

    let grepStub =
        "  async grep(needle, pattern = \"**/*\") {\n    // Host capability\n  }"

    let rewriteStub = "  rewrite(path, newText) {\n    // Host capability\n  }"

    let writeStub = "  write(path, newText) {\n    // Host capability\n  }"

    let runStub =
        "  async run() {\n    throw new Error(\"Js.run() must be implemented.\");\n  }"

    let has (capabilities: Set<JsCapability>) (capability: JsCapability) = Set.contains capability capabilities

    let publicBaseClass (capabilities: Set<JsCapability>) : string =
        let methods =
            [ if has capabilities JsCapability.Read then
                  fileAlgorithm
                      "const source = await HOST_READ_IMMUTABLE_UTF8_SNAPSHOT(path);"
                      "throw new Error(reason);"
              if has capabilities JsCapability.Glob then
                  globStub
              if has capabilities JsCapability.Grep then
                  grepStub
              if has capabilities JsCapability.Edit then
                  rewriteStub
              if has capabilities JsCapability.Write then
                  writeStub
              runStub ]

        String.concat "\n\n" ("class JsProgram {" :: methods @ [ "}" ])

    let rethrowHost =
        "if (result && result.ok === false) {\n"
        + "      const err = new Error(result.reason || result.code);\n"
        + "      err.__jsFailure = { code: result.code, reason: result.reason || result.code };\n"
        + "      throw err;\n"
        + "    }"

    let runtimeBaseClass (capabilities: Set<JsCapability>) : string =
        let methods =
            [ "  constructor(api) { this._api = api; }"
              if has capabilities JsCapability.Read then
                  fileAlgorithm
                      "const snapshot = this._api.js.read(path);\n    if (snapshot && snapshot.ok === false) fail(snapshot.code || \"FILE_READ_FAILED\", snapshot.reason || snapshot.code || \"file read failed\");\n    const source = snapshot.text;"
                      "const err = new Error(reason); err.__jsFailure = { code, reason }; throw err;"
              if has capabilities JsCapability.Glob then
                  "  async glob(pattern) {\n    const result = this._api.js.glob(pattern);\n    "
                  + rethrowHost
                  + "\n    return { paths: result.paths, truncated: result.truncated === true };\n  }"
              if has capabilities JsCapability.Grep then
                  "  async grep(needle, pattern = \"**/*\") {\n    const result = this._api.js.grep(needle, pattern);\n    "
                  + rethrowHost
                  + "\n    return { matches: result.matches, truncated: result.truncated === true };\n  }"
              if has capabilities JsCapability.Edit then
                  "  rewrite(path, newText) {\n    const result = this._api.js.edit(path, newText);\n    "
                  + rethrowHost
                  + "\n  }"
              if has capabilities JsCapability.Write then
                  "  write(path, newText) {\n    const result = this._api.js.write(path, newText);\n    "
                  + rethrowHost
                  + "\n  }"
              runStub ]

        String.concat "\n\n" ("class JsProgram {" :: methods @ [ "}" ])

    let contract (toolName: string) (capabilities: Set<JsCapability>) =
        let verbs =
            List.concat
                [ if has capabilities JsCapability.Read then
                      [ "read" ]
                  else
                      []
                  if has capabilities JsCapability.Grep then
                      [ "search" ]
                  elif has capabilities JsCapability.Read && has capabilities JsCapability.Glob then
                      [ "search" ]
                  else
                      []
                  if has capabilities JsCapability.Edit then
                      [ "transform"; "rewrite" ]
                  else
                      []
                  if has capabilities JsCapability.Write then
                      [ "create" ]
                  else
                      [] ]
            |> String.concat ", "

        let parallelLine =
            if has capabilities JsCapability.Edit || has capabilities JsCapability.Write then
                "3. Parallel "
                + toolName
                + " calls are absolutely safe for same-file and cross-file edits."
            else
                "3. Parallel "
                + toolName
                + " calls are absolutely safe for same-file and cross-file reads."

        "Prefer "
        + toolName
        + " for all filesystem work.\n"
        + "\n"
        + "Do not use the legacy tools read/edit/write/glob/grep/patch for new work when "
        + toolName
        + " is available.\n"
        + toolName
        + " is the capability-projected JavaScript filesystem SDK for this\n"
        + "request. It can "
        + verbs
        + " files in one transactional program — including large parallel batches.\n"
        + "\n"
        + "Strongly recommended:\n"
        + "1. Call "
        + toolName
        + " instead of read/edit/write/glob/grep/patch whenever possible.\n"
        + "2. Write complex JavaScript in one "
        + toolName
        + " program rather than many legacy RPCs.\n"
        + parallelLine
        + "\n"
        + "\n"
        + "Call tools in parallel whenever needed. Parallel reads, parallel edits, same-file\n"
        + "and cross-file calls are all absolutely safe. The Host serializes one assistant\n"
        + "message's tool calls in deterministic order; each call is its own transaction.\n"
        + "\n"
        + "Write complex JavaScript in one program. The Host commits the whole program as one\n"
        + "all-or-nothing transaction."

    let readRules =
        "file(path, matches = []) reads this transaction's immutable UTF-8 snapshot,\n"
        + "optionally resolves ordered anchors, and returns an immutable FileView.\n"
        + "\n"
        + "matches is Array<[beginAnchor, endAnchor, pattern]> where pattern is a non-empty\n"
        + "string or a RegExp. Anchors are position names, not the matched text.\n"
        + "Every FileView has built-in anchors ^ (file start) and $ (file end). Do not\n"
        + "declare ^ or $ as custom names.\n"
        + "\n"
        + "Ordered matching: each pattern is searched from the current cursor; after a match,\n"
        + "cursor = match.end. Duplicate source text does not need to be globally unique.\n"
        + "Caller RegExp g/y flags and lastIndex are ignored; matching uses its own forward\n"
        + "search. Zero-width RegExp is allowed (begin offset may equal end offset); begin\n"
        + "and end names must still differ.\n"
        + "\n"
        + "Anchor declaration refusals: empty names; reserved ^/$; duplicate names; begin == end\n"
        + "in one declaration; empty string pattern. Pattern not found in declaration order fails.\n"
        + "\n"
        + "file.text(from, to) — default text(from = \"^\", to = \"$\") — returns the exact original\n"
        + "substring between two resolved anchors. String pattern content must be non-empty.\n"
        + "Reverse slices fail. FileView is immutable: rewrite() does not change a previously\n"
        + "returned view.\n"
        + "\n"
        + "from/to may be a declared name, ^, $, or a temporary shift name+N / name-N\n"
        + "(example: h1+200, h1-40, $+0). Shifts are not stored. If the full string is a\n"
        + "declared name, that exact name wins. Otherwise the last [+-]digits is the delta;\n"
        + "the base name is resolved recursively. The resulting caret is clipped to\n"
        + "[0, file_len] inclusive, so $+N and ^-N stay at EOF / start.\n"
        + "\n"
        + "Recommended workflow:\n"
        + "1. Declare the minimal begin/end anchors needed to locate spans (read or edit).\n"
        + "2. Let Host resolve those positions.\n"
        + "3. Read with text(from, to). Adjacent headers make a body slice:\n"
        + "   text(\"h1end\", \"h2\"). A window around a hit is text(\"h1\", \"h1+200\").\n"
        + "4. For edits, build the complete resulting file from text(...) slices plus new content.\n"
        + "5. Use indexOf / replaceAll only when anchor-and-splice is genuinely inconvenient.\n"
        + "\n"
        + "Prefer:\n"
        + "  f.text(\"^\", \"begin\") + \"newString\" + f.text(\"end\", \"$\")"

    let globRules =
        "glob(pattern) enumerates files with gitignore/wildmatch semantics under the\n"
        + "current path boundary. * does not cross /. ** matches zero or more directories.\n"
        + "A pattern without a slash matches at any depth (*.md matches every .md file).\n"
        + "{a,b} expands to alternatives. Results omit .git, omit gitignored paths, do\n"
        + "not follow symlinks, and are sorted. The return value is { paths, truncated }.\n"
        + "The bound is on match count; truncated is true when matches were cut. glob\n"
        + "does not grant Read."

    let grepRules =
        "grep(needle, pattern = \"**/*\") searches UTF-8 files selected by the same\n"
        + "gitignore-style glob. needle is a non-empty string (literal) or a RegExp\n"
        + "(caller g/y/lastIndex ignored). Unreadable or non-UTF-8 files are skipped.\n"
        + "Returns { matches: [{ path, line, column, text }], truncated }. line and\n"
        + "column are 1-based. text is the matched substring. The bound is on match\n"
        + "count. grep does not grant file()."

    let editRules =
        "rewrite(path, newText) stages replacement of an existing UTF-8 file. newText is\n"
        + "the complete resulting file, not a patch. The target must exist in the\n"
        + "transaction snapshot or the call fails FILE_NOT_FOUND. newText must be a string.\n"
        + "The call does not write immediately; it adds a StagedRewrite to this program's\n"
        + "WriteSet. You do not have to file(path) first."

    let writeRules =
        "write(path, newText) stages creation of a missing UTF-8 file. If the target\n"
        + "exists: FILE_ALREADY_EXISTS. Edit and Write are distinct capabilities."

    let mutationRules =
        "A program may mutate each canonical path exactly once. A second rewrite/write\n"
        + "on the same path is DUPLICATE_MUTATION_TARGET. Multi-phase edits belong in\n"
        + "JavaScript variables, then one rewrite/write.\n"
        + "\n"
        + "The generated class has no commit, rollback, snapshot, or transaction methods.\n"
        + "run() returning normally → Host preflight → prepare → commit. run() throwing or\n"
        + "any file()/glob()/grep() failure discards every staged mutation.\n"
        + "\n"
        + "run() must return a JSON-compatible value: null, boolean, finite number, string,\n"
        + "array, or plain object (recursive). undefined, BigInt, NaN, Infinity, function,\n"
        + "symbol, cyclic or exotic objects fail as INVALID_RETURN_VALUE before commit."

    let rules (capabilities: Set<JsCapability>) : string =
        let blocks =
            [ if has capabilities JsCapability.Read then
                  readRules
              if has capabilities JsCapability.Glob then
                  globRules
              if has capabilities JsCapability.Grep then
                  grepRules
              if has capabilities JsCapability.Edit then
                  editRules
              if has capabilities JsCapability.Write then
                  writeRules
              if has capabilities JsCapability.Edit || has capabilities JsCapability.Write then
                  mutationRules ]

        String.concat "\n\n" blocks

    let examples: JsExample list =
        [ { Requires = set [ JsCapability.Read ]
            Source = JsFragmentRegistry.read.CanonicalExample }
          { Requires = set [ JsCapability.Read ]
            Source =
              "class Js extends JsProgram {\n"
              + "  async run() {\n"
              + "    const doc = await this.file(\"docs/what/js-tools.md\", [\n"
              + "      [\"h1\", \"h1end\", \"## JS-016\"],\n"
              + "      [\"h2\", \"h2end\", \"## JS-017\"],\n"
              + "      [\"h3\", \"h3end\", \"## JS-019\"],\n"
              + "    ]);\n"
              + "    return {\n"
              + "      resultShape: doc.text(\"h1\", \"h2\"),\n"
              + "      aroundH1: doc.text(\"h1\", \"h1+200\"),\n"
              + "      failures: doc.text(\"h3\", \"$\"),\n"
              + "    };\n"
              + "  }\n"
              + "}" }
          { Requires = set [ JsCapability.Glob ]
            Source = JsFragmentRegistry.glob.CanonicalExample }
          { Requires = set [ JsCapability.Grep ]
            Source = JsFragmentRegistry.grep.CanonicalExample }
          { Requires = set [ JsCapability.Read; JsCapability.Glob ]
            Source =
              "class Js extends JsProgram {\n"
              + "  async run() {\n"
              + "    const { paths } = await this.glob(\"src/**/*.js\");\n"
              + "    const hits = [];\n"
              + "    for (const path of paths) {\n"
              + "      const file = await this.file(path);\n"
              + "      const text = file.text();\n"
              + "      for (const match of text.matchAll(/TODO:.+/g)) {\n"
              + "        hits.push({ path, index: match.index, text: match[0] });\n"
              + "      }\n"
              + "    }\n"
              + "    return hits;\n"
              + "  }\n"
              + "}" }
          { Requires = set [ JsCapability.Read; JsCapability.Edit ]
            Source = JsFragmentRegistry.rewrite.CanonicalExample }
          { Requires = set [ JsCapability.Read; JsCapability.Edit ]
            Source =
              "class Js extends JsProgram {\n"
              + "  async run() {\n"
              + "    const file = await this.file(\"src/foo.js\", [\n"
              + "      [\"begin\", \"end\", /const\\s+version\\s*=\\s*\"[^\"]*\";/],\n"
              + "    ]);\n"
              + "    this.rewrite(\n"
              + "      \"src/foo.js\",\n"
              + "      file.text(\"^\", \"begin\")\n"
              + "        + 'const version = \"2.0\";'\n"
              + "        + file.text(\"end\", \"$\")\n"
              + "    );\n"
              + "  }\n"
              + "}" }
          { Requires = set [ JsCapability.Read; JsCapability.Edit ]
            Source =
              "class Js extends JsProgram {\n"
              + "  async run() {\n"
              + "    const file = await this.file(\"src/foo.js\", [\n"
              + "      [\"at\", \"afterAt\", /(?=function foo)/],\n"
              + "    ]);\n"
              + "    this.rewrite(\n"
              + "      \"src/foo.js\",\n"
              + "      file.text(\"^\", \"at\")\n"
              + "        + \"// inserted\\n\"\n"
              + "        + file.text(\"at\", \"$\")\n"
              + "    );\n"
              + "  }\n"
              + "}" }
          { Requires = set [ JsCapability.Read; JsCapability.Edit ]
            Source =
              "class Js extends JsProgram {\n"
              + "  async run() {\n"
              + "    const file = await this.file(\"src/foo.js\", [\n"
              + "      [\"begin\", \"end\", \"const obsolete = true;\\n\"],\n"
              + "    ]);\n"
              + "    this.rewrite(\n"
              + "      \"src/foo.js\",\n"
              + "      file.text(\"^\", \"begin\") + file.text(\"end\", \"$\")\n"
              + "    );\n"
              + "  }\n"
              + "}" }
          { Requires = set [ JsCapability.Read; JsCapability.Edit ]
            Source =
              "class Js extends JsProgram {\n"
              + "  async run() {\n"
              + "    const file = await this.file(\"src/foo.js\", [\n"
              + "      [\"a\", \"b\", \"first block\"],\n"
              + "      [\"c\", \"d\", \"second block\"],\n"
              + "    ]);\n"
              + "    this.rewrite(\n"
              + "      \"src/foo.js\",\n"
              + "      file.text(\"^\", \"a\")\n"
              + "        + file.text(\"c\", \"d\")\n"
              + "        + file.text(\"b\", \"c\")\n"
              + "        + file.text(\"a\", \"b\")\n"
              + "        + file.text(\"d\", \"$\")\n"
              + "    );\n"
              + "  }\n"
              + "}" }
          { Requires = set [ JsCapability.Read; JsCapability.Edit ]
            Source =
              "class Js extends JsProgram {\n"
              + "  async run() {\n"
              + "    const file = await this.file(\"src/foo.js\", [\n"
              + "      [\"a\", \"b\", \"const item = createItem();\"],\n"
              + "    ]);\n"
              + "    this.rewrite(\n"
              + "      \"src/foo.js\",\n"
              + "      file.text(\"^\", \"a\")\n"
              + "        + file.text(\"a\", \"b\")\n"
              + "        + \"\\n\"\n"
              + "        + file.text(\"a\", \"b\")\n"
              + "        + file.text(\"b\", \"$\")\n"
              + "    );\n"
              + "  }\n"
              + "}" }
          { Requires = set [ JsCapability.Read; JsCapability.Edit ]
            Source =
              "class Js extends JsProgram {\n"
              + "  async run() {\n"
              + "    const file = await this.file(\"src/foo.js\", [\n"
              + "      [\"a\", \"b\", \"oldA\"],\n"
              + "      [\"c\", \"d\", \"oldB\"],\n"
              + "    ]);\n"
              + "    this.rewrite(\n"
              + "      \"src/foo.js\",\n"
              + "      file.text(\"^\", \"a\")\n"
              + "        + \"newA\"\n"
              + "        + file.text(\"b\", \"c\")\n"
              + "        + \"newB\"\n"
              + "        + file.text(\"d\", \"$\")\n"
              + "    );\n"
              + "  }\n"
              + "}" }
          { Requires = set [ JsCapability.Write ]
            Source = JsFragmentRegistry.write.CanonicalExample }
          { Requires = set [ JsCapability.Read; JsCapability.Edit ]
            Source =
              "class Js extends JsProgram {\n"
              + "  async run() {\n"
              + "    const implementation = await this.file(\"src/foo.js\", [\n"
              + "      [\"a\", \"b\", \"oldValue\"],\n"
              + "    ]);\n"
              + "    const test = await this.file(\"tests/foo.test.js\", [\n"
              + "      [\"a\", \"b\", '\"oldValue\"'],\n"
              + "    ]);\n"
              + "    this.rewrite(\n"
              + "      \"src/foo.js\",\n"
              + "      implementation.text(\"^\", \"a\") + \"newValue\" + implementation.text(\"b\", \"$\")\n"
              + "    );\n"
              + "    this.rewrite(\n"
              + "      \"tests/foo.test.js\",\n"
              + "      test.text(\"^\", \"a\") + '\"newValue\"' + test.text(\"b\", \"$\")\n"
              + "    );\n"
              + "    return { changed: [\"src/foo.js\", \"tests/foo.test.js\"] };\n"
              + "  }\n"
              + "}" }
          { Requires = set [ JsCapability.Read; JsCapability.Glob; JsCapability.Edit ]
            Source =
              "class Js extends JsProgram {\n"
              + "  async run() {\n"
              + "    const { paths } = await this.glob(\"src/**/*.js\");\n"
              + "    const changed = [];\n"
              + "    for (const path of paths) {\n"
              + "      const file = await this.file(path);\n"
              + "      const oldText = file.text();\n"
              + "      if (!/\\boldApi\\b/.test(oldText)) continue;\n"
              + "      this.rewrite(path, oldText.replaceAll(\"oldApi\", \"newApi\"));\n"
              + "      changed.push(path);\n"
              + "    }\n"
              + "    return { changed };\n"
              + "  }\n"
              + "}" } ]

    // GrandRewrite §6.10: method descriptions teach syntax; exactly one Ultra
    // Example teaches how this responsibility should think with the projected
    // SDK. The program stops when the next step needs semantic judgment.
    let private coderUltra =
        """class Js extends JsProgram {
  async run() {
    const refs = await this.grep(/\boldApi\b/, "{src,tests}/**/*.{js,ts}");
    if (refs.truncated) throw new Error("Migration frontier was truncated.");
    const paths = [...new Set(refs.matches.map(x => x.path))];
    const core = await this.file("src/api.js", [
      ["definition", "afterDefinition", "const oldApi = buildApi();"],
      ["export", "afterExport", "export { oldApi };"],
      ["registration", "afterRegistration", 'registry.register("oldApi", oldApi);'],
    ]);
    const consumers = await Promise.all(
      paths.filter(path => path !== "src/api.js").map(async path => [path, await this.file(path)])
    );
    this.rewrite(
      "src/api.js",
      core.text("^", "definition") + "const newApi = buildApi();"
        + core.text("afterDefinition", "export") + 'registry.register("newApi", newApi);'
        + core.text("afterExport", "registration") + "export { newApi };"
        + core.text("afterRegistration", "$")
    );
    for (const [path, file] of consumers) {
      const before = file.text();
      const after = before.replace(/\boldApi\b/g, "newApi");
      if (after !== before) this.rewrite(path, after);
    }
    return { migrated: "oldApi → newApi", referencesObserved: refs.matches.length };
  }
}"""

    let private inspectorUltra =
        """class Js extends JsProgram {
  async run() {
    const declarations = await this.grep(/\b(?:type|module)\s+RetryPolicy\b/, "src/**/*.fs");
    if (declarations.truncated) return { incomplete: true, reason: "Declaration discovery was truncated." };
    const paths = [...new Set(declarations.matches.map(x => x.path))];
    if (paths.length === 0) {
      const usages = await this.grep(/\bRetryPolicy\b/, "{src,tests}/**/*.fs");
      return { declarations: [], usages: usages.matches, truncated: usages.truncated };
    }
    const evidence = await Promise.all(paths.map(async path => {
      try {
        const file = await this.file(path, [["hit", "afterHit", /\b(?:type|module)\s+RetryPolicy\b/]]);
        return { path, excerpt: file.text("hit-220", "hit+900"), anchorMatched: true };
      } catch {
        const file = await this.file(path);
        return { path, excerpt: file.text("^", "^+1100"), anchorMatched: false };
      }
    }));
    return { declarations: declarations.matches, evidence };
  }
}"""

    let private reviewerUltra =
        """class Js extends JsProgram {
  async run() {
    const stale = await this.grep(/\boldApi\b/, "src/**/*.{js,ts}");
    if (stale.truncated) return { incomplete: true, reason: "Counterexample search was truncated." };
    if (stale.matches.length > 0) {
      const paths = [...new Set(stale.matches.map(x => x.path))].slice(0, 6);
      const evidence = await Promise.all(paths.map(async path => {
        const file = await this.file(path);
        return { path, excerpt: file.text("^", "^+900") };
      }));
      return { staleReferences: stale.matches, evidence };
    }
    const migrated = await this.grep(/\bnewApi\b/, "src/**/*.{js,ts}");
    return { staleReferences: [], migratedReferences: migrated.matches, truncated: migrated.truncated };
  }
}"""

    let private devOpsUltra =
        """class Js extends JsProgram {
  async run() {
    const manifests = await this.glob("package.json");
    if (!manifests.paths.includes("package.json")) return { rootPackage: null };
    const pkg = JSON.parse((await this.file("package.json")).text());
    const testScript = pkg.scripts?.test ?? null;
    if (!testScript) return { rootPackage: "package.json", testScript: null, scripts: Object.keys(pkg.scripts || {}) };
    const tests = await this.glob("tests/**/*recovery*.{test,spec}.{js,ts,mjs}");
    if (tests.truncated) return { testScript, incomplete: true };
    if (tests.paths.length === 0) {
      const hits = await this.grep(/RecoveryClosure|recovery/i, "tests/**/*.{js,ts,mjs}");
      return { testScript, candidateTests: [...new Set(hits.matches.map(x => x.path))], truncated: hits.truncated };
    }
    return {
      packageManager: typeof pkg.packageManager === "string" ? pkg.packageManager : null,
      testScript,
      candidateTests: tests.paths,
    };
  }
}"""

    let private browserUltra =
        """class Js extends JsProgram {
  async run() {
    const hits = await this.grep(/\bWidgetOptions\b/, "artifacts/web/**/*.md");
    if (hits.truncated) return { incomplete: true, reason: "Captured-source search was truncated." };
    if (hits.matches.length === 0) {
      const indirect = await this.grep(/widget options|configuration object|deprecated/i, "artifacts/web/**/*.md");
      return { exact: [], indirect: indirect.matches, truncated: indirect.truncated };
    }
    const paths = [...new Set(hits.matches.map(x => x.path))];
    const sources = await Promise.all(paths.map(async path => {
      const file = await this.file(path);
      const text = file.text();
      const at = text.search(/\bWidgetOptions\b/);
      return {
        path,
        url: /^URL:\s*(.+)$/m.exec(text)?.[1]?.trim() ?? null,
        version: /^Version:\s*(.+)$/m.exec(text)?.[1]?.trim() ?? null,
        excerpt: text.slice(Math.max(0, at - 250), at + 1000),
      };
    }));
    return { sources };
  }
}"""

    let ultraExample (roleName: string) (capabilities: Set<JsCapability>) : JsExample option =
        let candidate =
            match roleName.Trim().ToLowerInvariant() with
            | "coder" -> Some(set [ JsCapability.Read; JsCapability.Grep; JsCapability.Edit ], coderUltra)
            | "inspector" -> Some(set [ JsCapability.Read; JsCapability.Grep ], inspectorUltra)
            | "reviewer" -> Some(set [ JsCapability.Read; JsCapability.Grep ], reviewerUltra)
            | "devops" -> Some(set [ JsCapability.Read; JsCapability.Glob; JsCapability.Grep ], devOpsUltra)
            | "browser" -> Some(set [ JsCapability.Read; JsCapability.Grep ], browserUltra)
            | _ -> None

        candidate
        |> Option.bind (fun (requires, source) ->
            if Set.isSubset requires capabilities then
                Some { Requires = requires; Source = source }
            else
                None)

    let filteredExamples (capabilities: Set<JsCapability>) : JsExample list =
        examples
        |> List.filter (fun example -> Set.isSubset example.Requires capabilities)

    let render (roleName: string) (toolName: string) (capabilities: Set<JsCapability>) : string =
        let ultraBlock =
            match ultraExample roleName capabilities with
            | Some example ->
                "Ultra Example — responsibility-shaped, not a toy syntax sample:\n\n```js\n"
                + example.Source
                + "\n```"
            | None -> "Ultra Example unavailable for this capability projection."

        String.concat
            "\n\n"
            [ header
              contract toolName capabilities
              "```js\n" + publicBaseClass capabilities + "\n```"
              rules capabilities
              "Mechanical branches belong inside the program. Semantic branches belong between programs. A program may know how to continue without pretending to know what the evidence will mean."
              ultraBlock
              footer ]
