namespace ElmishStore


open Elmish
open Fable.Core
open System.Collections.Generic

type ElmishStore<'model, 'msg> = {
  GetModel: unit -> 'model
  Dispatch: 'msg -> unit
  Subscribe: UseSyncExternalStoreSubscribe
}

type private StoreState<'model, 'msg> = {
  Store: ElmishStore<'model, 'msg>
  SetTermination: bool -> unit
}

type private RuntimeState<'model, 'msg> = {
  UniqueName: string
  mutable State: 'model option
  mutable FinalDispatch: Dispatch<'msg> option
  mutable ShouldTerminate: bool
  Subscribers: ResizeArray<unit -> unit>
  mutable PreviousStore: StoreState<'model, 'msg> option
}

type ElmishProgramState<'arg, 'model, 'msg, 'view> = {
  Program: Program<'arg, 'model, 'msg, 'view>
  Run: Program<'arg, 'model, 'msg, 'view> -> 'arg -> ElmishStore<'model, 'msg>
}

module private Internal =

  let mutable stores: Dictionary<string, obj> = Dictionary<string, obj>()

  let tryGetStoreState (uniqueName: string) =
    if stores.ContainsKey(uniqueName) then
      Some(stores[uniqueName] |> unbox<StoreState<'model, 'msg>>)
    else
      None

module ElmishProgram =

  let setup (uniqueName: string) (program: Program<'arg, 'model, 'msg, 'view>) =
    let runtime = {
      UniqueName = uniqueName
      State = None
      FinalDispatch = None
      ShouldTerminate = false
      Subscribers = ResizeArray()
      PreviousStore = None
    }

    let dispatch msg =
      match runtime.FinalDispatch with
      | Some finalDispatch -> finalDispatch msg
      | None -> failwith "You're using initial dispatch. That shouldn't happen."

    let subscribe callback =
      runtime.Subscribers.Add(callback)
      fun () -> runtime.Subscribers.Remove(callback) |> ignore

    let mapSetState setState model dispatch =
      setState model dispatch
      let oldModel = runtime.State
      runtime.State <- Some model
      runtime.FinalDispatch <- Some dispatch

      match runtime.PreviousStore with
      | Some previousStore ->
        previousStore.SetTermination true
        runtime.PreviousStore <- None
      | None -> ()

      // Skip re-renders if model hasn't changed
      if not (obj.ReferenceEquals(model, oldModel)) then
        runtime.Subscribers |> Seq.iter (fun callback -> callback ())

    let mapInit userInit arg =
      match runtime.PreviousStore with
      | Some previousStore ->
        let model = previousStore.Store.GetModel()
        runtime.State <- Some model
        model, Cmd.none
      | None -> userInit arg

    let mapTermination (predicate, terminate) =
      let pred msg = predicate msg || runtime.ShouldTerminate
      pred, terminate

    let getState () =
      match runtime.State with
      | Some state -> state
      | None -> failwith "State is not initialized. That shouldn't happen."

    let store = {
      GetModel = getState
      Dispatch = dispatch
      Subscribe = UseSyncExternalStoreSubscribe subscribe
    }

    let preparedProgram =
      program
      |> Program.map mapInit id id mapSetState id mapTermination

    let run (program: Program<'arg, 'model, 'msg, 'view>) (arg: 'arg) =
      runtime.PreviousStore <- Internal.tryGetStoreState runtime.UniqueName
      runtime.ShouldTerminate <- false

      let setTermination should = runtime.ShouldTerminate <- should

      program
      |> Program.runWith arg

      let storeState = {
        Store = store
        SetTermination = setTermination
      }

      Internal.stores[runtime.UniqueName] <- box storeState
      store

    {
      Program = preparedProgram
      Run = run
    }

  let mapProgram
    (mapper: Program<'arg, 'model, 'msg, 'view> -> Program<'arg, 'model, 'msg, 'view>)
    (elmishProgram: ElmishProgramState<'arg, 'model, 'msg, 'view>)
    =
    {
      Program = mapper elmishProgram.Program
      Run = fun program arg -> elmishProgram.Run program arg
    }

  let runWith (arg: 'arg) (elmishProgram: ElmishProgramState<'arg, 'model, 'msg, 'view>) =
    elmishProgram.Run elmishProgram.Program arg

  let inline run (elmishProgram: ElmishProgramState<unit, 'model, 'msg, 'view>) =
    runWith () elmishProgram

  let createStoreWith (uniqueName: string) (arg: 'arg) (program: Program<'arg, 'model, 'msg, 'view>) =
    program
    |> setup uniqueName
    |> runWith arg

  let inline createStore (uniqueName: string) (program: Program<unit, 'model, 'msg, 'view>) =
    createStoreWith uniqueName () program

module ElmishStore =

  let createStoreWith (uniqueName: string) (arg: 'arg) (program: Program<'arg, 'model, 'msg, 'view>) =
    ElmishProgram.createStoreWith uniqueName arg program

  let inline createStore (uniqueName: string) (program: Program<unit, 'model, 'msg, 'view>) : ElmishStore<'model, 'msg> =
    ElmishProgram.createStore uniqueName program

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
