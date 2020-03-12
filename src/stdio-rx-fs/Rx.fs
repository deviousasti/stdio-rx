namespace FSharp.Control.Reactive

open System

module Observable = 
    
    /// Creates an observable sequence from a specified Subscribe method implementation.
    let createWithDisposable subscribe =
        System.Reactive.Linq.Observable.Create(Func<IObserver<'Result>,IDisposable> subscribe)
    
    /// Log to console for debugging
    let subcribeLog v = v |> Observable.materialize |> Observable.subscribe (printf "%A\n")

    /// Ignore elements of a sequence
    let ignoreElements = System.Reactive.Linq.Observable.IgnoreElements

module Disposable =
    /// Create an empty disposable
    let empty = Disposable.create id
    let compose seq = seq |> Seq.toArray |> Disposables.compose

module Subject =
    /// Create a unit Subject<T> which does no work
    let empty = { new System.Reactive.Subjects.ISubject<_> with
                      member this.OnCompleted() = ()
                      member this.OnError(_) = ()
                      member this.OnNext(_) = ()
                      member this.Subscribe(_) = Disposable.empty
                }
    
    /// Create a unit Subject<TSource, TResult> which does no work
    let empty2 = { new System.Reactive.Subjects.ISubject<_,_> with
        member this.OnCompleted() = ()
        member this.OnError(_) = ()
        member this.OnNext(_) = ()
        member this.Subscribe(_) = Disposable.empty
    }