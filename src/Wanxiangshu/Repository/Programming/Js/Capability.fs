namespace Wanxiangshu.Repository.Programming.Js

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
    | Park

module JsCapability =

    /// The only ToolPermission → JsCapability mapping. A permission without a
    /// js-* projection (edit, pty, …) produces no member (JS-004 four-layer
    /// exactness). Commission stays on the Fork path: Orchestrator's host
    /// commission verb is the same executeReceipt the fork member already runs.
    let ofToolPermission (permission: ToolPermission) : JsCapability option =
        match permission with
        | ToolPermission.Read -> Some JsCapability.Read
        | ToolPermission.Write -> Some JsCapability.Write
        | ToolPermission.Glob -> Some JsCapability.Glob
        | ToolPermission.Grep -> Some JsCapability.Grep
        | ToolPermission.Move -> Some JsCapability.Move
        | ToolPermission.Remove -> Some JsCapability.Remove
        | ToolPermission.Exec -> Some JsCapability.Exec
        | ToolPermission.Fork -> Some JsCapability.Fork
        | ToolPermission.Resume -> Some JsCapability.Resume
        | ToolPermission.DevOpsConsultation -> Some JsCapability.DevOps
        | ToolPermission.Join -> Some JsCapability.Join
        | ToolPermission.Fission -> Some JsCapability.Fission
        | ToolPermission.Horizon -> Some JsCapability.Horizon
        | ToolPermission.Advisor -> Some JsCapability.Advisor
        | ToolPermission.Ask -> Some JsCapability.Ask
        | _ -> None

    let ofToolCapabilities (capabilities: Set<ToolPermission>) : Set<JsCapability> =
        // JsPlan is the office grant for the plan programming surface. It is
        // not itself a member; it expands into the same Read/Write/Ask set the
        // plan base class has always carried, so the plan surface is born from
        // the one projection instead of a private capability list.
        let expanded =
            if Set.contains ToolPermission.JsPlan capabilities then
                [ JsCapability.Read; JsCapability.Write; JsCapability.Ask ]
            else
                []

        capabilities
        |> Set.toList
        |> List.choose ofToolPermission
        |> List.append expanded
        |> Set.ofList

/// One capability's SDK projection: the single source for the member name a
/// capability brings into the base class. The member's signature and prose
/// live in the localized `tool/js-program/base-class` resources, so the base
/// class carries one document truth per language.
type JsCapabilityFragment =
    {
        Capability: JsCapability
        MemberName: string
    }

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
        if fragment = dag then
            [ JsCapability.Join ]
        else
            []

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
