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

    /// Fixed canonical member order for generated surfaces (JS-002
    /// deterministic generation: Read, Glob, Grep, Edit, Write).
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
    { Capability: JsCapability
      MemberName: string
      Description: string
      CanonicalExample: string
      RuntimeBindingKey: string }

/// Fixed registry of all fragments: the only place a js-* member can be born.
module JsFragmentRegistry =

    let read: JsCapabilityFragment =
        { Capability = JsCapability.Read
          MemberName = "file"
          Description =
            "file(path) — read a strict-UTF-8 file into an immutable FileView. "
            + "Returns { path, text, byteCount }; refuses non-UTF-8 as FILE_NOT_UTF8."
          CanonicalExample = "const view = api.file('src/main.fs'); view.text"
          RuntimeBindingKey = "js.read" }

    let glob: JsCapabilityFragment =
        { Capability = JsCapability.Glob
          MemberName = "glob"
          Description =
            "glob(pattern) — bounded, deterministically ordered path enumeration. "
            + "Returns { paths: string[] }; capability-invisible paths never appear."
          CanonicalExample = "const paths = api.glob('src/**/*.fs').paths"
          RuntimeBindingKey = "js.glob" }

    let grep: JsCapabilityFragment =
        { Capability = JsCapability.Grep
          MemberName = "grep"
          Description =
            "grep(regexp, pattern) — search files with a RegExp (Read + Glob derived). "
            + "Returns { matches: [{ path, index, text }] }."
          CanonicalExample = "const hits = api.grep(/TODO/, 'src/**/*.fs').matches"
          RuntimeBindingKey = "js.grep" }

    let rewrite: JsCapabilityFragment =
        { Capability = JsCapability.Edit
          MemberName = "rewrite"
          Description =
            "rewrite(path, { find, replace, occurrence }) — rewrite an existing file via "
            + "ordered string/RegExp anchors. Staged; commits with the program (all-or-nothing)."
          CanonicalExample = "api.rewrite('src/main.fs', { find: 'old', replace: 'new' })"
          RuntimeBindingKey = "js.edit" }

    let write: JsCapabilityFragment =
        { Capability = JsCapability.Write
          MemberName = "write"
          Description =
            "write(path, text) — create a missing file with strict-UTF-8 text. "
            + "Staged; commits with the program. Fails FILE_EXISTS when the target exists."
          CanonicalExample = "api.write('src/new.fs', 'module New')"
          RuntimeBindingKey = "js.write" }

    /// Fixed canonical fragment list; surface order follows JsCapability.order.
    let all: JsCapabilityFragment list = [ read; glob; grep; rewrite; write ]

    let byCapability: Map<JsCapability, JsCapabilityFragment> =
        all |> List.map (fun fragment -> fragment.Capability, fragment) |> Map.ofList

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
        |> List.map (fun capability -> Map.find capability JsFragmentRegistry.byCapability)

    let toolNameFor (roleName: string) : string = "js-" + roleName.ToLowerInvariant()

    let renderBaseClass (members: JsCapabilityFragment list) : string =
        let memberLines =
            members
            |> List.map (fun fragment ->
                $"    {fragment.MemberName}(...args) {{ return this._api.{fragment.RuntimeBindingKey}(...args); }}")

        String.concat
            "\n"
            ("class JsProgram {" :: "  constructor(api) { this._api = api; }" :: memberLines
             @ [ "}" ])

    let renderDescription (roleName: string) (members: JsCapabilityFragment list) : string =
        let methodList =
            members |> List.map (fun fragment -> fragment.MemberName) |> String.concat ", "

        "Capability-projected JavaScript SDK for "
        + roleName
        + ". "
        + "Program filesystem work as one all-or-nothing JS program; parallel calls are safe. "
        + "Available methods: "
        + methodList
        + ". "
        + "Prefer this tool over builtin read/edit/write/glob/grep."

    let renderExamples (members: JsCapabilityFragment list) : string list =
        members |> List.map (fun fragment -> fragment.CanonicalExample)

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
                  Description = renderDescription roleName members
                  BaseClassSource = renderBaseClass members
                  Examples = renderExamples members
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
