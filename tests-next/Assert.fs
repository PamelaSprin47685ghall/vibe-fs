namespace Xunit

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop

type FactAttribute() =
    inherit global.Xunit.FactAttribute()

module private AssertHelper =
    let resetHeartbeat () : unit = ()

type Assert =
    static member Equal<'T>(expected: 'T, actual: 'T) =
        AssertHelper.resetHeartbeat ()

        if not (Unchecked.equals expected actual) then
            failwithf "Assert.Equal failed.\nExpected: %A\nActual:   %A" expected actual

    static member True(condition: bool) =
        AssertHelper.resetHeartbeat ()

        if not condition then
            failwith "Assert.True failed: expected true, got false"

    static member True(condition: bool, userMessage: string) =
        AssertHelper.resetHeartbeat ()

        if not condition then
            failwithf "Assert.True failed: %s" userMessage

    static member False(condition: bool) =
        AssertHelper.resetHeartbeat ()

        if condition then
            failwith "Assert.False failed: expected false, got true"

    static member False(condition: bool, userMessage: string) =
        AssertHelper.resetHeartbeat ()

        if condition then
            failwithf "Assert.False failed: %s" userMessage

    static member NotNull(value: obj) =
        AssertHelper.resetHeartbeat ()

        if isNull value then
            failwith "Assert.NotNull failed: object is null"

    static member Null(value: obj) =
        AssertHelper.resetHeartbeat ()

        if not (isNull value) then
            failwithf "Assert.Null failed: expected null, got %A" value

    static member Empty(collection: System.Collections.IEnumerable) =
        AssertHelper.resetHeartbeat ()

        if isNull collection then
            ()
        else
            let enum = collection.GetEnumerator()

            if enum.MoveNext() then
                failwith "Assert.Empty failed: collection is not empty"

    static member NotEmpty(collection: System.Collections.IEnumerable) =
        AssertHelper.resetHeartbeat ()

        if isNull collection then
            failwith "Assert.NotEmpty failed: collection is null"
        else
            let enum = collection.GetEnumerator()

            if not (enum.MoveNext()) then
                failwith "Assert.NotEmpty failed: collection is empty"

    static member Single(collection: System.Collections.IEnumerable) =
        AssertHelper.resetHeartbeat ()

        if isNull collection then
            failwith "Assert.Single failed: collection is null"
        else
            let enum = collection.GetEnumerator()

            if not (enum.MoveNext()) then
                failwith "Assert.Single failed: collection is empty"
            elif enum.MoveNext() then
                failwith "Assert.Single failed: collection has more than 1 item"

    static member Fail(message: string) =
        AssertHelper.resetHeartbeat ()
        failwithf "Assert.Fail: %s" message

    static member IsAssignableFrom<'T>(value: obj) =
        AssertHelper.resetHeartbeat ()

        if isNull value then
            failwith "Assert.IsAssignableFrom failed: object is null"

type Record =
    static member ExceptionAsync(f: unit -> Task) : Task<exn option> =
        task {
            try
                do! f ()
                return None
            with ex ->
                return Some ex
        }
