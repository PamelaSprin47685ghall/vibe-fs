open System
open System.Collections
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Reflection
open System.Runtime.Loader
open System.Text.Json

type DeclarationUseRecord =
    { ConsumerPath: string
      ProviderPaths: string array
      Symbol: string
      SymbolKind: string
      Assembly: string
      IsNamespace: bool
      IsModule: bool
      Line: int
      Column: int
      IsFromOpenStatement: bool
      IsFromPattern: bool
      IsFromType: bool
      IsFromUse: bool }

type ObservationSite =
    { SourcePath: string
      SemanticDeclarationAnchor: string
      SameAnchorOccurrenceOrdinal: int }

type ExternalSymbolUseRecord =
    { Assembly: string
      FullyQualifiedSymbol: string
      SymbolKind: string
      Site: ObservationSite }

type FSharpNodeRecord =
    { NodeKind: string
      SemanticIdentity: string
      Site: ObservationSite }

type SignatureExportRecord =
    { ExportKind: string
      DeclarationIdentity: string
      Site: ObservationSite }

type ExtractionDiagnosticRecord =
    { Code: string
      SourcePath: string
      SemanticDeclarationAnchor: string
      SyntaxKind: string
      Line: int
      Column: int
      RawIdentity: string }

type ScanResult =
    { SchemaVersion: int
      ProjectFile: string
      ProductionFiles: string array
      SignatureFiles: string array
      DeclarationUses: DeclarationUseRecord array
      ExternalSymbolUses: ExternalSymbolUseRecord array
      FsharpNodes: FSharpNodeRecord array
      FableInterop: obj array
      SignatureExports: SignatureExportRecord array
      Diagnostics: ExtractionDiagnosticRecord array
      ElapsedMilliseconds: int64 }

let arguments = fsi.CommandLineArgs |> Array.skip 1

if arguments.Length <> 7 then
    eprintfn "usage: locality-symbol-uses.fsx <project> <production-root> <scratch-root> <tool-dir> <fable-library> <assets-json> <result-json>"
    exit 2

let stopwatch = Stopwatch.StartNew()
let projectFile = Path.GetFullPath arguments.[0]
let productionRoot = Path.GetFullPath(arguments.[1]).TrimEnd(Path.DirectorySeparatorChar) + string Path.DirectorySeparatorChar
let scratchRoot = Path.GetFullPath arguments.[2]
let toolDirectory = Path.GetFullPath arguments.[3]
let fableLibraryPath = Path.GetFullPath arguments.[4]
let assetsPath = Path.GetFullPath arguments.[5]
let resultPath = Path.GetFullPath arguments.[6]

type ToolLoadContext(directory: string) =
    inherit AssemblyLoadContext("locality-symbol-uses", true)

    override this.Load(name: AssemblyName) =
        let candidate = Path.Combine(directory, name.Name + ".dll")
        if File.Exists candidate then this.LoadFromAssemblyPath candidate else null

let context = ToolLoadContext toolDirectory

let load name =
    let path = Path.Combine(toolDirectory, name)
    if not (File.Exists path) then failwith $"missing Fable tool assembly: {path}"
    context.LoadFromAssemblyPath path

let fsharpCore = load "FSharp.Core.dll"
let astAssembly = load "Fable.AST.dll"
let compilerAssembly = load "Fable.Compiler.dll"
let fcsAssembly = load "FSharp.Compiler.Service.dll"

let flags =
    BindingFlags.Public
    ||| BindingFlags.NonPublic
    ||| BindingFlags.Static
    ||| BindingFlags.Instance

let requireType (assembly: Assembly) name =
    match assembly.GetType(name, false) with
    | null -> failwith $"missing type: {name}"
    | candidate -> candidate

let requireProperty (candidate: Type) name =
    candidate.GetProperties flags
    |> Array.filter (fun property -> property.Name = name && property.GetIndexParameters().Length = 0)
    |> Array.tryFind (fun property -> property.DeclaringType = candidate)
    |> Option.orElseWith (fun () -> candidate.GetProperties flags |> Array.tryFind (fun property -> property.Name = name))
    |> Option.defaultWith (fun () -> failwith $"missing property: {candidate.FullName}.{name}")

let propertyValue (target: obj) name = requireProperty (target.GetType()) name |> fun property -> property.GetValue target
let staticPropertyValue (candidate: Type) name = requireProperty candidate name |> fun property -> property.GetValue null

let boolPropertyOrFalse (target: obj) name =
    try propertyValue target name :?> bool
    with _ -> false

let invokeConstructor (candidate: Type) (values: obj array) =
    candidate.GetConstructors flags
    |> Array.find (fun constructor -> constructor.GetParameters().Length = values.Length)
    |> fun constructor -> constructor.Invoke values

let emptyList itemType =
    requireType fsharpCore "Microsoft.FSharp.Collections.FSharpList`1"
    |> fun candidate -> candidate.MakeGenericType [| itemType |]
    |> fun candidate -> staticPropertyValue candidate "Empty"

let listOfValues itemType (values: obj array) =
    let listType =
        requireType fsharpCore "Microsoft.FSharp.Collections.FSharpList`1"
        |> fun candidate -> candidate.MakeGenericType [| itemType |]

    let cons =
        listType.GetMethods flags
        |> Array.find (fun methodInfo ->
            methodInfo.Name = "Cons"
            && methodInfo.IsStatic
            && methodInfo.GetParameters().Length = 2)

    values
    |> Array.rev
    |> Array.fold (fun tail value -> cons.Invoke(null, [| value; tail |])) (staticPropertyValue listType "Empty")

let emptyMap keyType valueType =
    requireType fsharpCore "Microsoft.FSharp.Collections.MapModule"
    |> fun candidate -> candidate.GetMethods flags
    |> Array.find (fun methodInfo -> methodInfo.Name = "Empty" && methodInfo.IsGenericMethodDefinition)
    |> fun methodInfo -> methodInfo.MakeGenericMethod [| keyType; valueType |]
    |> fun methodInfo -> methodInfo.Invoke(null, [||])

let some valueType value =
    requireType fsharpCore "Microsoft.FSharp.Core.FSharpOption`1"
    |> fun candidate -> candidate.MakeGenericType [| valueType |]
    |> fun candidate -> candidate.GetMethod("Some", flags).Invoke(null, [| value |])

let normalizePath (path: string) = Path.GetFullPath(path).Replace(Path.DirectorySeparatorChar, '/')
let isProductionPath path = Path.GetFullPath(path).StartsWith(productionRoot, StringComparison.Ordinal)

