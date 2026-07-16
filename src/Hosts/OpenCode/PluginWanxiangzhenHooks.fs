module Wanxiangshu.Hosts.Opencode.PluginWanxiangzhenHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorRuntime
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorOps
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorLifecycle
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorSquadUpdate
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorReplay
open Wanxiangshu.Hosts.Opencode.PluginWanxiangzhenE2eMeta
open Wanxiangshu.Runtime.Wanxiangzhen.SquadEventDisplayCodec

let internal twoArgHook (f: obj -> obj -> JS.Promise<unit>) =
    box (System.Func<obj, obj, JS.Promise<unit>>(f))

let internal mutateOutputParts (output: obj) (part: obj) : unit =
    let existing = get output "parts"

    if isNullish existing then
        setKey output "parts" (box [| part |])
    else
        let list = existing :?> System.Collections.Generic.List<obj>
        list.Clear()
        list.Add(part)
        setKey output "parts" (box list)

let internal handleCommandExecuteBefore (rt: CoordinatorRuntime) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let command = str input "command"

        match command with
        | "squad" ->
            let sessionId = str input "sessionID"

            if sessionId <> "" && rt.MasterSessionId = "" then
                rt.MasterSessionId <- sessionId
                do! replayFromEventLog rt

            let requirement = str input "arguments"

            if not rt.Dag.Tasks.IsEmpty && rt.Dag.SessionId <> "" then
                rt.Sessions <- rt.Sessions.Add(rt.Dag.SessionId, rt.Dag)

            let newSid =
                "squad-session-"
                + (rt.Deps.Now()).Substring(0, 19).Replace("T", "-").Replace(":", "-")

            let evt = SquadCreated(newSid, requirement)
            let! cr = commitEvent rt evt

            match cr with
            | Error err -> rt.InjectError <- Some(sprintf "SquadCreated append failed: %s" err)
            | Ok() ->
                rt.Dag <- empty newSid requirement
                writeE2eMetaIfEnabled rt

            let part =
                box
                    {| ``type`` = "text"
                       text = encodeEvent evt |}

            mutateOutputParts output part
        | "squad-kill" ->
            let args = str input "arguments"
            let sidOpt = if args = "" then None else Some args
            do! handleSquadKill rt sidOpt
        | "squad-status" ->
            let dagText = formatDagText rt
            let part = box {| ``type`` = "text"; text = dagText |}
            mutateOutputParts output part
        | _ -> ()
    }

let private squadUpdateArgsSchema () : obj =
    createObj
        [ "events",
          box (
              createObj
                  [ "type", box "array"
                    "minItems", box 1
                    "items",
                    box (
                        createObj
                            [ "type", box "object"
                              "properties",
                              box (
                                  createObj
                                      [ "type",
                                        box (
                                            createObj
                                                [ "type", box "string"
                                                  "enum", box [| "tasks_created"; "squad_cancelled" |] ]
                                        )
                                        "tasks",
                                        box (
                                            createObj
                                                [ "type", box "array"
                                                  "items",
                                                  box (
                                                      createObj
                                                          [ "type", box "object"
                                                            "properties",
                                                            box (
                                                                createObj
                                                                    [ "taskId", box (createObj [ "type", box "string" ])
                                                                      "title", box (createObj [ "type", box "string" ])
                                                                      "description",
                                                                      box (createObj [ "type", box "string" ])
                                                                      "dependsOn",
                                                                      box (
                                                                          createObj
                                                                              [ "type", box "array"
                                                                                "items",
                                                                                box (createObj [ "type", box "string" ]) ]
                                                                      ) ]
                                                            )
                                                            "required", box [| "title"; "description" |] ]
                                                  ) ]
                                        ) ]
                              )
                              "required", box [| "type" |] ]
                    ) ]
          ) ]

let internal assembleCoordinatorHooks (rt: CoordinatorRuntime) : obj =
    let squadUpdateToolDef () : obj =
        let args = squadUpdateArgsSchema ()

        let executeDef =
            createObj
                [ "description", box "Submit task decomposition or status update for the current squad session."
                  "args", box args
                  "execute", box (System.Func<obj, obj, JS.Promise<string>>(fun a _ -> handleSquadUpdate rt a)) ]

        createObj [ "squad_update", box executeDef ]

    let result = createObj []
    setKey result "id" (box "wanxiangzhen")
    setKey result "name" (box "wanxiangzhen")
    setKey result "tool" (squadUpdateToolDef ())

    setKey
        result
        "config"
        (box (fun (cfg: obj) ->
            promise {
                let commands = get cfg "command"

                if not (isNullish commands) then
                    setKey
                        commands
                        "squad"
                        (box
                            {| template = "/squad <requirement>"
                               description = "Decompose requirement into parallel task DAG" |})

                    setKey
                        commands
                        "squad-kill"
                        (box
                            {| template = "/squad-kill [session_id]"
                               description = "Kill squad slave processes" |})

                    setKey
                        commands
                        "squad-status"
                        (box
                            {| template = "/squad-status"
                               description = "Show current squad DAG status" |})

                return cfg
            }))

    setKey result "command.execute.before" (twoArgHook (handleCommandExecuteBefore rt))

    setKey
        result
        "chat.message"
        (twoArgHook (fun input _output ->
            promise {
                if rt.MasterSessionId = "" then
                    let sid = str input "sessionID"

                    if sid <> "" then
                        rt.MasterSessionId <- sid
                        do! replayFromEventLog rt
            }))

    setKey
        result
        "dispose"
        (box (fun () ->
            promise {
                rt.Server.Close()
                rt.PidPollHandle |> Option.iter rt.Deps.StopPolling
            }))

    result
