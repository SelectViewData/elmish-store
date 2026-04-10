namespace ElmishStore


open Elmish
open Fable.Core
open ElmishStore
open System.Collections.Generic

type ElmishStore<'model, 'msg> = {
  GetModel: unit -> 'model
  Dispatch: 'msg -> unit
  Subscribe: UseSyncExternalStoreSubscribe
}

type private ActiveStoreState<'model> = {
  GetModel: unit -> 'model
  SetTermination: bool -> unit
}

type private RunningStoreEntry = {
  StoreState: obj
}

type private PreparedStoreEntry = {
  SetStateToken: obj
  StoreState: obj
}

type private StoreLifecycle =
  | Prepared of PreparedStoreEntry
  | Running of RunningStoreEntry

type private PendingStoreState<'model, 'msg> = {
  Store: ElmishStore<'model, 'msg>
  SetTermination: bool -> unit
}

module private Internal =

  let mutable stores: Dictionary<string, StoreLifecycle> = Dictionary<string, StoreLifecycle>()

  let tryGetActiveStoreState (uniqueName: string) =
    match stores.TryGetValue uniqueName with
    | true, Running storeState ->
      Some(storeState.StoreState |> unbox<ActiveStoreState<'model>>)
    | _ -> None

  let setup
    (uniqueName: string)
    (program: Program<'arg, 'model, 'msg, unit>)
    =

    let mutable state: 'model option = None
    let mutable finalDispatch = None
    let mutable shouldTerminate = false
    let mutable previousStoreState = tryGetActiveStoreState uniqueName

    let setTermination should = shouldTerminate <- should

    let dispatch msg =
      match finalDispatch with
      | Some finalDispatch -> finalDispatch msg
      | None -> failwith "You're using initial dispatch. That shouldn't happen."

    let subscribers = ResizeArray<unit -> unit>()

    let subscribe callback =
      subscribers.Add(callback)
      fun () -> subscribers.Remove(callback) |> ignore

    let mapSetState setState model dispatch =
      setState model dispatch
      let oldModel = state
      state <- Some model
      finalDispatch <- Some dispatch

      match previousStoreState with
      | Some previousStore ->
        previousStore.SetTermination true
        previousStoreState <- None
      | None -> ()

      // Skip re-renders if model hasn't changed
      if not (obj.ReferenceEquals(model, oldModel)) then
        subscribers |> Seq.iter (fun callback -> callback ())

    let mapInit userInit arg =
      match previousStoreState with
      | Some previousStore ->
        let model = previousStore.GetModel()
        state <- Some model
        model, Cmd.none
      | None -> userInit arg

    let mapTermination (predicate, terminate) =
      let pred msg = predicate msg || shouldTerminate
      pred, terminate

    let getCurrentState () =
      match state with
      | Some state -> state
      | None -> failwith "State is not initialized. That shouldn't happen."

    let store = {
      GetModel = getCurrentState
      Dispatch = dispatch
      Subscribe = UseSyncExternalStoreSubscribe subscribe
    }

    let preparedProgram =
      program
      |> Program.map mapInit id (fun _ -> fun _ _ -> store) mapSetState id mapTermination

    let storeState = {
      Store = store
      SetTermination = setTermination
    }

    stores[uniqueName] <- Prepared {
      SetStateToken = box (Program.setState preparedProgram)
      StoreState = box storeState
    }

    preparedProgram

  let takePendingStoreStateByToken (setStateToken: 'model -> Dispatch<'msg> -> unit) =
    let matchingStoreEntry =
      stores
      |> Seq.tryPick (fun (KeyValue(uniqueName, storeEntryObj)) ->
        match storeEntryObj with
        | Prepared storeEntry when obj.ReferenceEquals(storeEntry.SetStateToken, box setStateToken) ->
          Some(uniqueName, storeEntry)
        | _ -> None)

    match matchingStoreEntry with
    | Some(uniqueName, storeEntry) ->
      stores.Remove(uniqueName) |> ignore
      Some(uniqueName, unbox<PendingStoreState<'model, 'msg>> storeEntry.StoreState)
    | None -> None

module ElmishProgram =

  let setup (uniqueName: string) (program: Program<'arg, 'model, 'msg, unit>) =
    Internal.setup uniqueName program

  let runWith (arg: 'arg) (program: Program<'arg, 'model, 'msg, ElmishStore<'model, 'msg>>) =
    let uniqueName, storeState =
      match Internal.takePendingStoreStateByToken (Program.setState program) with
      | Some storeState -> storeState
      | None -> failwith "Program was not prepared. Call ElmishProgram.setup before ElmishProgram.run."

    program |> Program.runWith arg

    Internal.stores[uniqueName] <- Running {
      StoreState = box {
        GetModel = storeState.Store.GetModel
        SetTermination = storeState.SetTermination
      }
    }
    storeState.Store

  let inline run (program: Program<unit, 'model, 'msg, ElmishStore<'model, 'msg>>) =
    runWith () program

module ElmishStore =

  let createStoreWith (uniqueName: string) (arg: 'arg) (program: Program<'arg, 'model, 'msg, unit>) =
    program
    |> ElmishProgram.setup uniqueName
    |> ElmishProgram.runWith arg

  let inline createStore (uniqueName: string) program : ElmishStore<'model, 'msg> =
    createStoreWith uniqueName () program

[<Erase>]
type ElmishStore =

  static member Create
    (
      program: Program<'arg, 'model, 'msg, unit>,
      arg: 'arg,
      uniqueName: string
    ) : ElmishStore<'model, 'msg> =
    ElmishStore.createStoreWith uniqueName arg program

  static member inline Create(program: Program<unit, 'model, 'msg, unit>, uniqueName: string) =
    ElmishStore.Create(program, (), uniqueName)

  static member inline Create
    (
      init: 'arg -> 'model * Cmd<'msg>,
      update: 'msg -> 'model -> 'model * Cmd<'msg>,
      arg: 'arg,
      uniqueName: string
    ) =
    ElmishStore.Create((Program.mkProgram init update (fun _ _ -> ())), arg, uniqueName)

  static member inline Create
    (
      init: unit -> 'model * Cmd<'msg>,
      update: 'msg -> 'model -> 'model * Cmd<'msg>,
      uniqueName: string
    ) =
    ElmishStore.Create(Program.mkProgram init update (fun _ _ -> ()), uniqueName)

  static member inline Create
    (
      init: 'model * Cmd<'msg>,
      update: 'msg -> 'model -> 'model * Cmd<'msg>,
      uniqueName: string
    ) =
    ElmishStore.Create(Program.mkProgram (fun () -> init) update (fun _ _ -> ()), uniqueName)
