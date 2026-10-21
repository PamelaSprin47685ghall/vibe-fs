namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation.HostSession
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host.Ports

/// HNI P-03: the OpenCode adapter for the neutral transcript port.
///
/// The adapter is a *projection*: it reuses the existing snapshot plumbing
/// (`ISessionSnapshotPort`, already implemented by `SdkSnapshotPort` /
/// `HttpSnapshotPort`) and translates the host-shaped `SessionMessage` into the
/// neutral `TranscriptMessage`. No host wire term survives the translation, and
/// the legacy snapshot contract is left untouched — the product still consumes
/// it, and cutting those consumers over to this port is remaining work.
module TranscriptPort =

    /// Role vocabulary of the neutral port. The host role string is matched
    /// strictly: an unknown (or blank) role refuses the whole message rather
    /// than coercing it into one of the three neutral roles. Coercion would
    /// silently mis-attribute a message; refusing keeps the transcript honest.
    let private roleOf (role: string) : Result<TranscriptRole, HostError> =
        match role with
        | "user" -> Ok TranscriptRole.User
        | "assistant" -> Ok TranscriptRole.Assistant
        | "system" -> Ok TranscriptRole.System
        | other -> Error(HostError.Unknown(sprintf "unrecognised transcript role '%s'" other))

    /// Provider/model selection. The host model record already carries the
    /// provider, the model and the variant, so the neutral route is a direct
    /// reading — nothing is hardcoded and no provider is resolved by guessing.
    /// A message without a model has no route; the adapter never invents one.
    let private routeOf (model: OpencodeModel option) : ModelRoute option =
        match model with
        | None -> None
        | Some value ->
            Some
                { Provider = value.providerID
                  Model = value.modelID
                  Variant = value.variant }

    /// The raw text of a canonical JSON blob.
    let private canonicalText (value: CanonicalJsonText) : string =
        match value with
        | CanonicalJsonText text -> text

    /// A non-empty text wins; an empty one falls back to the value carried by
    /// the host part.
    let private preferredText (recorded: string) (fallback: string) : string =
        if System.String.IsNullOrWhiteSpace recorded then fallback else recorded

    /// The failure carried by a message that reported an error name.
    ///
    /// The host reports a raw error name; classifying it into an execution
    /// failure, a retry decision or a budget decision is product logic
    /// (Execution/Failure), so the adapter only carries the raw name inside a
    /// `HostFailure` of kind `Provider`. The status code and the retry flag
    /// are not observable on the host, so they stay absent.
    let private failureTerminal (name: string) : TurnTerminal =
        TurnTerminal.Failed
            { Kind = HostFailureKind.Provider
              Message = name
              StatusCode = None
              HostRetryable = None }

    /// The terminal of a message that reported no error: a completed message
    /// ends in `Completed` (carrying the host finish reason, which may be
    /// absent), an unfinished one has no observable terminal yet — `Completed`
    /// is never invented from the parts.
    let private completedTerminal (message: SessionMessage) : TurnTerminal option =
        match message.Completed with
        | true -> Some(TurnTerminal.Completed message.Finish)
        | false -> None

    /// The terminal of one provider attempt.
    let private terminalOf (message: SessionMessage) : TurnTerminal option =
        match message.ErrorName with
        | Some name -> Some(failureTerminal name)
        | None -> completedTerminal message

    /// Tool-call state of one host tool part, with the output it carries.
    /// The host snapshot has no "running" state, so `Running` is never produced.
    let private toolStateOf (state: SnapshotToolPartState) : ToolCallState * string option =
        match state with
        | SnapshotToolPartState.Pending -> ToolCallState.Pending, None
        | SnapshotToolPartState.Completed output -> ToolCallState.Completed, Some output
        | SnapshotToolPartState.Failed error -> ToolCallState.Failed, Some error

    /// The host tool parts of one message, keyed by their call id.
    let private toolPartsByCallId (message: SessionMessage) : Map<string, ToolCallRecord> =
        message.ToolParts
        |> Array.map (fun part ->
            let state, output = toolStateOf part.State

            ( ToolCallId.value part.ToolCallId,
              { CallId = part.ToolCallId
                Tool = part.ToolName
                Input = CanonicalJsonText part.InputCanonical
                State = state
                Output = output } ))
        |> Map.ofArray

    /// A tool part merged with the facts the host message carries on the
    /// matching call: the tool name and the call arguments.
    let private mergeToolUse (record: ToolCallRecord) (name: string) (args: string) : ToolCallRecord =
        { record with
            Tool = preferredText name record.Tool
            Input = CanonicalJsonText(preferredText (canonicalText record.Input) args) }

    /// One tool part of the message, projected onto the neutral tool-call
    /// record. The tool call id of the host snapshot is the join key: a tool
    /// part that cannot be matched is reported as an opaque part, never as a
    /// fabricated call record.
    let private toolUseOf (tools: Map<string, ToolCallRecord>) (callId: string) (name: string) (args: string) : TranscriptPart list =
        match Map.tryFind callId tools with
        | None -> [ TranscriptPart.Other "tool" ]
        | Some record -> [ TranscriptPart.ToolUse(mergeToolUse record name args) ]

    /// A tool call part of the host message.
    let private toolCallPartOf (tools: Map<string, ToolCallRecord>) (seen: System.Collections.Generic.HashSet<string>) (callId: string) (name: string) (args: string) : TranscriptPart list =
        seen.Add callId |> ignore
        toolUseOf tools callId name args

    /// A tool result part of the host message. The result completes the call
    /// when the snapshot knows it; otherwise the part stays opaque.
    let private toolResultPartOf (tools: Map<string, ToolCallRecord>) (seen: System.Collections.Generic.HashSet<string>) (callId: string) (result: string) : TranscriptPart list =
        seen.Add callId |> ignore

        match Map.tryFind callId tools with
        | None -> [ TranscriptPart.Other "tool" ]
        | Some record ->
            [ TranscriptPart.ToolUse
                  { record with
                        State = ToolCallState.Completed
                        Output = Some result } ]

    /// One host part projected onto the neutral part vocabulary.
    ///
    /// Text and reasoning map one to one; tool parts map onto `ToolUse` when
    /// the host snapshot knows the matching tool part, and onto an opaque
    /// `Other "tool"` part otherwise; every other host part kind stays an
    /// opaque `Other` part carrying its kind name, because the neutral port
    /// has no richer home for it.
    let private partOf (tools: Map<string, ToolCallRecord>) (seen: System.Collections.Generic.HashSet<string>) (part: MessagePart) : TranscriptPart list =
        match part with
        | MessagePart.Text text -> [ TranscriptPart.Text text ]
        | MessagePart.Reasoning text -> [ TranscriptPart.Reasoning text ]
        | MessagePart.Activity kind -> [ TranscriptPart.Other kind ]
        | MessagePart.ToolCall(callId, name, args) -> toolCallPartOf tools seen callId name args
        | MessagePart.ToolResult(callId, result) -> toolResultPartOf tools seen callId result

    /// The parts of one message, projected onto the neutral part vocabulary.
    ///
    /// A tool part of the snapshot that no message part mentions is still
    /// projected, so no call is lost.
    let private partsOf (message: SessionMessage) : TranscriptPart list =
        let tools = toolPartsByCallId message
        let seen = System.Collections.Generic.HashSet<string>()

        let fromParts =
            message.Parts
            |> Array.collect (fun part -> partOf tools seen part)
            |> Array.toList

        let unmatchedTools =
            tools
            |> Map.toList
            |> List.filter (fun (callId, _) -> not (seen.Contains callId))
            |> List.map (fun (_, record) -> TranscriptPart.ToolUse record)

        fromParts @ unmatchedTools

    /// One host message projected onto the neutral transcript record.
    let project (message: SessionMessage) : Result<TranscriptMessage, HostError> =
        match roleOf message.Role with
        | Error error -> Error error
        | Ok role ->
            Ok
                { Id = TranscriptMessageAddress.create message.Id
                  Role = role
                  Agent = message.Agent |> Option.map AgentRole
                  Route = routeOf message.Model
                  Parent = message.ParentId |> Option.map TranscriptMessageAddress.create
                  CreatedAt = message.CreatedAt
                  Completed = message.Completed
                  Terminal = terminalOf message
                  IsCompaction = message.IsCompaction
                  Correlation = message.PromptKey |> Option.map PromptKey.create
                  Parts = partsOf message }

    /// A whole session projected. The first message that cannot be projected
    /// refuses the whole transcript: a partially projected transcript would
    /// silently lose messages, and a caller cannot tell a short transcript
    /// from a broken one.
    let rec private projectLoop (acc: TranscriptMessage list) (remaining: SessionMessage list) : Result<TranscriptMessage list, HostError> =
        match remaining with
        | [] -> Ok(List.rev acc)
        | head :: tail -> projectStep (project head) acc tail

    and private projectStep (step: Result<TranscriptMessage, HostError>) (acc: TranscriptMessage list) (remaining: SessionMessage list) : Result<TranscriptMessage list, HostError> =
        match step with
        | Error error -> Error error
        | Ok value -> projectLoop (value :: acc) remaining

    /// A whole session projected onto the neutral transcript records.
    let projectAll (messages: SessionMessage list) : Result<TranscriptMessage list, HostError> =
        projectLoop [] messages

    /// The snapshot outcome translated onto the neutral error channel.
    /// A snapshot failure is a transport failure: the transcript is unknown.
    let private ofSnapshotOutcome (outcome: Result<SessionMessage list, string>) : Result<TranscriptMessage list, HostError> =
        match outcome with
        | Error reason -> Error(HostError.Transport reason)
        | Ok messages -> projectAll messages

    /// The OpenCode implementation of the neutral transcript port. It owns no
    /// transport: the session messages are fetched by the snapshot port and
    /// only projected here.
    type HostTranscriptPort(inner: ISessionSnapshotPort) =
        interface ITranscriptPort with
            member this.Messages(session: SessionId) : Task<Result<TranscriptMessage list, HostError>> =
                task {
                    let! outcome = inner.GetMessages session
                    return ofSnapshotOutcome outcome
                }
