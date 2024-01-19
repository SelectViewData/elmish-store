open Fake
open Fake.Core
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.Core.TargetOperators

open BuildHelpers
open BuildTools

initializeContext ()

let srcPath = Path.getFullName "src"
let clientSrcPath = srcPath </> "ElmishStore.Example"

let clean proj =
  [ proj </> "bin"; proj </> "obj"; proj </> ".fable-build" ] |> Shell.cleanDirs

Target.create "Clean" (fun _ -> clean clientSrcPath)

Target.create "InstallClient" (fun _ ->
  printfn "Node version:"
  run Tools.node "--version" clientSrcPath
  printfn "Yarn version:"
  run Tools.yarn "--version" clientSrcPath
  run Tools.yarn "install --frozen-lockfile" clientSrcPath)

Target.create "Run" (fun _ -> run Tools.yarn "start" "")

let dependencies = [ "InstallClient" ==> "Clean" ==> "Run" ]

[<EntryPoint>]
let main args = runOrDefault "Run" args
