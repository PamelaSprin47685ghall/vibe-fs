module Wanxiangshu.Hosts.Opencode.CompactionHook

open Wanxiangshu.Kernel.Messaging

let cleanupCapsEpochBySession (runtimeScope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope) (sessionID: string) : unit =
    if sessionID <> "" then
        let sessionKey = "caps_epoch_session_" + sessionID

        match runtimeScope.TryFindKey(sessionKey) with
        | Some epObj ->
            let epoch = epObj :?> string
            runtimeScope.Remove(sessionKey)
            runtimeScope.Remove("caps_epoch_reverse_session_" + epoch)
            let reverseConvKey = "caps_epoch_reverse_conv_" + epoch

            match runtimeScope.TryFindKey(reverseConvKey) with
            | Some ckObj ->
                let ck = ckObj :?> string
                runtimeScope.Remove("caps_epoch_conv_" + ck)
                runtimeScope.Remove(reverseConvKey)
            | None -> ()
        | None -> ()

let private findExistingEpoch (msgs: Message<obj> list) =
    let rec loop =
        function
        | [] -> None
        | msg :: rest ->
            let id = msg.info.id

            if id.StartsWith "caps-synth-user-" then
                Some(id.Substring("caps-synth-user-".Length))
            elif id.StartsWith "caps-synth-assistant-" then
                Some(id.Substring("caps-synth-assistant-".Length))
            elif id.StartsWith "caps-synth-ack-" then
                Some(id.Substring("caps-synth-ack-".Length))
            else
                loop rest

    loop msgs

let private findFirstNativeUserMsgId (msgs: Message<obj> list) =
    let rec loop =
        function
        | [] -> None
        | msg :: rest ->
            let id = msg.info.id

            let isSynth =
                id.StartsWith "caps-synth-"
                || id.StartsWith "backlog-projection-"
                || id.StartsWith "backlog-prefix-"
                || id.StartsWith "semble-call-"
                || id.StartsWith "nudge-"
                || id.StartsWith "context-budget-nudge-"

            if msg.info.role = User && not isSynth && id <> "" then
                Some id
            else
                loop rest

    loop msgs

let resolveCapsEpoch
    (messagesList: Message<obj> list)
    (sessionID: string)
    (runtimeScope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    : string =
    match findExistingEpoch messagesList with
    | Some ep when ep <> "" -> ep
    | _ ->
        let convKeyOpt = findFirstNativeUserMsgId messagesList

        let tryFindEpoch () =
            match convKeyOpt with
            | Some ck ->
                match runtimeScope.TryFindKey("caps_epoch_conv_" + ck) with
                | Some ep -> Some(ep :?> string)
                | _ ->
                    if sessionID <> "" then
                        runtimeScope.TryFindKey("caps_epoch_session_" + sessionID)
                        |> Option.map (fun x -> x :?> string)
                    else
                        None
            | _ ->
                if sessionID <> "" then
                    runtimeScope.TryFindKey("caps_epoch_session_" + sessionID)
                    |> Option.map (fun x -> x :?> string)
                else
                    None

        match tryFindEpoch () with
        | Some ep ->
            if sessionID <> "" then
                runtimeScope.Add("caps_epoch_session_" + sessionID, box ep)
                runtimeScope.Add("caps_epoch_reverse_session_" + ep, box sessionID)

            ep
        | None ->
            match convKeyOpt, sessionID with
            | None, "" -> ""
            | _ ->
                let newEpoch = "epoch-" + System.Guid.NewGuid().ToString("N")

                match convKeyOpt with
                | Some ck ->
                    runtimeScope.Add("caps_epoch_conv_" + ck, box newEpoch)
                    runtimeScope.Add("caps_epoch_reverse_conv_" + newEpoch, box ck)
                | None -> ()

                if sessionID <> "" then
                    runtimeScope.Add("caps_epoch_session_" + sessionID, box newEpoch)
                    runtimeScope.Add("caps_epoch_reverse_session_" + newEpoch, box sessionID)

                newEpoch
