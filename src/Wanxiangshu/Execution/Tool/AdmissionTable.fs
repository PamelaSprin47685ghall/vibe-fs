namespace Wanxiangshu.Execution.Tool

open System
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Repository.Knowledge.Casebook

module ToolAdmissionTable =

    let bookkeeper: ToolAdmission =
        ToolAdmission.PrivateAttachment(fun ctx ->
            not (String.IsNullOrWhiteSpace ctx.SessionId)
            && BookkeeperRuntime.isAttached ctx.SessionId)

    let private staticAdmissions () : (string * ToolAdmission) list =
        [ "join", ToolAdmissions.join
          "horizon", ToolAdmissions.horizon
          "fission", ToolAdmissions.fission
          "review", ToolAdmissions.review
          "deliver", ToolAdmissions.deliver
          "bash-honeypot", ToolAdmissions.bashHoneypot
          "assume", ToolAdmissions.assume
          "fetch", ToolAdmissions.fetch
          "fork", ToolAdmissions.fork
          "resume", ToolAdmissions.resume
          "commission", ToolAdmissions.commission
          "todowrite-for-bookkeeper", bookkeeper ]

    let private tryAdmissionFor (specName: string) : ToolAdmission option =
        match staticAdmissions () |> List.tryFind (fun (name, _) -> name = specName) with
        | Some(_, admission) -> Some admission

        | None when specName.StartsWith "todowrite-for-" && specName <> "todowrite-for-bookkeeper" ->
            Some(ToolAdmissions.programmingFor (specName.Substring 14))
        | None -> None

    let private probeContext (sessionId: string) : HostToolContext =
        { SessionId = sessionId
          Agent = None
          ToolCallId = None
          ProviderRunId = None
          PromptText = None
          AttachAbort = fun _ -> id }

    /// capability-enforcement-006: the authority the execute gate resolves for a tool, so a
    /// consumer can tell an office tool from an internal leaf without guessing
    /// from the tool name.
    let tryAdmission (specName: string) : ToolAdmission option = tryAdmissionFor specName

    /// capability-enforcement-006: the internal-leaf decision for a session that holds no public
    /// office profile at all. An office tool is never admitted this way.
    let privateAttachmentAdmits (specName: string) (sessionId: string) : bool =
        match tryAdmissionFor specName with
        | Some(ToolAdmission.PrivateAttachment predicate) -> predicate (probeContext sessionId)
        | Some(ToolAdmission.OfficeRole _)
        | None -> false

    /// AGENT-007 role gate, delegates to owner-defined tool admissions.
    /// sessionId is the tool call's Host session.
    let rolePredicate (specName: string) (sessionId: string) : Role -> bool =
        // capability-enforcement-006: an internal leaf tool is admitted by attachment, never by a
        // public office, so no public Role may ever see it on this surface.
        match tryAdmissionFor specName with
        | Some(ToolAdmission.OfficeRole predicate) -> predicate (probeContext sessionId)
        | Some(ToolAdmission.PrivateAttachment _)
        | None -> fun _ -> false
