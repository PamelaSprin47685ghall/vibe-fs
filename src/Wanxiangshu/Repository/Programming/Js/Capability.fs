namespace Wanxiangshu.Repository.Programming.Js

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation

/// DSL-class: Vocabulary — filesystem primitive capabilities projected into
/// the js-ROLE SDK. Pure projection of ToolPermission; never decided here
/// (JS-001: no second permission matrix).
[<RequireQualifiedAccess>]
type JsCapability =
    | Read
    | Write

    | Glob
    /// `rg(argv)`: one ripgrep argv, always invoked as `rg --json`.
    | Grep
    | Move
    | Remove
    /// Host-owned long-running command engineering (`this.call`).
    | Exec
    /// Delegation primitives: create a child work (`fork`) and wait for the
    /// caller's children (`join`).
    | Fork
    | Resume
    | Commission
    | DevOps
    | Join
    /// Same-identity multi-lane execution (`this.fission`).
    | Fission
    /// Who remains at the horizon (`this.horizon`): a data-only roster of the
    /// session's horizon-visible participants — never a prose rendering.
    | Horizon
    /// Consultation with this session's own advisor seat (`this.advisor`): the
    /// seat answers in prose and the member hands that prose back as data —
    /// never as a rendering.
    | Advisor
    /// The one question a program may put to the user (`this.ask`).
    | Ask
    /// A lifecycle capability, not a ToolPermission projection: `deliver`
    /// ends a program by handing over its view. Like `park`, every generated
    /// surface carries it unconditionally.
    | Deliver
    | Park

module JsCapability =

    /// Stateless `wanxiangli/programming` surface (CUT-25). Office→JS projection,
    /// JsPlan expansion and admitsSurface live in the library; this module maps
    /// labels onto the host `JsCapability` DU for Description/ToolsBindings.
    type private ProgrammingLibrary =
        abstract projectPermission: label: string -> obj
        abstract projectPermissions: labels: string array -> string array
        abstract admitsSurface: labels: string array -> bool
        abstract toolNameFor: role: string -> string
        abstract membersFor: jsCapabilities: string array * roleName: string -> string array
        abstract projectRole: role: string * officePermissions: string array -> obj

    [<Import("Programming", "wanxiangli/programming")>]
    let private programmingFactory: unit -> ProgrammingLibrary = jsNative

    let private law: ProgrammingLibrary = programmingFactory ()

    /// Host label ↔ DU (the one table; RuntimeSurface reads it too).
    let ofLabel (label: string) : JsCapability option =
        match label with
        | "Read" -> Some JsCapability.Read
        | "Write" -> Some JsCapability.Write
        | "Glob" -> Some JsCapability.Glob
        | "Grep" -> Some JsCapability.Grep
        | "Move" -> Some JsCapability.Move
        | "Remove" -> Some JsCapability.Remove
        | "Exec" -> Some JsCapability.Exec
        | "Fork" -> Some JsCapability.Fork
        | "Resume" -> Some JsCapability.Resume
        | "Commission" -> Some JsCapability.Commission
        | "DevOps" -> Some JsCapability.DevOps
        | "Join" -> Some JsCapability.Join
        | "Fission" -> Some JsCapability.Fission
        | "Horizon" -> Some JsCapability.Horizon
        | "Advisor" -> Some JsCapability.Advisor
        | "Ask" -> Some JsCapability.Ask
        | "Deliver" -> Some JsCapability.Deliver
        | "Park" -> Some JsCapability.Park
        | _ -> None

    let labelOf (capability: JsCapability) : string =
        match capability with
        | JsCapability.Read -> "Read"
        | JsCapability.Write -> "Write"
        | JsCapability.Glob -> "Glob"
        | JsCapability.Grep -> "Grep"
        | JsCapability.Move -> "Move"
        | JsCapability.Remove -> "Remove"
        | JsCapability.Exec -> "Exec"
        | JsCapability.Fork -> "Fork"
        | JsCapability.Resume -> "Resume"
        | JsCapability.Commission -> "Commission"
        | JsCapability.DevOps -> "DevOps"
        | JsCapability.Join -> "Join"
        | JsCapability.Fission -> "Fission"
        | JsCapability.Horizon -> "Horizon"
        | JsCapability.Advisor -> "Advisor"
        | JsCapability.Ask -> "Ask"
        | JsCapability.Deliver -> "Deliver"
        | JsCapability.Park -> "Park"

    /// The only ToolPermission → JsCapability mapping (library-owned).
    let ofToolPermission (permission: ToolPermission) : JsCapability option =
        let projected = law.projectPermission (OfficeCapability.permissionLabel permission)

        if isNull projected then
            None
        else
            ofLabel (string projected)

    let ofToolCapabilities (capabilities: Set<ToolPermission>) : Set<JsCapability> =
        let labels =
            capabilities
            |> Set.toList
            |> List.map OfficeCapability.permissionLabel
            |> List.toArray

        law.projectPermissions labels |> Array.choose ofLabel |> Set.ofArray

    /// True iff the office permission set projects at least one programming-surface
    /// member. Schema, execution admission and the generator all ask this one question.
    let admitsProgrammingSurface (capabilities: Set<ToolPermission>) : bool =
        let labels =
            capabilities
            |> Set.toList
            |> List.map OfficeCapability.permissionLabel
            |> List.toArray

        law.admitsSurface labels

    let toolNameFor (roleName: string) : string = law.toolNameFor roleName

    /// Library member directory for a JS capability set, in canonical order.
    /// Lifecycle members (deliver, park) are never part of it.
    let memberNamesFor (capabilities: Set<JsCapability>) (roleName: string) : string array =
        let labels = capabilities |> Set.toArray |> Array.map labelOf
        law.membersFor (labels, roleName)

    /// Library projectRole: Some (toolName, jsCapabilities, memberNames) or None.
    let projectRole
        (roleName: string)
        (capabilities: Set<ToolPermission>)
        : (string * Set<JsCapability> * string array) option =
        let labels =
            capabilities
            |> Set.toList
            |> List.map OfficeCapability.permissionLabel
            |> List.toArray

        let raw = law.projectRole (roleName, labels)

        if isNull raw then
            None
        else
            let toolName = string raw?toolName
            let caps: string array = unbox raw?capabilities
            let members: string array = unbox raw?members
            let jsCaps = caps |> Array.choose ofLabel |> Set.ofArray
            Some(toolName, jsCaps, members)

