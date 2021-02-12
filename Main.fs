open System
open System.Diagnostics;
open System.IO
open System.IO.Compression
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
        Directory.CreateDirectory(folderPath) |> ignore
        File.WriteAllText($"{folderPath}/{modelSharedCounters.[0]}_{ruleName}.vbs", $"/*\nCondition: '{condition}'\n*/\n\n{implication}")
    )

let exportModelFile(modelFileEntry: ZipArchiveEntry) =
    let modelFileName = modelFileEntry.Name
    let modelName = string modelFileName.[0] + modelFileName.Substring(1, modelFileName.IndexOf(',') - 1).ToLower()
    let modelSharedCounters = [| 0 |]

    if Directory.Exists modelName then
        Directory.Delete(modelName, true)

    printfn $"Exporting model: {modelName}"

    use modelFileStream = modelFileEntry.Open()
    XDocument.Load(modelFileStream)
             .Element(XName.Get "CLASS")
             .Element(XName.Get "RULES")
             .Elements(XName.Get "RULES") |> Seq.iter(fun k -> exportRulesFromFolder(k, modelName, modelSharedCounters))


match Directory.GetFiles "." |> Seq.tryFind(fun k -> k.EndsWith ".zip") with
    | None -> printfn "No input .zip file found in current folder!"
    | Some inputZipFile ->
        printfn $"Using zip file: '{inputZipFile |> Path.GetFileName}'\n"
        printfn "Enter commit message!"

        let commitMessage = Console.ReadLine()
        use zipFile = ZipFile.OpenRead(inputZipFile)

        zipFile.Entries |> Seq.filter(fun k -> k.Name.EndsWith(".xml") && k.Name.[0] <> 'I')
                        |> Seq.iter(exportModelFile)

        File.Delete(inputZipFile)

        printfn "Publishing to GitHub"
        Process.Start("cmd.exe", $"/c \"git config --global core.autocrlf true && git add -A 2>nul && git commit -m \"{commitMessage}\" 2>nul && git push origin master 2>nul\"")
               .WaitForExit()

printfn "\nDone! Press enter to exit"
Console.Read() |> ignore