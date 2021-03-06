﻿using System.IO;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Data.Text;
using ML.Network;
using System;

namespace ML.Model
{
    class Perceptron : NetworkModel
    {
        Neuron perceptron;

        new PerceptronConfig Config;

        /// <summary>
        /// Perceptron model constructor.
        /// </summary>
        /// <param name="model"></param>
        /// <param name="config"></param>
        public Perceptron(string model, PerceptronConfig config) : base(model, config)
        {
            Config = config;

            Initialize();
        }

        /// <summary>
        /// Generate new perceptron with random parameters.
        /// </summary>
        /// <returns></returns>
        protected Neuron Generate()
        {
            return Neuron.Generate(Config.Inputs, Config.Function.ToString());
        }

        /// <summary>
        /// Initialize current model.
        /// </summary>
        public override void Initialize()
        {
            try
            {
                var state = DelimitedReader.Read<double>(Path(Config.State)).Row(0);
                var bias = state[0];
                var weights = state.SubVector(1, state.Count - 1);
                perceptron = new Neuron(Config.Inputs, weights, bias, Config.Function.ToString());
            }
            catch
            {
                perceptron = Neuron.Generate(Config.Inputs, Config.Function.ToString());
            }
        }

        /// <summary>
        /// Save currently loaded model.
        /// </summary>
        override public void Save()
        {
            var state = Vector<double>.Build.Dense(perceptron.Weights.Count + 1, (index) =>
            {
                if (index == 0)
                {
                    return perceptron.Bias;
                }
                else
                {
                    return perceptron.Weights.At(index - 1);
                }
            });

            DelimitedWriter.Write<double>(Path(Config.State), state.ToRowMatrix());
        }

        /// <summary>
        /// Delete currently loaded model.
        /// </summary>
        override public void Delete()
        {
            var file = Path(Config.State);

            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Get model info as text string.
        /// </summary>
        /// <returns></returns>
        override public string GetInfo()
        {
            string result = "";

            result += "Weights:\n";
            result += perceptron.Weights.ToVectorString();
            result += "\n";
            result += "Bias: " + perceptron.Bias;

            return result;
        }

        /// <summary>
        /// Process given inputs through the model.
        /// </summary>
        /// <param name="inputs"></param>
        /// <returns></returns>
        override public Vector<double> Process(Vector<double> inputs)
        {
            return Vector<double>.Build.DenseOfArray(new double[] { perceptron.Forward(inputs) });
        }

        /// <summary>
        /// Run teaching interation.
        /// </summary>
        public double Teach(Vector<double> inputs, Vector<double> outputs)
        {
            if (perceptron.InputCount != inputs.Count)
            {
                throw new Exception("Incorrect number of inputs.");
            }

            var error = Process(inputs).Subtract(outputs).At(0);

            for (var i = 0; i < perceptron.Weights.Count; i++)
            {
                var weight = perceptron.Weights.At(i);
                weight -= Config.LearningRate * error * inputs[i];
                perceptron.Weights.At(i, weight);
            }
            // Delta sign is reversed because e = (y - t) instead of (t - y)
            perceptron.Bias -= Config.LearningRate * error;

            return error;
        }

        /// <summary>
        /// Run teaching epoch using online teaching method.
        /// Weight updates are done on for each sample individually.
        /// </summary>
        override public double RunEpoch()
        {
            double error = 0;
            var samples = DelimitedReader.Read<double>(Path(Config.Samples));
            var permutation = Combinatorics.GeneratePermutation(samples.RowCount);

            foreach (var index in permutation)
            {
                var inputs = samples.Row(index).SubVector(0, perceptron.InputCount);
                var outputs = samples.Row(index).SubVector(perceptron.InputCount, 1);
                error += Teach(inputs, outputs);
            }

            return error;
        }
    }
}
