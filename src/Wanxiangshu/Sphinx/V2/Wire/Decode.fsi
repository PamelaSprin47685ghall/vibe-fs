namespace Wanxiangshu.Sphinx.V2.Wire

open System
open Fable.Core.JsInterop
open Wanxiangshu.Sphinx.V2.Core

type WireError = { Code: string; Path: string; Message: string }

module Decode =
    val stringField: obj -> string -> Result<string, WireError>

    /// NaN and Infinity are refused, never clamped.
    val finiteField: obj -> string -> Result<float, WireError>
    val nonNegativeIntegerField: obj -> string -> Result<int64, WireError>

    /// A revision arrives as a decimal string so no precision is lost in JavaScript.
    val revisionField: obj -> string -> Result<Revision, WireError>

    /// A list of unique, non-blank strings; a repeated entry is refused.
    val uniqueStringListField: obj -> string -> Result<string list, WireError>
