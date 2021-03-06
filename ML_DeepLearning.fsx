﻿#r @"C:\Users\Jeremiah Jeschke\Documents\Visual Studio 2015\Projects\officeAutomata\packages\FSharp.Data.2.4.3\lib\net45\FSharp.Data.dll"
#r @"C:\Users\Jeremiah Jeschke\Documents\Visual Studio 2015\Projects\Packages\OpenNLP.1.3.4\lib\net45\OpenNLP.dll"
#r @"C:\Users\Jeremiah Jeschke\Documents\Visual Studio 2015\Projects\Packages\OpenNLP.1.3.4\lib\net45\SharpEntropy.dll"
#r @"C:\Users\Jeremiah Jeschke\Documents\Visual Studio 2015\Projects\Packages\Accord.3.8.0\lib\net45\Accord.dll"
#r @"C:\Users\Jeremiah Jeschke\Documents\Visual Studio 2015\Projects\Packages\Accord.MachineLearning.3.8.0\lib\net45\Accord.MachineLearning.dll"
#r @"C:\Users\Jeremiah Jeschke\Documents\Visual Studio 2015\Projects\Packages\Accord.Math.3.8.0\lib\net45\Accord.Math.Core.dll"
#r @"C:\Users\Jeremiah Jeschke\Documents\Visual Studio 2015\Projects\Packages\Accord.Math.3.8.0\lib\net45\Accord.Math.dll"
#r @"C:\Users\Jeremiah Jeschke\Documents\Visual Studio 2015\Projects\Packages\Accord.Statistics.3.8.0\lib\net45\Accord.Statistics.dll"
#r @"C:\Users\Jeremiah Jeschke\Documents\Visual Studio 2015\Projects\Packages\Accord.IO.3.8.0\lib\net45\Accord.IO.dll"
#r @"C:\Users\Jeremiah Jeschke\Documents\Visual Studio 2015\Projects\Packages\Accord.Neuro.3.8.0\lib\net45\Accord.Neuro.dll"
#r @"C:\Users\Jeremiah Jeschke\Documents\Visual Studio 2015\Projects\Packages\Accord.Genetic.3.8.0\lib\net45\Accord.Genetic.dll"


open System 
open System.Diagnostics
open System.IO
open System.Threading
open System.Net
open System.Collections.Generic
open System.Data
open Accord.Statistics.Filters
//open Accord.Statistics.Kernels
open Accord.Math.Optimization
open Accord.Math.Optimization.Losses
open Accord.Neuro
open Accord.Neuro.Learning
open Accord.Neuro.Networks
open Accord.Neuro.ActivationFunctions
open Accord.Genetic
open Accord.IO
open Accord.Math

module Array = 
    let zip4 (array1: _[]) (array2: _[]) (array3: _[]) (array4: _[]) = 
        let len1 = array1.Length
        let res = Array.zeroCreate len1 
        for i = 0 to res.Length-1 do 
            res.[i] <- (array1.[i],array2.[i],array3.[i],array4.[i])
        res

    /// returns the index of the max item
    let maxi xs = xs |> Array.mapi (fun i x -> (i,x)) |> Array.maxBy snd |> fst


let test_set () =
        let inputs =
            [|  [|1.0; 1.0; |] 
                [|1.0; 0.0; |] 
                [|1.0; 1.0; |]
                [|1.0; 1.0; |] 
                [|1.0; 0.0; |] 
                [|1.0; 1.0; |]
                [|2.0; 1.0; |] 
                [|2.0; 1.0; |]
                [|2.0; 0.0; |]
                [|0.0; 1.0; |] 
                [|0.0; 1.0; |]
                [|0.0; 0.0; |]
                
                                     |]
        let outputs = 
            [|  0;
                0;
                1;
                1;
                1;
                0;
                2;
                2;
                2;
                2;
                2;
                2;
                |]
        let o1 = [|0..10|] |> Array.fold (fun acc x -> Array.append outputs acc) outputs   
        let i1 = [|0..10|] |> Array.fold (fun acc x -> Array.append inputs acc) inputs  
        i1,o1