let language = requireType astAssembly "Fable.Language" |> fun candidate -> staticPropertyValue candidate "JavaScript"
let verbosity = requireType astAssembly "Fable.Verbosity" |> fun candidate -> staticPropertyValue candidate "Silent"
let majorVersion = compilerAssembly.GetName().Version.Major

let compilerOptions =
    invokeConstructor
        (requireType astAssembly "Fable.CompilerOptions")
        [| box true
           box false
           language
           listOfValues typeof<string> [| box "FABLE_COMPILER"; box $"FABLE_COMPILER_{majorVersion}"; box "FABLE_COMPILER_JAVASCRIPT" |]
           box false
           box false
           verbosity
           box ".fs.js"
           box false
           box false |]

let cliArgs =
    invokeConstructor
        (requireType compilerAssembly "Fable.Compiler.Util+CliArgs")
        [| box projectFile
           box (Path.GetDirectoryName projectFile)
           some typeof<string> (box scratchRoot)
           box false
           box false
           null
           box false
           some typeof<string> (box fableLibraryPath)
           box "Debug"
           box true
           box true
           box true
           box false
           box false
           null
           emptyList typeof<string>
           emptyMap typeof<string> typeof<string>
           null
           compilerOptions
           verbosity |]

let crackerOptions =
    invokeConstructor (requireType compilerAssembly "Fable.Compiler.ProjectCracker+CrackerOptions") [| cliArgs; box false |]

let resolver = invokeConstructor (requireType compilerAssembly "Fable.Compiler.MSBuildCrackerResolver") [||]

let projectResponse =
    requireType compilerAssembly "Fable.Compiler.ProjectCracker"
    |> fun candidate -> candidate.GetMethod("getFullProjectOpts", flags)
    |> fun methodInfo -> methodInfo.Invoke(null, [| resolver; crackerOptions |])

let crackedProjectOptions = propertyValue projectResponse "ProjectOptions"

let packageReferences =
    use document = JsonDocument.Parse(File.ReadAllText assetsPath)
    let root = document.RootElement
    let packagesPath = root.GetProperty("project").GetProperty("restore").GetProperty("packagesPath").GetString()
    let target = root.GetProperty("targets").EnumerateObject() |> Seq.exactlyOne |> fun property -> property.Value
    let libraries = root.GetProperty("libraries")

    target.EnumerateObject()
    |> Seq.collect (fun package ->
        match package.Value.TryGetProperty "compile" with
        | false, _ -> Seq.empty
        | true, compile ->
            let libraryPath = libraries.GetProperty(package.Name).GetProperty("path").GetString()
            compile.EnumerateObject()
            |> Seq.choose (fun reference ->
                if reference.Name = "_._" || Path.GetFileName(reference.Name) = "FSharp.Core.dll" then None
                else Some(Path.Combine(packagesPath, libraryPath, reference.Name))))
    |> Seq.distinct
    |> Seq.map (fun reference -> $"-r:{reference}")
    |> Seq.toArray

let projectOptions =
    let projectOptionsType = crackedProjectOptions.GetType()
    let constructor =
        projectOptionsType.GetConstructors flags
        |> Array.maxBy (fun candidate -> candidate.GetParameters().Length)
    let existingOptions = propertyValue crackedProjectOptions "OtherOptions" :?> string array
    let mergedOptions = Array.append existingOptions packageReferences |> Array.distinct

    constructor.GetParameters()
    |> Array.map (fun parameter ->
        projectOptionsType.GetProperties flags
        |> Array.tryFind (fun property ->
            property.GetIndexParameters().Length = 0
            && String.Equals(property.Name, parameter.Name, StringComparison.OrdinalIgnoreCase))
        |> Option.map (fun property ->
            if String.Equals(property.Name, "OtherOptions", StringComparison.OrdinalIgnoreCase) then box mergedOptions
            else property.GetValue crackedProjectOptions)
        |> Option.defaultWith (fun () -> failwith $"cannot clone FSharpProjectOptions constructor field: {parameter.Name}"))
    |> constructor.Invoke
let sourceFiles = propertyValue projectOptions "SourceFiles" :?> string array
let implementationProjectOptions =
    let projectOptionsType = projectOptions.GetType()
    let constructor =
        projectOptionsType.GetConstructors flags
        |> Array.maxBy (fun candidate -> candidate.GetParameters().Length)
    let implementationSources = sourceFiles |> Array.filter (fun path -> Path.GetExtension path = ".fs")
    constructor.GetParameters()
    |> Array.map (fun parameter ->
        projectOptionsType.GetProperties flags
        |> Array.tryFind (fun property ->
            property.GetIndexParameters().Length = 0
            && String.Equals(property.Name, parameter.Name, StringComparison.OrdinalIgnoreCase))
        |> Option.map (fun property ->
            if String.Equals(property.Name, "SourceFiles", StringComparison.OrdinalIgnoreCase) then box implementationSources
            else property.GetValue projectOptions)
        |> Option.defaultWith (fun () -> failwith $"cannot clone implementation FSharpProjectOptions field: {parameter.Name}"))
    |> constructor.Invoke
let missingSources = sourceFiles |> Array.filter (File.Exists >> not)
if missingSources.Length > 0 then
    let sample = missingSources |> Array.truncate 8 |> String.concat ", "
    failwith $"Fable project options contain missing source files: {sample}"

let checkerType = requireType fcsAssembly "FSharp.Compiler.CodeAnalysis.FSharpChecker"

let checker =
    let create =
        checkerType.GetMethods flags
        |> Array.find (fun methodInfo ->
            methodInfo.Name = "Create"
            && methodInfo.IsStatic
            && methodInfo.GetParameters().Length > 10)

    create.GetParameters()
    |> Array.map (fun parameter ->
        match parameter.Name with
        | "keepAssemblyContents"
        | "keepAllBackgroundSymbolUses" -> some typeof<bool> (box true)
        | _ -> null)
    |> fun values -> create.Invoke(null, values)

let checkAsync =
    checkerType.GetMethods flags
    |> Array.find (fun methodInfo ->
        methodInfo.Name = "ParseAndCheckProject"
        && methodInfo.GetParameters().Length = 2
        && methodInfo.GetParameters().[0].ParameterType.FullName = "FSharp.Compiler.CodeAnalysis.FSharpProjectOptions")
    |> fun methodInfo -> methodInfo.Invoke(checker, [| projectOptions; null |])

let checkResultType = requireType fcsAssembly "FSharp.Compiler.CodeAnalysis.FSharpCheckProjectResults"

