open System
open System.Diagnostics
open System.IO
open System.IO.Compression
open System.Reflection
open System.Xml.Linq

let rec exportRulesFromFolder(folder: XElement, modelName: string, modelSharedCounters: int[]) =
    let folderName = folder.Attribute(XName.Get "LABEL").Value
    let folderPath = $"{modelName}/{folderName}"

    folder.Elements(XName.Get "RULES") |> Seq.iter(fun k -> exportRulesFromFolder(k, $"{modelName}/{folderName}", modelSharedCounters))
    folder.Elements(XName.Get "RULE") |> Seq.iter(fun rule ->
        let ruleName = rule.Attribute(XName.Get "LABEL").Value.Replace('/', '_')
        let condition = rule.Element(XName.Get "CONDITION").Value
        let implication = rule.Element(XName.Get "IMPLICATION").Value

        modelSharedCounters.[0] <- modelSharedCounters.[0] + 1
        Directory.CreateDirectory folderPath |> ignore
        File.WriteAllText($"{folderPath}/{modelSharedCounters.[0]}_{ruleName}.vbs", $"/*\nCondition: '{condition}'\n*/\n\n{implication}")
    )

let exportModelFile(modelFileEntry: ZipArchiveEntry) =
    use modelFileStream = modelFileEntry.Open()
    let topClassElement = XDocument.Load(modelFileStream).Element(XName.Get "CLASS")
    let modelFileName = modelFileEntry.Name
    let isInterface = topClassElement.Attribute(XName.Get "ISINTERFACE").Value = "true"
    let nameUpperEnd = if isInterface then 2 else 1
    let modelName = modelFileName.Substring(0, nameUpperEnd) + modelFileName.Substring(nameUpperEnd, modelFileName.IndexOf(',') - nameUpperEnd).ToLower()

    if Directory.Exists modelName then
        Directory.Delete(modelName, true)

    Directory.CreateDirectory modelName |> ignore
    printfn $"Exporting model: {modelName}"

    let modelSharedCounters = [| 0 |]
    let propertiesElement = topClassElement.Element(XName.Get "PROPERTIES").ToString()
    let referencesElement = topClassElement.Element(XName.Get "REFERENCES").ToString()

    File.WriteAllText($"{modelName}/Structure.xml", propertiesElement + "\n" + referencesElement);

    if not isInterface then
        topClassElement.Element(XName.Get "RULES")
                       .Elements(XName.Get "RULES") |> Seq.iter(fun k -> exportRulesFromFolder(k, modelName, modelSharedCounters))


printfn $"Exporter version: {Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion}"
match Directory.GetFiles "." |> Seq.tryFind(fun k -> k.EndsWith ".zip") with
    | None -> printfn "No input .zip file found in current folder!"
    | Some inputZipFile ->
        printfn $"Using zip file: '{inputZipFile |> Path.GetFileName}'\n"
        printfn "Enter commit message!"

        let commitMessage = Console.ReadLine()
        let zipFile = ZipFile.OpenRead(inputZipFile)

        zipFile.Entries |> Seq.filter(fun k -> k.Name.EndsWith(".xml"))
                        |> Seq.iter(exportModelFile)

        zipFile.Dispose()
        File.Delete(inputZipFile)

        printfn "\nPublishing to GitHub"
        Process.Start("cmd.exe", $"/c \"git reset && git add -A 2>nul && git commit -m \"{commitMessage}\" 2>nul && git push origin master 2>nul\"")
               .WaitForExit()

printfn "Done! Press enter to exit"
Console.Read() |> ignore