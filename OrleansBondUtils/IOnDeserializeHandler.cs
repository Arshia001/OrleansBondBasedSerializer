using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrleansBondUtils
{
    public interface IOnDeserializedHandler
    {
        void OnDeserialized();
    }
}
