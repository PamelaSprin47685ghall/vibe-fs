namespace Wanxiangshu.Participant.Cognition

open Fable.Core
open Fable.Core.JsInterop

/// JS → JSON text at the tool boundary.
///
/// The canvas crosses as text, never as a parsed value tree: jq receives a JSON
/// string and returns one, so keeping the lexical form means no re-encoding step can
/// quietly change the value the model wrote. The only conversion here is the
/// deterministic one the tool needs to answer a call — rendering the committed text
/// back into a `LlmFacing` document, which happens in the tool adapter where the
/// representation owner lives.
[<RequireQualifiedAccess>]
module CanvasCodec =

    [<Emit("$0 === null")>]
    let private jsNull (value: obj) : bool = jsNative

    [<Emit("typeof $0")>]
    let private jsType (value: obj) : string = jsNative

    /// jq output must be a JSON value, and a JS object/array/scalar is one. `undefined`
    /// is the one shape that cannot round-trip, so it is refused rather than coerced
    /// into the string "undefined".
    let isJsonValue (value: obj) : bool =
        not (jsNull value) && jsType value <> "undefined"

    /// Canonical JSON text for a jq output.
    ///
    /// Key order follows the object's own insertion order, exactly as jq produced it:
    /// re-sorting would make the text the model reads back differ from the text it
    /// wrote for no semantic reason.
    ///
    /// Written as an inline expression rather than a standalone `[<Emit>]` member:
    /// Fable only reliably emits those when it also inlines them, and a public
    /// signature member forces a standalone export that this project's emit pipeline
    /// drops. Building the string through `JS.JSON.stringify` at the call site keeps
    /// the single owner here while producing a real module export.
    let toJson (value: obj) : string = JS.JSON.stringify value

    /// An empty canvas. Kept as a named value so the tool and its tests agree on what
    /// "no commit yet" looks like instead of each writing its own `{}`.
    let emptyCanvasJson = "{}"