let runAsync resultType asyncValue =
    requireType fsharpCore "Microsoft.FSharp.Control.FSharpAsync"
    |> fun candidate -> candidate.GetMethods flags
    |> Array.find (fun methodInfo ->
        methodInfo.Name = "RunSynchronously"
        && methodInfo.IsGenericMethodDefinition
        && methodInfo.GetParameters().Length = 3)
    |> fun methodInfo -> methodInfo.MakeGenericMethod [| resultType |]
    |> fun methodInfo -> methodInfo.Invoke(null, [| asyncValue; null; null |])

let checkResult = runAsync checkResultType checkAsync

let diagnostics = propertyValue checkResult "Diagnostics" :?> Array
let errors =
    diagnostics
    |> Seq.cast<obj>
    |> Seq.filter (fun diagnostic -> string (propertyValue diagnostic "Severity") = "Error")
    |> Seq.toArray

if errors.Length > 0 || propertyValue checkResult "HasCriticalErrors" :?> bool then
    errors
    |> Array.truncate 20
    |> Array.iter (fun diagnostic ->
        eprintfn
            "%s(%O,%O): %s"
            (propertyValue diagnostic "FileName" :?> string)
            (propertyValue diagnostic "StartLine")
            (propertyValue diagnostic "StartColumn")
            (propertyValue diagnostic "Message" :?> string))
    failwith $"FCS project check failed with {errors.Length} error(s)"

let symbolName symbol =
    try
        match propertyValue symbol "FullName" with
        | :? string as value when not (String.IsNullOrWhiteSpace value) -> value
        | _ -> propertyValue symbol "DisplayName" :?> string
    with _ -> propertyValue symbol "DisplayName" :?> string

let optionValue (value: obj) =
    if isNull value then None
    elif value.GetType().FullName.StartsWith("Microsoft.FSharp.Core.FSharpOption`1", StringComparison.Ordinal) then
        Some(propertyValue value "Value")
    else Some value

let asObjects (value: obj) =
    match value with
    | :? string -> Seq.empty
    | :? IEnumerable as values -> values |> Seq.cast<obj>
    | _ -> Seq.empty

let rangeStart (range: obj) =
    propertyValue range "StartLine" :?> int, propertyValue range "StartColumn" :?> int

let sourceRange (value: obj) =
    try
        let range = propertyValue value "Range"
        let line, column = rangeStart range
        let sourcePath = propertyValue range "FileName" :?> string |> normalizePath
        sourcePath, line, column
    with _ -> "", 0, 0

let occurrenceByAnchor = Dictionary<string, int>(StringComparer.Ordinal)

let observationSite (sourcePath: string) (anchor: string) (rawCase: string) (rawPayload: string list) =
    let key =
        [ sourcePath; anchor; rawCase ] @ rawPayload
        |> List.map (fun value -> $"{value.Length}:{value}")
        |> String.concat "\u0000"
    let ordinal =
        match occurrenceByAnchor.TryGetValue key with
        | true, value -> value
        | false, _ -> 0
    occurrenceByAnchor.[key] <- ordinal + 1
    { SourcePath = sourcePath
      SemanticDeclarationAnchor = anchor
      SameAnchorOccurrenceOrdinal = ordinal }

let diagnosticsOut = ResizeArray<ExtractionDiagnosticRecord>()

let diagnostic code sourcePath anchor syntaxKind line column rawIdentity =
    diagnosticsOut.Add
        { Code = code
          SourcePath = if String.IsNullOrWhiteSpace sourcePath then null else sourcePath
          SemanticDeclarationAnchor = if String.IsNullOrWhiteSpace anchor then null else anchor
          SyntaxKind = syntaxKind
          Line = line
          Column = column
          RawIdentity = rawIdentity }

let fsharpValue = requireType fsharpCore "Microsoft.FSharp.Reflection.FSharpValue"

let getUnionFields =
    fsharpValue.GetMethods flags
    |> Array.find (fun methodInfo ->
        methodInfo.Name = "GetUnionFields"
        && methodInfo.GetParameters().Length = 3)

let unionFields (value: obj) =
    let pair = getUnionFields.Invoke(null, [| value; value.GetType(); null |])
    let caseInfo = propertyValue pair "Item1"
    propertyValue caseInfo "Name" :?> string, propertyValue pair "Item2" :?> obj array

let rec payloadObjects (value: obj) =
    seq {
        if not (isNull value) then
            yield value
            let candidateType = value.GetType()
            if candidateType.FullName.StartsWith("System.Tuple`", StringComparison.Ordinal) then
                for property in candidateType.GetProperties flags |> Array.filter (fun item -> item.Name.StartsWith("Item", StringComparison.Ordinal)) do
                    yield! payloadObjects (property.GetValue value)
            elif candidateType.FullName.StartsWith("Microsoft.FSharp.Core.FSharpOption`1", StringComparison.Ordinal) then
                yield! payloadObjects (propertyValue value "Value")
            elif not (value :? string) then
                for item in asObjects value do yield! payloadObjects item
    }

let symbolTypes =
    [| "FSharp.Compiler.Symbols.FSharpMemberOrFunctionOrValue"
       "FSharp.Compiler.Symbols.FSharpEntity"
       "FSharp.Compiler.Symbols.FSharpUnionCase"
       "FSharp.Compiler.Symbols.FSharpField" |]
    |> Array.map (requireType fcsAssembly)

let tryPayloadSymbol payload =
    payloadObjects payload
    |> Seq.tryFind (fun value -> symbolTypes |> Array.exists (fun symbolType -> symbolType.IsInstanceOfType value))

let kebabCase (value: string) =
    System.Text.RegularExpressions.Regex.Replace(value, "(?<!^)([A-Z])", "-$1").ToLowerInvariant()

let tryBooleanProperty value name =
    try
        match propertyValue value name with
        | :? bool as result -> Some result
        | _ -> None
    with _ -> None

let hasDeclaringEntity value =
    try propertyValue value "DeclaringEntity" |> optionValue |> Option.isSome
    with _ -> false

let entityHasCapabilityShape entity =
    let abstractMember =
        try propertyValue entity "MembersFunctionsAndValues" |> asObjects |> Seq.exists (fun memberValue -> boolPropertyOrFalse memberValue "IsAbstractMember")
        with _ -> false
    let functionField =
        try propertyValue entity "FSharpFields" |> asObjects |> Seq.exists (fun field -> boolPropertyOrFalse (propertyValue field "FieldType") "IsFunctionType")
        with _ -> false
    boolPropertyOrFalse entity "IsInterface" || boolPropertyOrFalse entity "IsAbstractClass" || abstractMember || functionField

let typeDefinitionIdentity assembly namespaceName compiledName =
    $"{assembly}\u0000{namespaceName}\u0000{compiledName}"