try
//__Deep Belief Neural Network___________________________

    let wc = new WebClient()
    let xs = wc.DownloadString(@"https://raw.githubusercontent.com/primaryobjects/Accord.NET/master/Samples/Neuro/Deep%20Learning/Resources/optdigits-tra.txt").Split([|"\n"|], StringSplitOptions.RemoveEmptyEntries)

    //for i in [0..33..63789] do (printfn "%A %A" i (xs.[i + 32]))

    let inputs,outputs = 
            [| for i in [0..33..63789] -> ([|for j in i..i+31 do yield! (xs.[j].Split() |> Array.map (fun x -> Convert.ToDouble(x)))|], 
                                             let s = (xs.[i + 32])                                             
                                             int  (s.TrimStart())) |] 
            |> Array.unzip

    let inputs,outputs = test_set()

    (* Initiate Network *)
    let dbn = new DeepBeliefNetwork(new BernoulliFunction(), 1024, 50)

    GaussianWeights(dbn).Randomize()
    dbn.UpdateVisibleWeights()

    (* Hyperparameters *)
    let learningRate    = 0.1
    let weigthDecay     = 0.001
    let momentum        = 0.9
    let batchSize       = 100

    (* Split training data from testing data *)
    let inputs = inputs //1000 |> Seq.take 1000 

    let numberOfClasses = outputs |> Array.distinct |> fun x -> x.Length
    let outputs = Jagged.OneHot(outputs, 50) 

    let testInputs = inputs //|> Seq.skip 1000 |> Seq.toArray //1000
    let testActuals = outputs //|> Seq.skip 1000 |> Seq.toArray

    // LearnLayerUnsupervised
    let learnLayerUnspervised(layer:int,epochs:int) =
        let teacher = new DeepBeliefNetworkLearning(
                        dbn,
                        Algorithm = RestrictedBoltzmannNetworkLearningConfigurationFunction( fun h v i -> new ContrastiveDivergenceLearning(h, v, LearningRate = learningRate, Momentum = 0.5, Decay = weigthDecay) :> IUnsupervisedLearning),
                        LayerIndex = layer
                        )
        let batchCount = max 1 (inputs.Length-1 / batchSize)
        
        // Create mini-batches to speed learning
        let groups = Accord.Statistics.Classes.Random(inputs.Length-1,batchCount-1)
        let batches = Accord.Statistics.Classes.Separate(inputs,groups)
        
        let layerData = teacher.GetLayerInput(batches)
        let cd = teacher.GetLayerAlgorithm(teacher.LayerIndex) :?> ContrastiveDivergenceLearning

        // Start running the learning procedure
        [| for i in 0..epochs -> 
            if i = 10 then cd.Momentum <- momentum
            let e = teacher.RunEpoch(layerData)
            printfn "LU: %A" e
            e / float inputs.Length |]

    // LearnLayerSupervised  
    let learnLayerSupervised(epochs:int) =
            let teacher = DeepNeuralNetworkLearning(
                                dbn, 
                                Algorithm = ActivationNetworkLearningConfigurationFunction((fun ann i -> new ParallelResilientBackpropagationLearning(ann) :> ISupervisedLearning)), 
                                LayerIndex = 0) 
            let layerData = teacher.GetLayerInput(inputs)
            let errors = [| for i in 0..epochs -> let e = teacher.RunEpoch(layerData,outputs)  
                                                  printfn "LS: %A" e
                                                  e|]
            dbn.UpdateVisibleWeights()
            errors

    // LearnNetworkSupervised
    let learnNetworkSupervised(epochs:int) =
        let teacher = ResilientBackpropagationLearning(dbn, LearningRate = learningRate)
        let errors = [| for i in 0..epochs ->   let e = teacher.RunEpoch(inputs,outputs)  
                                                printfn "MS: %A" e
                                                e|]
        dbn.UpdateVisibleWeights()
        errors

    (* This may take a while *)
    let errors =
        [|
            //yield! learnLayerUnspervised(0,2) //0,200
            //yield! learnLayerSupervised(20)   //2000
            yield! learnNetworkSupervised(5)  //200
        |]

    printfn "%A" errors.Length

//    let predicted = testInputs |> Array.map (fun x -> dbn.Compute(x) |> Array.maxi)
//
//    let confustionMatrix = new Accord.Statistics.Analysis.GeneralConfusionMatrix(100, testActuals, predicted)
//    printfn "Accuracy: %f" confustionMatrix.OverallAgreement

with
| exn -> printfn "%A" exn

printfn "DNN Complete"
System.Console.ReadKey(true) |>ignore
