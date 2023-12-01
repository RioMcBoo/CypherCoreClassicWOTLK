using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Database
{
    public interface IDataBase
    {
        public abstract SQLResult Query(PreparedStatement stmt);
    }
}