let entityDefinitionIdentity entity =
    try
        let assembly = propertyValue (propertyValue entity "Assembly") "SimpleName" :?> string
        let namespaceName =
            match propertyValue entity "Namespace" |> optionValue with
            | Some (:? string as value) -> value
            | _ -> ""
        let compiledName = propertyValue entity "CompiledName" :?> string
        typeDefinitionIdentity assembly namespaceName compiledName
    with _ -> ""

let systemPureRepresentationNames =
    [ "Boolean"
      "Byte"
      "Char"
      "DateTime"
      "DateTimeOffset"
      "Decimal"
      "Double"
      "Guid"
      "Int16"
      "Int32"
      "Int64"
      "SByte"
      "Single"
      "String"
      "TimeSpan"
      "UInt16"
      "UInt32"
      "UInt64" ]

let pureRepresentationDefinitions =
    seq {
        yield typeDefinitionIdentity "FSharp.Core" "Microsoft.FSharp.Core" "Unit"
        for assembly in [ "System.Runtime"; "netstandard" ] do
            for compiledName in systemPureRepresentationNames do
                yield typeDefinitionIdentity assembly "System" compiledName
    }
    |> fun definitions -> HashSet<string>(definitions, StringComparer.Ordinal)

let immutableGenericContainerDefinitions =
    HashSet<string>(
        [ typeDefinitionIdentity "FSharp.Core" "Microsoft.FSharp.Collections" "FSharpList`1"
          typeDefinitionIdentity "FSharp.Core" "Microsoft.FSharp.Collections" "FSharpMap`2"
          typeDefinitionIdentity "FSharp.Core" "Microsoft.FSharp.Collections" "FSharpSet`1"
          typeDefinitionIdentity "FSharp.Core" "Microsoft.FSharp.Core" "FSharpChoice`2"
          typeDefinitionIdentity "FSharp.Core" "Microsoft.FSharp.Core" "FSharpChoice`3"
          typeDefinitionIdentity "FSharp.Core" "Microsoft.FSharp.Core" "FSharpOption`1"
          typeDefinitionIdentity "FSharp.Core" "Microsoft.FSharp.Core" "FSharpResult`2"
          typeDefinitionIdentity "FSharp.Core" "Microsoft.FSharp.Core" "FSharpValueOption`1" ],
        StringComparer.Ordinal)

let mutableArrayDefinitions =
    HashSet<string>(
        [ typeDefinitionIdentity "FSharp.Core" "Microsoft.FSharp.Core" "array`1" ],
        StringComparer.Ordinal)

type ClosedImmutableDefinition =
    | PrimitiveRepresentation
    | ImmutableGenericContainer
    | MutableArrayContainer
    | UnclassifiedDefinition

let closedImmutableDefinition identity =
    if pureRepresentationDefinitions.Contains identity then PrimitiveRepresentation
    elif immutableGenericContainerDefinitions.Contains identity then ImmutableGenericContainer
    elif mutableArrayDefinitions.Contains identity then MutableArrayContainer
    else UnclassifiedDefinition

if closedImmutableDefinition (typeDefinitionIdentity "System.Runtime" "System" "String") <> PrimitiveRepresentation
   || closedImmutableDefinition (typeDefinitionIdentity "netstandard" "System" "String") <> PrimitiveRepresentation
   || closedImmutableDefinition (typeDefinitionIdentity "fixture" "System" "String") <> UnclassifiedDefinition
   || closedImmutableDefinition (typeDefinitionIdentity "FSharp.Core" "Microsoft.FSharp.Core" "FSharpOption`1") <> ImmutableGenericContainer
   || closedImmutableDefinition (typeDefinitionIdentity "fixture" "Microsoft.FSharp.Core" "FSharpOption`1") <> UnclassifiedDefinition
   || closedImmutableDefinition (typeDefinitionIdentity "FSharp.Core" "Microsoft.FSharp.Core" "array`1") <> MutableArrayContainer
   || closedImmutableDefinition (typeDefinitionIdentity "fixture" "Microsoft.FSharp.Core" "array`1") <> UnclassifiedDefinition
   || closedImmutableDefinition (typeDefinitionIdentity "" "System" "String") <> UnclassifiedDefinition then
    failwith "closed immutable definition classifier is not assembly-qualified"

type ImmutableTypeEvidence =
    | ProvenPure
    | ProvenCapability
    | ProvenMutable
    | ProvenMutableCapability
    | Unproven

let mergeTypeEvidence left right =
    match left, right with
    | Unproven, _
    | _, Unproven -> Unproven
    | ProvenPure, evidence
    | evidence, ProvenPure -> evidence
    | ProvenCapability, ProvenCapability -> ProvenCapability
    | ProvenMutable, ProvenMutable -> ProvenMutable
    | ProvenMutableCapability, _
    | _, ProvenMutableCapability
    | ProvenCapability, ProvenMutable
    | ProvenMutable, ProvenCapability -> ProvenMutableCapability

let typeArguments fullType =
    try propertyValue fullType "GenericArguments" |> asObjects |> Seq.toArray
    with _ -> [||]

let entityFields entity =
    let directFields =
        try propertyValue entity "FSharpFields" |> asObjects |> Seq.toArray
        with _ -> [||]
    let unionFields =
        try
            propertyValue entity "UnionCases"
            |> asObjects
            |> Seq.collect (fun unionCase -> propertyValue unionCase "Fields" |> asObjects)
            |> Seq.toArray
        with _ -> [||]
    Array.append directFields unionFields |> Array.distinct

let entityGenericParameters entity =
    try propertyValue entity "GenericParameters" |> asObjects |> Seq.toArray
    with _ -> [||]

let qualifiedTypeIdentity (assembly: string) (fullName: string) =
    let assemblyIdentity = if String.IsNullOrWhiteSpace assembly then "?" else assembly
    $"{assemblyIdentity.Length}:{assemblyIdentity}\u0000{fullName.Length}:{fullName}"

let typeConstructorIdentity entity =
    let assembly =
        try propertyValue (propertyValue entity "Assembly") "SimpleName" :?> string
        with _ -> ""
    qualifiedTypeIdentity assembly (symbolName entity)

let constructorIdentityCanary = qualifiedTypeIdentity "fixture-a" "Shared.Type"
let unknownAssemblyIdentityCanary = qualifiedTypeIdentity "" "Shared.Type"

if constructorIdentityCanary <> qualifiedTypeIdentity "fixture-a" "Shared.Type"
   || constructorIdentityCanary = qualifiedTypeIdentity "fixture-b" "Shared.Type"
   || constructorIdentityCanary = unknownAssemblyIdentityCanary
   || not (unknownAssemblyIdentityCanary.Contains("?", StringComparison.Ordinal)) then
    failwith "closed type identity is not deterministic and assembly-qualified"

