using Microsoft.ML;
using Microsoft.ML.Data;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Threading;
using SysProgTRECII.Services;

namespace SysProgTRECII.Services
{
    public sealed class TextSentiment : ITextSentiment
    {
        private readonly MLContext _ml = new(seed: 7);
        private readonly PredictionEngine<Input, Output> _engine;

        public TextSentiment()
        {
            var seedData = new List<Input>
            {
                new() { Text = "excellent great amazing inspiring", Label = true },
                new() { Text = "well written helpful informative", Label = true },
                new() { Text = "boring bad terrible disappointing", Label = false },
                new() { Text = "fake misleading awful", Label = false },
                new() { Text = "I love this article", Label = true },
                new() { Text = "I hate this piece", Label = false },
            };

            var view = _ml.Data.LoadFromEnumerable(seedData);
            var pipeline = _ml.Transforms.Text.FeaturizeText("Features", nameof(Input.Text))
                           .Append(_ml.BinaryClassification.Trainers.SdcaLogisticRegression());

            var model = pipeline.Fit(view);
            _engine = _ml.Model.CreatePredictionEngine<Input, Output>(model);
        }

        public Task<(double Probability, bool IsPositive)> AnalyzeAsync(string text)
        {
            text ??= string.Empty;
            var pred = _engine.Predict(new Input { Text = text });
            return Task.FromResult(((double)pred.Probability, pred.PredictedLabel));
        }

        private sealed class Input
        {
            public string Text { get; set; } = string.Empty;
            public bool Label { get; set; }
        }

        private sealed class Output
        {
            [ColumnName("PredictedLabel")] public bool PredictedLabel { get; set; }
            public float Probability { get; set; }
            public float Score { get; set; }
        }
    }
}
