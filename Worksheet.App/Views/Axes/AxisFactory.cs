using System;
using System.Collections.Generic;
using ScottPlot.WPF;
using Worksheet.Core.Models;

namespace Worksheet.App.Views.Axes
{
    public class AxisFactory
    {
        private readonly Dictionary<ScaleType, AxisItem> _items;

        public AxisFactory()
            : this(new LinearAxisItem(), new LogarithmicAxisItem())
        {
        }

        public AxisFactory(LinearAxisItem linearAxisItem, LogarithmicAxisItem logarithmicAxisItem)
        {
            _items = new Dictionary<ScaleType, AxisItem>
            {
                { linearAxisItem.ScaleType, linearAxisItem },
                { logarithmicAxisItem.ScaleType, logarithmicAxisItem }
            };
        }

        public AxisItem Get(ScaleType scaleType)
        {
            if (_items.TryGetValue(scaleType, out var item))
                return item;

            throw new ArgumentOutOfRangeException(nameof(scaleType), scaleType, "Unsupported axis scale type.");
        }

        public void Apply(ScaleType scaleType, WpfPlot plot, ParameterPlotSettings settings)
        {
            var item = Get(scaleType);
            item.Apply(plot, settings, AxisOrientation.Bottom);
            item.Apply(plot, settings, AxisOrientation.Left);
        }
    }
}