let rec typeShapeIdentity (substitutions: Dictionary<obj, obj>) (resolving: HashSet<obj>) fullType =
    if boolPropertyOrFalse fullType "IsGenericParameter" then
        let parameter = propertyValue fullType "GenericParameter"
        match substitutions.TryGetValue parameter with
        | true, replacement when resolving.Add parameter ->
            let identity = typeShapeIdentity substitutions resolving replacement
            resolving.Remove parameter |> ignore
            identity
        | _ -> "generic:?"
    elif boolPropertyOrFalse fullType "IsFunctionType" then
        typeArguments fullType
        |> Array.map (typeShapeIdentity substitutions resolving)
        |> String.concat ","
        |> sprintf "function<%s>"
    elif boolPropertyOrFalse fullType "IsTupleType" then
        typeArguments fullType
        |> Array.map (typeShapeIdentity substitutions resolving)
        |> String.concat ","
        |> sprintf "tuple<%s>"
    elif boolPropertyOrFalse fullType "HasTypeDefinition" then
        let entity = propertyValue fullType "TypeDefinition"
        typeArguments fullType
        |> Array.map (typeShapeIdentity substitutions resolving)
        |> String.concat ","
        |> fun arguments -> $"{typeConstructorIdentity entity}<{arguments}>"
    else "type:?"

let genericParameterOf fullType =
    if boolPropertyOrFalse fullType "IsGenericParameter" then Some(propertyValue fullType "GenericParameter")
    else None

let rec typeContainsGenericParameter parameter fullType =
    match genericParameterOf fullType with
    | Some candidate -> candidate.Equals parameter
    | None -> typeArguments fullType |> Array.exists (typeContainsGenericParameter parameter)

let withEntityTypeArguments
    (entity: obj)
    (arguments: obj array)
    (substitutions: Dictionary<obj, obj>)
    (classify: unit -> ImmutableTypeEvidence)
    =
    let parameters = entityGenericParameters entity
    if parameters.Length <> arguments.Length then Unproven
    elif
        Array.zip parameters arguments
        |> Array.exists (fun (parameter, argument) ->
            typeContainsGenericParameter parameter argument
            && genericParameterOf argument <> Some parameter)
    then
        Unproven
    else
        let previous = ResizeArray<obj * obj option>()
        for index in 0 .. parameters.Length - 1 do
            let parameter = parameters.[index]
            let argument = arguments.[index]
            if genericParameterOf argument <> Some parameter then
                let prior =
                    match substitutions.TryGetValue parameter with
                    | true, value -> Some value
                    | _ -> None
                previous.Add(parameter, prior)
                substitutions.[parameter] <- argument
        let evidence = classify ()
        for parameter, prior in previous do
            match prior with
            | Some value -> substitutions.[parameter] <- value
            | None -> substitutions.Remove parameter |> ignore
        evidence

let rec immutableTypeEvidence
    (visiting: HashSet<string>)
    (substitutions: Dictionary<obj, obj>)
    (resolving: HashSet<obj>)
    fullType
    =
    if boolPropertyOrFalse fullType "IsFunctionType" then ProvenCapability
    elif boolPropertyOrFalse fullType "IsArrayType" then
        typeArguments fullType
        |> Array.map (immutableTypeEvidence visiting substitutions resolving)
        |> Array.fold mergeTypeEvidence ProvenMutable
    elif boolPropertyOrFalse fullType "IsGenericParameter" then
        let parameter = propertyValue fullType "GenericParameter"
        match substitutions.TryGetValue parameter with
        | true, replacement when resolving.Add parameter ->
            let evidence = immutableTypeEvidence visiting substitutions resolving replacement
            resolving.Remove parameter |> ignore
            evidence
        | _ -> Unproven
    elif boolPropertyOrFalse fullType "IsTupleType" then
        match typeArguments fullType with
        | [||] -> Unproven
        | arguments ->
            arguments
            |> Array.map (immutableTypeEvidence visiting substitutions resolving)
            |> Array.fold mergeTypeEvidence ProvenPure
    elif not (boolPropertyOrFalse fullType "HasTypeDefinition") then Unproven
    else
        let entity = propertyValue fullType "TypeDefinition"
        let constructorIdentity = typeConstructorIdentity entity
        let arguments = typeArguments fullType
        let definition = closedImmutableDefinition (entityDefinitionIdentity entity)
        if constructorIdentity.Contains("?", StringComparison.Ordinal) then
            Unproven
        elif definition = MutableArrayContainer then
            arguments
            |> Array.map (immutableTypeEvidence visiting substitutions resolving)
            |> Array.fold mergeTypeEvidence ProvenMutable
        elif boolPropertyOrFalse entity "IsFSharpAbbreviation" then
            withEntityTypeArguments entity arguments substitutions (fun () ->
                immutableTypeEvidence visiting substitutions resolving (propertyValue entity "AbbreviatedType"))
        elif definition = PrimitiveRepresentation && arguments.Length = 0 then ProvenPure
        elif definition = ImmutableGenericContainer then
            if arguments.Length = 0 then Unproven
            else
                arguments
                |> Array.map (immutableTypeEvidence visiting substitutions resolving)
                |> Array.fold mergeTypeEvidence ProvenPure
        elif boolPropertyOrFalse entity "IsInterface" || boolPropertyOrFalse entity "IsAbstractClass" then ProvenCapability
        elif boolPropertyOrFalse entity "IsFSharpRecord" || boolPropertyOrFalse entity "IsFSharpUnion" then
            let cycleIdentity =
                typeShapeIdentity substitutions (HashSet<obj>()) fullType
            if visiting.Contains cycleIdentity then ProvenPure
            else
                visiting.Add cycleIdentity |> ignore
                let evidence = withEntityTypeArguments entity arguments substitutions (fun () ->
                    entityFields entity
                    |> Array.map (fun field ->
                        let fieldEvidence =
                            immutableTypeEvidence visiting substitutions resolving (propertyValue field "FieldType")
                        if boolPropertyOrFalse field "IsMutable" then mergeTypeEvidence ProvenMutable fieldEvidence
                        else fieldEvidence)
                    |> Array.fold mergeTypeEvidence ProvenPure)
                visiting.Remove cycleIdentity |> ignore
                evidence
        else Unproven

let immutableTypeEvidenceCache = Dictionary<string, ImmutableTypeEvidence>(StringComparer.Ordinal)

