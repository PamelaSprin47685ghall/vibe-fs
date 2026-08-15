namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation.Identity

/// CRASH-018: explicit, user-visible session resume. Nothing in this module is
/// reachable from plugin load or ordinary turns. `/continue` only discovers and
/// process-locally re-enlists surviving child sessions; it never repairs the old
/// tool call, appends recovery facts, or sends a prompt on the user's behalf.
[<RequireQualifiedAccess>]
module ExplicitSessionResume =

    [<Emit("$0['continue'] = $1")>]
    let private setContinueCommand (_commands: obj) (_command: obj) : unit = jsNative

    [<Emit("$0?.[$1] == null ? '' : String($0[$1])")>]
    let private fieldText (_value: obj) (_field: string) : string = jsNative

    [<Literal>]
    let CommandName = "continue"

    let private commandTemplate =
        "The user explicitly requested session continuation after an OpenCode/Wanxiangshu process restart. "
        + "Use the Wanxiangshu restart briefing attached to this command. Do not assume the interrupted tool completed."

    let private commandsOf (config: obj) =
        if isNull config?command then createObj [] else config?command

    let registerCommand (config: obj) : unit =
        if not (isNull config) then
            let commands = commandsOf config

            setContinueCommand
                commands
                (createObj
                    [ "template" ==> commandTemplate
                      "description" ==> "Resume this session explicitly after a Wanxiangshu/OpenCode restart" ])

            config?command <- commands

    let private lifecycleText =
        function
        | HandleLifecycle.Active -> "active-at-crash"
        | HandleLifecycle.CompletedAwaitingJoin _ -> "completed-awaiting-join-at-crash"
        | HandleLifecycle.Retired -> "retired-before-crash"
        | HandleLifecycle.Abandoned _ -> "abandoned"

    let private roleText (record: HandleRecord) = sprintf "%A" record.CanonicalRole

    let private isParentVisible (record: HandleRecord) =
        match record.Ownership with
        | HandleOwnership.DurableParentHandle -> true
        | HandleOwnership.HostOwnedHidden -> false

    let private candidateRecords (journal: AgentJournal) (parentId: SessionId) =
        AgentJournal.handleProjection journal parentId
        |> HandleProjection.linkedChildren
        |> List.filter isParentVisible

    let private renderLine (prefix: string) (record: HandleRecord) (detail: string) =
        sprintf
            "- %s byname=%s session_id=%s role=%s agent=%s prior_handle_state=%s%s"
            prefix
            record.Byname
            (SessionId.value record.ChildSessionId)
            (roleText record)
            record.TargetAgent
            (lifecycleText record.Lifecycle)
            detail

    let private textPart text = createObj [ "type" ==> "text"; "text" ==> text ]

    let private existingParts (output: obj) : obj array =
        if isNull output || isNull output?parts then [||] else unbox<obj array> output?parts

    let private appendVisiblePart (output: obj) (text: string) =
        if not (isNull output) then
            output?parts <- Array.append (existingParts output) [| textPart text |]

    let private commandName (input: obj) =
        fieldText input "command" |> fun value -> value.Trim().TrimStart('/')

    let private sessionText (input: obj) = fieldText input "sessionID"
    let private argumentTextRaw (input: obj) = fieldText input "arguments"

    type AdoptExistingChild = SessionId -> HandleRecord -> Result<unit, string>

    type private ResumeObservation =
        | Surviving of string
        | Unavailable of string

    let private sanitizeReason (reason: string) = reason.Replace("\n", " ").Replace("\r", " ")

    let private unavailable prefix reason record =
        Unavailable(renderLine prefix record (" reason=" + sanitizeReason reason))

    let private adoptObservation parentId adopt record =
        match adopt parentId record with
        | Ok() -> Surviving(renderLine "surviving" record "")
        | Error error -> unavailable "not-adopted" error record

    let private probePhysical
        (parentId: SessionId)
        (snapshot: ISessionSnapshotPort)
        (adopt: AdoptExistingChild)
        (record: HandleRecord)
        : Task<ResumeObservation> =
        task {
            match! snapshot.GetMessages record.ChildSessionId with
            | Error error -> return unavailable "unavailable" error record
            | Ok _ -> return adoptObservation parentId adopt record
        }

    let private inspectRecord
        (parentId: SessionId)
        (snapshot: ISessionSnapshotPort)
        (adopt: AdoptExistingChild)
        (record: HandleRecord)
        : Task<ResumeObservation> =
        match record.Lifecycle with
        | HandleLifecycle.Abandoned _ -> Task.FromResult(unavailable "not-resumable" "abandoned" record)
        | HandleLifecycle.Retired -> Task.FromResult(unavailable "not-resumable" "retired-tombstone" record)
        | HandleLifecycle.Active
        | HandleLifecycle.CompletedAwaitingJoin _ -> probePhysical parentId snapshot adopt record

    let private inspectAll parentId snapshot adopt records : Task<ResumeObservation array> =
        let rec loop remaining acc =
            task {
                match remaining with
                | [] -> return acc |> List.rev |> List.toArray
                | record :: tail ->
                    let! observation = inspectRecord parentId snapshot adopt record
                    return! loop tail (observation :: acc)
            }

        loop records []

    let private unverifiedObservations records =
        records
        |> List.map (unavailable "unverified" "snapshot-port-unavailable")
        |> List.toArray

    let private observations
        (journal: AgentJournal option)
        (snapshot: ISessionSnapshotPort option)
        (adopt: AdoptExistingChild)
        (parentId: SessionId)
        : Task<ResumeObservation array> =
        match journal, snapshot with
        | None, _ ->
            Task.FromResult(
                [| Unavailable("- durable journal unavailable; no child sessions were re-enlisted") |]
            )
        | Some durable, None ->
            candidateRecords durable parentId |> unverifiedObservations |> Task.FromResult
        | Some durable, Some snapshotPort ->
            candidateRecords durable parentId |> inspectAll parentId snapshotPort adopt

    let private survivingLine =
        function
        | Surviving line -> Some line
        | Unavailable _ -> None

    let private unavailableLine =
        function
        | Unavailable line -> Some line
        | Surviving _ -> None

    let private renderLines (selector: ResumeObservation -> string option) (items: ResumeObservation array) =
        let lines: string array = items |> Array.choose selector
        if lines.Length = 0 then "- none" else String.Join("\n", lines)

    let private continueArguments (arguments: string) =
        if String.IsNullOrWhiteSpace arguments then
            ""
        else
            "\nUser /continue arguments: " + arguments.Trim()

    let private renderBriefing (items: ResumeObservation array) arguments =
        let survivingText = renderLines survivingLine items
        let unavailableText = renderLines unavailableLine items

        String.concat
            "\n"
            [ "[wanxiangshu restart briefing]"
              "The user explicitly invoked /continue. OpenCode/Wanxiangshu has just restarted."
              "The tool invocation that was in progress before the restart remains interrupted/failed in visible history. Do not infer that it completed, do not hide it, and do not manufacture a terminal result for it."
              "Surviving sub sessions re-enlisted process-locally for OPTIONAL reuse:"
              survivingText
              "Durable children that were not re-enlisted:"
              unavailableText
              "If useful, choose a surviving sub session explicitly with the normal reuse path (for example fork with the existing byname and a new charge). Reuse is a new action; the old broken tool stays broken." ]
        + continueArguments arguments

    let private resumeSession journal snapshot adopt parentId arguments output : Task<unit> =
        task {
            let! items = observations journal snapshot adopt parentId
            appendVisiblePart output (renderBriefing items arguments)
        }

    let private runContinue journal snapshot adopt input output : Task<unit> =
        let session = sessionText input

        if String.IsNullOrWhiteSpace session then
            appendVisiblePart
                output
                "[wanxiangshu restart briefing]\nThe user explicitly invoked /continue, but no session id was supplied. Nothing was resumed. The previous interrupted tool remains failed."

            Task.FromResult(())
        else
            resumeSession journal snapshot adopt (SessionId.create session) (argumentTextRaw input) output

    let before
        (journal: AgentJournal option)
        (snapshot: ISessionSnapshotPort option)
        (adopt: AdoptExistingChild)
        (input: obj)
        (output: obj)
        : Task<unit> =
        if String.Equals(commandName input, CommandName, StringComparison.Ordinal) then
            runContinue journal snapshot adopt input output
        else
            Task.FromResult(())
