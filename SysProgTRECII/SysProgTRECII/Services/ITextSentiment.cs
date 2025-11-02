//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
using System.Threading.Tasks;

namespace SysProgTRECII.Services
{
    public interface ITextSentiment
    {
        System.Threading.Tasks.Task<(double Probability, bool IsPositive)> AnalyzeAsync(string text);
    }
}