let immutableTypeNodeKind pureKind capabilityKind mutableKind combinedKind fallbackKind fullType =
    try
        let substitutions = Dictionary<obj, obj>()
        let typeIdentity = typeShapeIdentity substitutions (HashSet<obj>()) fullType
        let evidence =
            match immutableTypeEvidenceCache.TryGetValue typeIdentity with
            | true, cached -> cached
            | _ ->
                let extracted =
                    immutableTypeEvidence
                        (HashSet<string>(StringComparer.Ordinal))
                        substitutions
                        (HashSet<obj>())
                        fullType
                if not (typeIdentity.Contains("?", StringComparison.Ordinal)) then
                    immutableTypeEvidenceCache.[typeIdentity] <- extracted
                extracted
        match evidence with
        | ProvenPure -> pureKind
        | ProvenCapability -> capabilityKind
        | ProvenMutable -> mutableKind
        | ProvenMutableCapability -> combinedKind
        | Unproven -> fallbackKind
    with _ -> fallbackKind

let immutableValueNodeKind symbol =
    try
        immutableTypeNodeKind
            "pure-immutable-value"
            "capability-immutable-value"
            "mutable-container-value"
            "capability-mutable-container-value"
            "immutable-value"
            (propertyValue symbol "FullType")
    with _ -> "immutable-value"

let immutableFieldNodeKind field =
    try
        immutableTypeNodeKind
            "immutable-field-get"
            "capability-immutable-field-get"
            "mutable-container-field-get"
            "capability-mutable-container-field-get"
            "f-sharp-field-get"
            (propertyValue field "FieldType")
    with _ -> "f-sharp-field-get"

let resolvedFSharpNodeKind patternName payload =
    let fallback = kebabCase patternName
    match patternName, tryPayloadSymbol payload with
    | "Value", Some symbol ->
        match tryBooleanProperty symbol "IsMutable", tryBooleanProperty symbol "IsModuleValueOrMember" with
        | Some false, _ -> immutableValueNodeKind symbol
        | Some true, Some true -> "module-mutable-value-read"
        | Some true, Some false ->
            if hasDeclaringEntity symbol then "captured-mutable-value-read"
            else "local-mutable-value-read"
        | _ -> fallback
    | "CallWithWitnesses", Some symbol ->
        match tryBooleanProperty symbol "IsMutable", tryBooleanProperty symbol "IsModuleValueOrMember" with
        | Some true, Some true -> "module-mutable-value-read"
        | Some true, Some false ->
            if hasDeclaringEntity symbol then "captured-mutable-value-read"
            else "local-mutable-value-read"
        | _ -> fallback
    | "ValueSet", Some symbol ->
        match tryBooleanProperty symbol "IsMutable", tryBooleanProperty symbol "IsModuleValueOrMember" with
        | Some true, Some true -> "module-mutable-value-set"
        | Some true, Some false ->
            if hasDeclaringEntity symbol then "captured-mutable-value-set"
            else "local-mutable-value-set"
        | _ -> fallback
    | "FSharpFieldGet", Some field ->
        match tryBooleanProperty field "IsMutable" with
        | Some false -> immutableFieldNodeKind field
        | Some true -> "mutable-field-get"
        | None -> fallback
    | "FSharpFieldSet", Some field ->
        match tryBooleanProperty field "IsMutable" with
        | Some true -> "mutable-field-set"
        | _ -> fallback
    | _ -> fallback

let expressionType = requireType fcsAssembly "FSharp.Compiler.Symbols.FSharpExpr"
let declarationType = requireType fcsAssembly "FSharp.Compiler.Symbols.FSharpImplementationFileDeclaration"
let expressionPatterns = requireType fcsAssembly "FSharp.Compiler.Symbols.FSharpExprPatterns"

let expressionPatternMethods =
    expressionPatterns.GetMethods flags
    |> Array.filter (fun methodInfo ->
        methodInfo.IsStatic
        && methodInfo.Name.StartsWith("|", StringComparison.Ordinal)
        && methodInfo.GetParameters().Length = 1
        && methodInfo.GetParameters().[0].ParameterType = expressionType)
    |> Array.sortBy (fun methodInfo -> methodInfo.Name)

let expressionPattern (expression: obj) =
    expressionPatternMethods
    |> Array.tryPick (fun methodInfo ->
        match optionValue (methodInfo.Invoke(null, [| expression |])) with
        | None -> None
        | Some payload ->
            let pieces = methodInfo.Name.Split '|'
            Some(pieces.[1], payload))

let immediateExpressions expression =
    propertyValue expression "ImmediateSubExpressions"
    |> asObjects
    |> Seq.filter expressionType.IsInstanceOfType
    |> Seq.toArray

let constantString expression =
    match expressionPattern expression with
    | Some("Const", payload) -> payloadObjects payload |> Seq.tryPick (function :? string as text -> Some text | _ -> None)
    | _ -> None

let fsharpNodesOut = ResizeArray<FSharpNodeRecord>()
let fableInteropOut = ResizeArray<obj>()
let fableInteropKeys = HashSet<string>(StringComparer.Ordinal)

let addFableImport moduleSpecifier selector (site: ObservationSite) =
    let key = $"fable-import\u0000{moduleSpecifier}\u0000{selector}\u0000{site.SourcePath}\u0000{site.SemanticDeclarationAnchor}\u0000{site.SameAnchorOccurrenceOrdinal}"
    if fableInteropKeys.Add key then
        fableInteropOut.Add(
            dict [
                "kind", box "fable-import"
                "moduleSpecifier", box moduleSpecifier
                "selector", box selector
                "site", box site
            ])

let addFableEmit kind expression (site: ObservationSite) =
    let key = $"{kind}\u0000{expression}\u0000{site.SourcePath}\u0000{site.SemanticDeclarationAnchor}\u0000{site.SameAnchorOccurrenceOrdinal}"
    if fableInteropKeys.Add key then
        fableInteropOut.Add(
            dict [
                "kind", box kind
                "expression", box expression
                "site", box site
            ])

let rec visitExpression sourcePath anchor expression =
    let line, column =
        let _, line, column = sourceRange expression
        line, column
    match expressionPattern expression with
    | None ->
        diagnostic "unsupported-fsharp-expression" sourcePath anchor "unknown" line column (expression.GetType().FullName)
    | Some(patternName, payload) ->
        let nodeKind = resolvedFSharpNodeKind patternName payload
        let identity =
            match patternName with
            | "Value"
            | "CallWithWitnesses"
            | "ValueSet"
            | "FSharpFieldGet"
            | "FSharpFieldSet" ->
                tryPayloadSymbol payload
                |> Option.map symbolName
                |> Option.defaultValue $"fsharp:{nodeKind}"
            | _ -> $"fsharp:{nodeKind}"
        fsharpNodesOut.Add
            { NodeKind = nodeKind
              SemanticIdentity = identity
              Site = observationSite sourcePath anchor "fsharp-node" [ nodeKind; identity ] }
        if identity.EndsWith("emitJsExpr", StringComparison.Ordinal) then
            immediateExpressions expression
            |> Array.choose constantString
            |> Array.tryLast
            |> function
                | Some emitted -> addFableEmit "emit-js-expr" emitted (observationSite sourcePath anchor "emit-js-expr" [ emitted ])
                | None -> diagnostic "dynamic-emit-expression" sourcePath anchor nodeKind line column identity
    for child in immediateExpressions expression do visitExpression sourcePath anchor child

