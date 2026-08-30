open System
open System.IO
open System.Reflection
open System.Runtime.Loader
open System.Text.Json

type SymbolUseRecord =
    {
        ConsumerPath: string
        ProviderPaths: string array
        Symbol: string
        SymbolKind: string
        Assembly: string
        Line: int
        Column: int
        IsNamespace: bool
        IsModule: bool
        IsFromOpenStatement: bool
        IsFromPattern: bool
        IsFromType: bool
        IsFromUse: bool
        MissingDeclaration: bool
    }

type ScanResult =
    {
        ProjectFile: string
        ProjectAssembly: string
        ProductionFiles: string array
        SymbolUses: SymbolUseRecord array
    }

let arguments = fsi.CommandLineArgs |> Array.skip 1

if arguments.Length <> 6 then
    eprintfn "usage: owner-symbol-uses.fsx <project> <production-root> <scratch-root> <tool-dir> <fable-library> <result-json>"
    exit 2

let projectFile = Path.GetFullPath arguments.[0]
let productionRoot = Path.GetFullPath(arguments.[1]).TrimEnd(Path.DirectorySeparatorChar) + string Path.DirectorySeparatorChar
let scratchRoot = Path.GetFullPath arguments.[2]
let toolDirectory = Path.GetFullPath arguments.[3]
let fableLibraryPath = Path.GetFullPath arguments.[4]
let resultPath = Path.GetFullPath arguments.[5]

type ToolLoadContext(directory: string) =
    inherit AssemblyLoadContext("owner-symbol-uses", true)

    override this.Load(name: AssemblyName) =
        let candidate = Path.Combine(directory, name.Name + ".dll")

        if File.Exists candidate then
            this.LoadFromAssemblyPath candidate
        else
            null

let context = ToolLoadContext toolDirectory

let load name =
    let path = Path.Combine(toolDirectory, name)

    if not (File.Exists path) then
        failwith $"missing Fable tool assembly: {path}"

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
    let properties =
        candidate.GetProperties flags
        |> Array.filter (fun property -> property.Name = name && property.GetIndexParameters().Length = 0)

    match properties |> Array.tryFind (fun property -> property.DeclaringType = candidate) with
    | Some property -> property
    | None ->
        match properties |> Array.tryHead with
        | Some property -> property
        | None -> failwith $"missing property: {candidate.FullName}.{name}"

let propertyValue (target: obj) name =
    requireProperty (target.GetType()) name
    |> fun property -> property.GetValue target

let staticPropertyValue (candidate: Type) name =
    requireProperty candidate name
    |> fun property -> property.GetValue null

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
    |> Array.find (fun methodInfo ->
        methodInfo.Name = "Empty"
        && methodInfo.IsGenericMethodDefinition
        && methodInfo.GetGenericArguments().Length = 2)
    |> fun methodInfo -> methodInfo.MakeGenericMethod [| keyType; valueType |]
    |> fun methodInfo -> methodInfo.Invoke(null, [||])

let some valueType value =
    requireType fsharpCore "Microsoft.FSharp.Core.FSharpOption`1"
    |> fun candidate -> candidate.MakeGenericType [| valueType |]
    |> fun candidate -> candidate.GetMethod("Some", flags).Invoke(null, [| value |])

let optionValue (value: obj) =
    if isNull value then
        None
    elif value.GetType().FullName.StartsWith("Microsoft.FSharp.Core.FSharpOption`1", StringComparison.Ordinal) then
        Some(propertyValue value "Value")
    else
        Some value

let optionalPropertyValue target name =
    try
        propertyValue target name |> optionValue
    with
    | :? TargetInvocationException as error
        when not (isNull error.InnerException)
             && error.InnerException.Message.EndsWith("property not available", StringComparison.Ordinal) ->
        None

let optionalBooleanProperty target name =
    try
        match propertyValue target name with
        | :? bool as value -> value
        | _ -> false
    with _ ->
        false

let normalizePath (path: string) =
    Path.GetFullPath(path).Replace(Path.DirectorySeparatorChar, '/')

let isProductionPath path =
    Path.GetFullPath(path).StartsWith(productionRoot, StringComparison.Ordinal)

let language =
    requireType astAssembly "Fable.Language"
    |> fun candidate -> staticPropertyValue candidate "JavaScript"

let verbosity =
    requireType astAssembly "Fable.Verbosity"
    |> fun candidate -> staticPropertyValue candidate "Silent"

let majorVersion = compilerAssembly.GetName().Version.Major

let defines =
    [|
        box "FABLE_COMPILER"
        box $"FABLE_COMPILER_{majorVersion}"
        box "FABLE_COMPILER_JAVASCRIPT"
    |]

let compilerOptions =
    invokeConstructor
        (requireType astAssembly "Fable.CompilerOptions")
        [|
            box true
            box false
            language
            listOfValues typeof<string> defines
            box false
            box false
            verbosity
            box ".fs.js"
            box false
            box false
        |]

let cliArgs =
    invokeConstructor
        (requireType compilerAssembly "Fable.Compiler.Util+CliArgs")
        [|
            box projectFile
            box (Path.GetDirectoryName projectFile)
            some typeof<string> (box scratchRoot)
            box false
            box false
            null
            box false
            some typeof<string> (box fableLibraryPath)
            box "Debug"
            box false
            box true
            box true
            box false
            box false
            null
            emptyList typeof<string>
            emptyMap typeof<string> typeof<string>
            null
            compilerOptions
            verbosity
        |]

