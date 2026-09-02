namespace Wanxiangshu.Persistence.Journal

open System

/// Pure canonical envelope/fold owner for obligation facts. Journal handles are
/// intentionally absent; durable append/read stays in ObligationJournalSurface.
[<RequireQualifiedAccess>]
module ObligationEnvelopeSurface =
    /// Serialize a typed MagicTodo fact JSON string into canonical fact bytes.
    val serializeMagicTodoEnvelope: typed: string -> string

    /// Deserialize canonical fact bytes into a JS result object.
    val deserializeMagicTodoEnvelope: encoded: string -> obj

    /// Fold one MagicTodo envelope and return a JS result object.
    val foldMagicEnvelope: sessionId: string -> providerRun: string -> typed: string -> obj

    /// Fold a sequence of lifecycle events and return a JS result object.
    val foldLifecycleSequence: sessionId: string -> events: obj array -> obj
