﻿using CommandLine;
using MathNet.Numerics.Data.Text;
using MathNet.Numerics.LinearAlgebra;
using ML.Model;
using ML.Network;
using OxyPlot;
using OxyPlot.WindowsForms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace ML
{
    class Program
    {
        public static bool Debug { get; protected set; }

        static void Main(string[] args)
        {
            CultureInfo customCulture = (CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";
            System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;

            /*
            Tinker();
            Console.ReadLine();
            Environment.Exit(0);
            */
            NetworkModel model = null;
            bool teaching = false;
            int epochRuns = 1;
            int repeats = 1;
            string inputFile = null;

            CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(opts =>
                {
                    teaching = opts.Teaching;
                    epochRuns = opts.EpochRuns;
                    repeats = opts.Repeats;
                    Debug = opts.Debug;

#if !DEBUG
                    try
                    {
#endif
                    model = NetworkModel.Load(opts.Model);
                    inputFile = model.Path(opts.Input);
#if !DEBUG

                        Console.Clear();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        Environment.Exit(1);
                    }
#endif
                });

            if (model is null)
            {
                Environment.Exit(0);
            }

            if (teaching)
            {
                Console.WriteLine("Launching model in teaching mode");

                // Save newly generated model.
                if (!model.Loaded)
                {
                    model.Save();
                }

                var plotModel = new PlotModel { Title = String.Format("{0} model \"{1}\" learning graph", model.Config.Type, model.Name) };
                var series = new OxyPlot.Series.LineSeries();

                try
                {
                    var graphMatrix = DelimitedReader.Read<double>(model.Path("graph"));

                    for (int i = 0; i < graphMatrix.RowCount; i++)
                    {
                        series.Points.Add(new DataPoint(i + 1, graphMatrix.Row(i).At(0)));
                    }
                }
                catch { }

                plotModel.Series.Add(series);
                plotModel.Axes.Add(new OxyPlot.Axes.LinearAxis
                {
                    Minimum = 1,
                    Position = OxyPlot.Axes.AxisPosition.Bottom,
                    Title = "Epochs",
                });
                plotModel.Axes.Add(new OxyPlot.Axes.LinearAxis
                {
                    Position = OxyPlot.Axes.AxisPosition.Left,
                    Title = "Cost",
                });

                var epochOffset = series.Points.Count;

                var epoch = epochOffset;

                var epochRunStep = epochRuns;

                var stopwatch = new Stopwatch();

                var epochStopwatch = new Stopwatch();

                bool forceStop = false;

                stopwatch.Restart();

                var repeat = 1;

                while (epochRuns == 0 || epoch < epochRuns + epochOffset)
                {
                    Console.Write("\nepoch {0} / {1}...", ++epoch, epochOffset + epochRuns);

                    var previous = model.GetInfo();

                    epochStopwatch.Restart();

                    stopwatch.Start();

                    var cost = model.RunEpoch();

                    stopwatch.Stop();

                    Console.Write(" {0:f2} + {1:f2}s,", stopwatch.Elapsed.TotalSeconds, epochStopwatch.Elapsed.TotalSeconds);

                    Console.Write(" cost {0}", cost);

                    series.Points.Add(new DataPoint(epoch, cost));

                    if (model.Config.Threshold != null && Math.Abs(cost) <= model.Config.Threshold)
                    {
                        Console.WriteLine("\nError threshold ({0}) reached. Finished learning.", model.Config.Threshold);
                        forceStop = true;
                    }

                    if (epochRuns == 0 || epoch >= epochRuns + epochOffset || forceStop)
                    {
                        forceStop = false;
                        epochRuns += epochRunStep;
                        var pngExporter = new PngExporter();
                        pngExporter.ExportToFile(plotModel, model.Path("learning-graph.png"));
                        if (repeat++ >= repeats)
                        {
                            DisplayActions(model, series);
                        }
                        else
                        {
                            SaveModel(model, series);
                        }
                    }
                }
            }
            else
            {
                if (model.DataTransformer is Model.Transformers.VectorDataTransformer)
                {
                    Console.WriteLine("Running execution loop\n");

                    while (true)
                    {
                        Console.WriteLine("Model info:");
                        Console.WriteLine("===========");
                        Console.WriteLine(model.GetInfo());

                        DisplayActions(model);

                        var inputs = Vector<double>.Build.Dense(model.Config.Inputs);

                        for (int i = 0; i < model.Config.Inputs; i++)
                        {
                            inputs[i] = ReadDouble(String.Format("Input[{0}]", i));
                        }

                        Console.Clear();

                        Console.WriteLine("Inputs:");
                        Console.WriteLine("=======");
                        Console.WriteLine(inputs.ToVectorString());

                        Console.WriteLine("Result:");
                        Console.WriteLine("=======");
                        foreach (var label in model.Run(inputs))
                        {
                            Console.WriteLine(label);
                        }
                        Console.WriteLine();
                    }
                }
                else if (model.DataTransformer is Model.Transformers.MnistDataTransformer)
                {
                    if (!File.Exists(inputFile))
                    {
                        Console.WriteLine("Input file '{0}' doesn't exist", inputFile);
                        Environment.Exit(1);
                    }

                    var inputs = model.InputTransformer.Transform(inputFile).Row(0);

                    foreach (var label in model.Run(inputs))
                    {
                        Console.WriteLine(label);
                    }
                }
                else
                {
                    Console.WriteLine("Unsupported model input transformer '{0}'.", model.DataTransformer);
                    Environment.Exit(1);
                }
            }
        }

        static void SaveModel(NetworkModel model, OxyPlot.Series.LineSeries series = null)
        {
            Console.WriteLine("\nSaving model...");
            if (series != null)
            {
                try
                {
                    var graphPoints = new List<Vector<double>>();

                    foreach (var point in series.Points)
                    {
                        graphPoints.Add(Vector<double>.Build.Dense(new double[] { point.Y }));
                    }

                    DelimitedWriter.Write(model.Path("graph"), Matrix<double>.Build.DenseOfRowVectors(graphPoints));

                    Console.WriteLine("Saved graph data");
                }
                catch
                {
                    Console.WriteLine("Unable to save graph data");
                }
            }
            model.Save();
            Console.WriteLine("Model has been saved.");
        }

        static void DeleteModel(NetworkModel model)
        {
            Console.WriteLine("Deleting model...");
            if (File.Exists(model.Path("graph")))
            {
                File.Delete(model.Path("graph"));
            }
            model.Delete();
            Console.WriteLine("Model has been deleted.");
        }

        static void DisplayActions(NetworkModel model, OxyPlot.Series.LineSeries series = null)
        {
            Console.WriteLine("\n\nActions:");
            Console.WriteLine("[Any key] Run | [Q]uit | [S]ave | [D]elete");
            Console.WriteLine("");

            switch (Console.ReadKey(true).Key)
            {
                case ConsoleKey.Q:
                    {
                        Environment.Exit(0);
                        break;
                    }
                case ConsoleKey.S:
                    {
                        SaveModel(model, series);
                        Environment.Exit(0);
                        break;
                    }
                case ConsoleKey.D:
                    {
                        DeleteModel(model);
                        Environment.Exit(0);
                        break;
                    }
            }
        }

        /// <summary>
        /// Read input and try convert it to double.
        /// Prompt user again when convertion fails.
        /// </summary>
        /// <param name="label"></param>
        /// <returns></returns>
        static double ReadDouble(string label)
        {
            while (true)
            {
                Console.Write("{0}: ", label);
                try
                {
                    return Convert.ToDouble(Console.ReadLine());
                }
                catch
                {
                    Console.WriteLine("Incorrect format.");
                }
            }
        }

        static void Tinker()
        {
            /*
            var matrix1 = Matrix<double>.Build.DenseOfArray(new double[,] {
                { 0, 0.5, 0 },
                { 0, 0, 1 }
            });
            var matrix2 = Matrix<double>.Build.DenseOfArray(new double[,] {
                { 0, -0.02, 0 },
                { 0, 0, 1 }
            });

            Console.WriteLine(matrix1);
            Console.WriteLine(matrix2);
            Console.WriteLine(matrix1.Add(matrix2));

            Console.ReadLine();
            Environment.Exit(0);
            */
            var v = Vector<double>.Build;
            //var o1 = new Neuron(2, v.DenseOfArray(new double[] { 2.0, -3.0 }), -3.0, "Sigmoid");
            //var forward1 = o1.Forward(v.DenseOfArray(new double[] { -1, -2 }));
            //Console.WriteLine("forward1: {0}", forward1);
            //var o2 = new Neuron(2, v.DenseOfArray(new double[] { 2.0, -3.0 }), -3.0, "Sigmoid");

            //var forward2 = o2.Forward(v.DenseOfArray(new double[] { -1, -2 }));
            //Console.WriteLine("forward2: {0}", forward2);
            /*
            var cost = 1;
            var backward1 = o1.Backward(cost);
            var backward2 = o2.Backward(cost);
            Console.WriteLine("backward1 gradient: {0}", backward1);
            */
            var neurons1 = new List<Neuron>
            {
                Neuron.Generate(2, "Sigmoid"),
                Neuron.Generate(2, "Sigmoid")
            };

            var layer1 = new Layer(neurons1);

            var neurons2 = new List<Neuron>
            {
                Neuron.Generate(2, "Sigmoid"),
                Neuron.Generate(2, "Sigmoid")
            };

            var layer2 = new Layer(neurons2);

            var inputs = v.DenseOfArray(new double[] { 0.05, 0.10 });

            var targets = v.DenseOfArray(new double[] { 0.01, 0.99 });

            var runs = 0;

            var rate = 0.5;

            double cost = 0;


            while (true)
            {

                Console.Clear();

                Console.WriteLine("run {0}:\n", ++runs);

                Console.WriteLine("inputs: {0}", inputs);

                Console.WriteLine("targets: {0}", targets);

                var forward1 = layer1.Forward(inputs);
                Console.WriteLine("layer 1 forward: {0}", forward1);

                var forward2 = layer2.Forward(inputs);
                Console.WriteLine("layer 2 forward: {0}", forward2);

                var diff = targets.Subtract(forward2);

                Console.WriteLine("diff: {0}", diff);

                //var backward = layer.Backward(v.DenseOfArray(new double[] { 1, 1 }));
                var backward2 = layer2.Backward(diff);
                Console.WriteLine("layer 2 backward: {0}", backward2);

                for (int i = 0; i < layer2.Size; i++)
                {
                    var neuron = layer2.Neurons[i];
                    var neuronBackward = backward2.SubMatrix(i * layer2.InputCount, layer2.InputCount, 0, backward2.ColumnCount);
                    for (int j = 0; j < layer2.InputCount; j++)
                    {
                        var neuronInputBackward = neuronBackward.Row(j);
                        neuron.Weights.At(j, neuron.Weights.At(j) + neuronInputBackward.At(1) * rate);
                    }
                    //neuron.Weights = neuron.Weights.Subtract(backward.Column(1).Multiply(rate));
                    neuron.Bias += rate * neuronBackward.Row(0).At(0);

                }

                // Recalculate layer output vector
                diff = v.Dense(layer1.Size);

                // Take previous layer rows, and sum all output gradient columns (3),
                // corresponding to each neuron.
                // Number of rows in a group is number of neurons in current layer.
                for (int i = 0; i < backward2.RowCount; i += layer1.Size)
                {
                    double outputGradientSum = 0;
                    for (int j = 0; j < layer1.Size; j++)
                    {
                        outputGradientSum += backward2.Row(i + j).At(2);
                    }
                    diff.At(i / layer1.Size, outputGradientSum);
                }

                Console.WriteLine("diff: {0}", diff);

                var backward1 = layer1.Backward(diff);
                Console.WriteLine("layer 1 backward: {0}", backward1);

                for (int i = 0; i < layer1.Size; i++)
                {
                    var neuron = layer1.Neurons[i];
                    var neuronBackward = backward1.SubMatrix(i * layer1.InputCount, layer1.InputCount, 0, backward1.ColumnCount);
                    for (int j = 0; j < layer1.InputCount; j++)
                    {
                        var neuronInputBackward = neuronBackward.Row(j);
                        neuron.Weights.At(j, neuron.Weights.At(j) + neuronInputBackward.At(1) * rate);
                    }
                    //neuron.Weights = neuron.Weights.Subtract(backward.Column(1).Multiply(rate));
                    neuron.Bias += rate * neuronBackward.Row(0).At(0);

                }

                cost = targets.Subtract(layer2.Forward(inputs)).PointwisePower(2).Divide(2).Sum();

                Console.WriteLine("cost {0}", cost);

                Console.ReadKey(true);

            }
        }
    }
}
