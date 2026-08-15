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

    [<Fable.Core.Emit("$0['continue'] = $1")>]
    let private setContinueCommand (_commands: obj) (_command: obj) : unit = jsNative

    [<Literal>]
    let CommandName = "continue"

    let private commandTemplate =
        "The user explicitly requested session continuation after an OpenCode/Wanxiangshu process restart. "
        + "Use the Wanxiangshu restart briefing attached to this command. Do not assume the interrupted tool completed."

    let registerCommand (config: obj) : unit =
        if not (isNull config) then
            let commands =
                if isNull config?command then createObj [] else config?command

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

    let private candidateRecords (journal: AgentJournal) (parentId: SessionId) =
        AgentJournal.handleProjection journal parentId
        |> HandleProjection.linkedChildren
        |> List.filter (fun record ->
            match record.Ownership with
            | HandleOwnership.DurableParentHandle -> true
            | HandleOwnership.HostOwnedHidden -> false)

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

    let private appendVisiblePart (output: obj) (text: string) =
        if not (isNull output) then
            let existing: obj array =
                if isNull output?parts then [||] else unbox<obj array> output?parts

            output?parts <- Array.append existing [| textPart text |]

    let private commandName (input: obj) =
        if isNull input || isNull input?command then
            ""
        else
            string input?command |> fun value -> value.Trim().TrimStart('/')

    type AdoptExistingChild = SessionId -> HandleRecord -> Result<unit, string>

    let before
        (journal: AgentJournal option)
        (snapshot: ISessionSnapshotPort option)
        (adopt: AdoptExistingChild)
        (input: obj)
        (output: obj)
        : Task<unit> =
        task {
            if not (String.Equals(commandName input, CommandName, StringComparison.Ordinal)) then
                return ()
            else
                let sessionText =
                    if isNull input || isNull input?sessionID then "" else string input?sessionID

                let arguments =
                    if isNull input || isNull input?arguments then "" else string input?arguments

                if String.IsNullOrWhiteSpace sessionText then
                    appendVisiblePart
                        output
                        "[wanxiangshu restart briefing]\nThe user explicitly invoked /continue, but no session id was supplied. Nothing was resumed. The previous interrupted tool remains failed."
                else
                    let parentId = SessionId.create sessionText
                    let surviving = ResizeArray<string>()
                    let unavailable = ResizeArray<string>()

                    match journal, snapshot with
                    | None, _ ->
                        unavailable.Add("- durable journal unavailable; no child sessions were re-enlisted")
                    | Some durable, None ->
                        for record in candidateRecords durable parentId do
                            unavailable.Add(renderLine "unverified" record " reason=snapshot-port-unavailable")
                    | Some durable, Some snapshotPort ->
                        for record in candidateRecords durable parentId do
                            match record.Lifecycle with
                            | HandleLifecycle.Abandoned _ ->
                                unavailable.Add(renderLine "not-resumable" record " reason=abandoned")
                            | HandleLifecycle.Retired ->
                                unavailable.Add(renderLine "not-resumable" record " reason=retired-tombstone")
                            | HandleLifecycle.Active
                            | HandleLifecycle.CompletedAwaitingJoin _ ->
                                match! snapshotPort.GetMessages record.ChildSessionId with
                                | Error error ->
                                    unavailable.Add(
                                        renderLine
                                            "unavailable"
                                            record
                                            (" reason=" + error.Replace("\n", " ").Replace("\r", " "))
                                    )
                                | Ok _ ->
                                    match adopt parentId record with
                                    | Error error ->
                                        unavailable.Add(
                                            renderLine
                                                "not-adopted"
                                                record
                                                (" reason=" + error.Replace("\n", " ").Replace("\r", " "))
                                        )
                                    | Ok() -> surviving.Add(renderLine "surviving" record "")

                    let survivingText =
                        if surviving.Count = 0 then "- none" else String.Join("\n", surviving)

                    let unavailableText =
                        if unavailable.Count = 0 then "- none" else String.Join("\n", unavailable)

                    let argumentText =
                        if String.IsNullOrWhiteSpace arguments then
                            ""
                        else
                            "\nUser /continue arguments: " + arguments.Trim()

                    let briefing =
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
                        + argumentText

                    appendVisiblePart output briefing
        }
