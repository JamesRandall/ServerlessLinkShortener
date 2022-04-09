module LinkShortener

open System
open System.Net
open System.Threading.Tasks
open Pholly
open Retry

[<RequireQualifiedAccess>]
type PersistenceError =
  | KeyConflict
  | UnexpectedError of string
  
[<RequireQualifiedAccess>]
type GetResult =
  | Link of string
  | NotFound
  | UnexpectedError of string

let initLinkShortener (getLink:string -> Async<GetResult>) (saveLink:string -> string -> Async<Result<unit,PersistenceError>>) codeLength domain =
  let generateRandomCode () =
    // their are a million and one blog posts on various algorithms for doing this "properly"
    let r = Random()
    let chars = Array.concat([[|'a' .. 'z'|];[|'A' .. 'Z'|];[|'0' .. '9'|]])
    let sz = Array.length chars in String(Array.init codeLength (fun _ -> chars.[r.Next sz]))
  
  let save url = async {
    // attempt to save using code as primary key
    // on a key conflict we want to generate a new code and try again
    // repeat until successful
    let retryPolicy = Policy.retryAsync [
      retry (upto 5<times>)
      shouldRetry (fun result -> match result with | Error PersistenceError.KeyConflict -> true | _ -> false)
    ]
    let attemptSave () = task {
      let code = generateRandomCode ()
      let! saveResult = (saveLink code url)
      return saveResult |> Result.map(fun _ -> code)
    }
    
    let! result = attemptSave |> retryPolicy |> Async.AwaitTask
    return result |> Result.map(fun code -> $"https://{domain}/{code}")
  }
  
  let get shortcode = async {
    return! getLink shortcode
  }
  
  get,save