let rec valuesOfType (candidateType: Type) (value: obj) =
    seq {
        if not (isNull value) then
            if candidateType.IsInstanceOfType value then yield value
            elif not (value :? string) then
                for item in asObjects value do yield! valuesOfType candidateType item
    }

let rec visitDeclaration sourcePath fallbackAnchor declaration =
    let caseName, fields = unionFields declaration
    let anchor =
        fields
        |> Array.tryPick (fun field ->
            symbolTypes
            |> Array.tryFind (fun symbolType -> symbolType.IsInstanceOfType field)
            |> Option.map (fun _ -> symbolName field))
        |> Option.defaultValue fallbackAnchor
    for field in fields do
        for expression in valuesOfType expressionType field do visitExpression sourcePath anchor expression
        for nested in valuesOfType declarationType field do
            if not (obj.ReferenceEquals(nested, declaration)) then visitDeclaration sourcePath anchor nested

let parseAndCheckFile =
    checkerType.GetMethods flags
    |> Array.find (fun methodInfo ->
        methodInfo.Name = "ParseAndCheckFileInProject"
        && methodInfo.GetParameters().Length = 5
        && methodInfo.GetParameters().[1].ParameterType = typeof<int>)

let sourceTextOfString =
    requireType fcsAssembly "FSharp.Compiler.Text.SourceText"
    |> fun candidate -> candidate.GetMethods flags
    |> Array.find (fun methodInfo -> methodInfo.Name = "ofString" && methodInfo.IsStatic)

let implementationFiles =
    sourceFiles
    |> Array.filter (fun path -> isProductionPath path && Path.GetExtension path = ".fs")
    |> Array.choose (fun sourcePath ->
        let sourceText = sourceTextOfString.Invoke(null, [| File.ReadAllText sourcePath |])
        let check = parseAndCheckFile.Invoke(checker, [| sourcePath; box 0; sourceText; implementationProjectOptions; null |])
        let resultType = parseAndCheckFile.ReturnType.GetGenericArguments().[0]
        let pair = runAsync resultType check
        let answer = propertyValue pair "Item2"
        let answerCase, answerFields = unionFields answer
        let fileResult = if answerCase = "Succeeded" then Some answerFields.[0] else None
        match fileResult |> Option.bind (fun result -> optionValue (propertyValue result "ImplementationFile")) with
        | Some implementationFile -> Some implementationFile
        | None ->
            diagnostic
                "fsharp-implementation-file-missing"
                (normalizePath sourcePath)
                ("source:" + normalizePath sourcePath)
                "implementation-file"
                0
                0
                (normalizePath sourcePath)
            None)

for implementationFile in implementationFiles do
    let sourcePath = propertyValue implementationFile "FileName" :?> string |> normalizePath
    if isProductionPath sourcePath && Path.GetExtension sourcePath = ".fs" then
        let anchor = propertyValue implementationFile "QualifiedName" :?> string
        for declaration in propertyValue implementationFile "Declarations" |> asObjects do
            visitDeclaration sourcePath anchor declaration
let symbolLocations symbol =
    [| "DeclarationLocation"; "ImplementationLocation"; "SignatureLocation" |]
    |> Array.choose (fun name ->
        try
            let value = propertyValue symbol name
            if isNull value then None
            elif value.GetType().FullName.StartsWith("Microsoft.FSharp.Core.FSharpOption`1", StringComparison.Ordinal) then
                Some(propertyValue value "Value")
            else Some value
        with _ -> None)
    |> Array.map (fun range -> propertyValue range "FileName" :?> string)
    |> Array.filter (String.IsNullOrWhiteSpace >> not)
    |> Array.map normalizePath
    |> Array.distinct

let symbolUses =
    checkResultType.GetMethod("GetAllUsesOfAllSymbols", flags).Invoke(checkResult, [| null |]) :?> Array
    |> Seq.cast<obj>
    |> Seq.sortBy (fun symbolUse ->
        let range = propertyValue symbolUse "Range"
        propertyValue symbolUse "FileName" :?> string,
        propertyValue range "StartLine" :?> int,
        propertyValue range "StartColumn" :?> int,
        symbolName (propertyValue symbolUse "Symbol"))
    |> Seq.toArray

let declarationUses =
    symbolUses
    |> Seq.choose (fun symbolUse ->
        let consumer = propertyValue symbolUse "FileName" :?> string
        let isDefinition = propertyValue symbolUse "IsFromDefinition" :?> bool
        if isDefinition || not (isProductionPath consumer) || Path.GetExtension consumer <> ".fs" then None
        else
            let symbol = propertyValue symbolUse "Symbol"
            let providers = symbolLocations symbol |> Array.filter isProductionPath |> Array.distinct
            let isNamespace = boolPropertyOrFalse symbol "IsNamespace"
            if providers.Length = 0 || isNamespace then None
            else
                let range = propertyValue symbolUse "Range"
                Some
                    { ConsumerPath = normalizePath consumer
                      ProviderPaths = providers
                      Symbol = symbolName symbol
                      SymbolKind = symbol.GetType().Name
                      Assembly = propertyValue (propertyValue symbol "Assembly") "SimpleName" :?> string
                      IsNamespace = isNamespace
                      IsModule = boolPropertyOrFalse symbol "IsFSharpModule"
                      Line = propertyValue range "StartLine" :?> int
                      Column = propertyValue range "StartColumn" :?> int
                      IsFromOpenStatement = propertyValue symbolUse "IsFromOpenStatement" :?> bool
                      IsFromPattern = propertyValue symbolUse "IsFromPattern" :?> bool
                      IsFromType = propertyValue symbolUse "IsFromType" :?> bool
                      IsFromUse = propertyValue symbolUse "IsFromUse" :?> bool })
    |> Seq.distinct
    |> Seq.sortBy (fun item -> item.ConsumerPath, item.ProviderPaths, item.Symbol, item.Line, item.Column)
    |> Seq.toArray