let crackerOptions =
    invokeConstructor
        (requireType compilerAssembly "Fable.Compiler.ProjectCracker+CrackerOptions")
        [| cliArgs; box false |]

let resolver =
    invokeConstructor (requireType compilerAssembly "Fable.Compiler.MSBuildCrackerResolver") [||]

let projectResponse =
    requireType compilerAssembly "Fable.Compiler.ProjectCracker"
    |> fun candidate -> candidate.GetMethod("getFullProjectOpts", flags)
    |> fun methodInfo -> methodInfo.Invoke(null, [| resolver; crackerOptions |])

let projectOptions = propertyValue projectResponse "ProjectOptions"
let sourceFiles = propertyValue projectOptions "SourceFiles" :?> string array
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

let checkResult =
    requireType fsharpCore "Microsoft.FSharp.Control.FSharpAsync"
    |> fun candidate -> candidate.GetMethods flags
    |> Array.find (fun methodInfo ->
        methodInfo.Name = "RunSynchronously"
        && methodInfo.IsGenericMethodDefinition
        && methodInfo.GetParameters().Length = 3)
    |> fun methodInfo -> methodInfo.MakeGenericMethod [| checkResultType |]
    |> fun methodInfo -> methodInfo.Invoke(null, [| checkAsync; null; null |])

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

let projectAssembly =
    let fullName = propertyValue checkResult "AssemblyFullName" :?> string
    AssemblyName(fullName).Name

let symbolName symbol =
    try
        match propertyValue symbol "FullName" with
        | :? string as value when not (String.IsNullOrWhiteSpace value) -> value
        | _ -> propertyValue symbol "DisplayName" :?> string
    with _ ->
        propertyValue symbol "DisplayName" :?> string

let symbolLocations symbol =
    [| "DeclarationLocation"; "ImplementationLocation"; "SignatureLocation" |]
    |> Array.choose (optionalPropertyValue symbol)
    |> Array.map (fun range -> propertyValue range "FileName" :?> string)
    |> Array.filter (String.IsNullOrWhiteSpace >> not)
    |> Array.map normalizePath
    |> Array.distinct

let symbolUses =
    checkResultType.GetMethod("GetAllUsesOfAllSymbols", flags).Invoke(checkResult, [| null |]) :?> Array

let records =
    symbolUses
    |> Seq.cast<obj>
    |> Seq.choose (fun symbolUse ->
        let consumer = propertyValue symbolUse "FileName" :?> string
        let normalizedConsumer = normalizePath consumer
        let isDefinition = propertyValue symbolUse "IsFromDefinition" :?> bool

        if isDefinition || not (isProductionPath consumer) then
            None
        else
            let symbol = propertyValue symbolUse "Symbol"
            let locations = symbolLocations symbol
            let providers =
                locations
                |> Array.filter isProductionPath
                |> Array.filter ((<>) normalizedConsumer)
                |> Array.distinct

            let assembly = propertyValue (propertyValue symbol "Assembly") "SimpleName" :?> string
            let missingDeclaration = locations.Length = 0 && assembly = projectAssembly
            let isFromOpenStatement = propertyValue symbolUse "IsFromOpenStatement" :?> bool
            let isNamespace = optionalBooleanProperty symbol "IsNamespace"
            let isModule = optionalBooleanProperty symbol "IsFSharpModule"

            if isFromOpenStatement || isNamespace || isModule || (providers.Length = 0 && not missingDeclaration) then
                None
            else
                let range = propertyValue symbolUse "Range"

                Some
                    {
                        ConsumerPath = normalizedConsumer
                        ProviderPaths = providers
                        Symbol = symbolName symbol
                        SymbolKind = symbol.GetType().Name
                        Assembly = assembly
                        Line = propertyValue range "StartLine" :?> int
                        Column = propertyValue range "StartColumn" :?> int
                        IsNamespace = isNamespace
                        IsModule = isModule
                        IsFromOpenStatement = isFromOpenStatement
                        IsFromPattern = propertyValue symbolUse "IsFromPattern" :?> bool
                        IsFromType = propertyValue symbolUse "IsFromType" :?> bool
                        IsFromUse = propertyValue symbolUse "IsFromUse" :?> bool
                        MissingDeclaration = missingDeclaration
                    })
    |> Seq.distinct
    |> Seq.sortBy (fun item -> item.ConsumerPath, item.ProviderPaths, item.Symbol, item.Line, item.Column)
    |> Seq.toArray

let result =
    {
        ProjectFile = normalizePath projectFile
        ProjectAssembly = projectAssembly
        ProductionFiles =
            sourceFiles
            |> Array.filter isProductionPath
            |> Array.map normalizePath
            |> Array.sort
        SymbolUses = records
    }

Directory.CreateDirectory(Path.GetDirectoryName resultPath) |> ignore

let jsonOptions = JsonSerializerOptions(WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
File.WriteAllText(resultPath, JsonSerializer.Serialize(result, jsonOptions))
