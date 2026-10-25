namespace Wanxiangshu.Context.Scope

type OutboundModel =
    { ProviderId: string
      ModelId: string
      Variant: string option }

type OutboundSnapshot =
    { SessionId: string
      Messages: obj
      System: string array
      Model: OutboundModel
      NativePayload: obj option
      ToolsCaptured: bool }

[<RequireQualifiedAccess>]
module OutboundViewCapture =
    val recordMessages: messages: obj -> unit
    val recordMessagesFor: sessionId: string -> messages: obj -> unit
    val recordSystem: sessionId: string -> system: obj -> unit
    val recordModel: sessionId: string -> model: obj -> unit
    val recordNativeRequest: sessionId: string -> payload: obj -> model: obj -> bool
    val tryGetSealed: sessionId: string -> OutboundSnapshot option
    val discardSealed: sessionId: string -> unit
    val clearSession: sessionId: string -> unit
    val serializeSnapshot: snapshot: OutboundSnapshot -> string
