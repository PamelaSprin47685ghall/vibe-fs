namespace Wanxiangshu.Domain

open Wanxiangshu.Kernel

/// DSL-class: Vocabulary — filesystem primitive capabilities projected into
/// the js-ROLE SDK. Pure projection of ToolPermission; never decided here
/// (JS-001: no second permission matrix).
[<RequireQualifiedAccess>]
type JsCapability =
    | Read
    | Write
    | Edit
    | Glob
    | Grep

module JsCapability =

    /// The only ToolPermission → JsCapability mapping. Non-filesystem
    /// permissions produce no js-* member (JS-004 four-layer exactness).
    let ofToolPermission (permission: ToolPermission) : JsCapability option =
        match permission with
        | ToolPermission.Read -> Some JsCapability.Read
        | ToolPermission.Write -> Some JsCapability.Write
        | ToolPermission.Edit -> Some JsCapability.Edit
        | ToolPermission.Glob -> Some JsCapability.Glob
        | ToolPermission.Grep -> Some JsCapability.Grep
        | _ -> None

    let ofToolCapabilities (capabilities: Set<ToolPermission>) : Set<JsCapability> =
        capabilities |> Set.toList |> List.choose ofToolPermission |> Set.ofList

    /// Fixed canonical member order (JS-002): file, glob, grep, rewrite, write.
    let order (capability: JsCapability) : int =
        match capability with
        | JsCapability.Read -> 0
        | JsCapability.Glob -> 1
        | JsCapability.Grep -> 2
        | JsCapability.Edit -> 3
        | JsCapability.Write -> 4

/// One capability's complete SDK projection: the single source for member
/// name, description, canonical example, and runtime binding key. The four
/// layers must stay identical or the surface lies (JS-002/004).
type JsCapabilityFragment =
    {
        Capability: JsCapability
        MemberName: string
        /// LLM-visible method signature in the public base class (JS-002).
        Signature: string
        Description: string
        CanonicalExample: string
        RuntimeBindingKey: string
    }

