namespace Wanxiangshu.Context.Scope

open System
open System.Collections.Generic
open System.Text.RegularExpressions
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection

module Protocol =

    [<Literal>]
    let ResetScopeTool = "resetScope"

    [<Literal>]
    let SuffixSeparator = "\u0000\uFEFF"

    [<Literal>]
    let UserReminder =
        "[System: Estimate the turnBudget for the first fine-grained task block (state closed-loop), and call resetScope immediately before doing anything else.]"

    [<Literal>]
    let BudgetExhaustedSteer =
        "[System: Weak, optional reminder — the scope turnBudget is exhausted (0 remaining). If you are not busy and at a natural boundary, consider calling resetScope with a fresh budget. Do not interrupt, pause, or abandon your current work for this; keep going and reset when convenient.]"

    let private budgetRegex = Regex(@"\[Remaining turnBudget: \d+\]")

    let budgetSuffix (budget: int) : string =
        let remaining = max 0 budget
        $"[Remaining turnBudget: %d{remaining}]"

    [<Emit("typeof $0 === 'string'")>]
    let private isString (_value: obj) : bool = jsNative

    [<Emit("Array.isArray($0)")>]
    let private isArray (_value: obj) : bool = jsNative

    [<Emit("$0 == null ? undefined : $0[$1]")>]
    let private jsGet (target: obj) (key: string) : obj = jsNative

    let mapArrayIfChanged (arr: obj array) (mapper: obj -> obj) : obj array =
        let next = arr |> Array.map mapper
        let changed = Array.exists2 (fun a b -> not (obj.ReferenceEquals(a, b))) arr next
        if changed then next else arr

    let isUnchanged (arr: obj array) (other: obj array) : bool =
        obj.ReferenceEquals(arr, other)
        || (arr.Length = other.Length && Array.forall2 (fun a b -> obj.ReferenceEquals(a, b)) arr other)

    let toObjArray (messages: obj) : obj array =
        if isNull messages then [||]
        elif isArray messages then unbox<obj array> messages
        else emitJsExpr messages "Array.from($0)"

    let private tryGetTextBlock (block: obj) : string option =
        if isNull block then None
        else
            let bType = jsGet block "type"
            let bText = jsGet block "text"
            if not (isNull bType) && string bType = "text" && not (isNull bText) && isString bText then
                Some(unbox<string> bText)
            else None

    let rec private contentText (content: obj) : string =
        if isNull content then
            ""
        elif isString content then
            unbox<string> content
        elif isArray content then
            (unbox<obj array> content)
            |> Array.choose tryGetTextBlock
            |> String.concat "\n"
        else
            ""

    let private appendedTail (content: obj) : string =
        let text = contentText content
        let index = text.LastIndexOf(SuffixSeparator, StringComparison.Ordinal)
        if index = -1 then ""
        else text.Substring(index + SuffixSeparator.Length)

    let private appendBlockWithAddition (block: obj) (addition: string) : obj =
        let cloned = emitJsExpr block "Object.assign({}, $0)"
        let curText = string (jsGet block "text")
        cloned?text <- $"{curText}{SuffixSeparator}{addition}"
        cloned

    let private appendToArrayWithAddition (arr: obj array) (addition: string) : obj array =
        let lastText = arr |> Array.tryFindIndexBack (fun b -> (tryGetTextBlock b).IsSome) |> Option.defaultValue -1
        if lastText = -1 then
            let next = Array.zeroCreate (arr.Length + 1)
            Array.blit arr 0 next 0 arr.Length
            next.[arr.Length] <- createObj [ "type" ==> "text"; "text" ==> $"{SuffixSeparator}{addition}" ]
            next
        else
            arr
            |> Array.mapi (fun i block ->
                if i = lastText then appendBlockWithAddition block addition
                else block)

    let appendText (content: obj) (addition: string) : obj =
        if isNull content then
            box [| createObj [ "type" ==> "text"; "text" ==> $"{SuffixSeparator}{addition}" ] |]
        elif isString content then
            box $"{unbox<string> content}{SuffixSeparator}{addition}"
        elif isArray content then
            box (appendToArrayWithAddition (unbox<obj array> content) addition)
        else
            box [| createObj [ "type" ==> "text"; "text" ==> $"{SuffixSeparator}{addition}" ] |]

    let appendUserReminder (content: obj) : obj =
        if (appendedTail content).StartsWith(UserReminder, StringComparison.Ordinal) then
            content
        else
            appendText content UserReminder

    let appendBudgetReminder (content: obj) (budget: int) : obj =
        if budgetRegex.IsMatch(appendedTail content) then
            content
        else
            appendText content (budgetSuffix budget)

    let private stripBudgetBeforeReminder (text: string) : string =
        let marker = $"{SuffixSeparator}{UserReminder}"
        if not (text.Contains(marker)) then
            text
        else
            let pattern = Regex.Escape(SuffixSeparator) + @"\[Remaining turnBudget: \d+\](?=" + Regex.Escape(marker) + ")"
            Regex.Replace(text, pattern, "")

    let private updateStrippedBlock (block: obj) (original: string) (stripped: string) : obj option =
        if original <> stripped then
            let cloned = emitJsExpr block "Object.assign({}, $0)"
            cloned?text <- stripped
            Some cloned
        else None

    let private tryStripBlockText (block: obj) : obj option =
        tryGetTextBlock block
        |> Option.bind (fun original -> updateStrippedBlock block original (stripBudgetBeforeReminder original))

    let private stripContent (content: obj) : obj =
        if isNull content then
            content
        elif isString content then
            box (stripBudgetBeforeReminder (unbox<string> content))
        elif isArray content then
            let arr = unbox<obj array> content
            let next = arr |> Array.map (fun block -> defaultArg (tryStripBlockText block) block)
            let changed = Array.exists2 (fun a b -> not (obj.ReferenceEquals(a, b))) arr next
            if changed then box next else content
        else
            content

    let private rewriteContent (message: obj) (content: obj) : obj =
        let cloned = emitJsExpr message "Object.assign({}, $0)"
        cloned?content <- content
        emitJsStatement cloned "delete $0.providerPayload"
        cloned

    let private rewriteAssistant (message: obj) (content: obj) : obj =
        let cloned = emitJsExpr message "Object.assign({}, $0)"
        let hasParts = not (isNull (jsGet message "parts")) && isArray (jsGet message "parts")
        if hasParts then
            cloned?parts <- content
            emitJsStatement cloned "delete $0.content"
        else
            cloned?content <- content
            emitJsStatement cloned "delete $0.parts"
        emitJsStatement cloned "delete $0.providerPayload; delete $0.responseId"
        cloned

    let private updateStrippedContent (message: obj) (original: obj) (stripped: obj) : obj =
        if not (obj.ReferenceEquals(original, stripped)) then rewriteContent message stripped
        else message

    let private stripContradictoryMessage (message: obj) : obj =
        let role = jsGet message "role"
        if not (isNull message) && not (isNull role) && string role = "toolResult" then
            let original = jsGet message "content"
            updateStrippedContent message original (stripContent original)
        else
            message

    let stripContradictoryBudgetSuffixes (messages: obj) : obj array =
        let messages = toObjArray messages
        mapArrayIfChanged messages stripContradictoryMessage

    let private messageTimestamp (msg: obj) : obj option =
        if isNull msg then None
        else
            let ts = jsGet msg "timestamp"
            if not (isNull ts) then Some ts else None

    let private lastMessageTimestamp (messages: obj array) : obj option =
        if messages.Length = 0 then None
        else messageTimestamp messages.[messages.Length - 1]

    let addBudgetExhaustedSteer (messages: obj) : obj array =
        let messages = toObjArray messages
        let tsOpt = lastMessageTimestamp messages
        let baseFields =
            [ "role" ==> "user"
              "content"
              ==> [| createObj [ "type" ==> "text"; "text" ==> BudgetExhaustedSteer ] |]
              "steering" ==> true
              "attribution" ==> "agent" ]
        let fields =
            match tsOpt with
            | Some ts -> baseFields @ [ "timestamp" ==> ts ]
            | None -> baseFields
        let steerMessage = createObj fields
        Array.append messages [| steerMessage |]

    let endsWithSideRequest (messages: obj) : bool =
        let messages = toObjArray messages
        if messages.Length = 0 then false
        else
            let last = messages.[messages.Length - 1]
            if isNull last then false
            else
                let role =
                    let r = jsGet last "role"
                    if not (isNull r) then string r
                    else
                        let info = jsGet last "info"
                        if not (isNull info) then string (jsGet info "role") else ""
                let attribution =
                    let a = jsGet last "attribution"
                    if not (isNull a) then string a
                    else
                        let info = jsGet last "info"
                        if not (isNull info) then string (jsGet info "attribution") else ""
                let steering =
                    let s = jsGet last "steering"
                    if not (isNull s) then s
                    else
                        let info = jsGet last "info"
                        if not (isNull info) then jsGet info "steering" else box false
                role = "user" && attribution = "agent" && steering <> box true

    let isScopePrompt (message: obj) : bool =
        if isNull message then
            false
        else
            let role = jsGet message "role"
            let attribution = jsGet message "attribution"
            let rStr = if isNull role then "" else string role
            let aStr = if isNull attribution then "" else string attribution
            rStr = "user" && aStr <> "agent"

    let private isUserAuthored (message: obj) : bool =
        if isNull message then
            false
        else
            let role = jsGet message "role"
            let attribution = jsGet message "attribution"
            let rStr = if isNull role then "" else string role
            let aStr = if isNull attribution then "" else string attribution
            rStr = "user" || (rStr = "custom" && aStr = "user")

    let promptKey (message: obj) : string =
        if isNull message then
            ""
        else
            let ts = jsGet message "timestamp"
            let tsStr = if isNull ts then "" else string ts
            let content = jsGet message "content"
            let text = contentText content
            $"{tsStr}:{text}"

    let lastPromptIndex (messages: obj) : int =
        toObjArray messages
        |> Array.tryFindIndexBack isScopePrompt
        |> Option.defaultValue -1

    let private isResetCall (block: obj) : bool =
        if isNull block then
            false
        else
            let bType = jsGet block "type"
            let bTypeStr = if isNull bType then "" else string bType
            let bName =
                let n = jsGet block "name"
                if not (isNull n) then string n
                else
                    let t = jsGet block "tool"
                    if not (isNull t) then string t else ""
            (bTypeStr = "toolCall" || bTypeStr = "tool-call" || bTypeStr = "tool_call" || bTypeStr = "tool")
            && bName = ResetScopeTool

    let private messageItems (message: obj) : obj array =
        if isNull message then [||]
        else
            let c = jsGet message "content"
            if isArray c then unbox<obj array> c
            else
                let p = jsGet message "parts"
                if isArray p then unbox<obj array> p else [||]

    let private isAssistantWithReset (message: obj) : bool =
        if isNull message then false
        else
            let role = jsGet message "role"
            not (isNull role) && string role = "assistant" && (messageItems message |> Array.exists isResetCall)

    let hasResetSinceLastPrompt (messages: obj) : bool =
        let arr = toObjArray messages
        let boundary = lastPromptIndex (box arr)
        if boundary = -1 then false
        else
            let slice = arr |> Array.skip (boundary + 1)
            Array.exists isAssistantWithReset slice

    let private toolCallIdOf (block: obj) : string option =
        if isNull block then None
        else
            [ "id"; "toolCallId"; "callID"; "callId" ]
            |> List.tryPick (fun key ->
                let v = jsGet block key
                if not (isNull v) && isString v then Some(unbox<string> v) else None)

    let private isToolResultRole (role: string) : bool =
        role = "toolResult" || role = "tool" || role = "tool_result"

    let private toolResultPartCallId (part: obj) : string option =
        if isNull part then None
        else
            let pType = string (jsGet part "type")
            if isToolResultRole pType then toolCallIdOf part
            else None

    let private messageRoleToolCallId (message: obj) : string option =
        let id = jsGet message "id"
        if not (isNull id) && isString id && isToolResultRole (string (jsGet message "role")) then
            Some(unbox<string> id)
        else
            None

    let private messageDirectCallId (message: obj) : string option =
        let fromTool =
            [ "toolCallId"; "callID"; "callId" ]
            |> List.tryPick (fun key ->
                let v = jsGet message key
                if not (isNull v) && isString v then Some(unbox<string> v) else None)
        fromTool |> Option.orElseWith (fun () -> messageRoleToolCallId message)

    let private messagePartsCallId (message: obj) : string option =
        let parts = jsGet message "parts"
        if isArray parts then unbox<obj array> parts |> Array.tryPick toolResultPartCallId
        else None

    let private messageToolCallId (message: obj) : string option =
        if isNull message then None
        else messageDirectCallId message |> Option.orElseWith (fun () -> messagePartsCallId message)

    let private findMessageResetCall (message: obj) : int option =
        if isNull message then None
        else
            let role = jsGet message "role"
            if not (isNull role) && string role = "assistant" then
                let content = messageItems message
                content |> Array.tryFindIndexBack isResetCall
            else
                None

    let private findLastResetBarrier (messages: obj array) : (int * int) option =
        messages
        |> Array.mapi (fun i m -> (i, m))
        |> Array.tryFindBack (fun (_, m) -> (findMessageResetCall m).IsSome)
        |> Option.bind (fun (i, m) ->
            match findMessageResetCall m with
            | Some bIdx -> Some(i, bIdx)
            | None -> None)

    let private rewritePriorAssistantContent (message: obj) : obj option =
        let content = messageItems message
        let filtered = content |> Array.filter isResetCall
        if filtered.Length > 0 then Some(rewriteAssistant message (box filtered))
        else None

    let private rewritePriorAssistant (message: obj) : obj option =
        if isNull message then None
        else
            let role = jsGet message "role"
            if not (isNull role) && string role = "assistant" then
                rewritePriorAssistantContent message
            else
                None

    let private rewriteBarrierMessage (barrier: obj) (activeBlock: int) : obj =
        let barrierContent = messageItems barrier
        let filteredBarrier =
            barrierContent
            |> Array.mapi (fun blockIndex block -> (blockIndex, block))
            |> Array.filter (fun (blockIndex, block) -> isResetCall block || blockIndex >= activeBlock)
            |> Array.map snd
        rewriteAssistant barrier (box filteredBarrier)

    let private buildScopeRewrites (messages: obj array) (activeMessage: int) (activeBlock: int) : Dictionary<int, obj> =
        let rewrites = Dictionary<int, obj>()
        for msgIndex = 0 to activeMessage - 1 do
            rewritePriorAssistant messages.[msgIndex] |> Option.iter (fun rw -> rewrites.[msgIndex] <- rw)
        rewrites.[activeMessage] <- rewriteBarrierMessage messages.[activeMessage] activeBlock
        rewrites

    let private assistantContentForIndex (messages: obj array) (rewrites: Dictionary<int, obj>) (activeMessage: int) (i: int) : obj array =
        let message = messages.[i]
        if isNull message then [||]
        else
            let role = jsGet message "role"
            if isNull role || string role <> "assistant" then [||]
            elif rewrites.ContainsKey(i) then messageItems rewrites.[i]
            elif i > activeMessage then messageItems message
            else [||]

    let private recordBlockToolCall (kept: HashSet<string>) (block: obj) =
        toolCallIdOf block |> Option.iter (fun id -> kept.Add(id) |> ignore)

    let private recordAssistantBlocks (kept: HashSet<string>) (content: obj array) =
        content |> Array.iter (recordBlockToolCall kept)

    let private collectKeptToolCalls (messages: obj array) (rewrites: Dictionary<int, obj>) (activeMessage: int) : HashSet<string> =
        let kept = HashSet<string>()
        for i = 0 to messages.Length - 1 do
            let content = assistantContentForIndex messages rewrites activeMessage i
            recordAssistantBlocks kept content
        kept

    let private shouldKeepPriorToolResult (message: obj) (keptToolCalls: HashSet<string>) : bool =
        match messageToolCallId message with
        | Some id -> keptToolCalls.Contains(id)
        | None -> false

    let private filterPriorMessage (message: obj) (rwOpt: obj option) (keptToolCalls: HashSet<string>) : obj option =
        if isNull message then None
        elif isUserAuthored message then Some message
        elif rwOpt.IsSome then rwOpt
        else
            let role = jsGet message "role"
            let rStr = if isNull role then "" else string role
            if isToolResultRole rStr && shouldKeepPriorToolResult message keptToolCalls then
                Some message
            else
                None

    let private shouldKeepPostToolResult (message: obj) (keptToolCalls: HashSet<string>) : bool =
        match messageToolCallId message with
        | None -> true
        | Some id -> keptToolCalls.Contains(id)

    let private filterPostMessage (message: obj) (keptToolCalls: HashSet<string>) : obj =
        if isNull message then message
        else
            let role = jsGet message "role"
            let rStr = if isNull role then "" else string role
            if isToolResultRole rStr && not (shouldKeepPostToolResult message keptToolCalls) then
                null
            else
                message

    let private assembleScopeMessages (messages: obj array) (activeMessage: int) (rewrites: Dictionary<int, obj>) (kept: HashSet<string>) : obj array =
        let result = List<obj>()
        for i = 0 to activeMessage - 1 do
            let rwOpt = if rewrites.ContainsKey(i) then Some rewrites.[i] else None
            filterPriorMessage messages.[i] rwOpt kept |> Option.iter result.Add
        result.Add(rewrites.[activeMessage])
        for i = activeMessage + 1 to messages.Length - 1 do
            let m = filterPostMessage messages.[i] kept
            if not (isNull m) then result.Add(m)
        result.ToArray()

    let filterScopeMessages (messages: obj) : obj array =
        let arr = toObjArray messages
        match findLastResetBarrier arr with
        | None -> arr
        | Some(activeMessage, activeBlock) ->
            let rewrites = buildScopeRewrites arr activeMessage activeBlock
            let kept = collectKeptToolCalls arr rewrites activeMessage
            assembleScopeMessages arr activeMessage rewrites kept

    let private applyUpdatedReminder (message: obj) (original: obj) (updated: obj) : obj =
        if not (obj.ReferenceEquals(original, updated)) then rewriteContent message updated
        else message

    let private updateUserReminder (message: obj) : obj =
        if isScopePrompt message then
            let original = jsGet message "content"
            applyUpdatedReminder message original (appendUserReminder original)
        else
            message

    let addUserReminders (messages: obj) : obj array =
        let arr = toObjArray messages
        mapArrayIfChanged arr updateUserReminder

    let private isResetWirePart (part: WirePart) : bool =
        match part with
        | WireToolCall(_, name, _) -> name = ResetScopeTool
        | _ -> false

    let private findWireMessageResetPart (msg: WireMessage) : int option =
        if msg.Role = "assistant" then
            msg.Parts |> List.tryFindIndexBack isResetWirePart
        else
            None

    let private findLastWireResetBarrier (messages: WireMessage list) : (int * int) option =
        messages
        |> List.mapi (fun i m -> (i, m))
        |> List.tryFindBack (fun (_, m) -> (findWireMessageResetPart m).IsSome)
        |> Option.bind (fun (i, m) ->
            match findWireMessageResetPart m with
            | Some pIdx -> Some(i, pIdx)
            | None -> None)

    let private rewritePriorWireAssistant (msg: WireMessage) : WireMessage option =
        if msg.Role = "assistant" then
            let filtered = msg.Parts |> List.filter isResetWirePart
            if not (List.isEmpty filtered) then Some { msg with Parts = filtered }
            else None
        else
            None

    let private rewriteBarrierWireMessage (barrier: WireMessage) (activePart: int) : WireMessage =
        let barrierParts =
            barrier.Parts
            |> List.mapi (fun i part -> (i, part))
            |> List.filter (fun (i, part) -> isResetWirePart part || i >= activePart)
            |> List.map snd
        { barrier with Parts = barrierParts }

    let private buildWireRewrites (messages: WireMessage list) (activeMessage: int) (activePart: int) : Dictionary<int, WireMessage> =
        let rewrites = Dictionary<int, WireMessage>()
        let arr = List.toArray messages
        for i = 0 to activeMessage - 1 do
            rewritePriorWireAssistant arr.[i] |> Option.iter (fun rw -> rewrites.[i] <- rw)
        rewrites.[activeMessage] <- rewriteBarrierWireMessage arr.[activeMessage] activePart
        rewrites

    let private wireMessageContentForIndex (messages: WireMessage array) (rewrites: Dictionary<int, WireMessage>) (activeMessage: int) (i: int) : WirePart list =
        if rewrites.ContainsKey(i) then rewrites.[i].Parts
        elif i > activeMessage && messages.[i].Role = "assistant" then messages.[i].Parts
        else []

    let private addWirePartToolCall (kept: HashSet<ToolCallId>) (part: WirePart) : unit =
        match part with
        | WireToolCall(callId, _, _) -> kept.Add(callId) |> ignore
        | _ -> ()

    let private collectKeptWireToolCalls (messages: WireMessage array) (rewrites: Dictionary<int, WireMessage>) (activeMessage: int) : HashSet<ToolCallId> =
        let kept = HashSet<ToolCallId>()
        for i = 0 to messages.Length - 1 do
            let parts = wireMessageContentForIndex messages rewrites activeMessage i
            for p in parts do addWirePartToolCall kept p
        kept

    let private isKeptToolResult (kept: HashSet<ToolCallId>) (part: WirePart) : bool =
        match part with
        | WireToolResult(callId, _) -> kept.Contains(callId)
        | _ -> false

    let private filterPriorWireMessage (msg: WireMessage) (rwOpt: WireMessage option) (kept: HashSet<ToolCallId>) : WireMessage option =
        if msg.Role = "user" then Some msg
        elif rwOpt.IsSome then rwOpt
        else
            let toolResults = msg.Parts |> List.filter (isKeptToolResult kept)
            if not (List.isEmpty toolResults) then Some { msg with Parts = toolResults }
            else None

    let private assembleWireMessages (messages: WireMessage array) (activeMessage: int) (rewrites: Dictionary<int, WireMessage>) (kept: HashSet<ToolCallId>) : WireMessage list =
        let result = List<WireMessage>()
        for i = 0 to activeMessage - 1 do
            let rwOpt = if rewrites.ContainsKey(i) then Some rewrites.[i] else None
            filterPriorWireMessage messages.[i] rwOpt kept |> Option.iter result.Add
        result.Add(rewrites.[activeMessage])
        for i = activeMessage + 1 to messages.Length - 1 do
            result.Add(messages.[i])
        result |> Seq.toList

    let filterWireMessages (messages: WireMessage list) : WireMessage list =
        match findLastWireResetBarrier messages with
        | None -> messages
        | Some(activeMessage, activePart) ->
            let arr = List.toArray messages
            let rewrites = buildWireRewrites messages activeMessage activePart
            let kept = collectKeptWireToolCalls arr rewrites activeMessage
            assembleWireMessages arr activeMessage rewrites kept
