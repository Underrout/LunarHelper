using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LunarHelper
{
    class GlobuleException : Exception
    {
        public GlobuleException(string message) : base(message)
        {
        }
    }
}
