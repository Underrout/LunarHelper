using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LunarHelper.Resolvers
{
    internal interface IResolve<T> where T : HashFileVertex
    {
        public void ResolveDependencies(T vertex);
    }
}