/// Fixed registry of all fragments: the only place a js-* member can be born.
module JsFragmentRegistry =

    let read: JsCapabilityFragment =
        { Capability = JsCapability.Read
          MemberName = "file"
          Signature = "async file(path, matches = [])"
          Description =
            "file(path, matches = []) — read a strict-UTF-8 immutable snapshot and "
            + "optionally resolve ordered begin/end anchors. Returns FileView with text(from, to)."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    const file = await this.file(\"README.md\");\n"
            + "    return file.text();\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.read" }

    let glob: JsCapabilityFragment =
        { Capability = JsCapability.Glob
          MemberName = "glob"
          Signature = "async glob(pattern)"
          Description =
            "glob(pattern) — gitignore-style bounded path enumeration. Returns "
            + "{ paths, truncated }. Does not grant Read on those paths."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    return await this.glob(\"src/**/*.fs\");\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.glob" }

    let grep: JsCapabilityFragment =
        { Capability = JsCapability.Grep
          MemberName = "grep"
          Signature = "async grep(needle, pattern = \"**/*\")"
          Description =
            "grep(needle, pattern = \"**/*\") — search UTF-8 files selected by the same "
            + "gitignore-style glob. needle is a non-empty string or RegExp. Returns "
            + "{ matches, truncated } with 1-based line/column. Does not grant file()."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    return await this.grep(/TODO:.+/, \"src/**/*.js\");\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.grep" }

    let rewrite: JsCapabilityFragment =
        { Capability = JsCapability.Edit
          MemberName = "rewrite"
          Signature = "rewrite(path, newText)"
          Description =
            "rewrite(path, newText) — stage replacement of an existing UTF-8 file. "
            + "Missing target is FILE_NOT_FOUND. Commits with the program (all-or-nothing)."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    const file = await this.file(\"src/foo.js\", [\n"
            + "      [\"begin\", \"end\", \"oldString\"],\n"
            + "    ]);\n"
            + "    this.rewrite(\n"
            + "      \"src/foo.js\",\n"
            + "      file.text(\"^\", \"begin\")\n"
            + "        + \"newString\"\n"
            + "        + file.text(\"end\", \"$\")\n"
            + "    );\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.edit" }

    let write: JsCapabilityFragment =
        { Capability = JsCapability.Write
          MemberName = "write"
          Signature = "write(path, newText)"
          Description =
            "write(path, newText) — stage creation of a missing UTF-8 file. "
            + "Existing target is FILE_ALREADY_EXISTS. Commits with the program."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    this.write(\n"
            + "      \"generated/version.txt\",\n"
            + "      \"1.2.3\\n\"\n"
            + "    );\n"
            + "    return { created: \"generated/version.txt\" };\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.write" }

    let all: JsCapabilityFragment list = [ read; glob; grep; rewrite; write ]

    let byCapability: Map<JsCapability, JsCapabilityFragment> =
        all |> List.map (fun fragment -> fragment.Capability, fragment) |> Map.ofList

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
    let fileAlgorithm (readSourceLine: string) =
        "  async file(path, matches = []) {\n"
        + "    "
        + readSourceLine
        + "\n"
        + """
    const anchors = new Map([
      ["^", 0],
      ["$", source.length],
    ]);

    let cursor = 0;

    const findNext = pattern => {
      if (typeof pattern === "string") {
        if (pattern.length === 0)
          throw new Error("String anchor patterns must be non-empty.");

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

        const regexp = new RegExp(pattern.source, flags);
        regexp.lastIndex = cursor;

        const match = regexp.exec(source);

        if (!match)
          return null;

        return {
          start: match.index,
          end: match.index + match[0].length,
        };
      }

      throw new Error(
        "Anchor pattern must be a string or RegExp."
      );
    };

    for (const [begin, end, pattern] of matches) {
      if (!begin || !end)
        throw new Error("Anchor names must be non-empty.");

      if (
        begin === "^" || begin === "$" ||
        end === "^" || end === "$"
      )
        throw new Error("^ and $ are reserved anchors.");

      if (begin === end)
        throw new Error(
          "Begin and end anchor names must differ."
        );

      if (anchors.has(begin) || anchors.has(end))
        throw new Error("Anchor names must be unique.");

      const match = findNext(pattern);

      if (!match)
        throw new Error(
          "Anchor pattern was not found in declaration order."
        );

      anchors.set(begin, match.start);
      anchors.set(end, match.end);

      cursor = match.end;
    }

    const offset = name => {
      if (!anchors.has(name))
        throw new Error(`Unknown anchor: ${name}`);

      return anchors.get(name);
    };

    return Object.freeze({
      text(from = "^", to = "$") {
        const start = offset(from);
        const end = offset(to);

        if (start > end)
          throw new Error(
            `Invalid slice: ${from} is after ${to}`
          );

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
                  fileAlgorithm "const source = await HOST_READ_IMMUTABLE_UTF8_SNAPSHOT(path);"
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

    let runtimeBaseClass (capabilities: Set<JsCapability>) : string =
        let methods =
            [ "  constructor(api) { this._api = api; }"
              if has capabilities JsCapability.Read then
                  fileAlgorithm
                      "const snapshot = this._api.js.read(path);\n    if (snapshot && snapshot.ok === false) throw new Error(snapshot.reason || snapshot.code);\n    const source = snapshot.text;"
              if has capabilities JsCapability.Glob then
                  "  async glob(pattern) {\n    const result = this._api.js.glob(pattern);\n    if (result && result.ok === false) throw new Error(result.reason || result.code);\n    return { paths: result.paths, truncated: result.truncated === true };\n  }"
              if has capabilities JsCapability.Grep then
                  "  async grep(needle, pattern = \"**/*\") {\n    const result = this._api.js.grep(needle, pattern);\n    if (result && result.ok === false) throw new Error(result.reason || result.code);\n    return { matches: result.matches, truncated: result.truncated === true };\n  }"
              if has capabilities JsCapability.Edit then
                  "  rewrite(path, newText) {\n    const result = this._api.js.edit(path, newText);\n    if (result && result.ok === false) throw new Error(result.reason || result.code);\n  }"
              if has capabilities JsCapability.Write then
                  "  write(path, newText) {\n    const result = this._api.js.write(path, newText);\n    if (result && result.ok === false) throw new Error(result.reason || result.code);\n  }"
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
        + "Recommended workflow:\n"
        + "1. Declare the minimal begin/end anchor set needed to locate edits.\n"
        + "2. Let Host resolve those positions.\n"
        + "3. Build the complete resulting file from text(...) slices plus new content.\n"
        + "4. Use indexOf / replaceAll only when anchor-and-splice is genuinely inconvenient.\n"
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
        "rewrite(path, newText) stages replacement of an existing UTF-8 file. The target\n"
        + "must exist in the transaction snapshot or the call fails FILE_NOT_FOUND.\n"
        + "newText must be a string. The call does not write immediately; it adds a\n"
        + "StagedRewrite to this program's WriteSet. You do not have to file(path) first."

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

    let filteredExamples (capabilities: Set<JsCapability>) : JsExample list =
        examples
        |> List.filter (fun example -> Set.isSubset example.Requires capabilities)

    let render (toolName: string) (capabilities: Set<JsCapability>) : string =
        let exampleBlocks =
            filteredExamples capabilities
            |> List.map (fun example -> "```js\n" + example.Source + "\n```")
            |> String.concat "\n\n"

        String.concat
            "\n\n"
            [ header
              contract toolName capabilities
              "```js\n" + publicBaseClass capabilities + "\n```"
              rules capabilities
              "Examples:\n\n" + exampleBlocks
              footer ]

/// The generated js-ROLE surface for one Attempt profile. Deterministic:
/// same capabilities → same bytes (JS-002; fast/deep identical).
type JsSurface =
    {
        ToolName: string
        RoleName: string
        Capabilities: Set<JsCapability>
        Members: JsCapabilityFragment list
        Description: string
        BaseClassSource: string
        /// Canonical examples, one per present member, in member order.
        Examples: string list
        /// member name → runtime binding key; the runtime gate checks the
        /// invoked member against this exact map (JS-004).
        RuntimeBindings: Map<string, string>
    }

module JsToolGenerator =

    let membersFor (capabilities: Set<JsCapability>) : JsCapabilityFragment list =
        capabilities
        |> Set.toList
        |> List.sortBy JsCapability.order
        |> List.choose (fun capability -> Map.tryFind capability JsFragmentRegistry.byCapability)

    let toolNameFor (roleName: string) : string = "js-" + roleName.ToLowerInvariant()

    let renderBaseClass (capabilities: Set<JsCapability>) : string =
        JsCanonicalDescription.runtimeBaseClass capabilities

    let renderPublicBaseClass (capabilities: Set<JsCapability>) : string =
        JsCanonicalDescription.publicBaseClass capabilities

    let renderDescription (roleName: string) (capabilities: Set<JsCapability>) : string =
        JsCanonicalDescription.render (toolNameFor roleName) capabilities

    let renderExamples (capabilities: Set<JsCapability>) : string list =
        JsCanonicalDescription.filteredExamples capabilities
        |> List.map (fun example -> example.Source)

    /// Deterministic projection: an Attempt profile with no filesystem
    /// capability gets no js-* surface at all (JS-001/004).
    let generate (roleName: string) (capabilities: Set<ToolPermission>) : JsSurface option =
        let jsCapabilities = JsCapability.ofToolCapabilities capabilities

        if Set.isEmpty jsCapabilities then
            None
        else
            let members = membersFor jsCapabilities

            Some
                { ToolName = toolNameFor roleName
                  RoleName = roleName
                  Capabilities = jsCapabilities
                  Members = members
                  Description = renderDescription roleName jsCapabilities
                  BaseClassSource = renderBaseClass jsCapabilities
                  Examples = renderExamples jsCapabilities
                  RuntimeBindings =
                    members
                    |> List.map (fun fragment -> fragment.MemberName, fragment.RuntimeBindingKey)
                    |> Map.ofList }

    /// Generated-name gate: a js-* tool call is accepted iff its name is the
    /// surface this profile generates; any other name fails closed (JS-001).
    let isGeneratedToolName (roleName: string) (capabilities: Set<ToolPermission>) (toolName: string) : bool =
        generate roleName capabilities
        |> Option.map (fun surface -> surface.ToolName = toolName)
        |> Option.defaultValue false

    /// Runtime member gate: a member invocation is accepted iff the member is
    /// present in this profile's surface; the returned binding key names the
    /// exact executor (JS-004 — forged calls have no binding).
    let memberBinding (roleName: string) (capabilities: Set<ToolPermission>) (memberName: string) : string option =
        generate roleName capabilities
        |> Option.bind (fun surface -> Map.tryFind memberName surface.RuntimeBindings)

/// DSL-class: Vocabulary — JS-019: stable failure codes (proposal §77/§77.1) —
/// program-foreseeable failures are typed branches with stable LLM-visible
/// codes; exceptions are only for crashes. Codes are frozen once shipped.
[<RequireQualifiedAccess>]
type JsFailure =
    | InvalidProgram
    | ProgramFailed
    | ProgramTimeout
    | ProgramResourceLimit
    | PermissionDenied of string
    | PathDenied of string
    | FileNotFound of string
    | FileAlreadyExists of string
    | FileReadFailed of string
    | InvalidUtf8 of string
    | AnchorEmptyContent
    | AnchorInvalidPattern
    | AnchorNotFound of int
    | AnchorNotUnique
    | AnchorCrossFile
    | DuplicateMutationTarget of string
    | ResultTooLarge of string option
    | InvalidReturnValue
    | FileChanged of string
    | TransactionPrepareFailed
    | TransactionCommitFailed
    | TransactionRollbackFailed
    | TransactionRecoveryRequired
    | UnknownMember

module JsFailure =

    /// Stable machine-readable code; the LLM-visible rendering is code + reason.
    let code (failure: JsFailure) : string =
        match failure with
        | JsFailure.InvalidProgram -> "INVALID_PROGRAM"
        | JsFailure.ProgramFailed -> "PROGRAM_FAILED"
        | JsFailure.ProgramTimeout -> "PROGRAM_TIMEOUT"
        | JsFailure.ProgramResourceLimit -> "PROGRAM_RESOURCE_LIMIT"
        | JsFailure.PermissionDenied _ -> "PERMISSION_DENIED"
        | JsFailure.PathDenied _ -> "PATH_DENIED"
        | JsFailure.FileNotFound _ -> "FILE_NOT_FOUND"
        | JsFailure.FileAlreadyExists _ -> "FILE_ALREADY_EXISTS"
        | JsFailure.FileReadFailed _ -> "FILE_READ_FAILED"
        | JsFailure.InvalidUtf8 _ -> "INVALID_UTF8"
        | JsFailure.AnchorEmptyContent -> "EMPTY_ANCHOR_CONTENT"
        | JsFailure.AnchorInvalidPattern -> "INVALID_ANCHOR_PATTERN"
        | JsFailure.AnchorNotFound _ -> "ANCHOR_NOT_FOUND"
        | JsFailure.AnchorNotUnique -> "ANCHOR_NOT_UNIQUE"
        | JsFailure.AnchorCrossFile -> "ANCHOR_CROSS_FILE"
        | JsFailure.DuplicateMutationTarget _ -> "DUPLICATE_MUTATION_TARGET"
        | JsFailure.ResultTooLarge _ -> "RESULT_TOO_LARGE"
        | JsFailure.InvalidReturnValue -> "INVALID_RETURN_VALUE"
        | JsFailure.FileChanged _ -> "FILE_CHANGED"
        | JsFailure.TransactionPrepareFailed -> "TRANSACTION_PREPARE_FAILED"
        | JsFailure.TransactionCommitFailed -> "TRANSACTION_COMMIT_FAILED"
        | JsFailure.TransactionRollbackFailed -> "TRANSACTION_ROLLBACK_FAILED"
        | JsFailure.TransactionRecoveryRequired -> "TRANSACTION_RECOVERY_REQUIRED"
        | JsFailure.UnknownMember -> "UNKNOWN_MEMBER"

    /// LLM-visible stable reason text (proposal §78: readable, stable, no stack noise).
    let reason (failure: JsFailure) : string =
        match failure with
        | JsFailure.InvalidProgram -> "program source is invalid JavaScript"
        | JsFailure.ProgramFailed -> "program threw; see program error payload"
        | JsFailure.ProgramTimeout -> "program exceeded its deadline"
        | JsFailure.ProgramResourceLimit -> "program exceeded a resource bound"
        | JsFailure.PermissionDenied capability -> "capability not present in this attempt: " + capability
        | JsFailure.PathDenied path -> "path outside capability boundary: " + path
        | JsFailure.FileNotFound path -> "target file does not exist: " + path
        | JsFailure.FileAlreadyExists path -> "target file already exists: " + path
        | JsFailure.FileReadFailed path -> "file read failed: " + path
        | JsFailure.InvalidUtf8 path -> "file is not strict UTF-8: " + path
        | JsFailure.AnchorEmptyContent -> "anchor content is empty"
        | JsFailure.AnchorInvalidPattern -> "anchor RegExp is invalid"
        | JsFailure.AnchorNotFound index -> "anchor did not match at declaration " + string index
        | JsFailure.AnchorNotUnique -> "anchor matches multiple locations and no occurrence was declared"
        | JsFailure.AnchorCrossFile -> "anchor declaration crosses files"
        | JsFailure.DuplicateMutationTarget path -> "the same path was mutated twice in one program: " + path
        | JsFailure.ResultTooLarge _ -> "result exceeds the output bound"
        | JsFailure.InvalidReturnValue -> "run() return value is not JSON-compatible"
        | JsFailure.FileChanged path -> "target changed since the read snapshot; no implicit retry: " + path
        | JsFailure.TransactionPrepareFailed -> "transaction prepare failed"
        | JsFailure.TransactionCommitFailed -> "transaction commit failed"
        | JsFailure.TransactionRollbackFailed -> "transaction rollback failed"
        | JsFailure.TransactionRecoveryRequired -> "durable transaction recovery is required"
        | JsFailure.UnknownMember -> "member is not part of this generated surface"

    /// JS-078.1 stable result shape for failures: { ok: false, code, reason }.
    let render (failure: JsFailure) : string =
        "{ ok: false, code: \""
        + code failure
        + "\", reason: \""
        + reason failure
        + "\" }"

/// JS-006: ordered anchor declarations — string or RegExp, with an optional
/// 1-based occurrence selector; duplicate textual occurrence resolves in
/// declaration order. `^`/`$` mean absolute file start/end (not line anchors).
[<RequireQualifiedAccess>]
type AnchorSpec =
    | Exact of string
    | Regex of string

type AnchorDeclaration =
    {
        Spec: AnchorSpec
        /// 1-based occurrence; None = the anchor must be unique (JS-006).
        Occurrence: int option
    }

module AnchorRules =

    /// The Domain-owned refusal class: an empty anchor is refused without
    /// touching file content. The other four classes (not-unique-without-
    /// occurrence / not-found / invalid-regex / cross-file) need the sandbox
    /// matcher or the transaction layer and are enforced there (JS-006/019).
    let validateDeclaration (declaration: AnchorDeclaration) : Result<unit, JsFailure> =
        match declaration.Spec with
        | AnchorSpec.Exact text when System.String.IsNullOrEmpty text -> Error JsFailure.AnchorEmptyContent
        | AnchorSpec.Regex pattern when System.String.IsNullOrEmpty pattern -> Error JsFailure.AnchorEmptyContent
        | AnchorSpec.Exact _
        | AnchorSpec.Regex _ -> Ok()

    /// Occurrence selector must be positive when declared (JS-006); a
    /// non-positive selector is an invalid declaration.
    let validateOccurrence (declaration: AnchorDeclaration) : Result<unit, JsFailure> =
        match declaration.Occurrence with
        | Some n when n < 1 -> Error JsFailure.AnchorInvalidPattern
        | Some _
        | None -> Ok()

/// DSL-class: DurableFact — JS-012/JS-013/JS-015: one staged mutation in a
/// js-* program. `Rewrite` edits an existing file; `Create` writes a missing
/// one (JS-008/009). The set of all mutations is the WriteSet; it commits
/// all-or-nothing or not at all.
[<RequireQualifiedAccess>]
type JsStagedMutation =
    | Rewrite of path: string * originalText: string * newText: string
    | Create of path: string * text: string

module JsStagedMutation =

    let path (mutation: JsStagedMutation) : string =
        match mutation with
        | JsStagedMutation.Rewrite(path, _, _) -> path
        | JsStagedMutation.Create(path, _) -> path

/// DSL-class: Decision — JS-012/013/014: the pure transaction rules. The
/// filesystem facts (existence, current content) are injected so the decision
/// stays deterministic and testable without Host I/O; the commit/rollback
/// side effects are the adapter's job, never this module's.
module JsTransaction =

    /// JS-026 same-path-once: one program may mutate each path exactly once.
    let validateSingleIntent (mutations: JsStagedMutation list) : Result<JsStagedMutation list, JsFailure> =
        let duplicates =
            mutations
            |> List.countBy JsStagedMutation.path
            |> List.tryFind (fun (_, count) -> count > 1)

        match duplicates with
        | Some(path, _) -> Error(JsFailure.DuplicateMutationTarget path)
        | None -> Ok mutations

    /// JS-008/009: rewrite targets must exist, create targets must not.
    let validateTargets (exists: string -> bool) (mutations: JsStagedMutation list) : Result<unit, JsFailure> =
        mutations
        |> List.tryPick (fun mutation ->
            match mutation with
            | JsStagedMutation.Rewrite(path, _, _) when not (exists path) -> Some(JsFailure.FileNotFound path)
            | JsStagedMutation.Create(path, _) when exists path -> Some(JsFailure.FileAlreadyExists path)
            | _ -> None)
        |> function
            | Some failure -> Error failure
            | None -> Ok()

    /// JS-014: a rewrite whose original text no longer matches the current
    /// file content is a conflict; no implicit retry. Create targets have no
    /// freshness constraint (they must be absent — enforced by validateTargets).
    let validateFreshness
        (readCurrent: string -> string option)
        (mutations: JsStagedMutation list)
        : Result<unit, JsFailure> =
        mutations
        |> List.tryPick (fun mutation ->
            match mutation with
            | JsStagedMutation.Rewrite(path, originalText, _) ->
                if readCurrent path = Some originalText then
                    None
                else
                    Some(JsFailure.FileChanged path)
            | JsStagedMutation.Create _ -> None)
        |> function
            | Some failure -> Error failure
            | None -> Ok()

    /// JS-013: full preflight — every rule must pass before any file effect.
    let preflight
        (exists: string -> bool)
        (readCurrent: string -> string option)
        (mutations: JsStagedMutation list)
        : Result<unit, JsFailure> =
        validateSingleIntent mutations
        |> Result.bind (fun validated ->
            validateTargets exists validated
            |> Result.bind (fun () -> validateFreshness readCurrent validated))

    /// JS-013: the commit plan — one write per mutation, in declaration order.
    /// The adapter applies this plan; a failure anywhere rolls the whole plan
    /// back (rollback restores each original text).
    let commitPlan (mutations: JsStagedMutation list) : (string * string) list =
        mutations
        |> List.map (fun mutation ->
            match mutation with
            | JsStagedMutation.Rewrite(path, _, newText) -> path, newText
            | JsStagedMutation.Create(path, text) -> path, text)

    /// JS-015: rollback plan — restore every original text (rewrites only;
    /// creates are removed by the adapter). Order is reversed so a partial
    /// commit unwinds last-write-first.
    let rollbackPlan (mutations: JsStagedMutation list) : (string * string option) list =
        mutations
        |> List.rev
        |> List.map (fun mutation ->
            match mutation with
            | JsStagedMutation.Rewrite(path, originalText, _) -> path, Some originalText
            | JsStagedMutation.Create(path, _) -> path, None)

/// DSL-class: DurableFact — JS-012/015: identity of one js-* transaction.
type JsTransactionId = private JsTransactionId of string

module JsTransactionId =

    let create (value: string) = JsTransactionId value
    let value (JsTransactionId v) = v

    let generate () =
        JsTransactionId(System.Guid.NewGuid().ToString("N"))

/// DSL-class: DurableFact — JS-015: one mutation as persisted in a prepared
/// transaction, sufficient to undo it after a crash (original text for
/// rewrites, absence for creates).
type JsDurableMutation =
    {
        Path: string
        /// Some = rewrite (rollback restores this text); None = create.
        OriginalText: string option
        NewText: string
    }

/// DSL-class: DurableFact — JS-012: the durable prepare fact. Written to the
/// unified EventStore BEFORE any filesystem effect; a committed transaction
/// is a pair (Prepared, Committed) on the same stream.
type JsTransactionPrepared =
    { TransactionId: JsTransactionId
      WorkspaceRoot: string
      Mutations: JsDurableMutation list }

/// DSL-class: DurableFact — JS-012: the durable commit fact. Its presence
/// after a Prepared fact is what makes the transaction committed.
type JsTransactionCommitted = { TransactionId: JsTransactionId }

module JsTransactionFacts =

    /// Durable mutations from a staged set (JS-012).
    let ofStaged (mutations: JsStagedMutation list) : JsDurableMutation list =
        mutations
        |> List.map (fun mutation ->
            match mutation with
            | JsStagedMutation.Rewrite(path, originalText, newText) ->
                { Path = path
                  OriginalText = Some originalText
                  NewText = newText }
            | JsStagedMutation.Create(path, text) ->
                { Path = path
                  OriginalText = None
                  NewText = text })
