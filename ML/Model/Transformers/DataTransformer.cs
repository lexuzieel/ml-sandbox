﻿using MathNet.Numerics.LinearAlgebra;

namespace ML.Model.Transformers
{
    abstract class DataTransformer
    {
        protected NetworkModel model;

        public DataTransformer(NetworkModel model)
        {
            this.model = model;
        }

        /// <summary>
        /// Transform data file into array of vectors.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public abstract Matrix<double> Transform(string file);
    }
}