let externalSymbolOccurrences =
    symbolUses
    |> Seq.choose (fun symbolUse ->
        let consumer = propertyValue symbolUse "FileName" :?> string |> normalizePath
        let isDefinition = propertyValue symbolUse "IsFromDefinition" :?> bool
        if isDefinition || not (isProductionPath consumer) || Path.GetExtension consumer <> ".fs" then None
        else
            let symbol = propertyValue symbolUse "Symbol"
            let providers = symbolLocations symbol |> Array.filter isProductionPath
            if providers.Length > 0 then None
            else
                let range = propertyValue symbolUse "Range"
                let line, column = rangeStart range
                let identity = symbolName symbol
                let assembly =
                    try propertyValue (propertyValue symbol "Assembly") "SimpleName" :?> string
                    with _ -> ""
                if String.IsNullOrWhiteSpace identity || String.IsNullOrWhiteSpace assembly then
                    diagnostic "unresolved-external-symbol" consumer ("source:" + consumer) (symbol.GetType().Name) line column identity
                    None
                else
                    Some(consumer, assembly, identity, symbol.GetType().Name, line, column))
    |> Seq.distinct
    |> Seq.sortBy (fun (consumer, assembly, identity, symbolKind, line, column) ->
        consumer, line, column, assembly, identity, symbolKind)
    |> Seq.toArray

let externalSymbolUses =
    externalSymbolOccurrences
    |> Array.map (fun (consumer, assembly, identity, symbolKind, _, _) ->
        { Assembly = assembly
          FullyQualifiedSymbol = identity
          SymbolKind = symbolKind
          Site = observationSite consumer ("source:" + consumer) "fcs-external-symbol-use" [ assembly; identity ] })

let attributeStrings attribute =
    try
        propertyValue attribute "ConstructorArguments"
        |> payloadObjects
        |> Seq.choose (function :? string as text -> Some text | _ -> None)
        |> Seq.toArray
    with _ -> [||]

let definitionSymbols =
    symbolUses
    |> Seq.filter (fun symbolUse -> propertyValue symbolUse "IsFromDefinition" :?> bool)
    |> Seq.map (fun symbolUse ->
        propertyValue symbolUse "FileName" :?> string |> normalizePath,
        propertyValue symbolUse "Range",
        propertyValue symbolUse "Symbol")
    |> Seq.filter (fun (sourcePath, _, _) -> isProductionPath sourcePath)
    |> Seq.toArray

for sourcePath, range, symbol in definitionSymbols do
    let identity = symbolName symbol
    let line, column = rangeStart range
    let interopSourcePath =
        if sourcePath.EndsWith(".fsi", StringComparison.Ordinal) then sourcePath.Substring(0, sourcePath.Length - 1)
        else sourcePath
    try
        for attribute in propertyValue symbol "Attributes" |> asObjects do
            let attributeName = symbolName (propertyValue attribute "AttributeType")
            let arguments = attributeStrings attribute
            if attributeName.EndsWith("ImportAttribute", StringComparison.Ordinal) then
                if arguments.Length >= 2 then
                    addFableImport arguments.[1] arguments.[0] (observationSite interopSourcePath identity "fable-import" [ arguments.[1]; arguments.[0] ])
                else
                    diagnostic "unparsed-fable-import" interopSourcePath identity attributeName line column identity
            elif attributeName.EndsWith("EmitAttribute", StringComparison.Ordinal) then
                if arguments.Length >= 1 then
                    addFableEmit "fable-emit" arguments.[0] (observationSite interopSourcePath identity "fable-emit" [ arguments.[0] ])
                else
                    diagnostic "unparsed-fable-emit" interopSourcePath identity attributeName line column identity
    with _ -> ()

let isPublic symbol =
    try boolPropertyOrFalse (propertyValue symbol "Accessibility") "IsPublic"
    with _ -> false

let hasFunctionType value =
    try boolPropertyOrFalse (propertyValue value "FullType") "IsFunctionType"
    with _ -> false

let capabilityEntity entity =
    entityHasCapabilityShape entity

let signatureExports =
    definitionSymbols
    |> Seq.choose (fun (sourcePath, range, symbol) ->
        if Path.GetExtension sourcePath <> ".fsi" || not (isPublic symbol) then None
        else
            let identity = symbolName symbol
            let symbolKind = symbol.GetType().Name
            let line, column = rangeStart range
            let exportKind =
                if symbolKind = "FSharpEntity" then
                    if boolPropertyOrFalse symbol "IsNamespace" || boolPropertyOrFalse symbol "IsFSharpModule" then None
                    elif capabilityEntity symbol then Some "capability-type"
                    else Some "pure-type"
                elif symbolKind = "FSharpMemberOrFunctionOrValue" then
                    if boolPropertyOrFalse symbol "IsMutable" then None
                    elif hasFunctionType symbol then Some "pure-function"
                    else Some "pure-value"
                elif symbolKind = "FSharpField" then Some "pure-value"
                elif symbolKind = "FSharpUnionCase" then
                    let hasFields =
                        try propertyValue symbol "Fields" |> asObjects |> Seq.isEmpty |> not
                        with _ -> false
                    Some(if hasFields then "pure-function" else "pure-value")
                else None
            match exportKind with
            | Some kind ->
                Some
                    { ExportKind = kind
                      DeclarationIdentity = identity
                      Site = observationSite sourcePath identity "public-signature-export" [ kind; identity ] }
            | None ->
                let containedDeclaration =
                    [ "FSharpParameter"; "FSharpGenericParameter" ]
                    |> List.contains symbolKind
                if not containedDeclaration && not (boolPropertyOrFalse symbol "IsNamespace" || boolPropertyOrFalse symbol "IsFSharpModule") then
                    diagnostic "unclassified-signature-export" sourcePath identity symbolKind line column identity
                None)
    |> Seq.distinct
    |> Seq.toArray

stopwatch.Stop()
let result =
    { SchemaVersion = 1
      ProjectFile = normalizePath projectFile
      ProductionFiles = sourceFiles |> Array.filter (fun path -> isProductionPath path && Path.GetExtension path = ".fs") |> Array.map normalizePath |> Array.sort
      SignatureFiles = sourceFiles |> Array.filter (fun path -> isProductionPath path && Path.GetExtension path = ".fsi") |> Array.map normalizePath |> Array.sort
      DeclarationUses = declarationUses
      ExternalSymbolUses = externalSymbolUses
      FsharpNodes = fsharpNodesOut |> Seq.distinct |> Seq.toArray
      FableInterop = fableInteropOut.ToArray()
      SignatureExports = signatureExports
      Diagnostics = diagnosticsOut.ToArray()
      ElapsedMilliseconds = stopwatch.ElapsedMilliseconds }

Directory.CreateDirectory(Path.GetDirectoryName resultPath) |> ignore
let jsonOptions = JsonSerializerOptions(WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
File.WriteAllText(resultPath, JsonSerializer.Serialize(result, jsonOptions))