/// One capability's SDK projection: the single source for the member name a
/// capability brings into the base class. The member's signature and prose
/// live in the localized `tool/js-program/base-class` resources, so the base
/// class carries one document truth per language.
type JsCapabilityFragment =
    { Capability: JsCapability
      MemberName: string }

/// Fixed registry of all fragments: the only place a js-* member can be born.
module JsFragmentRegistry =

    let write: JsCapabilityFragment =
        { Capability = JsCapability.Write
          MemberName = "write" }

    let rm: JsCapabilityFragment =
        { Capability = JsCapability.Remove
          MemberName = "rm" }

    let mv: JsCapabilityFragment =
        { Capability = JsCapability.Move
          MemberName = "mv" }

    let cat: JsCapabilityFragment =
        { Capability = JsCapability.Read
          MemberName = "cat" }

    let ls: JsCapabilityFragment =
        { Capability = JsCapability.Glob
          MemberName = "ls" }

    let rg: JsCapabilityFragment =
        { Capability = JsCapability.Grep
          MemberName = "rg" }

    let stat: JsCapabilityFragment =
        { Capability = JsCapability.Read
          MemberName = "stat" }

    let mkdir: JsCapabilityFragment =
        { Capability = JsCapability.Write
          MemberName = "mkdir" }

    let whereAll: JsCapabilityFragment =
        { Capability = JsCapability.Read
          MemberName = "whereAll" }

    let where: JsCapabilityFragment =
        { Capability = JsCapability.Read
          MemberName = "where" }

    let call: JsCapabilityFragment =
        { Capability = JsCapability.Exec
          MemberName = "call" }

    let fork: JsCapabilityFragment =
        { Capability = JsCapability.Fork
          MemberName = "fork" }

    let resume: JsCapabilityFragment =
        { Capability = JsCapability.Resume
          MemberName = "resume" }

    let commission: JsCapabilityFragment =
        { Capability = JsCapability.Commission
          MemberName = "commission" }

    let devops: JsCapabilityFragment =
        { Capability = JsCapability.DevOps
          MemberName = "devops" }

    let dag: JsCapabilityFragment =
        { Capability = JsCapability.Fork
          MemberName = "dag" }

    /// The capabilities a member needs beyond its own. `dag` is pure sugar
    /// over fork waves plus one join, so it needs Join as well as Fork: a
    /// surface without Join has no join to compile the waves into, and a
    /// member listed without a method body is a lie.
    let additionalCapabilities (fragment: JsCapabilityFragment) : JsCapability list =
        if fragment = dag then [ JsCapability.Join ] else []

    let join: JsCapabilityFragment =
        { Capability = JsCapability.Join
          MemberName = "join" }

    let fission: JsCapabilityFragment =
        { Capability = JsCapability.Fission
          MemberName = "fission" }

    let horizon: JsCapabilityFragment =
        { Capability = JsCapability.Horizon
          MemberName = "horizon" }

    let advisor: JsCapabilityFragment =
        { Capability = JsCapability.Advisor
          MemberName = "advisor" }

    let ask: JsCapabilityFragment =
        { Capability = JsCapability.Ask
          MemberName = "ask" }

    /// park fragment — the pause/resume mechanism (PLAN S1 B.7/B.17). Unlike
    /// the filesystem members this is not a ToolPermission projection: it is a
    /// lifecycle capability every js-* surface carries.
    let park: JsCapabilityFragment =
        { Capability = JsCapability.Park
          MemberName = "park" }

    /// deliver fragment — the terminal view handoff. A lifecycle member like
    /// park: not the projection of a ToolPermission, carried by every
    /// generated surface, and never part of a role's capability members.
    let deliver: JsCapabilityFragment =
        { Capability = JsCapability.Deliver
          MemberName = "deliver" }

    /// Canonical member order inside capability families. Filtering this list
    /// is the deterministic projection; never rebuild member order elsewhere.
    let all: JsCapabilityFragment list =
        [ write
          rm
          mv
          cat
          ls
          rg
          stat
          mkdir
          whereAll
          where
          call
          fork
          resume
          commission
          devops
          dag
          join
          fission
          horizon
          advisor
          ask
          deliver
          park ]

    let byCapability: Map<JsCapability, JsCapabilityFragment list> =
        all |> List.groupBy _.Capability |> Map.ofList

    /// Whether a member may be projected into a surface: its own capability
    /// plus every extra capability it needs. The base class and the member
    /// list read this one rule, so a member never appears without a body.
    let isProjected (capabilities: Set<JsCapability>) (fragment: JsCapabilityFragment) : bool =
        Set.contains fragment.Capability capabilities
        && additionalCapabilities fragment
           |> List.forall (fun capability -> Set.contains capability capabilities)
