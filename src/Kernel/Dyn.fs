module VibeFs.Kernel.Dyn

open Fable.Core
open Fable.Core.JsInterop

/// Reliable dynamic access helpers for heterogeneous host JS data.  The `?`
/// operator in Fable requires identifier keys; these Emit helpers accept any
/// string so we can read keys known only at runtime.

[<Emit("$0 == null ? undefined : $0[$1]")>]
let get (o: obj) (key: string) : obj = jsNative

[<Emit("$0[$1]")>]
let getValue<'a> (o: obj) (key: string) : 'a = jsNative

[<Emit("$0 == null ? false : ($0[$1] !== undefined && $0[$1] !== null)")>]
let has (o: obj) (key: string) : bool = jsNative

[<Emit("typeof $0 === $1")>]
let typeIs (o: obj) (ty: string) : bool = jsNative

[<Emit("$0($1)")>]
let call1 (f: obj) (a: obj) : obj = jsNative

[<Emit("$0($1, $2)")>]
let call2 (f: obj) (a: obj) (b: obj) : obj = jsNative

[<Emit("{ ...$0, [$1]: $2 }")>]
let withKey (o: obj) (key: string) (v: obj) : obj = jsNative

[<Emit("Array.isArray($0)")>]
let isArray (o: obj) : bool = jsNative

/// True when a value is null OR undefined (JS loose nullish check).  Used for
/// all dynamic-value guards so missing properties are never mistaken for present.
[<Emit("$0 == null")>]
let isNullish (o: obj) : bool = jsNative

/// A real JS `undefined` value, for testing the undefined branch of guards.
[<Emit("undefined")>]
let undefinedValue : obj = jsNative

/// Coerce a dynamically-typed value to bool using JS truthiness.
[<Emit("!!$0")>]
let truthy (o: obj) : bool = jsNative

[<Emit("structuredClone($0)")>]
let clone (o: obj) : obj = jsNative

/// Read a property as a string, "" when absent (null or undefined).
let str (o: obj) (key: string) : string =
    let v = get o key
    if isNullish v then "" else string v

/// Read a property as an option (None when null/undefined).
let opt (o: obj) (key: string) : obj option =
    let v = get o key
    if isNullish v then None else Some v
