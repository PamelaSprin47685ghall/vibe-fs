namespace LocalityDependencyFixture

open Fable.Core
open Fable.Core.JsInterop

type PublicValue = { Text: string }

type PublicChoice =
    | PublicChoice of string

module Provider =
    type private MutableCell = { mutable Value: int }

    type private CapabilityPort =
        abstract Invoke: unit -> unit

    type private PureLeaf =
        { Count: int
          Label: string option }

    type private PurePrimitiveRecord = { Count: int }

    type private PureTree =
        | Leaf of PureLeaf
        | Branch of PureTree list * (int * string)

    type private RecursiveCapability =
        | Recurse of RecursiveCapability option
        | Capability of CapabilityPort

    type private RecursiveMutable =
        | MutableRecurse of RecursiveMutable option
        | MutableBuffer of int array

    type private MutualCapabilityA = MutualB of MutualCapabilityB

    and private MutualCapabilityB =
        | MutualA of MutualCapabilityA
        | MutualPort of CapabilityPort

    type private ChangingCycle<'value> =
        | Change of ChangingCycle<(unit -> unit)>
        | Value of 'value

    type private GenericEnvelope<'value> = { Item: 'value }

    type private NestedCapability = { Port: CapabilityPort option }

    type private NestedArray = { Values: int array }

    type private NestedFunction = { Callback: unit -> unit }

    type private NestedMutableCapability = { Ports: CapabilityPort array }

    type private PlainClass(value: int) =
        member _.Value = value

    type private MutableOwner() =
        let mutable state = 0
        member _.Read() = state
        member _.Write(value) = state <- value

    let mutable private moduleCell = 0

    let private preserveCapability (capability: CapabilityPort) = capability

    let private makeCounter () =
        let mutable count = 0

        fun () ->
            count <- count + 1
            count

    let private classifyImmutableAlgebra
        (pureLeaf: PureLeaf)
        (purePrimitiveRecord: PurePrimitiveRecord)
        (pureTree: PureTree)
        (pureTuple: int * string)
        (pureOption: int option)
        (pureList: string list)
        (pureMap: Map<string, int>)
        (pureSet: Set<int>)
        (pureResult: Result<int, string>)
        (recursiveCapability: RecursiveCapability)
        (recursiveMutable: RecursiveMutable)
        (mutualCapability: MutualCapabilityA)
        (changingCycle: ChangingCycle<int>)
        (pureGeneric: GenericEnvelope<int>)
        (capabilityGeneric: GenericEnvelope<CapabilityPort>)
        (arrayGeneric: GenericEnvelope<int array>)
        (nestedCapability: NestedCapability)
        (nestedArray: NestedArray)
        (nestedFunction: NestedFunction)
        (nestedMutableCapability: NestedMutableCapability)
        (plainClass: PlainClass)
        genericValue
        =
        pureLeaf,
        purePrimitiveRecord,
        pureTree,
        pureTuple,
        pureOption,
        pureList,
        pureMap,
        pureSet,
        pureResult,
        recursiveCapability,
        recursiveMutable,
        mutualCapability,
        changingCycle,
        pureGeneric,
        capabilityGeneric,
        arrayGeneric,
        nestedCapability,
        nestedArray,
        nestedFunction,
        nestedMutableCapability,
        plainClass,
        genericValue

    let private inspectImmutableAlgebra
        (pureLeaf: PureLeaf)
        (nestedCapability: NestedCapability)
        (nestedArray: NestedArray)
        (nestedFunction: NestedFunction)
        (nestedMutableCapability: NestedMutableCapability)
        =
        pureLeaf.Count,
        nestedCapability.Port,
        nestedArray.Values,
        nestedFunction.Callback,
        nestedMutableCapability.Ports

    let make text = { Text = text }

    let preserve value = value

    let choose condition whenTrue whenFalse =
        if condition then whenTrue else whenFalse

    let duplicateConstants condition =
        if condition then 7 else 7

    let duplicateExternal values =
        List.map id values, List.map id values

    let classifyMutableScope value =
        let mutable localCell = value
        localCell <- localCell + 1
        moduleCell <- localCell
        let objectCell = { Value = moduleCell }
        objectCell.Value <- objectCell.Value + 1
        objectCell.Value

module FixtureInterop =
    [<Import("join", "node:path")>]
    let join (left: string) (right: string) : string = jsNative

    [<Emit("Date.now()")>]
    let now () : float = jsNative

    let cwd () : string = emitJsExpr () "process.cwd()"
